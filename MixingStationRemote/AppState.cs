using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixingStationRemote;
public class AppState
{
	public string msg { get; set; } = string.Empty;
	public int progress { get; set; } = 0;
	public string state { get; set; } = string.Empty;
	public string topState { get; set; } = string.Empty;

}
