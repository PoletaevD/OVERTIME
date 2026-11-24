using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overtime.Editor
{
    internal sealed class WeaponAssets
    {
        private const string Art = "Assets/Art/Weapons";
        private const string Settings = "Assets/Settings/Weapons";
        private const string Materials = "Assets/Art/Gravity Playground";
        private const int ViewmodelLayer = 30;
        private const int SampleRate = 22050;

        internal WeaponDefinition[] LoadOrCreate()
        {
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory(Settings);
            AssetDatabase.Refresh();
            var weapons = new WeaponDefinition[3];
            weapons[0] = Create(WeaponId.Compliance, WeaponType.Carbine, "Compliance", "Automatic carbine for mobile mid-range engagements.", new Vector3(0.075f, 8f, 0.16f), new Vector3(0.31f, -0.23f, 0.99f), null);
            weapons[1] = Create(WeaponId.Termination, WeaponType.Scatter, "Termination", "Eight separate bolts. Close the distance and take the flank.", new Vector3(0.133f, 13f, 0.31f), new Vector3(0.31f, -0.24f, 1.00f), data =>
            {
                Set(data, "_roundsPerMinute", 100f);
                Set(data, "_magazine", 6);
                Set(data, "_reserve", 36);
                Set(data, "_magazinePerLevel", 2);
                Set(data, "_roomReserve", 8);
                Set(data, "_damage", 9);
                Set(data, "_reload", 2f);
                Set(data, "_speed", 38f);
                Set(data, "_pellets", 8);
                Set(data, "_spread", 7f);
                Set(data, "_range", 35f);
                Set(data, "_lifetime", 1f);
                Set(data, "_radius", 0.032f);
            });

            weapons[2] = Create(WeaponId.Deadline, WeaponType.Charged, "Deadline", "Charge a precision bolt and line up targets in BULLET TIME.", new Vector3(0.16f, 10f, 0.20f), new Vector3(0.31f, -0.24f, 1.08f), data =>
            {
                Set(data, "_postShotDelay", 0.55f);
                Set(data, "_magazine", 4);
                Set(data, "_reserve", 16);
                Set(data, "_magazinePerLevel", 1);
                Set(data, "_roomReserve", 4);
                Set(data, "_damage", 90);
                Set(data, "_reload", 2.2f);
                Set(data, "_speed", 100f);
                Set(data, "_range", 100f);
                Set(data, "_lifetime", 1.1f);
                Set(data, "_radius", 0.052f);
                Set(data, "_charge", 0.7f);
                Set(data, "_targets", 3);
            });

            AssetDatabase.SaveAssets();
            return weapons;
        }

        private WeaponDefinition Create(WeaponId id, WeaponType type, string name, string description, Vector3 recoil, Vector3 muzzle, Action<SerializedProperty> tune)
        {
            var modelName = $"ARC-{(int)id + 1:00} {name}";
            var assetPath = $"{Settings}/{modelName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            var primary = id == WeaponId.Compliance;
            var model = primary ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ARC-01 Viewmodel.prefab") : CreateViewmodel(id, modelName);
            var icon = primary ? new RunUpgradeAssets().CreateIcon($"{Art}/{modelName}.png", "3,11 3,19 8,19 10,16 25,16 25,14 30,14 30,11 14,11 11,6 8,6 9,11 3,11|14,11 15,6 20,6 20,11|12,19 18,19") : LoadIcon(modelName);
            var data = new SerializedObject(definition);
            data.FindProperty("_id").enumValueIndex = (int)id;
            data.FindProperty("_type").enumValueIndex = (int)type;
            data.FindProperty("_displayName").stringValue = $"ARC-{(int)id + 1:00}  /  {name}";
            data.FindProperty("_description").stringValue = description;
            data.FindProperty("_icon").objectReferenceValue = icon;
            data.FindProperty("_viewmodel").objectReferenceValue = model;
            data.FindProperty("_recoil").vector3Value = recoil;
            data.FindProperty("_shot").objectReferenceValue = id switch
            {
                WeaponId.Termination => CreateAudio("Termination Discharge", 0.27f, 123f, 43f, 0.74f, 4.4f),
                WeaponId.Deadline => CreateAudio("Deadline Discharge", 0.35f, 900f, 82f, 0.42f, 4.1f),
                _ => CreateAudio("Compliance Discharge", 0.095f, 108f, 56f, 0.53f, 5f)
            };
            data.FindProperty("_reload").objectReferenceValue = id switch
            {
                WeaponId.Termination => CreateAudio("Termination Reload", 2f, 220f, 460f, 0.5f, 2f, true),
                WeaponId.Deadline => CreateAudio("Deadline Reload", 2.2f, 620f, 280f, 0.39f, 2.7f, true),
                _ => CreateAudio("Compliance Reload", 1.5f, 440f, 730f, 0.39f, 2.5f, true)
            };

            if (id == WeaponId.Deadline)
            {
                data.FindProperty("_charge").objectReferenceValue = CreateAudio("Deadline Charge", 0.7f, 135f, 820f, 0.11f, 1f);
            }

            var tuning = data.FindProperty("_tuning");
            tuning.FindPropertyRelative("_muzzle").vector3Value = muzzle;
            tune?.Invoke(tuning);
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, assetPath);
            return definition;
        }

        private GameObject CreateViewmodel(WeaponId id, string name)
        {
            var path = $"Assets/Prefabs/{name} Viewmodel.prefab";
            var root = new GameObject(name + " Viewmodel");
            try
            {
                var art = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/{name}.fbx");
                if (art == null)
                {
                    throw new InvalidOperationException($"Missing {name} mesh.");
                }

                var model = (GameObject)PrefabUtility.InstantiatePrefab(art);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false);
                var scatter = id == WeaponId.Termination;
                model.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                model.transform.localScale *= scatter ? 0.68f : 0.56f;
                var module = PlaygroundSceneBuilder.Child("Reload Module Pivot", root.transform);
                var rails = new List<Renderer>();
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = LoadMaterial(renderer.sharedMaterial.name);
                    if (renderer.name.StartsWith("Module ", StringComparison.Ordinal))
                    {
                        renderer.transform.SetParent(module, true);
                    }

                    if (renderer.name.StartsWith("Charge Rail", StringComparison.Ordinal) || renderer.name.StartsWith("Energy Window", StringComparison.Ordinal))
                    {
                        rails.Add(renderer);
                    }
                }

                var flash = PlaygroundSceneBuilder.Child("Muzzle Flash", root.transform);
                flash.localPosition = new Vector3(0f, 0.01f, scatter ? 0.55f : 0.64f);
                var shape = new PlaygroundArt(false);
                var color = LoadMaterial(scatter ? "ARC Orange" : "ARC Cyan");
                var core = LoadMaterial("ARC Cyan");
                shape.Block(flash, "Discharge Core", 0f, 0f, 0.04f, scatter ? 0.16f : 0.048f, 0.032f, scatter ? 0.15f : 0.24f, core, false).Rotate(0f, 0f, 27f);
                shape.Block(flash, "Discharge Streak", 0f, 0f, 0.025f, scatter ? 0.24f : 0.13f, 0.016f, 0.095f, color, false).Rotate(0f, 0f, -24f);
                flash.gameObject.SetActive(false);
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ViewmodelLayer;
                }

                var viewmodel = root.AddComponent<WeaponViewmodel>();
                PlaygroundSceneBuilder.Wire(viewmodel, "_module", module, "_flash", flash, "_chargeRails", rails.ToArray());
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private Material LoadMaterial(string material)
        {
            var name = material switch
            {
                "ARC Alloy" => "Graphite Alloy",
                "ARC Shell" => "Porcelain Shell",
                "ARC Grip" => "Ceramic Panel",
                "ARC Orange" => "Safety Orange",
                "ARC Cyan" => "Cyan Guide",
                "ARC Violet" => "Violet Guide",
                _ => "Graphite Alloy"
            };

            return AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat") ?? AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/Graphite Alloy.mat");
        }

        private Sprite LoadIcon(string name)
        {
            var path = $"{Art}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private void Set(SerializedProperty data, string property, float value)
        {
            data.FindPropertyRelative(property).floatValue = value;
        }

        private void Set(SerializedProperty data, string property, int value)
        {
            data.FindPropertyRelative(property).intValue = value;
        }

        private AudioClip CreateAudio(string name, float seconds, float from, float to, float noise, float decay, bool mechanical = false)
        {
            var path = $"{Art}/{name}.wav";
            var samples = Mathf.RoundToInt(SampleRate * seconds);
            using (var wave = new BinaryWriter(File.Create(path)))
            {
                wave.Write(0x46464952);
                wave.Write(36 + samples * 2);
                wave.Write(0x45564157);
                wave.Write(0x20746D66);
                wave.Write(16);
                wave.Write((short)1);
                wave.Write((short)1);
                wave.Write(SampleRate);
                wave.Write(SampleRate * 2);
                wave.Write((short)2);
                wave.Write((short)16);
                wave.Write(0x61746164);
                wave.Write(samples * 2);
                var random = new System.Random(71);
                var phase = 0f;
                var ramp = name == "Deadline Charge";
                for (var index = 0; index < samples; index++)
                {
                    var t = (float)index / (samples - 1);
                    phase += Mathf.Lerp(from, to, t) * (2f * Mathf.PI / SampleRate);
                    var grain = ((float)random.NextDouble() * 2f - 1f) * noise;
                    var edge = Mathf.Min(1f, t * 38f) * Mathf.Pow(1f - t, decay);
                    if (mechanical)
                    {
                        edge = Mathf.Max(Pulse(t, 0.06f, 0.12f), Mathf.Max(Pulse(t, 0.47f, 0.14f), Pulse(t, 0.77f, 0.18f)));
                    }
                    else if (ramp)
                    {
                        edge = Mathf.Min(1f, (1f - t) * 45f) * Mathf.Pow(t, 1.1f);
                    }

                    var signal = (Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 2.77f) * 0.19f + grain) * edge;
                    wave.Write((short)(Mathf.Clamp(signal, -1f, 1f) * 32767f));
                }
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private float Pulse(float time, float start, float width)
        {
            var phase = (time - start) / width;
            return phase >= 0f && phase <= 1f ? Mathf.Sin(phase * Mathf.PI) * Mathf.Pow(1f - phase, 2.4f) : 0f;
        }
    }
}
