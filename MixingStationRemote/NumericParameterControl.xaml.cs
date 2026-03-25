using System.Windows.Input;

namespace MixingStationRemote;

public partial class NumericParameterControl : ParameterControlBase
{
    public NumericParameterControl()
    {
        InitializeComponent();
    }

    protected override void RefreshFromParameter()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshFromParameter);
            return;
        }
        if (IsReady)
        {

        }
        label.Text = Caption;
        textBox.Text = IsReady ? ValueString : "Loading...";
    }

    private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsReady)
            await EnsureLoaded();

        double delta = 0;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AnnounceValue();
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (Default != null)
            {
                e.Handled = true;
                await SetValueFromUser(Default);
            }
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            delta = GetSmallStep();
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            delta = -GetSmallStep();
        }
        else if (e.Key == Key.PageUp)
        {
            e.Handled = true;
            delta = GetLargeStep();
        }
        else if (e.Key == Key.PageDown)
        {
            e.Handled = true;
            delta = -GetLargeStep();
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            delta /= 10.0;

        if (delta != 0)
            await NudgeValue(delta);
    }

    private double GetSmallStep()
    {
        return (Parameter?.Definition?.Delta ?? 0.01) * 10;
    }

    private double GetLargeStep()
    {
        return (Parameter?.Definition?.Delta ?? 0.01) * 100;
    }
}