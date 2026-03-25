using System;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;

namespace MixingStationRemote;

public partial class EnumParameterControl : ParameterControlBase
{
    public EnumParameterControl()
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

        label.Text = Caption;
        textBox.Text = GetDisplayValue();

        if (IsFocused && IsReady)
            AnnounceValue();
    }

    protected override string FormatValue(object? value)
    {
        var entry = GetCurrentEntry();
        if (entry != null)
            return entry.Name;

        return base.FormatValue(value);
    }

    protected override void AnnounceValue()
    {
        Speech.SpeechManager.Say(GetDisplayValue());
    }

    protected override void AnnounceFocus()
    {
        Speech.SpeechManager.Say($"{Caption} ({GetDisplayValue()})");
    }

    private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsReady)
            await EnsureLoaded();

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AnnounceValue();
            return;
        }

        if (Parameter?.Definition?.EnumEntries == null || Parameter.Definition.EnumEntries.Count == 0)
            return;

        int delta = 0;

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            delta = 1;
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            delta = -1;
        }
        else if (e.Key == Key.PageUp)
        {
            e.Handled = true;
            delta = 5;
        }
        else if (e.Key == Key.PageDown)
        {
            e.Handled = true;
            delta = -5;
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await SetToFirstEntry();
            return;
        }

        if (delta != 0)
            await MoveSelection(delta);
    }

    private async Task SetToFirstEntry()
    {
        var entries = Parameter?.Definition?.EnumEntries;
        if (entries == null || entries.Count == 0)
            return;

        await SetValueFromUser(entries[0].ID);
    }

    private async Task MoveSelection(int delta)
    {
        var entries = Parameter?.Definition?.EnumEntries;
        if (entries == null || entries.Count == 0)
            return;

        var currentIndex = GetCurrentEntryIndex();
        if (currentIndex < 0)
            currentIndex = 0;

        var nextIndex = Math.Clamp(currentIndex + delta, 0, entries.Count - 1);

        if (nextIndex == currentIndex)
        {
            AnnounceValue();
            return;
        }

        await SetValueFromUser(entries[nextIndex].ID);
    }

    private EnumEntry? GetCurrentEntry()
    {
        var entries = Parameter?.Definition?.EnumEntries;
        if (entries == null || entries.Count == 0)
            return null;

        if (!TryGetIntValue(Parameter?.Value, out var selectedId))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ID == selectedId)
                return entries[i];
        }

        return null;
    }

    private int GetCurrentEntryIndex()
    {
        var entries = Parameter?.Definition?.EnumEntries;
        if (entries == null || entries.Count == 0)
            return -1;

        if (!TryGetIntValue(Parameter?.Value, out var selectedId))
            return -1;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ID == selectedId)
                return i;
        }

        return -1;
    }

    private string GetDisplayValue()
    {
        if (!IsReady)
            return "Loading...";

        var entry = GetCurrentEntry();
        if (entry != null)
            return entry.Name;

        return $"Unknown ({Parameter?.Value})";
    }

    private static bool TryGetIntValue(object? value, out int result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;

            case long l when l >= int.MinValue && l <= int.MaxValue:
                result = (int)l;
                return true;

            case double d when d >= int.MinValue && d <= int.MaxValue:
                result = (int)d;
                return true;

            case float f when f >= int.MinValue && f <= int.MaxValue:
                result = (int)f;
                return true;

            case string s when int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;

            default:
                result = 0;
                return false;
        }
    }
}