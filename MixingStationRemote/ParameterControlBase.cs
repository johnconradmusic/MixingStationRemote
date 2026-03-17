using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MixingStationRemote;

public abstract class ParameterControlBase : UserControl
{
    public static readonly DependencyProperty ParameterProperty =
        DependencyProperty.Register(
            nameof(Parameter),
            typeof(Parameter),
            typeof(ParameterControlBase),
            new PropertyMetadata(null, OnParameterChanged));

    public static readonly DependencyProperty ClientProperty =
        DependencyProperty.Register(
            nameof(Client),
            typeof(ApiClient),
            typeof(ParameterControlBase),
            new PropertyMetadata(null));

    private bool _suppressOutboundChange;
    private bool _ensureLoadStarted;

    public Parameter? Parameter
    {
        get => (Parameter?)GetValue(ParameterProperty);
        set => SetValue(ParameterProperty, value);
    }

    public ApiClient? Client
    {
        get => (ApiClient?)GetValue(ClientProperty);
        set => SetValue(ClientProperty, value);
    }

    public string Caption => Parameter?.Definition?.Title ?? Parameter?.Name ?? Parameter?.Path ?? string.Empty;

    public string Path => Parameter?.Path ?? string.Empty;

    public double Min => Parameter?.Definition?.Min ?? 0.0;

    public double Max => Parameter?.Definition?.Max ?? 1.0;

    public double Default => Min;

    public bool IsReady => Parameter?.IsReady == true;

    public double NumericValue
    {
        get => TryGetNumericValue(Parameter?.Value, out var v) ? v : Min;
    }

    public string ValueString
    {
        get
        {
            var valueText = FormatValue(Parameter?.Value);
            var unit = Parameter?.Definition?.Unit;

            return string.IsNullOrWhiteSpace(unit)
                ? valueText
                : $"{valueText} {unit}";
        }
    }

    public event Action<ParameterControlBase, object>? UserValueChanged;

    protected ParameterControlBase()
    {
        Loaded += async (_, _) =>
        {
            await EnsureLoaded();
            RefreshFromParameter();
        };

        GotFocus += async (_, _) =>
        {
            await EnsureLoaded();
            AnnounceFocus();
        };
    }

    protected async Task EnsureLoaded()
    {
        if (_ensureLoadStarted)
            return;

        if (Client == null || Parameter == null)
            return;

        _ensureLoadStarted = true;
        try
        {
            await Client.EnsureLoaded(Parameter);
        }
        finally
        {
            _ensureLoadStarted = false;
        }
    }

    protected void SetValueFromMixer(object? value)
    {
        if (Parameter == null)
            return;

        _suppressOutboundChange = true;
        try
        {
            Parameter.Value = value;
            RefreshFromParameter();
        }
        finally
        {
            _suppressOutboundChange = false;
        }
    }

    protected async Task SetValueFromUser(object value)
    {
        if (Parameter == null || Client == null)
            return;

        if (value is double d)
            value = Math.Clamp(d, Min, Max);

        _suppressOutboundChange = true;
        try
        {
            Parameter.Value = value;
            RefreshFromParameter();
        }
        finally
        {
            _suppressOutboundChange = false;
        }

        UserValueChanged?.Invoke(this, value);
        AnnounceValue();

        await Client.SetValue(Parameter, value);
    }

    protected async Task NudgeValue(double delta)
    {
        await SetValueFromUser(NumericValue + delta);
    }

    protected virtual string FormatValue(object? value)
    {
        if (value == null)
            return "unknown";

        if (value is double d)
            return d.ToString("0.###", CultureInfo.InvariantCulture);

        if (value is float f)
            return f.ToString("0.###", CultureInfo.InvariantCulture);

        return value.ToString() ?? "unknown";
    }

    protected virtual void RefreshFromParameter()
    {
    }

    protected virtual void AnnounceFocus()
    {
        Speech.SpeechManager.Say($"{Caption} ({ValueString})");
    }

    protected virtual void AnnounceValue()
    {
        Speech.SpeechManager.Say(ValueString);
    }

    protected bool IsOutboundSuppressed() => _suppressOutboundChange;

    private static void OnParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ParameterControlBase)d;

        if (e.OldValue is INotifyPropertyChanged oldNpc)
            oldNpc.PropertyChanged -= control.OnParameterPropertyChanged;

        if (e.NewValue is INotifyPropertyChanged newNpc)
            newNpc.PropertyChanged += control.OnParameterPropertyChanged;

        control.RefreshFromParameter();
    }

    private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(RefreshFromParameter);
    }

    private static bool TryGetNumericValue(object? value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}