using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MixingStationRemote;

public partial class StringParameterControl : ParameterControlBase
{
    private bool _updatingTextBox;

    public StringParameterControl()
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

        _updatingTextBox = true;
        try
        {
            label.Text = Caption;
            editor.Text = Parameter?.Value?.ToString() ?? string.Empty;
        }
        finally
        {
            _updatingTextBox = false;
        }
    }

    protected override string FormatValue(object? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    protected override void AnnounceFocus()
    {
        var value = Parameter?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value))
            Speech.SpeechManager.Say($"{Caption} (blank)");
        else
            Speech.SpeechManager.Say($"{Caption} ({value})");
    }

    protected override void AnnounceValue()
    {
        var value = Parameter?.Value?.ToString();
        Speech.SpeechManager.Say(string.IsNullOrWhiteSpace(value) ? "blank" : value);
    }

    private async void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsReady)
            await EnsureLoaded();


        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (editor.IsFocused)
            {
                await CommitEditorText();
                AnnounceValue();
                editor.Focusable  = false;
            }else
            {                
                editor.Focusable = true;
                editor.Focus();
                Keyboard.Focus(editor);
            }
            return;
        }

        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            editor.Text = string.Empty;
            await CommitEditorText();
            return;
        }
    }

    private async void Editor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingTextBox)
            return;

        await CommitEditorText();
    }

    private async Task CommitEditorText()
    {
        if (Parameter == null)
            return;

        var current = Parameter.Value?.ToString() ?? string.Empty;
        var edited = editor.Text ?? string.Empty;

        if (string.Equals(current, edited, StringComparison.Ordinal))
            return;

        await SetValueFromUser(edited);
    }
}