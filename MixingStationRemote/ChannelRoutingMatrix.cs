using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixingStationRemote
{
    public static class ChannelRoutingMatrixBuilder
    {
        public static ChannelRoutingMatrix Build(ConsoleArchitecture architecture)
        {
            if (architecture == null) throw new ArgumentNullException(nameof(architecture));
            if (architecture.channelTypes == null) throw new ArgumentNullException(nameof(architecture.channelTypes));

            var groups = architecture.channelTypes.OrderBy(g => g.offset).ToList();

            // First create all channel nodes without edges
            var tempNodes = new Dictionary<int, MutableChannelNode>();

            foreach (var group in groups)
            {
                for (int localIndex = 0; localIndex < group.count; localIndex++)
                {
                    int abs = group.offset + localIndex;

                    tempNodes[abs] = new MutableChannelNode
                    {
                        AbsoluteIndex = abs,
                        LocalIndex = localIndex,
                        Type = group.type,
                        Name = group.name ?? "",
                        ShortName = group.shortName ?? group.name ?? group.type.ToString(),
                        Stereo = group.stereo
                    };
                }
            }

            // Build outgoing/incoming edges
            foreach (var sourceGroup in groups)
            {
                var sourceIndices = ExpandGroupIndices(sourceGroup);

                var targets = sourceGroup.signalTargets ?? new List<SignalTarget>();

                var expandedTargetIndices = ExpandTargets(groups, targets);

                foreach (var sourceAbs in sourceIndices)
                {
                    var sourceNode = tempNodes[sourceAbs];

                    foreach (var targetAbs in expandedTargetIndices)
                    {
                        sourceNode.Outgoing.Add(targetAbs);

                        if (tempNodes.TryGetValue(targetAbs, out var targetNode))
                        {
                            targetNode.Incoming.Add(sourceAbs);
                        }
                    }
                }
            }

            // Freeze into immutable nodes, indexed by absolute index
            int maxIndex = tempNodes.Count == 0 ? -1 : tempNodes.Keys.Max();
            var finalNodes = new ChannelNode[maxIndex + 1];

            foreach (var pair in tempNodes)
            {
                var n = pair.Value;

                finalNodes[pair.Key] = new ChannelNode
                {
                    AbsoluteIndex = n.AbsoluteIndex,
                    LocalIndex = n.LocalIndex,
                    Type = n.Type,
                    Name = n.Name,
                    ShortName = n.ShortName,
                    Stereo = n.Stereo,
                    OutgoingAbsoluteIndices = n.Outgoing.OrderBy(x => x).ToArray(),
                    IncomingAbsoluteIndices = n.Incoming.OrderBy(x => x).ToArray()
                };
            }

            return new ChannelRoutingMatrix
            {
                Channels = finalNodes
            };
        }

        private static List<int> ExpandGroupIndices(ChannelArchitecture group)
        {
            var result = new List<int>(group.count);

            for (int i = 0; i < group.count; i++)
                result.Add(group.offset + i);

            return result;
        }

        private static List<int> ExpandTargets(
            List<ChannelArchitecture> groups,
            List<SignalTarget> signalTargets)
        {
            var result = new HashSet<int>();

            foreach (var target in signalTargets)
            {
                if (target.channelType == null)
                    continue;

                if (!Enum.IsDefined(typeof(ChType), target.channelType.type))
                    continue;

                var targetType = (ChType)target.channelType.type;

                // Match by type. Optionally also match stereo if your API requires that.
                var matchingGroups = groups.Where(g => g.type == targetType);

                foreach (var group in matchingGroups)
                {
                    for (int i = 0; i < group.count; i++)
                    {
                        result.Add(group.offset + i);
                    }
                }
            }

            return result.OrderBy(x => x).ToList();
        }

        private sealed class MutableChannelNode
        {
            public int AbsoluteIndex { get; init; }
            public int LocalIndex { get; init; }
            public ChType Type { get; init; }
            public string Name { get; init; } = "";
            public string ShortName { get; init; } = "";
            public bool Stereo { get; init; }

            public HashSet<int> Outgoing { get; } = new();
            public HashSet<int> Incoming { get; } = new();
        }
    }

    public sealed class ChannelRoutingMatrix
    {
        public IReadOnlyList<ChannelNode> Channels { get; init; } = Array.Empty<ChannelNode>();

        public ChannelNode? Get(int absoluteIndex) =>
            absoluteIndex >= 0 && absoluteIndex < Channels.Count
                ? Channels[absoluteIndex]
                : null;

        public IReadOnlyList<int> GetOutgoing(int absoluteIndex) =>
            Get(absoluteIndex)?.OutgoingAbsoluteIndices ?? Array.Empty<int>();

        public IReadOnlyList<int> GetIncoming(int absoluteIndex) =>
            Get(absoluteIndex)?.IncomingAbsoluteIndices ?? Array.Empty<int>();
    }

    public sealed class ChannelNode
    {
        public int AbsoluteIndex { get; init; }
        public int LocalIndex { get; init; }
        public ChType Type { get; init; }
        public string Name { get; init; } = "";
        public string ShortName { get; init; } = "";
        public bool Stereo { get; init; }

        public IReadOnlyList<int> OutgoingAbsoluteIndices { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> IncomingAbsoluteIndices { get; init; } = Array.Empty<int>();

        public override string ToString() =>
            $"{AbsoluteIndex}: {ShortName}{LocalIndex + 1} ({Type}, {(Stereo ? "ST" : "M")})";
    }
}
