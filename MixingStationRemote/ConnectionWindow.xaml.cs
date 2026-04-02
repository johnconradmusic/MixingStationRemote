using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MixingStationRemote;
namespace MixingStationRemote;
public partial class ConnectionWindow : Window
{
    private readonly ApiClient _client = new();

    private async void btnSearch_Click(object sender, RoutedEventArgs e)
    {
        await _client.StartSearch(((ConsoleGroup)cmbModels.SelectedItem).consoleId);
        var state = await _client.GetAppState();
        while (state != null && state.state == "searching")
        {
            Debug.WriteLine($"Searching... {state.progress}% - {state.msg}");
            await Task.Delay(500);
            state = await _client.GetAppState();
        }
        await Task.Delay(2000);
        await LoadResults();
    }

    private async void btnRefresh_Click(object sender, RoutedEventArgs e) => await LoadResults();

    private async Task LoadResults()
    {
        var results = await _client.GetSearchResults();

        var validResults = results.results.Where(r => r.name.Length > 0).ToList();

        lstMixers.ItemsSource = validResults;

        btnConnect.IsEnabled = validResults.Count > 0;
    }

    private async void btnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (lstMixers.SelectedItem is not MixerDevice d)
            return;
        if (cmbModels.SelectedItem is not ConsoleGroup console)
            return;
        await _client.ConnectToConsole(d, console);
        await Task.Delay(250);
        bool hasAnnouncedConnecting = false;
        var state = await _client.GetAppState();
        while (state != null && state.state != "connected")
        {
            if (state.state == "connecting" && !hasAnnouncedConnecting)
            {
                Speech.SpeechManager.Say("Connecting to mixer. Please wait.");
                hasAnnouncedConnecting = true;
            }
            Debug.WriteLine($"Connecting... {state.progress}% - {state.state}");
            await Task.Delay(500);
            state = await _client.GetAppState();
        }

        new MainWindow(_client).Show();
        Close();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _client.SetDiscoveryBase(txtStationUrl.Text.Trim());
        var models = await _client.GetSupportedMixerModels();
        cmbModels.ItemsSource = models.consoles;
        cmbModels.IsEnabled = true;

        var state = await _client.GetAppState();
        if (state != null && state.state == "connected")
        {
            var mixer = await _client.GetCurrentMixer();
            string mixerModel = mixer?.currentModel ?? "Unknown";
            var result = MessageBox.Show($"Already connected to {mixerModel}. Continue using this mixer?", "Info", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.No)
            {
                await _client.Disconnect();
            }
            else
            {
                //await _client.ConnectWebsocket();
                new MainWindow(_client).Show();
                Close();
            }
        }
    }

    private void cmbModels_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(e.AddedItems.Count == 0)
            return;
        if (e.AddedItems[0] is ConsoleGroup console)
        {
            Speech.SpeechManager.Say(console.manufacturer + " " + console.name);
            btnSearch.IsEnabled = true;
        }
        if (e.AddedItems[0] is MixerDevice mixer)
        {
            Speech.SpeechManager.Say(mixer.name);
        }


    }

    private void btn_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            Speech.SpeechManager.Say(btn.Content.ToString());

        if (sender is ComboBox cmb)
        {
            if (cmb.SelectedItem == null)
                cmb.SelectedIndex = 0;
            Speech.SpeechManager.Say(cmb.SelectedItem?.ToString() ?? "");
        }

        if (sender is ListBox lst)
        {
            if (lst.SelectedItem == null)
                lst.SelectedIndex = 0;
            Speech.SpeechManager.Say(lst.SelectedItem.ToString());

        }

    }
}