using System.Windows;

using MixingStationRemote;
namespace MixingStationRemote;
public partial class ConnectionWindow : Window
{
	private readonly ApiClient _client = new();

	private async void btnSearch_Click(object sender, RoutedEventArgs e)
	{
		await _client.StartSearch(((ConsoleGroup)cmbModels.SelectedItem).consoleId);
		await LoadResults();
	}

	private async void btnRefresh_Click(object sender, RoutedEventArgs e) => await LoadResults();

	private async Task LoadResults()
	{
		var results = await _client.GetSearchResults();
		lstMixers.ItemsSource = results.results;
		btnConnect.IsEnabled = results.results.Count > 0;
	}

	private async void btnConnect_Click(object sender, RoutedEventArgs e)
	{
		if (lstMixers.SelectedItem is not MixerDevice d)
			return;
		if (cmbModels.SelectedItem is not ConsoleGroup console)
			return;
		_client.ConnectToConsole(d, console);
		await Task.Delay(250);

		var state = await _client.GetAppState();
		while (state != null && state.state != "connected")
		{
			await Task.Delay(500);
			state = await _client.GetAppState();
		}

		new MainWindow(_client).Show();
		Close();
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		_client.SetDiscoveryBase(txtStationUrl.Text.Trim());

		var state = await _client.GetAppState();
		if (state != null && state.state == "connected")
		{
			var mixer = await _client.GetCurrentMixer();

			new MainWindow(_client).Show();
			Close();
			return;
		}

		var models = await _client.GetSupportedMixerModels();
		cmbModels.ItemsSource = models.consoles;
		cmbModels.IsEnabled = true;
	}

	private void cmbModels_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
	{
		btnSearch.IsEnabled = true;
	}
}