using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MixingStationRemote;
public class CustomMenuItem : MenuItem
{
    public ApiClient Client { get; set; }
    public String Path { get; set; }
    public bool Inverted { get; set; } = false;
    public CustomMenuItem()
    {
        GotFocus += CustomMenuItem_GotFocus;
        Unchecked += CustomMenuItem_CheckedChanged;
        Checked += CustomMenuItem_CheckedChanged;
        Loaded += CustomMenuItem_Loaded;
        PreviewKeyDown += CustomMenuItem_PreviewKeyDown;

    }

    private void CustomMenuItem_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    private async void CustomMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsCheckable)
        {
            bool checkState = bool.Parse(await Client.GetValue(Path));
            if (Inverted)
            {
                checkState = !checkState;
            }
            IsChecked = checkState;
        }

    }

    private void CustomMenuItem_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (IsChecked & IsFocused)
        {
            Speech.SpeechManager.Say($"on");
        }
        else
        {
            Speech.SpeechManager.Say($"off");
        }
        if (Inverted)
        {
            Client?.SendUpdate(Path, !IsChecked);
        }
        else
            Client?.SendUpdate(Path, IsChecked);

    }

    private void CustomMenuItem_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (IsCheckable)
        {
            string checkState = IsChecked ? "on" : "off";
            Speech.SpeechManager.Say($"{Header} ({checkState})");
        }
        else
        {
            Speech.SpeechManager.Say(Header);
        }
        e.Handled = true;
    }
}