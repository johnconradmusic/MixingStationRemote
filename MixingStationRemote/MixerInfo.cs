using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixingStationRemote;
public class MixerInfo
{

	public int consoleId { get; set; }
	public List<string> models { get; set; }

	public int currentModelId { get; set; }

	public List<string> supportedHardwareModels { get; set; }
	public string ipAddress { get; set; }

	public int manufacturerId { get; set; }

	public string name { get; set; }
	public string firmwareVersion { get; set; }
	public string currentModel { get; set; }
	public bool canSearch { get; set; }
	public List<MixingStationEnum> modelEnums { get; set; }
	public string manufacturer { get; set; }

}

public class MixingStationEnum
{
	public string name { get; set; }
	public int id { get; set; }
}