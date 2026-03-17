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

	private bool _suppressOutboundChange;

	public Parameter? Parameter
	{
		get => (Parameter?)GetValue(ParameterProperty);
		set => SetValue(ParameterProperty, value);
	}

	public string Caption => Parameter?.value?.title ?? Parameter?.path ?? string.Empty;

	public string Path => Parameter?.path ?? string.Empty;

	public double Min => Parameter?.value?.min ?? 0.0;

	public double Max => Parameter?.value?.max ?? 1.0;

	public double Default => Min; //HACK

	public double NumericValue
	{
		get => TryGetNumericValue(Parameter?.Value, out var v) ? v : Min;
	}

	public string ValueString => FormatValue(Parameter?.Value) + " " + Parameter.value.unit;

	public event Action<ParameterControlBase, object>? UserValueChanged;

	protected ParameterControlBase()
	{
		Loaded += (_, _) => RefreshFromParameter();
		GotFocus += (_, _) => AnnounceFocus();
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

	protected void SetValueFromUser(object value)
	{
		if (Parameter == null)
			return;

		if (value is double d)
		{
			value = Math.Clamp(d, Min, Max);
		}

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
	}

	protected void NudgeValue(double delta)
	{
		SetValueFromUser(NumericValue + delta);
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
		if (e.PropertyName == nameof(Parameter.Value) || string.IsNullOrEmpty(e.PropertyName))
		{
			Dispatcher.BeginInvoke(RefreshFromParameter);
		}
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