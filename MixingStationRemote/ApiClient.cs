using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
namespace MixingStationRemote;
public readonly record struct ParameterUpdate(string Path, string Value);

public class ApiClient
{
	private readonly HttpClient _httpClient = new();
	private readonly ClientWebSocket _websocket = new();
	private Task? _receiveLoop;

	public ConsoleArchitecture ConsoleArchitecture { get; private set; }
	public IReadOnlyDictionary<string, Parameter> ParameterDictionary => _parameters;
	private readonly Dictionary<string, Parameter> _parameters = new(StringComparer.Ordinal);
	public void SetDiscoveryBase(string stationUrl)
	{
		_httpClient.BaseAddress = new Uri(stationUrl.EndsWith("/") ? stationUrl : stationUrl + "/");
	}

	public async Task<SupportedMixersRoot> GetSupportedMixerModels()
	{
		var r = await _httpClient.GetAsync("app/mixers/available");
		r.EnsureSuccessStatusCode();
		var json = await r.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<SupportedMixersRoot>(json) ?? new();
	}

	public async Task StartSearch(int model)
	{
		await _httpClient.PostAsync("app/mixers/disconnect", null).ConfigureAwait(false);
		await Task.Delay(500);

		var body = JsonSerializer.Serialize(new { consoleId = model });
		using var content = new StringContent(body, Encoding.UTF8, "application/json");
		using var response = await _httpClient.PostAsync("app/mixers/search", content).ConfigureAwait(false);
		var cont = await response.Content.ReadAsStringAsync();
		await Task.Delay(500);

		response.EnsureSuccessStatusCode();
	}

	public async Task<MixerSearchRoot> GetSearchResults()
	{
		var r = await _httpClient.GetAsync("app/mixers/searchResults");
		r.EnsureSuccessStatusCode();
		var json = await r.Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<MixerSearchRoot>(json) ?? new();
	}

	public async Task ConnectToConsole(MixerDevice device, ConsoleGroup console)
	{
		var body = JsonSerializer.Serialize(new { consoleId = console.consoleId, ip = device.ip });
		using var content = new StringContent(body, Encoding.UTF8, "application/json");
		using var response = await _httpClient.PostAsync("app/mixers/connect", content).ConfigureAwait(false);
		var responseContent = response.Content.ReadAsStringAsync();
	}

	public async Task<MixerInfo> GetCurrentMixer()
	{
		var response = await _httpClient.GetAsync("app/mixers/current");
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync();
		var mixerInfo = JsonSerializer.Deserialize<MixerInfo>(content);
		return mixerInfo;
	}

	public async Task ConnectWebsocket()
	{
		if (_websocket.State == WebSocketState.Open)
			return;

		_cts = new CancellationTokenSource();

		await _websocket.ConnectAsync(new Uri("ws://localhost:8080"), _cts.Token);
		_receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
	}
	private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
	{
		if (_websocket == null)
			return;

		while (!cancellationToken.IsCancellationRequested && _websocket.State == WebSocketState.Open)
		{

			var message = await ReceiveFullTextMessageAsync(_websocket, cancellationToken).ConfigureAwait(false);
			if (message == null)
				break;

			ProcessWebSocketMessage(message);
		}
	}

	private void ProcessWebSocketMessage(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		if (root.ValueKind != JsonValueKind.Object)
			return;
		if (root.TryGetProperty("error", out _))
			return;

		if (!root.TryGetProperty("path", out var pathProp))
			return;

		var fullPath = pathProp.GetString();
		if (string.IsNullOrEmpty(fullPath))
			return;

		if (!root.TryGetProperty("body", out var body) ||
			!body.TryGetProperty("value", out var valElement))
			return;

		string path = fullPath.Replace("/console/data/get/", "").TrimStart('/');
		string valueStr = valElement.ToString();

		ParameterUpdated?.Invoke(new(path, valueStr));
	}
	private CancellationTokenSource? _cts;
	private static async Task<string?> ReceiveFullTextMessageAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

