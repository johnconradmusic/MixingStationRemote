using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MixingStationRemote;

public partial class ConnectionWindow : Window
{
    private readonly ApiClient _client = new();
    private readonly ConnectionSettings _settings = ConnectionSettings.Load();
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private bool _isLoadingSettings;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadSettingsIntoUi();
            _client.SetDiscoveryBase(txtStationUrl.Text.Trim());

            var models = await _client.GetSupportedMixerModels();
            cmbModels.ItemsSource = models.consoles;
            cmbModels.IsEnabled = true;
            FocusInitialControl();

            var state = await _client.GetAppState();
            if (state != null && state.state == "connected")
            {
                if (_settings.AutoConnectMixerId.HasValue)
                {
                    OpenMainWindow();
                    return;
                }

                var mixer = await _client.GetCurrentMixer();
                var result = MessageBox.Show($"Already connected to {mixer.currentModel}. Continue using this mixer?", "Info", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    OpenMainWindow();
                    return;
                }

                await _client.Disconnect();
            }

            await TryAutoConnectAsync();
        }
        catch (Exception ex)
        {
            ShowConnectionError("Unable to contact Mixing Station", ex);
        }
    }

    private void LoadSettingsIntoUi()
    {
        _isLoadingSettings = true;
        try
        {
            txtStationUrl.Text = _settings.StationUrl;
            RefreshSavedMixerList();
            UpdateConnectionStatus(GetSavedMixerStatusText());
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void RefreshSavedMixerList(Guid? selectMixerId = null)
    {
        var selectedId = selectMixerId
            ?? (lstSavedMixers.SelectedItem as SavedMixer)?.Id
            ?? _settings.AutoConnectMixerId;

        lstSavedMixers.ItemsSource = null;
        lstSavedMixers.ItemsSource = _settings.SavedMixers
            .OrderByDescending(m => m.LastConnectedAt ?? DateTimeOffset.MinValue)
            .ThenBy(m => m.DisplayName)
            .ToList();

        if (selectedId.HasValue)
            lstSavedMixers.SelectedItem = lstSavedMixers.Items.OfType<SavedMixer>().FirstOrDefault(m => m.Id == selectedId.Value);

        lstSavedMixers_SelectionChanged(this, new SelectionChangedEventArgs(ListBox.SelectionChangedEvent, new List<object>(), new List<object>()));
    }

    private void FocusInitialControl()
    {
        if (lstSavedMixers.Items.Count > 0)
        {
            if (lstSavedMixers.SelectedItem == null)
                lstSavedMixers.SelectedIndex = 0;

            lstSavedMixers.Focus();
            return;
        }

        cmbModels.Focus();
    }

    private async Task TryAutoConnectAsync()
    {
        if (!_settings.AutoConnectMixerId.HasValue)
            return;

        var mixer = _settings.SavedMixers.FirstOrDefault(m => m.Id == _settings.AutoConnectMixerId.Value);
        if (mixer == null)
        {
            _settings.AutoConnectMixerId = null;
            _settings.Save();
            RefreshSavedMixerList();
            return;
        }

        UpdateConnectionStatus($"Auto connecting to {mixer.DisplayName}...", speak: true);
        SetConnectionControlsEnabled(false);

        try
        {
            await ConnectSavedMixerAsync(mixer);
        }
        catch (Exception ex)
        {
            UpdateConnectionStatus($"Auto connect failed: {ex.Message}", speak: true);
            SetConnectionControlsEnabled(true);
        }
    }

    private async void btnConnectSaved_Click(object sender, RoutedEventArgs e)
    {
        if (lstSavedMixers.SelectedItem is not SavedMixer mixer)
            return;

        btnConnectSaved.IsEnabled = false;
        UpdateConnectionStatus($"Connecting to {mixer.DisplayName}...", speak: true);

        try
        {
            await ConnectSavedMixerAsync(mixer);
        }
        catch (Exception ex)
        {
            ShowConnectionError("Connection failed", ex);
            btnConnectSaved.IsEnabled = lstSavedMixers.SelectedItem is SavedMixer;
        }
    }

    private async Task ConnectSavedMixerAsync(SavedMixer mixer)
    {
        var console = FindConsole(mixer.ConsoleId)
            ?? throw new InvalidOperationException($"Saved mixer model is no longer available: {mixer.ConsoleName}");

        var device = new MixerDevice
        {
            modelId = mixer.ConsoleId,
            ip = mixer.Ip,
            name = mixer.Name,
            model = mixer.Model,
            version = mixer.Version
        };

        await _client.ConnectToConsole(device, console);
        await Task.Delay(250);
        await WaitForConnectedAsync();

        mixer.LastConnectedAt = DateTimeOffset.Now;
        _settings.StationUrl = txtStationUrl.Text.Trim();
        _settings.Save();

        OpenMainWindow();
    }

    private void btnForgetSaved_Click(object sender, RoutedEventArgs e)
    {
        if (lstSavedMixers.SelectedItem is not SavedMixer mixer)
            return;

        var result = MessageBox.Show($"Forget {mixer.DisplayName}?", "Forget Mixer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        _settings.SavedMixers.RemoveAll(m => m.Id == mixer.Id);
        if (_settings.AutoConnectMixerId == mixer.Id)
            _settings.AutoConnectMixerId = null;

        _settings.Save();
        RefreshSavedMixerList();
        UpdateConnectionStatus($"Forgot {mixer.DisplayName}. {GetSavedMixerStatusText()}", speak: true);
    }

    private void txtStationUrl_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyStationUrl();
    }

    private void ApplyStationUrl()
    {
        var url = txtStationUrl.Text.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            _client.SetDiscoveryBase(url);
            _settings.StationUrl = url;
            _settings.Save();
        }
    }

    private async void btnSearch_Click(object sender, RoutedEventArgs e)
    {
        ApplyStationUrl();
        if (cmbModels.SelectedItem is not ConsoleGroup console)
            return;

        btnSearch.IsEnabled = false;
        btnSaveAndConnect.IsEnabled = false;
        UpdateConnectionStatus($"Searching for {GetConsoleDisplayName(console)} mixers...", speak: true);

        try
        {
            await _client.StartSearch(console.consoleId);
            await WaitWhileStateAsync("searching", SearchTimeout, "Search");
            await Task.Delay(2000);
            await LoadResults();
            btnSearch.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowConnectionError("Search failed", ex);
            btnSearch.IsEnabled = true;
        }
    }

    private async void btnRefresh_Click(object sender, RoutedEventArgs e) => await LoadResults();

    private async Task LoadResults()
    {
        var results = await _client.GetSearchResults();
        var validResults = results.results.Where(r => !string.IsNullOrWhiteSpace(r.name)).ToList();

        lstMixers.ItemsSource = validResults;
        btnSaveAndConnect.IsEnabled = validResults.Count > 0;

        if (validResults.Count == 0)
        {
            UpdateConnectionStatus("No mixers found.", speak: true);
            return;
        }

        lstMixers.SelectedIndex = 0;
        UpdateConnectionStatus($"{validResults.Count} mixer{(validResults.Count == 1 ? "" : "s")} found. First result: {GetMixerDisplayName(validResults[0])}", speak: true);
    }

    private async void btnSaveAndConnect_Click(object sender, RoutedEventArgs e)
    {
        if (lstMixers.SelectedItem is not MixerDevice device)
            return;
        if (cmbModels.SelectedItem is not ConsoleGroup console)
            return;

        btnSaveAndConnect.IsEnabled = false;
        UpdateConnectionStatus($"Saving and connecting to {GetMixerDisplayName(device)}...", speak: true);

        try
        {
            await ConnectAndOpenAsync(device, console, saveMixer: true);
        }
        catch (Exception ex)
        {
            ShowConnectionError("Connection failed", ex);
            btnSaveAndConnect.IsEnabled = lstMixers.SelectedItem is MixerDevice;
        }
    }

    private async Task ConnectAndOpenAsync(MixerDevice device, ConsoleGroup console, bool saveMixer)
    {
        await _client.ConnectToConsole(device, console);
        await Task.Delay(250);
        await WaitForConnectedAsync();

        if (saveMixer)
            SaveMixer(device, console);

        OpenMainWindow();
    }

    private void SaveMixer(MixerDevice device, ConsoleGroup console)
    {
        _settings.StationUrl = txtStationUrl.Text.Trim();
        var saved = _settings.AddOrUpdate(device, console);
        if (chkAutoConnect.IsChecked == true)
            _settings.AutoConnectMixerId = saved.Id;

        _settings.Save();
        RefreshSavedMixerList(saved.Id);
        UpdateConnectionStatus($"Saved {saved.DisplayName}.", speak: true);
    }

    private void OpenMainWindow()
    {
        new MainWindow(_client).Show();
        Close();
    }

    private ConsoleGroup? FindConsole(int consoleId) =>
        cmbModels.Items.OfType<ConsoleGroup>().FirstOrDefault(c => c.consoleId == consoleId);

    private string GetSavedMixerStatusText()
    {
        if (_settings.SavedMixers.Count == 0)
            return "No saved mixers. Choose a model below, search, then Save and Connect.";

        if (_settings.AutoConnectMixerId.HasValue)
        {
            var autoMixer = _settings.SavedMixers.FirstOrDefault(m => m.Id == _settings.AutoConnectMixerId.Value);
            if (autoMixer != null)
                return $"Startup auto-connect: {autoMixer.DisplayName}";
        }

        return "Select a saved mixer and connect.";
    }

    private static string GetConsoleDisplayName(ConsoleGroup console)
    {
        var manufacturer = string.IsNullOrWhiteSpace(console.manufacturer) ? string.Empty : console.manufacturer + " ";
        return $"{manufacturer}{console.name}".Trim();
    }

    private static string GetMixerDisplayName(MixerDevice mixer)
    {
        var name = string.IsNullOrWhiteSpace(mixer.name) ? mixer.ip : mixer.name;
        if (string.IsNullOrWhiteSpace(mixer.model))
            return $"{name} at {mixer.ip}";

        return $"{name}, {mixer.model}, at {mixer.ip}";
    }

    private void UpdateConnectionStatus(string message, bool speak = false)
    {
        txtConnectionStatus.Text = message;
        if (speak && !string.IsNullOrWhiteSpace(message))
            Speech.SpeechManager.Say(message, false);
    }

    private void SetConnectionControlsEnabled(bool isEnabled)
    {
        lstSavedMixers.IsEnabled = isEnabled;
        btnConnectSaved.IsEnabled = isEnabled && lstSavedMixers.SelectedItem is SavedMixer;
        btnForgetSaved.IsEnabled = isEnabled && lstSavedMixers.SelectedItem is SavedMixer;
        chkAutoConnect.IsEnabled = isEnabled && lstSavedMixers.SelectedItem is SavedMixer;
        cmbModels.IsEnabled = isEnabled;
        btnSearch.IsEnabled = isEnabled && cmbModels.SelectedItem is ConsoleGroup;
        btnRefresh.IsEnabled = isEnabled;
        lstMixers.IsEnabled = isEnabled;
        btnSaveAndConnect.IsEnabled = isEnabled && lstMixers.SelectedItem is MixerDevice;
    }

    private void lstSavedMixers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var mixer = lstSavedMixers.SelectedItem as SavedMixer;
        var hasSelection = mixer != null;
        btnConnectSaved.IsEnabled = hasSelection;
        btnForgetSaved.IsEnabled = hasSelection;
        chkAutoConnect.IsEnabled = hasSelection;

        _isLoadingSettings = true;
        try
        {
            chkAutoConnect.IsChecked = mixer != null && _settings.AutoConnectMixerId == mixer.Id;
        }
        finally
        {
            _isLoadingSettings = false;
        }

        if (mixer != null)
            Speech.SpeechManager.Say($"Saved mixer: {mixer.DisplayName}");
    }

    private void chkAutoConnect_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
            return;

        if (chkAutoConnect.IsChecked == true)
        {
            if (lstSavedMixers.SelectedItem is not SavedMixer mixer)
            {
                chkAutoConnect.IsChecked = false;
                UpdateConnectionStatus("Select a saved mixer before enabling startup auto-connect.", speak: true);
                return;
            }

            _settings.AutoConnectMixerId = mixer.Id;
            UpdateConnectionStatus($"Startup auto-connect enabled for {mixer.DisplayName}.", speak: true);
        }
        else
        {
            _settings.AutoConnectMixerId = null;
            UpdateConnectionStatus("Startup auto-connect disabled.", speak: true);
        }

        _settings.StationUrl = txtStationUrl.Text.Trim();
        _settings.Save();
    }

    private async Task WaitWhileStateAsync(string stateName, TimeSpan timeout, string operationName)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var state = await GetRequiredAppState();
            if (IsFailureState(state.state))
                throw new InvalidOperationException($"Mixing Station reported state '{state.state}': {state.msg}");

            if (state.state != stateName)
                return;

            Debug.WriteLine($"{operationName}... {state.progress}% - {state.msg}");
            await Task.Delay(PollInterval);
        }

        throw new TimeoutException($"{operationName} did not finish within {timeout.TotalSeconds:0} seconds.");
    }

    private async Task WaitForConnectedAsync()
    {
        var deadline = DateTime.UtcNow + ConnectTimeout;
        var hasAnnouncedConnecting = false;

        while (DateTime.UtcNow < deadline)
        {
            var state = await GetRequiredAppState();
            if (state.state == "connected")
                return;

            if (IsFailureState(state.state))
                throw new InvalidOperationException($"Mixing Station reported state '{state.state}': {state.msg}");

            if (state.state == "connecting" && !hasAnnouncedConnecting)
            {
                Speech.SpeechManager.Say("Connecting to mixer. Please wait.");
                hasAnnouncedConnecting = true;
            }

            Debug.WriteLine($"Connecting... {state.progress}% - {state.state}");
            await Task.Delay(PollInterval);
        }

        throw new TimeoutException($"Connection did not finish within {ConnectTimeout.TotalSeconds:0} seconds.");
    }

    private async Task<AppState> GetRequiredAppState()
    {
        var state = await _client.GetAppState();
        return state ?? throw new InvalidOperationException("Could not read Mixing Station app state.");
    }

    private static bool IsFailureState(string state) =>
        state.Equals("error", StringComparison.OrdinalIgnoreCase) ||
        state.Equals("failed", StringComparison.OrdinalIgnoreCase);

    private static void ShowConnectionError(string title, Exception ex)
    {
        Debug.WriteLine($"{title}: {ex}");
        MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void cmbModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
            return;

        if (e.AddedItems[0] is ConsoleGroup console)
        {
            Speech.SpeechManager.Say(GetConsoleDisplayName(console));
            btnSearch.IsEnabled = true;
        }

        if (e.AddedItems[0] is MixerDevice mixer)
        {
            Speech.SpeechManager.Say(GetMixerDisplayName(mixer));
            btnSaveAndConnect.IsEnabled = true;
        }
    }

    private void Control_GotFocus(object sender, RoutedEventArgs e)
    {
        Speech.SpeechManager.Say(GetFocusAnnouncement(sender));
    }

    private string GetFocusAnnouncement(object sender)
    {
        if (sender is Label label)
            return label.Content?.ToString() ?? string.Empty;

        if (sender == txtStationUrl)
            return $"Mixing Station URL, {txtStationUrl.Text}";

        if (sender == lstSavedMixers)
        {
            if (lstSavedMixers.Items.Count == 0)
                return "Saved mixers list. No saved mixers.";

            if (lstSavedMixers.SelectedItem == null)
                lstSavedMixers.SelectedIndex = 0;

            return lstSavedMixers.SelectedItem is SavedMixer mixer
                ? $"Saved mixers list. {lstSavedMixers.Items.Count} saved. {mixer.DisplayName}"
                : $"Saved mixers list. {lstSavedMixers.Items.Count} saved.";
        }

        if (sender == chkAutoConnect)
        {
            var state = chkAutoConnect.IsChecked == true ? "checked" : "not checked";
            return $"Connect selected saved mixer on startup, {state}";
        }

        if (sender == btnConnectSaved)
            return lstSavedMixers.SelectedItem is SavedMixer mixer
                ? $"Connect to {mixer.DisplayName}"
                : "Connect saved mixer";

        if (sender == btnForgetSaved)
            return lstSavedMixers.SelectedItem is SavedMixer mixer
                ? $"Forget {mixer.DisplayName}"
                : "Forget saved mixer";

        if (sender == cmbModels)
        {
            if (cmbModels.SelectedItem == null && cmbModels.Items.Count > 0)
                cmbModels.SelectedIndex = 0;

            return cmbModels.SelectedItem is ConsoleGroup console
                ? $"Mixer model combo box. {GetConsoleDisplayName(console)}"
                : "Mixer model combo box";
        }

        if (sender == btnSearch)
            return cmbModels.SelectedItem is ConsoleGroup console
                ? $"Search for {GetConsoleDisplayName(console)} mixers"
                : "Search";

        if (sender == btnRefresh)
            return "Refresh search results";

        if (sender == lstMixers)
        {
            if (lstMixers.Items.Count == 0)
                return "Mixer search results list. No search results.";

            if (lstMixers.SelectedItem == null)
                lstMixers.SelectedIndex = 0;

            return lstMixers.SelectedItem is MixerDevice mixer
                ? $"Mixer search results list. {lstMixers.Items.Count} result{(lstMixers.Items.Count == 1 ? "" : "s")}. {GetMixerDisplayName(mixer)}"
                : "Mixer search results list";
        }

        if (sender == btnSaveAndConnect)
            return lstMixers.SelectedItem is MixerDevice mixer
                ? $"Save and connect to {GetMixerDisplayName(mixer)}"
                : "Save and connect";

        if (sender is Button btn)
            return btn.Content?.ToString() ?? string.Empty;

        return string.Empty;
    }
}
