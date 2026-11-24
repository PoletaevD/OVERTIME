using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class RouteGenerationTests
    {
        private RoomDefinition[] _library;

        [SetUp]
        public void LoadLibrary()
        {
            const string folder = "Assets/Prefabs/Security Route/Rooms";
            _library = AssetDatabase.FindAssets("t:RoomDefinition", new[] { folder }).Select(guid => AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid))).OrderBy(room => room.Id).ToArray();
            Assert.That(_library, Has.Length.EqualTo(8));
        }

        [Test]
        public void AuthoredRoomsHaveSocketsAndTypedEncounters()
        {
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/Security Route.unity"), Is.True);
            Assert.That(_library.Select(room => room.Id).Distinct().Count(), Is.EqualTo(8));
            foreach (var room in _library)
            {
                var prefab = room.Prefab;
                Assert.That(room.Title, Is.Not.Empty);
                Assert.That(room.Description, Is.Not.Empty);
                Assert.That(room.Geometry, Is.Not.EqualTo(RoomGeometry.None));
                Assert.That(room.Thumbnail, Is.Not.Null, room.Title);
                Assert.That(prefab, Is.Not.Null, room.Title);
                Assert.That(prefab.Entry, Is.Not.Null, room.Title);
                Assert.That(prefab.Exit, Is.Not.Null, room.Title);
                Assert.That(prefab.SocketOpening, Is.EqualTo(new Vector2(4.8f, 3.6f)));
                Assert.That(prefab.Encounter.CombatZone.isTrigger, Is.True, room.Title);
                Assert.That(prefab.Encounter.ExitZone.isTrigger, Is.True, room.Title);
                Assert.That(prefab.Encounter.Gate, Is.Not.Null, room.Title);
                Assert.That(prefab.GetComponentInChildren<PlaygroundPlayer>(true), Is.Null, room.Title);
                Assert.That(prefab.GetComponentInChildren<TemporalDirector>(true), Is.Null, room.Title);
                Assert.That(room.Variants.Length, Is.GreaterThanOrEqualTo(room.Category == RoomCategory.Entry ? 2 : 3));

                foreach (var variant in room.Variants)
                {
                    Assert.That(variant.Markers.Length, Is.InRange(3, 6), $"{room.Title}: {variant.Title}");
                    Assert.That(variant.Markers.Distinct().Count(), Is.EqualTo(variant.Markers.Length));
                    foreach (var index in variant.Markers)
                    {
                        Assert.That(index, Is.InRange(0, prefab.Markers.Length - 1));
                        var marker = prefab.Markers[index];
                        Assert.That(marker.Enemy, Is.Not.Null, room.Title);
                        Assert.That(marker.Enemy.Body, Is.Not.Null);
                        Assert.That(marker.Point.IsChildOf(prefab.transform), Is.True);
                        if (marker.Enemy.Kind == SecurityEnemyKind.Auditor)
                        {
                            Assert.That(marker.Patrol, Is.Empty);
                        }
                        else
                        {
                            Assert.That(marker.Patrol, Is.Not.Empty);
                            Assert.That(marker.Patrol.All(waypoint => waypoint.IsChildOf(prefab.transform)), Is.True);
                        }

                        if (marker.Enemy.Kind == SecurityEnemyKind.Drone)
                        {
                            foreach (var waypoint in marker.Patrol)
                            {
                                var delta = waypoint.localPosition - marker.Point.localPosition;
                                var volume = marker.FlightHalfExtents;
                                Assert.That(Mathf.Abs(delta.x), Is.LessThanOrEqualTo(volume.x));
                                Assert.That(Mathf.Abs(delta.y), Is.LessThanOrEqualTo(volume.y));
                                Assert.That(Mathf.Abs(delta.z), Is.LessThanOrEqualTo(volume.z));
                            }
                        }
                    }
                }
            }
        }

        [TestCase(int.MinValue)]
        [TestCase(0)]
        [TestCase(2505)]
        [TestCase(int.MaxValue)]
        public void RouteAndEncountersAreSeededIndependentlyOfProtocolOffers(int seed)
        {
            var first = new RouteGenerator(seed, _library).Generate();
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            var selection = new UpgradeSelection(catalog, new RunUpgradeState(), seed);
            for (var index = 0; index < 5; index++)
            {
                Assert.That(selection.Open(index, index == 3), Is.True);
                Assert.That(selection.Confirm(index % 3, out _), Is.True);
            }

            var second = new RouteGenerator(seed, _library).Generate();
            Assert.That(Signature(second), Is.EqualTo(Signature(first)));
            Assert.That(first.At(0)[0].Room.Id, Is.EqualTo("checkpoint"));
            Assert.That(first.At(4)[0].Room.Id, Is.EqualTo("containment"));
            Assert.That(first.At(1).Count, Is.EqualTo(2));
            Assert.That(first.At(3).Count, Is.EqualTo(2));
            Assert.That(first.At(1)[0].Reward, Is.Not.EqualTo(first.At(1)[1].Reward));
            Assert.That(first.At(1)[0].Room.Threat(first.At(1)[0].Variant), Is.LessThan(first.At(1)[1].Room.Threat(first.At(1)[1].Variant)));
            Assert.That(first.At(3)[0].Reward, Is.Not.EqualTo(RoomReward.PriorityProtocol));
            Assert.That(first.At(3)[1].Reward, Is.EqualTo(RoomReward.PriorityProtocol));
            Assert.That(first.At(3)[0].Room.Threat(first.At(3)[0].Variant), Is.LessThan(first.At(3)[1].Room.Threat(first.At(3)[1].Variant)));

            var ids = new HashSet<string>();
            for (var index = 0; index < RoutePlan.RoomCount; index++)
            {
                foreach (var node in first.At(index))
                {
                    Assert.That(node.Depth, Is.EqualTo(index + 1));
                    Assert.That(node.Room.AllowsDepth(node.Depth), Is.True);
                    Assert.That(ids.Add(node.Room.Id), Is.True);
                    var tier = node.Room.Variants[node.Variant].Difficulty;
                    Assert.That(tier, Is.InRange(index == 3 ? 3 : index + 1, Mathf.Min(index + 2, 5)));
                }
            }

            Assert.That(ids, Has.Count.EqualTo(7));
        }

        [Test]
        public void FullLibraryVariesAssignmentsAndKeepsBothBranchesAcrossSeeds()
        {
            var layouts = new HashSet<string>();
            var rooms = new HashSet<string>();
            for (var seed = -32; seed <= 128; seed++)
            {
                var plan = new RouteGenerator(seed, _library).Generate();
                Assert.That(plan.At(1).Count, Is.EqualTo(2));
                Assert.That(plan.At(3).Count, Is.EqualTo(2));
                layouts.Add(string.Join("/", Enumerable.Range(0, RoutePlan.RoomCount).SelectMany(index => plan.At(index)).Select(node => node.Room.Id)));
                foreach (var node in Enumerable.Range(0, RoutePlan.RoomCount).SelectMany(index => plan.At(index)))
                {
                    rooms.Add(node.Room.Id);
                }
            }

            Assert.That(layouts.Count, Is.GreaterThan(20));
            Assert.That(rooms, Has.Count.EqualTo(8));
        }

        [Test]
        public void ReducedLibraryCollapsesBranchesWithoutRepeatingOrSkippingRooms()
        {
            var ids = new[] { "checkpoint", "containment", "shaft", "cargo", "surveillance-center" };
            var reduced = _library.Where(room => ids.Contains(room.Id)).ToArray();
            var plan = new RouteGenerator(520, reduced).Generate();
            var assigned = new HashSet<string>();
            for (var index = 0; index < RoutePlan.RoomCount; index++)
            {
                Assert.That(plan.At(index).Count, Is.EqualTo(1));
                Assert.That(plan.At(index)[0].Room.AllowsDepth(index + 1), Is.True);
                Assert.That(assigned.Add(plan.At(index)[0].Room.Id), Is.True);
            }

            Assert.That(assigned, Is.EquivalentTo(ids));
        }

        [Test]
        public void PriorityRarityWeightsAreScopedToOneOffer()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            var common = catalog.RarityWeight(UpgradeRarity.Common);
            var rare = catalog.RarityWeight(UpgradeRarity.Rare);
            var standardRares = 0;
            var priorityRares = 0;
            var laterRares = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                var standard = new UpgradeSelection(catalog, new RunUpgradeState(), seed);
                var priority = new UpgradeSelection(catalog, new RunUpgradeState(), seed);
                standard.Open(1);
                priority.Open(1, true);
                for (var index = 0; index < standard.Count; index++)
                {
                    standardRares += standard.Offer(index).Rarity == UpgradeRarity.Rare ? 1 : 0;
                    priorityRares += priority.Offer(index).Rarity == UpgradeRarity.Rare ? 1 : 0;
                }

                Assert.That(priority.Confirm(0, out _), Is.True);
                Assert.That(priority.Open(1, true), Is.False);
                Assert.That(priority.Open(2), Is.True);
                for (var index = 0; index < priority.Count; index++)
                {
                    laterRares += priority.Offer(index).Rarity == UpgradeRarity.Rare ? 1 : 0;
                }
            }

            Assert.That(priorityRares, Is.GreaterThan(standardRares * 2));
            Assert.That(laterRares, Is.LessThan(priorityRares / 2));
            Assert.That(catalog.RarityWeight(UpgradeRarity.Common), Is.EqualTo(common));
            Assert.That(catalog.RarityWeight(UpgradeRarity.Rare), Is.EqualTo(rare));
        }

        private string[] Signature(RoutePlan plan)
        {
            return Enumerable.Range(0, RoutePlan.RoomCount).SelectMany(index => plan.At(index)).Select(node => $"{node.Id}/{node.Room.Id}/{node.Variant}/{node.Reward}").ToArray();
        }
    }
}
