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
            var weapons = new WeaponDefinition[4];
            weapons[0] = Create(WeaponId.Compliance, WeaponType.Carbine, "Compliance", "Automatic carbine for mobile mid-range engagements.", new Vector3(0.31f, -0.23f, 0.99f), null);
            weapons[1] = Create(WeaponId.Termination, WeaponType.Scatter, "Termination", "Eight separate bolts. Close the distance and take the flank.", new Vector3(0.31f, -0.24f, 1.00f), data =>
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

            weapons[2] = Create(WeaponId.Deadline, WeaponType.Charged, "Deadline", "Charge a precision bolt and line up targets in BULLET TIME.", new Vector3(0.31f, -0.24f, 1.08f), data =>
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

            weapons[3] = Create(WeaponId.Directive, WeaponType.SemiAutomatic, "Directive", "A compact semi-automatic sidearm with fast reloads and high bolt velocity.", new Vector3(0.29f, -0.22f, 0.70f), data =>
            {
                Set(data, "_roundsPerMinute", 240f);
                Set(data, "_magazine", 12);
                Set(data, "_reserve", 96);
                Set(data, "_magazinePerLevel", 3);
                Set(data, "_roomReserve", 20);
                Set(data, "_damage", 28);
                Set(data, "_reload", 0.85f);
                Set(data, "_speed", 75f);
                Set(data, "_range", 60f);
                Set(data, "_lifetime", 0.85f);
                Set(data, "_radius", 0.035f);
            });

            TuneRecoil(weapons[0], 0.65f, 0.18f, 0.035f, new Vector3(0.25f, 0.72f, -3.2f), new Vector3(-330f, 42f, 55f), 720f, 37f);
            TuneRecoil(weapons[1], 4.2f, 0.65f, 0.052f, new Vector3(0.5f, 1.7f, -6.1f), new Vector3(-490f, 82f, 130f), 520f, 30f);
            TuneRecoil(weapons[2], 3.5f, 0.25f, 0.047f, new Vector3(0.3f, 1.2f, -7f), new Vector3(-410f, 55f, 108f), 820f, 30f);
            TuneRecoil(weapons[3], 1.8f, 0.30f, 0.030f, new Vector3(0.23f, 0.9f, -3.5f), new Vector3(-450f, 68f, 73f), 950f, 42f);
            AssetDatabase.SaveAssets();
            return weapons;
        }

        private void TuneRecoil(WeaponDefinition weapon, float vertical, float horizontal, float duration, Vector3 position, Vector3 angles, float stiffness, float damping)
        {
            var data = new SerializedObject(weapon);
            var camera = data.FindProperty("_cameraRecoil");
            Set(camera, "_vertical", vertical);
            Set(camera, "_horizontal", horizontal);
            Set(camera, "_duration", duration);
            Set(camera, "_sustainedPerShot", weapon.Id == WeaponId.Compliance ? 0.035f : 0f);
            Set(camera, "_verticalVariation", weapon.Id == WeaponId.Compliance ? 0.12f : weapon.Id == WeaponId.Deadline ? 0.03f : 0.09f);
            var pattern = camera.FindPropertyRelative("_pattern");
            var phases = weapon.Id switch
            {
                WeaponId.Compliance => new[] { new Vector2(0.18f, -0.5f), new Vector2(-0.46f, 0.3f), new Vector2(-0.85f, 0.5f), new Vector2(0.7f, -0.25f), new Vector2(1f, 0.8f), new Vector2(-0.65f, -0.65f) },
                WeaponId.Termination => new[] { new Vector2(0.85f, -0.2f), new Vector2(0.34f, 0.6f), new Vector2(-0.93f, 0.2f), new Vector2(-0.43f, -0.7f) },
                WeaponId.Deadline => new[] { new Vector2(0.22f, -0.2f), new Vector2(0.09f, 0.6f), new Vector2(-0.24f, 0.2f), new Vector2(-0.11f, -0.7f) },
                _ => new[] { new Vector2(-0.5f, -0.2f), new Vector2(-0.2f, 0.6f), new Vector2(0.55f, 0.2f), new Vector2(0.25f, -0.7f) }
            };
            pattern.arraySize = phases.Length;
            for (var index = 0; index < phases.Length; index++)
            {
                pattern.GetArrayElementAtIndex(index).vector2Value = phases[index];
            }

            var visual = data.FindProperty("_viewmodelRecoil");
            Set(visual, "_positionStiffness", stiffness);
            Set(visual, "_positionDamping", damping);
            Set(visual, "_rotationStiffness", stiffness * 0.87f);
            Set(visual, "_rotationDamping", damping * 0.85f);
            visual.FindPropertyRelative("_positionImpulse").vector3Value = position;
            visual.FindPropertyRelative("_rotationImpulse").vector3Value = angles;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private WeaponDefinition Create(WeaponId id, WeaponType type, string name, string description, Vector3 muzzle, Action<SerializedProperty> tune)
        {
            var number = id == WeaponId.Directive ? 0 : (int)id + 1;
            var modelName = $"ARC-{number:00} {name}";
            var assetPath = $"{Settings}/{modelName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            var primary = id == WeaponId.Compliance;
            var model = primary ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ARC-01 Viewmodel.prefab") : CreateViewmodel(id, modelName);
            var icon = primary ? new RunUpgradeAssets().CreateIcon($"{Art}/{modelName}.png", "3,11 3,19 8,19 10,16 25,16 25,14 30,14 30,11 14,11 11,6 8,6 9,11 3,11|14,11 15,6 20,6 20,11|12,19 18,19") : id == WeaponId.Directive ? new RunUpgradeAssets().CreateIcon($"{Art}/{modelName}.png", "4,16 4,22 27,22 30,20 30,16 19,16 17,13 13,13 10,5 5,5 6,16 4,16|10,19 22,19|14,15 19,15") : LoadIcon(modelName);
            var data = new SerializedObject(definition);
            data.FindProperty("_id").enumValueIndex = (int)id;
            data.FindProperty("_type").enumValueIndex = (int)type;
            data.FindProperty("_displayName").stringValue = $"ARC-{number:00}  /  {name}";
            data.FindProperty("_description").stringValue = description;
            data.FindProperty("_icon").objectReferenceValue = icon;
            data.FindProperty("_viewmodel").objectReferenceValue = model;
            data.FindProperty("_shot").objectReferenceValue = id switch
            {
                WeaponId.Directive => CreateAudio("Directive Discharge", 0.14f, 590f, 119f, 0.34f, 4.6f),
                WeaponId.Termination => CreateAudio("Termination Discharge", 0.27f, 123f, 43f, 0.74f, 4.4f),
                WeaponId.Deadline => CreateAudio("Deadline Discharge", 0.35f, 900f, 82f, 0.42f, 4.1f),
                _ => CreateAudio("Compliance Discharge", 0.095f, 108f, 56f, 0.53f, 5f)
            };
            data.FindProperty("_reload").objectReferenceValue = id switch
            {
                WeaponId.Directive => CreateAudio("Directive Reload", 0.85f, 430f, 940f, 0.32f, 3f, true),
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
                var pistol = id == WeaponId.Directive;
                model.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                model.transform.localRotation = pistol ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                model.transform.localScale *= pistol ? 1f : scatter ? 0.68f : 0.56f;
                var module = PlaygroundSceneBuilder.Child("Reload Module Pivot", root.transform);
                var slide = pistol ? PlaygroundSceneBuilder.Child("Slide Pivot", root.transform) : null;
                var rails = new List<Renderer>();
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = LoadMaterial(renderer.sharedMaterial.name);
                    if (slide != null && renderer.name.StartsWith("Slide ", StringComparison.Ordinal))
                    {
                        renderer.transform.SetParent(slide, true);
                    }

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
                flash.localPosition = pistol ? new Vector3(0f, 0.017f, 0.338f) : new Vector3(0f, 0.01f, scatter ? 0.55f : 0.64f);
                flash.gameObject.SetActive(false);
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ViewmodelLayer;
                }

                var viewmodel = root.AddComponent<WeaponViewmodel>();
                PlaygroundSceneBuilder.Wire(viewmodel, "_module", module, "_flash", flash, "_slide", slide, "_chargeRails", rails.ToArray());
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
