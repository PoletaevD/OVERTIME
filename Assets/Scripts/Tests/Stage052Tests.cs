using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class Stage052Tests
    {
        [Test]
        public void LootUsesItsOwnSeedAndOnlyCompatibleModifiers()
        {
            var weapons = LoadWeapons();
            var room = new RouteNode(5, 3, null, 2, RoomReward.AmmunitionCache);
            var roller = new WeaponLootRoll();
            for (var seed = 0; seed < 256; seed++)
            {
                var first = roller.Roll(seed, room, weapons);
                var repeated = roller.Roll(seed, room, weapons);
                Assert.That(first[0].Definition, Is.Not.SameAs(first[1].Definition));
                for (var slot = 0; slot < first.Length; slot++)
                {
                    Assert.That(first[slot].Definition, Is.SameAs(repeated[slot].Definition));
                    Assert.That(first[slot].InstanceId, Is.EqualTo(repeated[slot].InstanceId));
                    Assert.That(first[slot].Rarity, Is.EqualTo(repeated[slot].Rarity));
                    Assert.That(first[slot].Affixes, Is.EqualTo(repeated[slot].Affixes));
                    Assert.That(first[slot].Affixes.Count, Is.InRange((int)first[slot].Rarity, Math.Max(1, (int)first[slot].Rarity)));
                    foreach (var affix in first[slot].Affixes)
                    {
                        Assert.That(roller.Compatible(first[slot].Definition.Type, affix.Kind), Is.True);
                        Assert.That(affix.Magnitude, Is.Positive);
                    }

                    Assert.That(first[slot].Modifier(WeaponModifier.Splitter) * first[slot].Modifier(WeaponModifier.ExplosiveCore), Is.Zero);
                    Assert.That(first[slot].Modifier(WeaponModifier.SlugConversion) * first[slot].Modifier(WeaponModifier.FanOutput), Is.Zero);
                }
            }
        }

        [Test]
        public void StockReconcilesOnlyGrantsNewerThanTheSnapshot()
        {
            var accrued = new AmmoStock();
            var watermark = new AmmoStock();
            var stock = new AmmoStock();
            stock.Add(WeaponId.Directive, 14);
            watermark.Add(WeaponId.Directive, 12);
            accrued.Add(WeaponId.Directive, 24);
            accrued.Add(WeaponId.Termination, 7);
            stock.Reconcile(accrued, watermark);
            Assert.That(stock.Get(WeaponId.Directive), Is.EqualTo(26));
            Assert.That(stock.Get(WeaponId.Termination), Is.EqualTo(7));
            Assert.That(stock.Get(WeaponId.Compliance), Is.Zero);
            stock.Reconcile(accrued, accrued);
            Assert.That(stock.Get(WeaponId.Directive), Is.EqualTo(26));
        }

        [Test]
        public void SavedCatalogPreservesIdsAndIncludesEveryStage052Protocol()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            Assert.That(catalog.Definitions, Has.Length.EqualTo(38));
            Assert.That((int)UpgradeId.LastWord, Is.EqualTo(25));
            var build = new RunUpgradeState();
            foreach (var definition in catalog.Definitions)
            {
                Assert.That(catalog.Definitions[(int)definition.Id], Is.SameAs(definition));
                Assert.That(definition.Icon, Is.Not.Null, definition.Title);
                Assert.That(build.Acquire(definition), Is.True);
            }

            Assert.That(build.Bonus(UpgradeId.SurgicalStrike), Is.EqualTo(8));
            Assert.That(build.Bonus(UpgradeId.BloodBattery), Is.EqualTo(10));
            Assert.That(build.Definition(UpgradeId.ExtendedEdge).Radius, Is.EqualTo(5));
            Assert.That(build.Definition(UpgradeId.TimeSever).Seconds, Is.EqualTo(3));
            Assert.That(build.Bonus(UpgradeId.FocusProtocol), Is.EqualTo(0.30f));
            Assert.That(build.Definition(UpgradeId.PrecisionLock).Seconds, Is.EqualTo(1));
            Assert.That(build.Bonus(UpgradeId.SteadyProcessing), Is.EqualTo(0.25f));
            Assert.That(build.Bonus(UpgradeId.Scavenger), Is.EqualTo(0.20f));
            Assert.That(build.Bonus(UpgradeId.EmergencySupply), Is.EqualTo(5));
            Assert.That(build.Bonus(UpgradeId.QuickChamber), Is.EqualTo(3));
            Assert.That(build.Bonus(UpgradeId.BladeAndBullet), Is.EqualTo(0.50f));
            Assert.That(build.Bonus(UpgradeId.MagneticExecution), Is.EqualTo(0.60f));
        }

        [Test]
        public void DoorSocketAlignsEveryArenaWithTheSameAperture()
        {
            var prefabs = AssetDatabase.FindAssets("t:RoomDefinition", new[] { "Assets/Prefabs/Security Route/Rooms" });
            Assert.That(prefabs, Has.Length.EqualTo(16));
            var socket = new Pose(new Vector3(11f, 0f, 53f), Quaternion.Euler(0f, 90f, 0f));
            foreach (var guid in prefabs)
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Prefab;
                var position = room.ConnectEntrance(socket);
                Assert.That(Vector3.Distance(socket.position, position.position), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(socket.rotation, position.rotation), Is.LessThan(0.01f));
                Assert.That(room.SocketOpening, Is.EqualTo(new Vector2(4.8f, 3.6f)));
                Assert.That(room.AmmoSockets, Has.Length.GreaterThanOrEqualTo(5));
                Assert.That(room.Exit.localPosition.z, Is.GreaterThan(room.Entry.localPosition.z + 10f));
            }
        }

        [Test]
        public void BothStartingVariantsFitTheBaseDirectivesAmmoBudget()
        {
            var pistol = LoadWeapons()[(int)WeaponId.Directive].Tuning;
            var guids = AssetDatabase.FindAssets("t:RoomDefinition", new[] { "Assets/Prefabs/Security Route/Rooms" });
            var variantsChecked = 0;
            foreach (var guid in guids)
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room.Category != RoomCategory.Entry)
                {
                    continue;
                }

                foreach (var variant in room.Variants)
                {
                    var rounds = 0;
                    foreach (var marker in variant.Markers)
                    {
                        var enemy = room.Prefab.Markers[marker].Enemy;
                        Assert.That(enemy.Kind, Is.Not.EqualTo(SecurityEnemyKind.Enforcer));
                        rounds += Mathf.CeilToInt((float)enemy.Health.Maximum / pistol.Damage);
                    }

                    Assert.That(rounds * 1.4f, Is.LessThan(pistol.Magazine + pistol.Reserve), variant.Title);
                    variantsChecked++;
                }
            }

            Assert.That(variantsChecked, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void SavedKnifeTrailStartsAtTheBladeTip()
        {
            var knife = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Security Route/Loot/MERIDIAN Knife Presentation.prefab");
            var pivot = knife.transform.Find("Knife Swing Pivot");
            var tip = pivot.Find("Blade Tip");
            var blade = Array.Find(pivot.GetComponentsInChildren<MeshFilter>(true), filter => filter.name == "Blade");
            var bounds = blade.sharedMesh.bounds;
            var endpointA = pivot.InverseTransformPoint(blade.transform.TransformPoint(bounds.min));
            var endpointB = pivot.InverseTransformPoint(blade.transform.TransformPoint(bounds.max));
            Assert.That(tip.localPosition.z, Is.EqualTo(Mathf.Max(endpointA.z, endpointB.z)).Within(0.004f));
            Assert.That(tip.GetComponent<TrailRenderer>().widthMultiplier, Is.Positive);
        }

        private WeaponDefinition[] LoadWeapons()
        {
            var paths = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/Settings/Weapons" });
            var definitions = new WeaponDefinition[4];
            foreach (var guid in paths)
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                definitions[(int)weapon.Id] = weapon;
            }

            return definitions;
        }
    }
}
