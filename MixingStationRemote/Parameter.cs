using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace MixingStationRemote;

public class Parameter : INotifyPropertyChanged
{

	public string path { get; set; } = "";

	public string name { get; set; }

	public ParameterDefinition value { get; set; }

	private object _value = 0;
public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }
	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ParameterDefinition
{
	public string unit { get; set; }
	public bool tap { get; set; }
	public double min { get; set; }
	public double max { get; set; }
	public double delta { get; set; }
	public string title { get; set; }
	public string type { get; set; }

}