using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Overtime.Editor
{
    internal sealed class RunUpgradeAssets
    {
        internal const string CatalogPath = "Assets/Settings/Run Upgrades.asset";
        private const string IconDirectory = "Assets/Art/Run Protocols";

        private readonly (string Title, UpgradeCategory Category, UpgradeRarity Rarity, int Level, float Amount, float Seconds, float Radius, string Description, string Numbers, string Icon)[] _entries =
        {
            ("Overclocked Receiver", UpgradeCategory.Weapon, UpgradeRarity.Common, 3, 0.2f, 0f, 0f, "Amplifies base weapon damage.", "+{0:P0} damage per level", "4,12 4,19 24,19 24,16 29,16 29,12 4,12|9,12 10,6 16,6 18,12|14,16 21,16"),
            ("Extended Magazine", UpgradeCategory.Weapon, UpgradeRarity.Common, 2, 10f, 0f, 0f, "Expands ARC-01, ARC-02 and ARC-03 capacity respectively.", "+10 / +2 / +1 rounds per level", "8,23 24,23 24,7 20,3 8,3 8,23|13,26 13,29|19,26 19,29|12,10 20,10|12,15 20,15"),
            ("Rapid Service", UpgradeCategory.Weapon, UpgradeRarity.Common, 2, 0.25f, 0f, 0f, "Shortens each weapon’s reload cycle.", "-{0:P0} reload time per level", "18,26 12,26 6,21 4,14 7,7 13,4 21,4 27,10|27,4 27,11 20,11|18,24 13,15 18,15 14,8"),
            ("Kinetic Charge", UpgradeCategory.Gravity, UpgradeRarity.Uncommon, 1, 0.35f, 2f, 0f, "Change gravity to charge the next shot.", "+{0:P0} shot damage / {1:0.##} s", "3,5 12,5 12,27 28,27|24,23 28,27 24,31|21,23 17,15 22,15 18,7"),
            ("Magnetic Acceleration", UpgradeCategory.Gravity, UpgradeRarity.Common, 1, 0.15f, 0f, 0f, "Move faster with magnetic gravity.", "+{0:P0} magnetic move speed", "5,7 5,22 11,26 17,22 17,7|5,12 17,12|22,10 27,16 22,22|27,10 31,16 27,22"),
            ("Auto Feed", UpgradeCategory.Gravity, UpgradeRarity.Uncommon, 1, 3f, 1f, 0f, "Magnetic turns feed rounds from reserve.", "Up to {0:0} rounds / {1:0.##} s cooldown", "16,27 26,27 26,6 16,6 16,27|3,16 19,16|14,21 19,16 14,11|21,22 23,22|21,10 23,10"),
            ("Freefall", UpgradeCategory.Gravity, UpgradeRarity.Common, 1, 0.25f, 0f, 0f, "Deal more damage when firing without support.", "+{0:P0} airborne shot damage", "16,28 16,5|9,12 16,5 23,12|5,26 5,17|27,26 27,17|10,30 22,30"),
            ("Second Draft", UpgradeCategory.Reset, UpgradeRarity.Uncommon, 1, 0.75f, 2f, 0f, "RESET primes one high-damage shot.", "+{0:P0} shot damage / {1:0.##} s", "20,27 11,27 5,21 5,11 11,5 21,5 27,11|5,28 5,21 12,21|14,12 25,16 14,20 14,12"),
            ("Quality Assurance", UpgradeCategory.Reset, UpgradeRarity.Uncommon, 1, 0.35f, 1.5f, 0f, "Gain protection after RESET.", "-{0:P0} incoming damage / {1:0.##} s", "16,29 26,24 26,12 22,7 16,3 10,7 6,12 6,24 16,29|11,16 15,12 22,20"),
            ("Exploit Window", UpgradeCategory.Override, UpgradeRarity.Uncommon, 1, 0.30f, 1.5f, 0f, "OVERRIDE opens a brief damage window.", "+{0:P0} shot damage / {1:0.##} s", "4,11 4,25 28,25 28,7 14,7|4,20 28,20|19,5 10,13 19,13|9,4 9,9"),
            ("Residual Memory", UpgradeCategory.Override, UpgradeRarity.Rare, 1, 0.35f, 3f, 0f, "OVERRIDE destabilizes living hostiles.", "+{0:P0} damage taken / {1:0.##} s", "16,29 26,22 26,10 16,3 6,10 6,22 16,29|6,22 11,19 14,21 17,13 21,16 26,10|17,13 15,7"),
            ("Efficient Recall", UpgradeCategory.Recall, UpgradeRarity.Common, 1, 0.25f, 0f, 0f, "Use less chronoenergy during RECALL.", "-{0:P0} RECALL cost", "5,16 13,24 13,19 23,19 27,15 27,9 23,5 17,5|5,16 13,9 13,14 19,14|23,25 29,25"),
            ("Safe Return", UpgradeCategory.Recall, UpgradeRarity.Uncommon, 1, 0.40f, 1.5f, 0f, "Gain protection after RECALL.", "-{0:P0} incoming damage / {1:0.##} s", "16,28 26,24 26,13 22,7 16,4 10,7 6,13 6,24 16,28|10,16 22,16|10,16 15,21|10,16 15,11"),
            ("Bad Take", UpgradeCategory.Recall, UpgradeRarity.Rare, 1, 35f, 0f, 2.5f, "Detonate a temporal echo at the RECALL origin.", "{0:0} damage / {2:0.##} m radius", "16,25 19,19 27,20 22,14 25,7 17,10 11,5 10,13 4,16 12,19 16,25|23,25 26,28|5,7 2,4|28,12 31,12"),
            ("Accelerated Processing", UpgradeCategory.BulletTime, UpgradeRarity.Uncommon, 1, 0.35f, 0f, 0f, "Shoot and charge faster in BULLET TIME.", "+{0:P0} fire rate", "4,7 15,16 4,25 4,7|13,7 24,16 13,25|23,7 30,16 23,25"),
            ("Hypervelocity", UpgradeCategory.BulletTime, UpgradeRarity.Uncommon, 1, 0.50f, 0f, 0f, "Launch faster bolts during BULLET TIME.", "+{0:P0} projectile speed", "12,12 23,12 29,16 23,20 12,20 12,12|3,16 9,16|5,23 16,23|4,9 16,9"),
            ("Extended Battery", UpgradeCategory.Energy, UpgradeRarity.Common, 2, 25f, 0f, 0f, "Expand your capacity and charge the difference.", "+{0:0} max and current per level", "5,8 25,8 25,12 29,12 29,20 25,20 25,24 5,24 5,8|11,16 20,16|15,12 15,20"),
            ("Performance Bonus", UpgradeCategory.Energy, UpgradeRarity.Common, 1, 5f, 0f, 0f, "Earn extra charge for each unique takedown.", "+{0:0} chronoenergy per first kill", "16,28 26,22 26,10 16,4 6,10 6,22 16,28|16,21 16,11|11,16 21,16|16,2 16,5"),
            ("Penetration Protocol", UpgradeCategory.Weapon, UpgradeRarity.Uncommon, 1, 1f, 0f, 0f, "Bolts pass through an extra hostile.", "+{0:0} target", "5,16 27,16|22,21 27,16 22,11|10,8 10,24|17,8 17,24"),
            ("Ricochet Matrix", UpgradeCategory.Weapon, UpgradeRarity.Rare, 1, 0.8f, 0f, 0f, "Bolts reflect once off static geometry.", "{0:P0} damage retained", "5,7 16,18 27,7|20,7 27,7 27,14|6,23 26,23|11,27 21,27"),
            ("Twin Output", UpgradeCategory.Weapon, UpgradeRarity.Rare, 1, 0.6f, 0f, 0f, "Every fifth shot launches one extra bolt.", "{0:P0} damage on the extra bolt", "4,16 11,16 24,24|17,25 24,24 22,18|11,16 24,8|17,7 24,8 22,14"),
            ("Terminal Ballistics", UpgradeCategory.Weapon, UpgradeRarity.Uncommon, 1, 0.015f, 0f, 0f, "Bolt damage rises with distance travelled.", "+{0:P1}/m up to +45%", "5,6 5,27|5,6 28,6|8,10 16,15 24,23|19,23 24,23 24,18"),
            ("Close Quarters", UpgradeCategory.Weapon, UpgradeRarity.Common, 1, 0.30f, 0f, 5f, "Boosts hits within a short flight distance.", "+{0:P0} within {2:0} m", "4,12 9,16 4,20|28,12 23,16 28,20|16,20 20,16 16,12 12,16 16,20"),
            ("Vertical Advantage", UpgradeCategory.Gravity, UpgradeRarity.Common, 1, 0.25f, 0f, 0f, "Boosts shots from magnetic walls and ceilings.", "+{0:P0} shot damage", "6,6 6,26 26,26|12,10 12,21 21,21|17,25 21,21 17,17"),
            ("Tactical Exchange", UpgradeCategory.Weapon, UpgradeRarity.Uncommon, 1, 0.40f, 2f, 0f, "The first shot after a completed switch hits harder.", "+{0:P0} / {1:0} s, 4 s cooldown", "5,21 27,21|22,26 27,21 22,16|27,11 5,11|10,16 5,11 10,6"),
            ("Last Word", UpgradeCategory.Weapon, UpgradeRarity.Common, 1, 0.50f, 0f, 0f, "Boosts the last round in the magazine.", "+{0:P0} last shot damage", "11,6 21,6 21,20 16,27 11,20 11,6|11,11 21,11|16,30 16,32"),
            ("Surgical Strike", UpgradeCategory.Melee, UpgradeRarity.Common, 1, 8f, 0f, 0f, "Kill a hostile for the first time with the knife to recover health.", "+{0:0} HP per unique kill", "7,6 12,11 24,23 27,28 22,27 8,15 7,6|4,10 13,19|19,6 29,6|24,1 24,11"),
            ("Blood Battery", UpgradeCategory.Melee, UpgradeRarity.Uncommon, 1, 10f, 0f, 0f, "First-time knife kills award additional chronoenergy.", "+{0:0} CHRONO per unique kill", "5,7 25,7 25,11 29,11 29,21 25,21 25,25 5,25 5,7|17,11 12,17 18,17 15,22"),
            ("Extended Edge", UpgradeCategory.Melee, UpgradeRarity.Rare, 1, 30f, 0f, 5f, "Your swing fires a wave that passes targets already hit by the blade.", "{0:0} wave damage / {2:0} m", "4,6 19,16 4,26|12,6 27,16 12,26|25,10 30,16 25,22"),
            ("Time Sever", UpgradeCategory.Melee, UpgradeRarity.Rare, 1, 35f, 3f, 2.1f, "After a qualified RECALL, your next knife swing triggers one blast.", "{0:0} damage / {1:0} s window / {2:0.#} m", "7,7 23,7 19,13 14,20 9,25 24,25|20,14 28,17 24,21|5,13 10,16"),
            ("Focus Protocol", UpgradeCategory.Aim, UpgradeRarity.Common, 1, 0.30f, 0f, 0f, "Reduces recoil while aiming down sights.", "-{0:P0} ADS recoil", "16,6 24,9 27,16 24,23 16,26 8,23 5,16 8,9 16,6|16,10 16,22|10,16 22,16"),
            ("Precision Lock", UpgradeCategory.Aim, UpgradeRarity.Uncommon, 1, 1f, 1f, 0f, "Hold ADS to prime the next shot to pierce one more hostile.", "+{0:0} target after {1:0} s ADS", "10,5 5,5 5,10|22,5 27,5 27,10|27,22 27,27 22,27|10,27 5,27 5,22|12,16 20,16|16,12 16,20"),
            ("Steady Processing", UpgradeCategory.Aim, UpgradeRarity.Uncommon, 1, 0.25f, 0f, 0f, "Aiming down sights reduces BULLET TIME energy drain.", "-{0:P0} cost while aiming", "8,7 24,7 24,25 8,25 8,7|13,10 13,22|19,10 19,22|3,16 7,16|25,16 29,16"),
            ("Scavenger", UpgradeCategory.Scavenging, UpgradeRarity.Common, 1, 0.20f, 0f, 0f, "Improves enemy ammo drop odds in future encounters.", "+20 percentage points", "5,10 27,10 27,24 5,24 5,10|5,18 27,18|12,14 20,14|12,24 12,28|20,24 20,28"),
            ("Emergency Supply", UpgradeCategory.Scavenging, UpgradeRarity.Uncommon, 1, 5f, 0f, 0f, "Once per room, a first-time knife kill drops ammo when your active total is low.", "Triggers at {0:0} rounds or fewer", "4,9 28,9 28,25 4,25 4,9|13,5 19,5 19,9|16,13 16,18|16,21 16,22"),
            ("Quick Chamber", UpgradeCategory.Scavenging, UpgradeRarity.Uncommon, 1, 3f, 0f, 0f, "Matching ammo pickups feed the active magazine before filling reserve.", "Up to {0:0} rounds chambered", "16,7 25,7 25,27 16,27 16,7|4,16 18,16|12,11 18,16 12,21|20,11 22,11|20,23 22,23"),
            ("Blade & Bullet", UpgradeCategory.Melee, UpgradeRarity.Uncommon, 1, 0.50f, 2f, 0f, "Land a knife hit to empower your next shot across the entire volley.", "+{0:P0} shot damage / {1:0} s", "5,6 10,13 16,20 12,24 6,17 5,6|19,10 24,5 27,8 22,14 19,10|22,17 28,17"),
            ("Magnetic Execution", UpgradeCategory.Melee, UpgradeRarity.Rare, 1, 0.60f, 2f, 1f, "After a magnetic surface change, your next swing gains damage and reach.", "+{0:P0} damage / +{2:0} m / {1:0} s", "5,5 5,26 26,26|10,20 15,14 19,20 10,20|20,15 28,5 29,10 24,17")
        };

        internal RunUpgradeCatalog LoadOrCreate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RunUpgradeCatalog>(CatalogPath);
            var create = catalog == null;
            Directory.CreateDirectory(IconDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = create ? ScriptableObject.CreateInstance<RunUpgradeCatalog>() : catalog;
            var data = new SerializedObject(catalog);
            var definitions = data.FindProperty("_definitions");
            var previousCount = definitions.arraySize;
            if (previousCount == _entries.Length)
            {
                return catalog;
            }

            definitions.arraySize = _entries.Length;
            for (var index = 0; index < _entries.Length; index++)
            {
                var (title, category, rarity, level, amount, seconds, radius, description, numbers, icon) = _entries[index];
                var property = definitions.GetArrayElementAtIndex(index);
                if (index < previousCount)
                {
                    if ((UpgradeId)index is UpgradeId.ExtendedMagazine or UpgradeId.RapidService or UpgradeId.AcceleratedProcessing)
                    {
                        property.FindPropertyRelative("_description").stringValue = description;
                        property.FindPropertyRelative("_numbers").stringValue = numbers;
                    }

                    continue;
                }

                property.FindPropertyRelative("_id").enumValueIndex = index;
                property.FindPropertyRelative("_title").stringValue = title;
                property.FindPropertyRelative("_category").enumValueIndex = (int)category;
                property.FindPropertyRelative("_rarity").enumValueIndex = (int)rarity;
                property.FindPropertyRelative("_maxLevel").intValue = level;
                property.FindPropertyRelative("_magnitude").floatValue = amount;
                property.FindPropertyRelative("_seconds").floatValue = seconds;
                property.FindPropertyRelative("_radius").floatValue = radius;
                property.FindPropertyRelative("_description").stringValue = description;
                property.FindPropertyRelative("_numbers").stringValue = numbers;
                property.FindPropertyRelative("_icon").objectReferenceValue = CreateIcon((UpgradeId)index, icon);

                property.FindPropertyRelative("_limit").floatValue = (UpgradeId)index switch
                {
                    UpgradeId.TerminalBallistics => 0.45f,
                    UpgradeId.TacticalExchange => 4f,
                    _ => 0f
                };
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            if (create)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private Sprite CreateIcon(UpgradeId id, string paths)
        {
            return CreateIcon($"{IconDirectory}/{id}.png", paths);
        }

        internal Sprite CreateIcon(string path, string paths)
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            foreach (var stroke in paths.Split('|'))
            {
                var points = stroke.Split(' ');
                for (var index = 1; index < points.Length; index++)
                {
                    Paint(pixels, size, ReadPoint(points[index - 1]), ReadPoint(points[index]));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private Vector2 ReadPoint(string text)
        {
            var parts = text.Split(',');
            return new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        private void Paint(Color32[] pixels, int size, Vector2 from, Vector2 to)
        {
            var direction = to - from;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f) * (32f / size);
                    var t = Mathf.Clamp01(Vector2.Dot(point - from, direction) / direction.sqrMagnitude);
                    var distance = Vector2.Distance(point, from + direction * t);
                    var alpha = (byte)(255f * Mathf.Clamp01((1.12f - distance) * 3f));
                    var index = y * size + x;
                    pixels[index] = new Color32(255, 255, 255, (byte)Mathf.Max(alpha, pixels[index].a));
                }
            }
        }
    }
}
