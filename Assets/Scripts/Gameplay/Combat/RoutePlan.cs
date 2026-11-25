using System;
using System.Collections.Generic;

namespace Overtime
{
    internal sealed class RouteNode
    {
        internal int Id { get; }
        internal int Depth { get; }
        internal RoomDefinition Room { get; }
        internal int Variant { get; }
        internal RoomReward Reward { get; }

        internal RouteNode(int id, int depth, RoomDefinition room, int variant, RoomReward reward)
        {
            Id = id;
            Depth = depth;
            Room = room;
            Variant = variant;
            Reward = reward;
        }
    }

    internal sealed class RoutePlan
    {
        internal const int StandardRoomCount = 5;
        private readonly RouteNode[][] _levels;

        internal RoutePlan(RouteNode[][] levels)
        {
            _levels = levels;
        }

        internal int Count => _levels.Length;

        internal IReadOnlyList<RouteNode> At(int roomIndex)
        {
            return _levels[roomIndex];
        }
    }

    internal sealed class RouteGenerator
    {
        private readonly Random _random;
        private readonly RoomDefinition[] _library;
        private readonly RoomDefinition _supervisor;

        internal RouteGenerator(int seed, RoomDefinition[] library, RoomDefinition supervisor = null)
        {
            _random = new Random(unchecked(seed ^ 0x4D455249));
            _library = library;
            _supervisor = supervisor;
        }

        internal RoutePlan Generate()
        {
            var nodes = new RouteNode[7];
            nodes[0] = Endpoint(1, RoomCategory.Entry, 1);
            nodes[6] = Endpoint(7, RoomCategory.Containment, RoutePlan.StandardRoomCount);
            var candidates = new[]
            {
                Candidates(2, 2, 3, false),
                Candidates(3, 2, 3, false),
                Candidates(4, 2, 4, false),
                Candidates(5, 2, 5, false),
                Candidates(6, 3, 5, true)
            };

            // Keep five distinct assignments even if a branch in a custom library has to collapse.
            foreach (var skipped in new[] { 0, 1 << 5, 1 << 2, (1 << 5) | (1 << 2) })
            {
                var used = new HashSet<string>(StringComparer.Ordinal) { nodes[0].Room.Id, nodes[6].Room.Id };
                if (!Fill(1, skipped, candidates, nodes, used))
                {
                    continue;
                }

                return Assemble(nodes);
            }

            // A reduced library can still supply five legal assignments without the risk/reward contrast.
            candidates[0] = Candidates(2, 2, 3, false, true);
            candidates[2] = Candidates(4, 2, 5, false, true);
            candidates[3] = Candidates(5, 2, 5, false, true);
            var reserved = new HashSet<string>(StringComparer.Ordinal) { nodes[0].Room.Id, nodes[6].Room.Id };
            if (Fill(1, (1 << 2) | (1 << 5), candidates, nodes, reserved))
            {
                return Assemble(nodes);
            }

            throw new InvalidOperationException("Security Route requires five distinct rooms eligible for their assigned depths.");
        }

        private RoutePlan Assemble(RouteNode[] nodes)
        {
            ShuffleBranch(nodes, 1);
            ShuffleBranch(nodes, 4);

            var levels = new RouteNode[_supervisor != null ? RoutePlan.StandardRoomCount + 1 : RoutePlan.StandardRoomCount][];
            levels[0] = new[] { nodes[0] };
            levels[1] = Branch(nodes[1], nodes[2]);
            levels[2] = new[] { nodes[3] };
            levels[3] = Branch(nodes[4], nodes[5]);
            levels[4] = new[] { nodes[6] };
            if (_supervisor != null)
            {
                levels[RoutePlan.StandardRoomCount] = new[] { new RouteNode(8, levels.Length, _supervisor, 0, RoomReward.None) };
            }

            return new RoutePlan(levels);
        }

