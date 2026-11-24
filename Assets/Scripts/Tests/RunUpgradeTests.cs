using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class RunUpgradeTests
    {
        private RunUpgradeCatalog _catalog;
        private RunUpgradeState _build;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>("Assets/Settings/Run Upgrades.asset");
            Assert.That(_catalog, Is.Not.Null);
            _build = new RunUpgradeState();
        }

        [Test]
        public void CatalogHasStableUniqueDefinitionsAndRealSprites()
        {
            var ids = new HashSet<UpgradeId>();
            var icons = new HashSet<Sprite>();
            Assert.That(_catalog.Definitions.Length, Is.EqualTo((int)UpgradeId.Count));
            foreach (var definition in _catalog.Definitions)
            {
                Assert.That(ids.Add(definition.Id), Is.True);
                Assert.That(definition.Icon, Is.Not.Null, definition.Title);
                Assert.That(icons.Add(definition.Icon), Is.True);
                Assert.That(definition.Numbers, Is.Not.Empty);
                Assert.That(definition.Description, Is.Not.Empty);
            }
        }

        [Test]
        public void OffersAreUniqueStableUntilConfirmedAndPaidOnce()
        {
            var selection = new UpgradeSelection(_catalog, _build, 2468);
            Assert.That(selection.Open(0), Is.True);
            Assert.That(selection.Count, Is.EqualTo(3));
            var cards = new[] { selection.Offer(0), selection.Offer(1), selection.Offer(2) };
            Assert.That(new HashSet<RunUpgradeDefinition>(cards), Has.Count.EqualTo(3));

            Assert.That(selection.Open(0), Is.False);
            Assert.That(selection.Open(12), Is.False);
            for (var index = 0; index < cards.Length; index++)
            {
                Assert.That(selection.Offer(index), Is.SameAs(cards[index]));
            }

            Assert.That(selection.Confirm(-1, out _), Is.False);
            Assert.That(selection.Confirm(1, out var acquired), Is.True);
            Assert.That(acquired, Is.SameAs(cards[1]));
            Assert.That(_build.Level(acquired.Id), Is.EqualTo(1));
            Assert.That(selection.Confirm(1, out _), Is.False);
            Assert.That(selection.Open(0), Is.False);
            Assert.That(_build.Revision, Is.EqualTo(1));
        }

        [Test]
        public void SeedAndChoiceSequenceReproduceTheOffers()
        {
            var first = new UpgradeSelection(_catalog, _build, 11);
            var second = new UpgradeSelection(_catalog, new RunUpgradeState(), 11);
            for (var reward = 0; reward < 10; reward++)
            {
                Assert.That(first.Open(reward), Is.True);
                Assert.That(second.Open(reward), Is.True);
                Assert.That(first.Count, Is.EqualTo(second.Count));
                for (var index = 0; index < first.Count; index++)
                {
                    Assert.That(second.Offer(index), Is.SameAs(first.Offer(index)));
                }

                var pick = reward % first.Count;
                Assert.That(first.Confirm(pick, out _), Is.True);
                Assert.That(second.Confirm(pick, out _), Is.True);
            }
        }

        [Test]
        public void MaxLevelOffersAreFilteredAndASmallPoolStillWorks()
        {
            foreach (var definition in _catalog.Definitions)
            {
                if (definition.Id is UpgradeId.ExtendedMagazine or UpgradeId.RapidService)
                {
                    continue;
                }

                for (var level = 0; level < definition.MaxLevel; level++)
                {
                    Assert.That(_build.Acquire(definition), Is.True);
                }
            }

            var selection = new UpgradeSelection(_catalog, _build, 7);
            Assert.That(selection.Open(1), Is.True);
            Assert.That(selection.Count, Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { UpgradeId.ExtendedMagazine, UpgradeId.RapidService }, new[] { selection.Offer(0).Id, selection.Offer(1).Id });
        }

        [Test]
        public void SynergyBoostsOncePerCategoryAndChangesTheDistribution()
        {
            var weighted = new RunUpgradeState();
            weighted.Acquire(_catalog.Definitions[(int)UpgradeId.EfficientRecall]);
            var selection = new UpgradeSelection(_catalog, weighted, 11);
            Assert.That(selection.CategoryWeight(UpgradeCategory.Recall), Is.EqualTo(1.35f).Within(0.0001f));
            Assert.That(selection.CategoryWeight(UpgradeCategory.Reset), Is.EqualTo(1f));

            var control = new RunUpgradeState();
            var recall = _catalog.Definitions[(int)UpgradeId.EfficientRecall];
            var recallOffers = 0;
            var controlOffers = 0;
            var controlCatalog = Object.Instantiate(_catalog);
            var data = new SerializedObject(controlCatalog);
            var multiplier = data.FindProperty("_synergyWeight");
            try
            {
                multiplier.floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
                control.Acquire(recall);
                for (var seed = 0; seed < 500; seed++)
                {
                    recallOffers += FirstIsRecall(_catalog, weighted, seed) ? 1 : 0;
                    controlOffers += FirstIsRecall(controlCatalog, control, seed) ? 1 : 0;
                }
            }
            finally
            {
                Object.DestroyImmediate(controlCatalog);
            }

            Assert.That(recallOffers, Is.GreaterThan(controlOffers));
            weighted.Acquire(_catalog.Definitions[(int)UpgradeId.SafeReturn]);
            Assert.That(selection.CategoryWeight(UpgradeCategory.Recall), Is.EqualTo(1.35f).Within(0.0001f));
        }

        [Test]
        public void WeaponModifiersUseBaseValuesAndReloadTransfersExistingReserve()
        {
            var host = new GameObject("Weapon stats test");
            try
            {
                var weapon = host.AddComponent<CombatWeapon>();
                weapon.BindUpgrades(_build, null);
                weapon.ResetState();
                Assert.That(weapon.Magazine, Is.EqualTo(30));
                Assert.That(weapon.EffectiveDamage, Is.EqualTo(12));
                Assert.That(weapon.ReloadSeconds, Is.EqualTo(1.5f));
                Assert.That(weapon.ProjectileSpeed(TemporalMode.BulletTime), Is.EqualTo(45f));
                Assert.That(weapon.RoundsPerMinute(TemporalMode.BulletTime), Is.EqualTo(600f));

                Grant(UpgradeId.OverclockedReceiver, 3);
                Grant(UpgradeId.ExtendedMagazine, 2);
                Grant(UpgradeId.RapidService, 2);
                Assert.That(weapon.EffectiveDamage, Is.EqualTo(19));
                Assert.That(weapon.MagazineCapacity, Is.EqualTo(50));
                Assert.That(weapon.ReloadSeconds, Is.EqualTo(0.75f));
                Assert.That(weapon.Magazine, Is.EqualTo(30));
                Assert.That(weapon.Reserve, Is.EqualTo(180));

                weapon.Simulate(0d, false, true, true, default, default, null);
                weapon.Simulate(0.5d, false, false, true, default, default, null);
                Assert.That(weapon.ReloadProgress, Is.EqualTo(2f / 3f).Within(0.001f));
                Assert.That(weapon.Magazine, Is.EqualTo(30));
                weapon.Simulate(0.25d, false, false, true, default, default, null);
                Assert.That(weapon.IsReloading, Is.False);
                Assert.That(weapon.Magazine, Is.EqualTo(50));
                Assert.That(weapon.Reserve, Is.EqualTo(160));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private bool FirstIsRecall(RunUpgradeCatalog catalog, RunUpgradeState build, int seed)
        {
            var selection = new UpgradeSelection(catalog, build, seed);
            selection.Open(0);
            return selection.Offer(0).Category == UpgradeCategory.Recall;
        }

        private void Grant(UpgradeId id, int levels)
        {
            for (var level = 0; level < levels; level++)
            {
                Assert.That(_build.Acquire(_catalog.Definitions[(int)id]), Is.True);
            }
        }
    }
}
