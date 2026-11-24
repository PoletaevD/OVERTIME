using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Overtime.Tests
{
    public sealed class SecurityAssetTests
    {
        [Test]
        public void DirectiveSlideAndFlashAlignWithTheFrontOfTheViewmodel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ARC-00 Directive Viewmodel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var model = prefab.GetComponent<WeaponViewmodel>();
            Assert.That(model, Is.Not.Null);
            var emitter = model.Slide.Find("Slide Emitter");
            var rearSight = model.Slide.Find("Slide Rear Sight");
            Assert.That(emitter.localPosition.z, Is.GreaterThan(rearSight.localPosition.z));
            Assert.That(Vector3.Distance(emitter.localPosition, model.Flash.localPosition), Is.LessThan(0.012f));
        }

        [TestCase("Security Unit", (int)SecurityEnemyKind.Guard, 70)]
        [TestCase("Surveillance Drone", (int)SecurityEnemyKind.Drone, 45)]
        [TestCase("Laser Auditor", (int)SecurityEnemyKind.Auditor, 90)]
        public void EnemyPrefabContainsItsHealthHitboxesAndPresentation(string name, int kind, int maximum)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Security/{name}.prefab");
            Assert.That(root, Is.Not.Null);
            var enemy = root.GetComponent<SecurityEnemy>();
            var presentation = root.GetComponent<SecurityEnemyPresentation>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(enemy.Health, Is.SameAs(root.GetComponent<CombatHealth>()));
            Assert.That(enemy.Health.Maximum, Is.EqualTo(maximum));
            Assert.That((int)enemy.Kind, Is.EqualTo(kind));
            Assert.That(enemy.Body.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Security Enemy")));

            var visual = new SerializedObject(presentation);
            foreach (var field in new[] { "_model", "_aimPivot", "_recoilPivot", "_wreck", "_rupture", "_shell", "_lens" })
            {
                Assert.That(visual.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }

            var armor = (Renderer)visual.FindProperty("_shell").objectReferenceValue;
            Assert.That(armor.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(24));
            if (enemy.Kind == SecurityEnemyKind.Auditor)
            {
                Assert.That(visual.FindProperty("_beam").objectReferenceValue, Is.Not.Null);
                Assert.That(new SerializedObject(enemy).FindProperty("_weakPoint").objectReferenceValue, Is.Not.Null);
            }
        }

        [Test]
        public void SecurityPlaygroundIsEnabledAlongsideAllPreviousPlaygrounds()
        {
            foreach (var name in new[] { "Gravity Playground", "Temporal Playground", "Combat Playground", "Security Playground" })
            {
                var path = $"Assets/Scenes/{name}.unity";
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
                Assert.That(Array.Exists(EditorBuildSettings.scenes, scene => scene.path == path && scene.enabled), Is.True, path);
            }
        }
    }
}