        private void ShuffleBranch(RouteNode[] nodes, int index)
        {
            if (nodes[index + 1] != null && _random.Next(2) == 0)
            {
                var first = nodes[index];
                var second = nodes[index + 1];
                nodes[index] = new RouteNode(first.Id, first.Depth, second.Room, second.Variant, second.Reward);
                nodes[index + 1] = new RouteNode(second.Id, second.Depth, first.Room, first.Variant, first.Reward);
            }
        }

        private RouteNode Endpoint(int id, RoomCategory category, int depth)
        {
            var matches = new List<RoomDefinition>();
            foreach (var definition in _library)
            {
                if (definition != null && definition.Category == category && definition.AllowsDepth(depth))
                {
                    matches.Add(definition);
                }
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException($"Security Route requires exactly one {category} room.");
            }

            var room = matches[0];
            return new RouteNode(id, depth, room, _random.Next(room.Variants.Length), room.Rewards.Length > 0 ? room.Rewards[_random.Next(room.Rewards.Length)] : RoomReward.None);
        }

        private List<RouteNode> Candidates(int id, int minTier, int maxTier, bool priority, bool fallback = false)
        {
            var candidates = new List<RouteNode>();
            var depth = id <= 3 ? 2 : id == 4 ? 3 : 4;

            foreach (var room in _library)
            {
                if (room == null || room.Category is RoomCategory.Entry or RoomCategory.Containment or RoomCategory.Supervisor || !room.AllowsDepth(depth))
                {
                    continue;
                }

                var rewards = new List<RoomReward>();
                foreach (var reward in room.Rewards)
                {
                    if (fallback || (priority ? reward == RoomReward.PriorityProtocol : reward != RoomReward.PriorityProtocol))
                    {
                        rewards.Add(reward);
                    }
                }

                if (fallback && rewards.Count == 0)
                {
                    rewards.Add(RoomReward.None);
                }

                for (var index = 0; index < room.Variants.Length; index++)
                {
                    var tier = room.Variants[index].Difficulty;
                    var threat = room.Threat(index);
                    if (tier < minTier || tier > maxTier || (!fallback && ((id == 5 && threat >= 25f) || (id == 6 && threat < 24f))))
                    {
                        continue;
                    }

                    foreach (var reward in rewards)
                    {
                        candidates.Add(new RouteNode(id, depth, room, index, reward));
                    }
                }
            }

            for (var index = candidates.Count - 1; index > 0; index--)
            {
                var swap = _random.Next(index + 1);
                (candidates[index], candidates[swap]) = (candidates[swap], candidates[index]);
            }

            return candidates;
        }

        private bool Fill(int index, int skipped, List<RouteNode>[] candidates, RouteNode[] nodes, HashSet<string> used)
        {
            if (index == 6)
            {
                return true;
            }

            nodes[index] = null;
            if ((skipped & (1 << index)) != 0)
            {
                return Fill(index + 1, skipped, candidates, nodes, used);
            }

            foreach (var candidate in candidates[index - 1])
            {
                if (index == 2 && (candidate.Reward == nodes[1].Reward || candidate.Room.Threat(candidate.Variant) == nodes[1].Room.Threat(nodes[1].Variant) || candidate.Room.Geometry == nodes[1].Room.Geometry))
                {
                    continue;
                }

                if (index == 5 && candidate.Room.Threat(candidate.Variant) <= nodes[4].Room.Threat(nodes[4].Variant))
                {
                    continue;
                }

                if (!used.Add(candidate.Room.Id))
                {
                    continue;
                }

                nodes[index] = candidate;
                if (Fill(index + 1, skipped, candidates, nodes, used))
                {
                    return true;
                }

                used.Remove(candidate.Room.Id);
            }

            nodes[index] = null;
            return false;
        }

        private RouteNode[] Branch(RouteNode first, RouteNode second)
        {
            return second == null ? new[] { first } : new[] { first, second };
        }
    }
}
