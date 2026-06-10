using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixingStationRemote;
public class SupportedMixersRoot
{
    public List<ConsoleGroup> consoles { get; set; } = new();
}

public class ConsoleGroup
{
    public int consoleId { get; set; }
    public List<string> models { get; set; } = new();
    public string name { get; set; } = string.Empty;
    public bool canSearch { get; set; }
    public string manufacturer { get; set; } = string.Empty;
    // add modelEnums, supportedHardwareModels etc. if needed later

    public override string ToString()
    {
        var prefix = string.IsNullOrWhiteSpace(manufacturer) ? string.Empty : manufacturer + " ";
        return $"{prefix}{name}".Trim();
    }
}

