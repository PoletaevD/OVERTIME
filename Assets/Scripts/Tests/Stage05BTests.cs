using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class Stage05BTests
    {
        private const string ChamberPath = "Assets/Prefabs/Security Route/Rooms/Supervisor Chamber.asset";

        [Test]
        public void SupervisorIsTheOnlyNewRouteNodeAndPreservesSeededBranches()
        {
            var rooms = AssetDatabase.FindAssets("t:RoomDefinition", new[] { "Assets/Prefabs/Security Route/Rooms" }).Select(guid => AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(room => room.Category != RoomCategory.Supervisor).ToArray();
            var chamber = AssetDatabase.LoadAssetAtPath<RoomDefinition>(ChamberPath);
            Assert.That(rooms, Has.Length.EqualTo(16));
            Assert.That(chamber, Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/Security Route Stage 05B.unity"), Is.True);
            for (var seed = 0; seed < 32; seed++)
            {
                var legacy = new RouteGenerator(seed, rooms).Generate();
                var plan = new RouteGenerator(seed, rooms, chamber).Generate();
                Assert.That(plan.Count, Is.EqualTo(legacy.Count + 1));
                for (var depth = 0; depth < legacy.Count; depth++)
                {
                    Assert.That(NodeSignature(plan, depth), Is.EqualTo(NodeSignature(legacy, depth)));
                }

                var last = plan.At(plan.Count - 1).Single();
                Assert.That(last.Room, Is.SameAs(chamber));
                Assert.That(last.Depth, Is.EqualTo(plan.Count));
                Assert.That(last.Room.AllowsDepth(last.Depth), Is.True);
            }
        }

        [Test]
        public void ChamberAuthorsFourCoresTwoDeflectorsAndEightReinforcements()
        {
            var chamber = AssetDatabase.LoadAssetAtPath<RoomDefinition>(ChamberPath);
            Assert.That(chamber, Is.Not.Null);
            var room = chamber.Prefab;
            var boss = room.Encounter.Boss;
            Assert.That(boss.Health.Maximum, Is.EqualTo(1800));
            Assert.That(room.WorldActors, Does.Contain(boss));
            Assert.That(room.WorldActors.OfType<ArenaMechanism>().Count(), Is.EqualTo(2));
            Assert.That(room.WorldActors.OfType<ArenaHazard>().Count(), Is.EqualTo(1));
            Assert.That(room.Markers, Has.Length.EqualTo(8));
            Assert.That(room.Markers.Select(marker => marker.Enemy.Kind).Distinct().Count(), Is.EqualTo(2));
            Assert.That(boss.GetComponentsInChildren<CombatShield>(), Has.Length.EqualTo(2));
            Assert.That(boss.GetComponentsInChildren<Collider>().Count(collider => collider.name.EndsWith(" Core", System.StringComparison.Ordinal)), Is.EqualTo(4));
            Assert.That(room.Encounter.CombatZone.size.x, Is.EqualTo(32f));
            Assert.That(room.Encounter.CombatZone.size.y, Is.InRange(12f, 15f));
        }

        private string[] NodeSignature(RoutePlan route, int depth)
        {
            return route.At(depth).Select(node => $"{node.Id}/{node.Room.Id}/{node.Variant}/{node.Reward}").ToArray();
        }
    }
}
