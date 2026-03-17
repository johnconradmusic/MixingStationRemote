using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixingStationRemote;
public enum ChType
{
	InputChannel = 0,
	AuxInput = 1,
	FxSend = 2,
	FxReturn = 3,
	Bus = 4,
	Matrix = 5,
	Main = 6,
	//7?
	DCA = 8
}
public class ConsoleArchitecture
{
	public int totalChannels { get; set; }
	public List<ChannelArchitecture> channelTypes { get; set; }
}
public class ChannelArchitecture
{
	public int offset { get; set; }
	public bool stereo { get; set; }
	public string name { get; set; }
	public int count { get; set; }

	public List<SignalTarget> signalTargets { get; set; }
	public string shortName { get; set; }
	public ChType type { get; set; }

}
public class SignalTarget
{
	public int count { get; set; }
	public ChannelType channelType { get; set; }
}
public class ChannelType
{
	public bool stereo { get; set; }
	public int type { get; set; }

}