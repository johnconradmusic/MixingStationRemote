using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace MixingStationRemote;


public sealed class Parameter : INotifyPropertyChanged
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public ParameterDefinition? Definition { get; set; }

    private object? _value;
    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValue));
                OnPropertyChanged(nameof(IsReady));
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasDefinition => Definition != null;
    public bool HasValue => Value != null;
    public bool IsReady => HasDefinition && HasValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetDefinition(ParameterDefinition definition)
    {
        Definition = definition;
        OnPropertyChanged(nameof(Definition));
        OnPropertyChanged(nameof(HasDefinition));
        OnPropertyChanged(nameof(IsReady));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ParameterDefinition
{
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("tap")]
    public bool Tap { get; set; }

    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }

    [JsonPropertyName("delta")]
    public double Delta { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("enums")]
    public List<EnumEntry> EnumEntries { get; set; }
}

public sealed class EnumEntry
{
    [JsonPropertyName("id")]
    public int ID { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}