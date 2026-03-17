using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace MixingStationRemote;

public partial class MainWindow : Window
{
	private readonly ApiClient _client;

	public MainWindow(ApiClient client)
	{
		_client = client;
		InitializeComponent();
	}
	int channelCount;
	int selectedChannel;

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		_client.ParameterUpdated += OnParameterValueUpdatedFromWs;
		var mixer = await _client.GetCurrentMixer();
		Title = mixer.currentModel + " - Mixing Station Remote";


		var arc = await _client.GetConsoleArchitecture();
		// Stay on UI thread after await, because you're building controls below
	//	var defs = await _client.GetAllParametersWithDefinitions();
		await _client.ConnectWebsocket();

		await Task.Delay(2500); //give some time for initial data to arrive, so we can group channels by number

		channelCount = arc.totalChannels;
		selectedChannel = 0;
		InitializeShortcuts();

		await SelectChannel(0);
	}

	private async Task BuildCurrentChannelControls()
	{
		mixPanel.Children.Clear();
		createdControls.Clear();
		await AddParameterControl(mixPanel, "trim", $"ch.{selectedChannel}.headamp.gain", _client);
		await AddParameterControl(mixPanel, "level", $"ch.{selectedChannel}.mix.lvl", _client);
		await AddParameterControl(mixPanel, "hpf", $"ch.{selectedChannel}.preamp.filter.0.freq", _client);
		await AddParameterControl(mixPanel, "pan", $"ch.{selectedChannel}.mix.pan", _client);
		await AddToggleControl(mixPanel, "solo", $"ch.{selectedChannel}.solo", _client);

		//var mutegroupCount = _client.ParameterDictionary.Keys.Where(c => c.StartsWith($"ch.{selectedChannel}.grp.mute.")).Count();
		//for (int i = 0; i < mutegroupCount; i++)
		//{
		//	await AddToggleControl(mixPanel, $"mute group {i + 1}", $"ch.{selectedChannel}.grp.mute.{i}");
		//}

	}

	Dictionary<string, FrameworkElement> createdControls = new();

	private async Task AddParameterControl(Panel parent, string name, string path, ApiClient client)
	{
		var param = client.GetParameter(path);

		var control = new NumericParameterControl
		{
			Parameter = param,
			Client = client
        };

		parent.Children.Add(control);

		control.UserValueChanged += async (_, value) =>
			await _client.SendUpdate(path, value);

		//await _client.Subscribe(path);

		createdControls[name] = control;
	}
	private async Task AddToggleControl(Panel parent, string name, string path, ApiClient client)
	{
        var param = client.GetParameter(path);


		var control = new BooleanParameterControl
		{
			Parameter = param,
			Client = client
		};

   //     if (param.Definition.Title == null)
			//param.Definition.Title = name;

		parent.Children.Add(control);

		control.UserValueChanged += async (_, value) =>
			await _client.SendUpdate(path, value);

		//await _client.Subscribe(path);

		createdControls[name] = control;
	}
	private void OnParameterValueUpdatedFromWs(ParameterUpdate p)
	{
		if (!Dispatcher.CheckAccess())
		{
			Dispatcher.BeginInvoke(() => OnParameterValueUpdatedFromWs(p));
			return;
		}



  //      if (double.TryParse(p.Value, out var num))
		//	param.Value = num;
		//else
		//	param.Value = p.Value;
	}

	private Dictionary<Key, RoutedEventHandler> shortcutActions = new Dictionary<Key, RoutedEventHandler>();
	private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.System)
		{
			if (shortcutActions.ContainsKey(e.SystemKey))
			{
				shortcutActions[e.SystemKey]?.Invoke(null, null);
				e.Handled = true;
			}
		}
		if (Keyboard.FocusedElement is CustomMenuItem)
		{
			return;
		}

		if (shortcutActions.ContainsKey(e.Key))
		{
			e.Handled = true;
			shortcutActions[e.Key]?.Invoke(null, null);
		}
	}

	void HandleFunctionKeys(int number)
	{
		//switch (number)
		//{
		//	case 1:
		//	case 2:
		//	case 3:
		//	case 4:
		//	case 5:
		//	case 6:
		//	case 7:
		//	case 8:
		//		var auxChannel = blindViewModel.Auxes[(number - 1) + (curBank * 8)];
		//		if (auxChannel.linkslave)
		//			SelectMixWithShortcut(blindViewModel.Auxes[(number - 2) + (curBank * 8)]);
		//		else
		//			SelectMixWithShortcut(auxChannel);
		//		break;
		//	case 10: //bank up
		//		if (curBank + 1 <= MaxBank)............
		//		{
		//			curBank++;
		//		}
		//		Speech.SpeechManager.Say("Mixes " + ((curBank * 8) + 1) + " through " + ((curBank * 8) + 8));
		//		break;
		//	case 9:
		//		if (curBank - 1 >= 0)
		//		{
		//			curBank--;
		//		}
		//		Speech.SpeechManager.Say("Mixes " + ((curBank * 8) + 1) + " through " + ((curBank * 8) + 8));
		//		break;
		//}
	}

	//void SelectMixWithShortcut(Channel channel)
	//{
	//	foreach (CustomMenuItem item in mixMenu.Items)
	//	{
	//		if ((string)item.Header == channel.username)
	//			item.IsChecked = true;
	//		else
	//			item.IsChecked = false;
	//	}
	//	MixSelected(channel);

	//	if (channel.link && channel.linkmaster)
	//		Speech.SpeechManager.Say(channel.username + " stereo pair");
	//	else
	//		Speech.SpeechManager.Say(channel.username);
	//}

	//private void MixSelected(Channel mix)
	//{
	//	_mix = mix;
	//	BuildControls();
	//	ValidateControls();
	//}
	private void InitializeShortcuts()
	{
		//MIX SELECTION
		shortcutActions[Key.D0] = (s, e) => HandleNumberKey(0);
		shortcutActions[Key.D1] = (s, e) => HandleNumberKey(1);
		shortcutActions[Key.D2] = (s, e) => HandleNumberKey(2);
		shortcutActions[Key.D3] = (s, e) => HandleNumberKey(3);
		shortcutActions[Key.D4] = (s, e) => HandleNumberKey(4);
		shortcutActions[Key.D5] = (s, e) => HandleNumberKey(5);
		shortcutActions[Key.D6] = (s, e) => HandleNumberKey(6);
		shortcutActions[Key.D7] = (s, e) => HandleNumberKey(7);
		shortcutActions[Key.D8] = (s, e) => HandleNumberKey(8);
		shortcutActions[Key.D9] = (s, e) => HandleNumberKey(9);

		shortcutActions[Key.F1] = (s, e) => HandleFunctionKeys(1);
		shortcutActions[Key.F2] = (s, e) => HandleFunctionKeys(2);
		shortcutActions[Key.F3] = (s, e) => HandleFunctionKeys(3);
		shortcutActions[Key.F4] = (s, e) => HandleFunctionKeys(4);
		shortcutActions[Key.F5] = (s, e) => HandleFunctionKeys(5);
		shortcutActions[Key.F6] = (s, e) => HandleFunctionKeys(6);
		shortcutActions[Key.F7] = (s, e) => HandleFunctionKeys(7);
		shortcutActions[Key.F8] = (s, e) => HandleFunctionKeys(8);
		shortcutActions[Key.F9] = (s, e) => HandleFunctionKeys(9);
		shortcutActions[Key.F10] = (s, e) => HandleFunctionKeys(10);
		shortcutActions[Key.F11] = (s, e) => HandleFunctionKeys(11);
		shortcutActions[Key.F12] = (s, e) => HandleFunctionKeys(12);

		//shortcutActions[Key.Escape] = (s, e) => SelectMixWithShortcut(blindViewModel.Main[0]);

		shortcutActions[Key.V] = (s, e) => createdControls["level"].Focus();
		shortcutActions[Key.H] = (s, e) => createdControls["hpf"].Focus();
		shortcutActions[Key.T] = (s, e) => createdControls["trim"].Focus();
		shortcutActions[Key.B] = (s, e) => createdControls["pan"].Focus();
		//shortcutActions[Key.G] = (s, e) => new GateToolWindow(_channel).ShowDialog();

		//DEBUG 
		//shortcutActions[Key.I] = (s, e) => blindViewModel.Mutegroup.AssignMutesToAGroup(0);

		//shortcutActions[Key.O] = (s, e) => blindViewModel.Mutegroup.mutegroup1 = !blindViewModel.Mutegroup.mutegroup1;


		//shortcutActions[Key.E] = (s, e) =>
		//{
		//	if (_channel is OutputDACBus)
		//		new EQ6ToolWindow(_channel).ShowDialog();
		//	else
		//		new EQ4ToolWindow(_channel).ShowDialog();
		//};
		//shortcutActions[Key.C] = (s, e) => new CompToolWindow(_channel).ShowDialog();
		//shortcutActions[Key.A] = (s, e) => new SendsView(_channel, blindViewModel).ShowDialog();
		//shortcutActions[Key.X] = (s, e) =>
		//{
		//	_channel.mute = !_channel.mute;
		//	if (_channel.mute)
		//		Speech.SpeechManager.Say($"Muted");
		//	else
		//		Speech.SpeechManager.Say($"Unmuted");
		//};
		//shortcutActions[Key.S] = (s, e) =>
		//{
		//	_channel.solo = !_channel.solo;
		//	if (_channel.solo)
		//		Speech.SpeechManager.Say($"Solo On");
		//	else
		//		Speech.SpeechManager.Say($"Solo Off");
		//};
		//shortcutActions[Key.M] = (s, e) =>
		//{
		//	if (UserControls.ModifierKeys.IsCtrlDown())
		//	{
		//		Speech.SpeechManager.Say($"{ValueTransformer.LinearToVolume((float)Peak)}");
		//	}
		//};

		shortcutActions[Key.Right] = async (s, e) =>
		{
			int newIndex = 0;
			if (ModKeys.IsCtrlDown())
				newIndex = Math.Min(IncrementToNextMultipleOfEight(selectedChannel), channelCount - 1);
			else
				newIndex = Math.Min(selectedChannel + 1, channelCount - 1);

			await SelectChannel(newIndex);
		};
		shortcutActions[Key.Left] = async (s, e) =>
		{
			int newIndex = 0;
			if (ModKeys.IsCtrlDown())
				newIndex = Math.Max(DecrementToPreviousMultipleOfEight(selectedChannel), 0);
			else
				newIndex = Math.Max(selectedChannel - 1, 0);

			await SelectChannel(newIndex);
		};
		//shortcutActions[Key.P] = (s, e) =>
		//{
		//	GlobalClipProtection = !GlobalClipProtection;
		//	Speech.SpeechManager.Say($"Global Clip Protection " + (GlobalClipProtection ? "On" : "Off"));
		//};
		//shortcutActions[Key.F] = (s, e) => //CTRL+F channel finder
		//{
		//	if (ModifierKeys.IsCtrlDown())
		//	{
		//		var dialog = new ChannelSelectorToolWindow(blindViewModel);
		//		dialog.ShowDialog();

		//		if (dialog.DialogResult.HasValue && dialog.DialogResult.Value)
		//		{
		//			ChannelSelector.SelectedIndex = dialog.Selection;
		//			ChannelSelected((Channel)ChannelSelector.SelectedItem);
		//		}
		//	}
		//	else
		//	{
		//		if (_channel is MicLineInput input)
		//		{
		//			input.phantom = !input.phantom;
		//			if (input.phantom)
		//				Speech.SpeechManager.Say($"Phantom On");
		//			else
		//				Speech.SpeechManager.Say($"Phantom Off");
		//		}
		//	}
		//};
	}

	private async Task<string> GetCurrentChannelName()
	{
		var chName = await _client.GetValue($"ch.{selectedChannel}.cfg.name");
		if (chName?.Length == 0)
			chName = $"Channel {selectedChannel + 1}";

		return chName;
	}

	private async Task SelectChannel(int channel)
	{
		selectedChannel = channel;
		await BuildCurrentChannelControls();
		Speech.SpeechManager.Say(await GetCurrentChannelName());
	}
	public int IncrementToNextMultipleOfEight(int value)
	{
		int remainder = value % 8;
		if (remainder == 0)
		{
			// 'value' is already a multiple of 8
			return value + 8;
		}
		else
		{
			// Calculate the next multiple of 8
			int nextMultipleOfEight = value + (8 - remainder);
			return nextMultipleOfEight;
		}
	}

	public int DecrementToPreviousMultipleOfEight(int value)
	{
		int remainder = value % 8;
		if (remainder == 0)
		{
			// 'value' is already a multiple of 8
			return value - 8;
		}
		else
		{
			// Calculate the previous multiple of 8
			int previousMultipleOfEight = value - remainder;
			return previousMultipleOfEight;
		}
	}

	void HandleNumberKey(int number)
	{
		if (ModKeys.IsCtrlDown())
		{
			ToggleMuteGroup(number);
			return;
		}
	}

	async void ToggleMuteGroup(int num)
	{
		string mutegroupName = (string)await _client.GetValue($"muteGroups.{num}.name");
		bool state = false;
		string path = $"muteGroups.{num}.mute";
		state = bool.Parse(await _client.GetValue(path));
		state = !state;
		await _client.SendUpdate(path, state);
		Speech.SpeechManager.Say($"Mute group {mutegroupName} " + (state ? "Muted" : "Unmuted"));
	}

	private void CustomMenuItem_Click(object sender, RoutedEventArgs e)
	{

	}

	private void exitMenuItem_Click(object sender, RoutedEventArgs e)
	{

	}

	private void LoadProjectMenuButton_Click(object sender, RoutedEventArgs e)
	{

	}
}

public static class ModKeys
{
	[DllImport("user32.dll")]
	private static extern short GetKeyState(int nVirtKey);

	public static bool IsAltDown()
	{
		return GetKeyState(0x12) < 0;
	}

	public static bool IsCtrlDown()
	{
		return GetKeyState(0x11) < 0;
	}

	public static bool IsShiftDown()
	{
		return GetKeyState(0x10) < 0;
	}
}