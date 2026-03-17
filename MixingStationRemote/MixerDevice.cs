namespace MixingStationRemote;

public class MixerSearchRoot
{
    public List<MixerDevice> results { get; set; } = new();

}

public class MixerDevice
{
public int modelId { get;set;}

	public string ip { get; set; } = string.Empty;
	public string name { get; set; } = string.Empty;
	public string model { get; set; } = string.Empty;
	public string version { get; set; } = string.Empty;
}

//{
//  "results": [
//    {
//      "modelId": 0,
//      "ip": "192.168.86.30",
//      "name": "JohnConradXR18",
//      "model": "XR18",
//      "version": "1.25"
//    }
//  ]
//}