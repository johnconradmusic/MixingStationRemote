using System.Windows;
using System.Windows.Controls;
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
		textBox.Text = ValueString;
		label.Text = Parameter.value.title;
		if (IsFocused)
			AnnounceValue();
	}

	private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		double delta = 0;

		if (e.Key == Key.Enter)
		{
			e.Handled = true;
			AnnounceValue();
			return;
		}

		if (e.Key == Key.Delete)
		{
			e.Handled = true;
			SetValueFromUser(Default);
			return;
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
			NudgeValue(delta);
	}

	private double GetSmallStep()
	{
		var minDelta = Parameter.value.delta;
		return minDelta * 10;
	}

	private double GetLargeStep()
	{
		var minDelta = Parameter.value.delta;
		return minDelta * 100;
	}

}