		try
		{
			using var stream = new MemoryStream();

			while (true)
			{
				var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

				if (result.MessageType == WebSocketMessageType.Close)
					return null;

				if (result.Count > 0)
					stream.Write(buffer, 0, result.Count);

				if (result.EndOfMessage)
					break;
			}

			return Encoding.UTF8.GetString(stream.ToArray());
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public async Task<Dictionary<string, Parameter>> GetAllParametersWithDefinitions()
	{

		var r = await _httpClient.GetAsync("console/information").ConfigureAwait(false);
		r.EnsureSuccessStatusCode();
		var jsonText = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
		ConsoleArchitecture = JsonSerializer.Deserialize<ConsoleArchitecture>(jsonText);


		var response = await _httpClient.GetAsync("console/data/paths").ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		var tree = JsonDocument.Parse(json).RootElement;

		var allPaths = new List<string>();
		CollectLeafPaths(tree, string.Empty, allPaths);

		var defs = new Dictionary<string, Parameter>(StringComparer.Ordinal);

		var tasks = allPaths.Select(async path =>
		{
			try
			{
				var defResponse = await _httpClient.GetAsync($"console/data/definitions2/{path}").ConfigureAwait(false);
				var defJson = await defResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				var param = JsonSerializer.Deserialize<Parameter>(defJson);

				if (param != null)
				{
					param.path = path;
					param.Value = await GetValue(path).ConfigureAwait(false);

					lock (defs)
					{
						defs[path] = param;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to load definition for {path}: {ex.Message}");
			}
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		_parameters.Clear();
		foreach (var kv in defs)
			_parameters[kv.Key] = kv.Value;

		return defs;
	}

	private void CollectLeafPaths(JsonElement node, string currentPath, List<string> paths)
	{
		if (node.ValueKind != JsonValueKind.Object)
			return;

		if (node.TryGetProperty("val", out var valArray) &&
			valArray.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in valArray.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.String)
				{
					var paramName = item.GetString();
					if (!string.IsNullOrEmpty(paramName))
					{
						var fullPath = string.IsNullOrEmpty(currentPath)
							? paramName
							: $"{currentPath}.{paramName}";   // using . as separator

						paths.Add(fullPath);
					}
				}
			}
		}

		if (node.TryGetProperty("child", out var child) &&
			child.ValueKind == JsonValueKind.Object)
		{
			foreach (var prop in child.EnumerateObject())
			{
				var key = prop.Name;
				var newPath = string.IsNullOrEmpty(currentPath)
					? key
					: $"{currentPath}.{key}";

				CollectLeafPaths(prop.Value, newPath, paths);
			}
		}
	}
	public event Action<ParameterUpdate>? ParameterUpdated;

	public async Task Subscribe(string path)
	{
		if (_websocket.State != WebSocketState.Open)
			await ConnectWebsocket();
		var subscription = new
		{
			path = "/console/data/subscribe",
			method = "POST",
			body = new
			{
				path = path,           // or more specific e.g. "ch.*.mix.lvl" for main LR faders only
				format = new[] { "val" } // or "norm" / "number" may work in some versions, but "val" is standard
										 // format = "number"     // ← this key is usually NOT used; see notes below
			}
		};

		var json = JsonSerializer.Serialize(subscription);

		await _websocket.SendAsync(
			new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
			WebSocketMessageType.Text,
			true,
			_cts.Token).ConfigureAwait(false);
	}

	public async Task<string?> GetValue(string path, string format = "val")
	{
		var response = await _httpClient.GetAsync($"/console/data/get/{path}/{format}").ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		var json = JsonDocument.Parse(content);
		json.RootElement.TryGetProperty("value", out var val);

		return val.ToString();
	}

	public async Task SendUpdate(string path, object value, string format = "val")
	{
		var body = JsonSerializer.Serialize(new { format = format, value = value });
		using var content = new StringContent(body, Encoding.UTF8, "application/json");
		using var response = await _httpClient.PostAsync($"/console/data/set/{path}/{format}", content).ConfigureAwait(false);
		var responseContent = response.Content.ReadAsStringAsync();
	}

	public async Task<AppState?> GetAppState()
	{
		var result = await _httpClient.GetAsync("app/state").ConfigureAwait(false);
		result.EnsureSuccessStatusCode();
		var json = await result.Content.ReadAsStringAsync();
		var appState = JsonSerializer.Deserialize<AppState>(json);
		return appState;
	}
}