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
	Mix = 4,
	Group = 5,
	Main = 6,
	//7?
	DCA = 8,
	Talkback = 9,
}
public class ConsoleArchitecture
{
	public int totalChannels { get; set; }
	public List<ChannelArchitecture> channelTypes { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Console Architecture");
        sb.AppendLine($"Total Channels: {totalChannels}");
        sb.AppendLine(new string('-', 60));

        int globalIndex = 0;

        foreach (var group in channelTypes.OrderBy(c => c.offset))
        {
            sb.AppendLine(
                $"{group.type} ({group.name}) | Count: {group.count} | Offset: {group.offset} | {(group.stereo ? "Stereo" : "Mono")}"
            );

            // Precompute signal target summary for this group
            string targetSummary = FormatSignalTargets(group.signalTargets);

            for (int i = 0; i < group.count; i++)
            {
                int absoluteIndex = group.offset + i;

                sb.AppendLine(
                    $"  [{globalIndex:D3}] -> Abs:{absoluteIndex:D3} | {group.shortName}{i + 1} | {(group.stereo ? "ST" : "M")} | Targets: {targetSummary}"
                );

                globalIndex++;
            }

            sb.AppendLine();
        }

        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Enumerated Channels: {globalIndex}");

        return sb.ToString();
    }

    private static string FormatSignalTargets(List<SignalTarget>? targets)
    {
        if (targets == null || targets.Count == 0)
            return "None";

        var parts = new List<string>();

        foreach (var t in targets)
        {
            if (t.channelType == null)
                continue;

            string typeName = Enum.IsDefined(typeof(ChType), t.channelType.type)
                ? ((ChType)t.channelType.type).ToString()
                : $"Type{t.channelType.type}";

            string stereo = t.channelType.stereo ? "ST" : "M";

            parts.Add($"{typeName}x{t.count}({stereo})");
        }

        return string.Join(", ", parts);
    }
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