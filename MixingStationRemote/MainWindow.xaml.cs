using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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

    int selectedMix = -1;

    private async Task SelectMix(int mix)
    {
        selectedMix = mix;
        await BuildCurrentChannelControls();
    }

    List<StackPanel> sectionPanels = new List<StackPanel>();
    StackPanel currentPanel;
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _client.ParameterUpdated += OnParameterValueUpdatedFromWs;
        var mixer = await _client.GetCurrentMixer();
        Title = mixer.currentModel + " - Mixing Station Remote";


        arc = await _client.GetConsoleArchitecture();
        matrix = ChannelRoutingMatrixBuilder.Build(arc);
        // Stay on UI thread after await, because you're building controls below
        //	var defs = await _client.GetAllParametersWithDefinitions();
        await _client.ConnectWebsocket();

        await Task.Delay(2500); //give some time for initial data to arrive, so we can group channels by number

        channelCount = arc.totalChannels;
        selectedChannel = 0;
        InitializeShortcuts();

        await SelectChannel(0);
    }

    private readonly List<Control> _headings = new();

    private Label CreateHeading(Panel parent, string text)
    {
        var heading = new Label
        {
            Content = text,
            Focusable = true,
            IsTabStop = true
        };

        heading.GotFocus += (s, e) =>
        {
            Speech.SpeechManager.Say(heading.Content);
        };

        _headings.Add(heading);
        parent.Children.Add(heading);
        return heading;
    }
    private async Task BuildCurrentChannelControls()
    {
        createdControls.Clear();

        await BuildMixSection();
        await BuildPreampSection();
        await BuildEqSection();
        await BuildCompressorSection();
        await BuildGateSection();
        await BuildLimiterSection();
        await BuildSendsSection();
        await BuildMuteGroupsSection();

    }

    private async Task BuildMuteGroupsSection()
    {
        muteGroupsPanel.Children.Clear();
        CreateHeading(muteGroupsPanel, "Mute Groups");
    }

    private async Task BuildSendsSection()
    {
        sendsPanel.Children.Clear();
        CreateHeading(sendsPanel, "Sends");
        var thisChannel = matrix.Get(selectedChannel);
        var outgoing = matrix.GetOutgoing(selectedChannel);
        foreach (var target in outgoing)
        {
            var targetCh = matrix.Get(target);
            var name = await GetChannelName(target);
            await AddParameterControl(sendsPanel, $"send to {name}", $"ch.{selectedChannel}.mix.sends.{targetCh.LocalIndex}.lvl", _client);
            bool targetMixIsLinked = await _client.GetValue($"ch.{target}.link.linked") == "true";
            //if (targetMixIsLinked && )
            await AddParameterControl(sendsPanel, $"pan to {name}", $"ch.{selectedChannel}.mix.sends.{targetCh.LocalIndex}.pan", _client);

        }
    }

    private async Task BuildLimiterSection()
    {
        limiterPanel.Children.Clear();
        CreateHeading(limiterPanel, "Limiter");
        await AddToggleControl(limiterPanel, "Limiter enable", $"ch.{selectedChannel}.limiter.on", _client);
        await AddParameterControl(limiterPanel, "Threshold", $"ch.{selectedChannel}.limiter.thr", _client);
    }

    private async Task BuildGateSection()
    {
        gatePanel.Children.Clear();
        CreateHeading(gatePanel, "Gate");

        await AddToggleControl(gatePanel, "Gate enable", $"ch.{selectedChannel}.gate.on", _client);
        await AddParameterControl(gatePanel, "Threshold", $"ch.{selectedChannel}.gate.thr", _client);
        await AddParameterControl(gatePanel, "Range", $"ch.{selectedChannel}.gate.range", _client);
        await AddParameterControl(gatePanel, "Attack", $"ch.{selectedChannel}.gate.attack", _client);
        await AddParameterControl(gatePanel, "Release", $"ch.{selectedChannel}.gate.release", _client);
        await AddToggleControl(gatePanel, "Expander", $"ch.{selectedChannel}.gate.expander", _client);
        //Filter
    }

    private async Task BuildCompressorSection()
    {
        compressorPanel.Children.Clear();
        CreateHeading(compressorPanel, "Compressor");

        await AddToggleControl(compressorPanel, "Compressor enable", $"ch.{selectedChannel}.dyn.on", _client);
        await AddParameterControl(compressorPanel, "Ratio", $"ch.{selectedChannel}.dyn.ratio", _client);
        await AddParameterControl(compressorPanel, "Threshold", $"ch.{selectedChannel}.dyn.thr", _client);
        await AddParameterControl(compressorPanel, "Gain", $"ch.{selectedChannel}.dyn.gain", _client);
        await AddToggleControl(compressorPanel, "Auto Time", $"ch.{selectedChannel}.dyn.autoTime", _client);
        await AddParameterControl(compressorPanel, "Attack", $"ch.{selectedChannel}.dyn.attack", _client);
        await AddParameterControl(compressorPanel, "Release", $"ch.{selectedChannel}.dyn.release", _client);
        await AddToggleControl(compressorPanel, "Soft Knee", $"ch.{selectedChannel}.dyn.softKnee", _client);
        await AddParameterControl(compressorPanel, "Knee", $"ch.{selectedChannel}.dyn.knee", _client);
        //   await AddParameterControl(compressorPanel, "Filter", $"ch.{selectedChannel}.dyn.knee", _client);

    }

    private async Task BuildEqSection()
    {
        var def = await _client.GetDefForPath("ch.0.peq.bands.0.type");

        var bands = await _client.GetNumberOfEntries($"ch.{selectedChannel}.peq.bands");
        eqPanel.Children.Clear();
        CreateHeading(eqPanel, "EQ");
        await AddToggleControl(eqPanel, "EQ enable", $"ch.{selectedChannel}.peq.on", _client);
        for (int i = 0; i < bands; i++)
        {
            CreateHeading(eqPanel, $"Band {i + 1}");

            await AddToggleControl(eqPanel, $"Band {i + 1} enable", $"ch.{selectedChannel}.peq.bands.{i}.on", _client);
            await AddParameterControl(eqPanel, "Frequency", $"ch.{selectedChannel}.peq.bands.{i}.freq", _client);
            await AddParameterControl(eqPanel, "Gain", $"ch.{selectedChannel}.peq.bands.{i}.gain", _client);
            await AddParameterControl(eqPanel, "Q", $"ch.{selectedChannel}.peq.bands.{i}.q", _client);
            //Add List Control for type (low shelf, high shelf, bell, etc)
        }
    }

    private async Task BuildPreampSection()
    {
        preampPanel.Children.Clear();
        CreateHeading(preampPanel, "Preamp");
        await AddParameterControl(preampPanel, "trim", $"ch.{selectedChannel}.headamp.gain", _client);
        await AddParameterControl(preampPanel, "hpf", $"ch.{selectedChannel}.preamp.filter.0.freq", _client);
        await AddToggleControl(preampPanel, "phase invert", $"ch.{selectedChannel}.preamp.inv", _client);

    }

    private async Task BuildMixSection()
    {
        mixPanel.Children.Clear();
        CreateHeading(mixPanel, "Channel");
        if (selectedMix == -1)
        {
            await AddParameterControl(mixPanel, "level", $"ch.{selectedChannel}.mix.lvl", _client);
            await AddParameterControl(mixPanel, "pan", $"ch.{selectedChannel}.mix.pan", _client);
            await AddToggleControl(mixPanel, "solo", $"ch.{selectedChannel}.solo", _client);
        }
        else
        {
            await AddParameterControl(mixPanel, "level", $"ch.{selectedChannel}.mix.sends.{selectedMix}.lvl", _client);
            await AddParameterControl(mixPanel, "pan", $"ch.{selectedChannel}.mix.sends.{selectedMix}.pan", _client);
        }
    }

    Dictionary<string, FrameworkElement> createdControls = new();

    private async Task AddParameterControl(Panel parent, string name, string path, ApiClient client)
    {

        var param = await client.GetDefForPath(path);

        if (param == null)
        {
            Debug.WriteLine($"Parameter {path} not found");
            return;
        }

        var control = new NumericParameterControl
        {
            Parameter = param,
            Client = client,
            Title = name
        };

        parent.Children.Add(control);

        control.UserValueChanged += async (_, value) =>
            await _client.SendUpdate(path, value);

        //await _client.Subscribe(path);

        createdControls[name] = control;
    }
    private async Task AddToggleControl(Panel parent, string name, string path, ApiClient client)
    {
        var param = await client.GetDefForPath(path);

        if (param == null)
        {
            Debug.WriteLine($"Parameter {path} not found");
            return;
        }

        var control = new BooleanParameterControl
        {
            Parameter = param,
            Client = client,
            Title = name
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
    private ConsoleArchitecture arc;
    private ChannelRoutingMatrix matrix;

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

    private async Task<bool?> ToggleBooleanParameter(string path)
    {
        var val = await _client.GetValue(path);
        if (val == null)
            return null;
        if (bool.TryParse(val, out var boolVal))
        {
            boolVal = !boolVal;
            await _client.SendUpdate(path, boolVal);
            return boolVal;
        }
        return null;
    }

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

        shortcutActions[Key.Escape] = async (s, e) => await SelectMix(-1);

        shortcutActions[Key.V] = (s, e) => createdControls["level"].Focus();

        shortcutActions[Key.H] = (s, e) =>
        {
            if (ModKeys.IsShiftDown())
                PreviousHeading();
            else
                NextHeading();

        };
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
        shortcutActions[Key.X] = async (s, e) =>
        {
            var channelIsOn = await ToggleBooleanParameter($"ch.{selectedChannel}.mix.on");
            if (channelIsOn == null)
            {
                Speech.SpeechManager.Say($"No on/off parameter found for this channel");
                return;
            }
            if (!channelIsOn.Value)
                Speech.SpeechManager.Say($"Muted");
            else
                Speech.SpeechManager.Say($"Unmuted");
        };
        shortcutActions[Key.S] = async (s, e) =>
        {
            var channelIsSolo = await ToggleBooleanParameter($"ch.{selectedChannel}.solo");
            if (channelIsSolo == null)
            {
                Speech.SpeechManager.Say($"No on/off parameter found for this channel");
                return;
            }
            if (!channelIsSolo.Value)
                Speech.SpeechManager.Say($"Solo Off");
            else
                Speech.SpeechManager.Say($"Solo On");
        };
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
        shortcutActions[Key.F] = async (s, e) => //CTRL+F channel finder
        {
            bool controlIsDown = ModKeys.IsCtrlDown();
            if (controlIsDown)
            {
                //var dialog = new ChannelSelectorToolWindow(blindViewModel);
                //dialog.ShowDialog();

                //if (dialog.DialogResult.HasValue && dialog.DialogResult.Value)
                //{
                //    ChannelSelector.SelectedIndex = dialog.Selection;
                //    ChannelSelected((Channel)ChannelSelector.SelectedItem);
                //}
            }
            else
            {
                var phantom = await ToggleBooleanParameter($"ch.{selectedChannel}.preamp.+48v");
                if (phantom == null)
                {
                    Speech.SpeechManager.Say($"No phantom found for this channel");
                    return;
                }
                if (phantom.Value)
                    Speech.SpeechManager.Say($"Phantom Power On");
                else
                    Speech.SpeechManager.Say($"Phantom Power Off");
            }
        };
    }

    private void NextHeading()
    {
        var current = Keyboard.FocusedElement as DependencyObject;

        int currentIndex = _headings.FindIndex(h => IsDescendantOf(current, h));

        int nextIndex = (currentIndex + 1) % _headings.Count;

        var next = _headings[nextIndex];

        next.Focus();
        Keyboard.Focus(next);
    }

    private void PreviousHeading()
    {
        var current = Keyboard.FocusedElement as DependencyObject;
        int currentIndex = _headings.FindIndex(h => IsDescendantOf(current, h));
        int previousIndex = (currentIndex - 1 + _headings.Count) % _headings.Count;
        var previous = _headings[previousIndex];
        previous.Focus();
        Keyboard.Focus(previous);
    }

    private async Task SelectChannel(int channel)
    {
        selectedChannel = channel;
        await BuildCurrentChannelControls();
        BuildCurrentChannelContextMenu();
        Speech.SpeechManager.Say(await GetChannelName(selectedChannel));
    }

    private async void BuildCurrentChannelContextMenu()
    {
        contextMenu.Items.Clear();
        await CreateMenuItem($"ch.{selectedChannel}.mix.on", "Mute", true, true);
        await CreateMenuItem($"ch.{selectedChannel}.solo", "Solo", true);
        await CreateMenuItem($"ch.{selectedChannel}.preamp.+48v", "Phantom", true);
        await CreateMenuItem($"ch.{selectedChannel}.preamp.inv", "Phase Invert", true);
        await CreateMenuItem($"ch.{selectedChannel}.link.linked", "Stereo Link", true);
    }

    private async Task<string> GetChannelName(int i)
    {
        var name = await _client.GetValue($"ch.{i}.cfg.name");
        return name ?? $"Channel {i + 1}";
    }

    private async Task CreateMenuItem(string path, string header, bool isCheckable = false, bool inverted = false)
    {
        var param = await _client.GetDefForPath(path);

        if (param == null)
        {
            Debug.WriteLine($"Parameter {path} not found");
            return;
        }

        var item = new CustomMenuItem
        {
            Header = header,
            Path = path,
            IsCheckable = isCheckable,
            Inverted = inverted,
            Client = _client
        };
        contextMenu.Items.Add(item);
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

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Apps)
        {
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
    private bool IsDescendantOf(DependencyObject? child, DependencyObject parent)
    {
        while (child != null)
        {
            if (child == parent)
                return true;

            child = VisualTreeHelper.GetParent(child);
        }

        return false;
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