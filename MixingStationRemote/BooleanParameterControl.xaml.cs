using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MixingStationRemote;
/// <summary>
/// Interaction logic for BooleanParameterControl.xaml
/// </summary>
public partial class BooleanParameterControl : ParameterControlBase
{
	public BooleanParameterControl()
	{
		InitializeComponent();
	}

	protected override void RefreshFromParameter()
	{
		if (IsFocused)
			AnnounceValue();
		var b = bool.Parse(Parameter.Value.ToString());
		textBox.Text = b ? "On" : "Off";
		label.Text = Parameter.value.title;
	}

	protected override void AnnounceValue()
	{
		var b = bool.Parse(Parameter.Value.ToString());
		Speech.SpeechManager.Say(b ? "On" : "Off");
	}

	protected override void AnnounceFocus()
	{
		var b = bool.Parse(Parameter.Value.ToString());
		Speech.SpeechManager.Say($"{Caption} ({(b ? "On" : "Off")})");

	}

	private void ParameterControlBase_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			e.Handled = true;
			Speech.SpeechManager.Say(ValueString);
		}
		if (e.Key == Key.Down)
		{
			e.Handled = true;
			SetValueFromUser(false);
		}
		if (e.Key == Key.Up)
		{
			e.Handled = true;
			SetValueFromUser(true);
		}
	}
}
