using System.Windows.Input;

namespace MixingStationRemote;

public partial class BooleanParameterControl : ParameterControlBase
{
    public BooleanParameterControl()
    {
        InitializeComponent();
    }

    public bool Invert { get; set; }

    protected override void RefreshFromParameter()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshFromParameter);
            return;
        }

        label.Text = Caption;

        if (!IsReady)
        {
            textBox.Text = "Loading...";
            return;
        }

        var b = ToBool(Parameter?.Value, Invert);
        textBox.Text = b ? "On" : "Off";

        if (IsFocused)
            AnnounceValue();
    }

    protected override void AnnounceValue()
    {
        var b = ToBool(Parameter?.Value, Invert);
        Speech.SpeechManager.Say(b ? "On" : "Off");
    }

    protected override void AnnounceFocus()
    {
        var b = ToBool(Parameter?.Value, Invert);
        Speech.SpeechManager.Say($"{Caption} ({(b ? "On" : "Off")})");
    }

    private async void ParameterControlBase_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsReady)
            await EnsureLoaded();

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AnnounceValue();
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            if(Invert)
                await SetValueFromUser(true);
            else
                await SetValueFromUser(false);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (Invert)
                await SetValueFromUser(false);
            else
                await SetValueFromUser(true);
        }
        else if (e.Key == Key.Delete)
        {
            if (Default != null)
            {
                e.Handled = true;
                await SetValueFromUser(Default);
            }

        }
    }

    private static bool ToBool(object? value, bool invert)
    {
        if (invert)
        {
            return value switch
            {
                bool b => !b,
                string s when bool.TryParse(s, out var parsed) => !parsed,
                int i => i == 0,
                long l => l == 0,
                _ => false
            };

        }
        else
            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                int i => i != 0,
                long l => l != 0,
                _ => false
            };
    }
}