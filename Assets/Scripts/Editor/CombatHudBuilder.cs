using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed partial class PlaygroundHudBuilder
    {
        internal CombatHud BuildCombat(PlaygroundHud movement, TemporalDirector director, PlaygroundPlayer player)
        {
            var root = movement.transform;
            var controls = root.Find("Controls");
            var hints = controls.Find("Bindings").GetComponent<TMP_Text>();
            hints.text = "WASD  MOVE   MOUSE  LOOK\nSPACE  JUMP / GRAVITY   LMB  FIRE\nR  RELOAD   F  CHARGE   F5  RESTART\nQ  RESET   E  OVERRIDE   C  RECALL\nSHIFT  BULLET TIME   ESC  RELEASE\nF3  DEBUG   F4  MOTION   H  HINTS";

            var left = Rect("Combat Vitals", root, Vector2.zero, new Vector2(40f, 29f), new Vector2(382f, 124f));
            var health = Label("Health", left, new Vector2(15f, 79f), new Vector2(345f, 57f), string.Empty, 46f, s_paper);
            var healthFill = Meter("Health", left, 75f, 11f, s_paper);
            var energy = Label("Chrono Energy", left, new Vector2(18f, 44f), new Vector2(346f, 25f), string.Empty, 20f, s_cyan);
            var energyFill = Meter("Chrono", left, 36f, 5f, s_cyan);
            var cost = Label("Energy Cost", left, new Vector2(18f, 9f), new Vector2(346f, 20f), string.Empty, 14f, s_muted);

            CombatHudLayout.AddHealthDivisions(left);
            CombatHudLayout.SetCombatDefaults(root, health, energy, player, director);

            var center = Vector2.one * 0.5f;
            var station = CenterLabel("Recharge Prompt", root, new Vector2(0f, -160f), new Vector2(440f, 36f), "F  /  RECHARGE CHRONO ENERGY", 20f, s_cyan);
            AddOverlayGroup(station.gameObject);

            var markerRoot = Rect("Hit Feedback", root, center, Vector2.zero, Vector2.zero);
            var hitGroup = AddOverlayGroup(markerRoot.gameObject);
            var hitMarker = CenterLabel("Hitmarker", markerRoot, new Vector2(0f, 24f), new Vector2(96f, 36f), "+", 32f, s_paper);
            var hitLabel = CenterLabel("Hit Detail", markerRoot, new Vector2(0f, -39f), new Vector2(280f, 24f), string.Empty, 17f, s_paper);

            var damage = Rect("Damage Edge Tint", root, Vector2.zero, Vector2.zero, Vector2.zero);
            damage.anchorMax = Vector2.one;
            damage.SetAsFirstSibling();
            var damageGroup = AddOverlayGroup(damage.gameObject);
            AddDamageEdge(damage, false);
            AddDamageEdge(damage, true);

            var death = Rect("Recovery Screen", root, Vector2.zero, Vector2.zero, Vector2.zero);
            death.anchorMax = Vector2.one;
            var deathGroup = AddOverlayGroup(death.gameObject);
            var deathTitle = CenterLabel("Recovery Status", death.transform, new Vector2(0f, 105f), new Vector2(920f, 72f), "TEMPORAL RECOVERY AVAILABLE", 44f, s_paper);
            CombatHudLayout.StyleHeading(deathTitle, new Color(1f, 0.39f, 0.20f));
            var deathInfo = CenterLabel("Recovery Instructions", death.transform, new Vector2(0f, 46f), new Vector2(680f, 36f), string.Empty, 21f, s_orange);

            var retry = Rect("Retry Button", death, center, new Vector2(0f, -28f), new Vector2(270f, 50f));
            retry.pivot = center;
            retry.anchoredPosition = new Vector2(0f, -28f);
            var restart = retry.gameObject.AddComponent<Button>();
            var retryLabel = CenterLabel("Retry Label", retry, Vector2.zero, new Vector2(260f, 44f), "RESTART DRILL", 22f, s_paper);
            retryLabel.raycastTarget = true;
            restart.targetGraphic = retryLabel;
            restart.gameObject.SetActive(false);
            root.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("Combat UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var hud = root.gameObject.AddComponent<CombatHud>();
            PlaygroundSceneBuilder.Wire(hud, "_director", director, "_player", player, "_healthLabel", health, "_healthFill", healthFill, "_damageFlash", damageGroup, "_energyLabel", energy, "_costLabel", cost, "_energyFill", energyFill, "_stationHint", station, "_hitMarker", hitMarker, "_hitLabel", hitLabel, "_hitGroup", hitGroup, "_deathGroup", deathGroup, "_deathTitle", deathTitle, "_deathInfo", deathInfo, "_restart", restart);
            return hud;
        }

        internal void BuildSecurity(PlaygroundHud movement, CombatHud hud, SecurityEnemy[] enemies, Camera view)
        {
            var root = movement.transform;
            var top = new Vector2(0.5f, 1f);
            var panel = Rect("Security Encounter", root, top, Vector2.zero, new Vector2(248f, 43f));
            panel.pivot = top;
            panel.anchoredPosition = new Vector2(0f, -42f);
            var remaining = Label("Remaining Hostiles", panel.transform, new Vector2(13f, 6f), new Vector2(222f, 30f), $"HOSTILES  {enemies.Length:00}", 18f, s_cyan);
            remaining.alignment = TextAlignmentOptions.Center;

            PlaygroundSceneBuilder.Wire(hud, "_enemies", enemies, "_remainingLabel", remaining);
            InstallDamageArcs(root, hud);
        }

        internal void BuildSector(PlaygroundHud movement, CombatHud combat, PlaygroundPlayer player, TemporalDirector director, SectorRoute route, Camera view)
        {
            var root = movement.transform;
            root.Find("Controls/Bindings").GetComponent<TMP_Text>().text = "WASD  MOVE   MOUSE  LOOK\nSPACE  JUMP / GRAVITY   LMB  FIRE\nR  RELOAD   F5  RETRY SECTOR\nQ  RESET   E  OVERRIDE   C  RECALL\nSHIFT  BULLET TIME   ESC  RELEASE\nF3  DEBUG   F4  MOTION   H  HINTS";
            InstallDamageArcs(root, combat);
            var card = Panel("Sector Encounter", root, Vector2.up, Vector2.zero, new Vector2(357f, 113f), new Color(0.035f, 0.051f, 0.061f, 0.45f)).rectTransform;
            card.pivot = Vector2.up;
            card.anchoredPosition = new Vector2(40f, -32f);
            var room = Label("Room Number", card.transform, new Vector2(17f, 81f), new Vector2(280f, 20f), string.Empty, 15f, s_cyan);
            var title = Label("Room Title", card.transform, new Vector2(17f, 47f), new Vector2(326f, 31f), string.Empty, 23f, s_paper);
            var state = Label("Encounter Status", card.transform, new Vector2(17f, 16f), new Vector2(325f, 23f), string.Empty, 15f, s_orange);
            CombatHudLayout.SetSectorDefaults(root, route, room, title, state);
            var arrow = CenterLabel("Exit Direction", card.transform, new Vector2(145f, -29f), new Vector2(30f, 30f), "^", 24f, s_cyan);
            arrow.gameObject.SetActive(false);

            var announcement = CenterLabel("Room Announcement", root, new Vector2(0f, 230f), new Vector2(750f, 56f), string.Empty, 35f, s_cyan);
            var intro = AddOverlayGroup(announcement.gameObject);
            var win = Rect("Sector Success", root, Vector2.zero, Vector2.zero, Vector2.zero);
            win.anchorMax = Vector2.one;
            var success = AddOverlayGroup(win.gameObject);
            var heading = CenterLabel("Sector Clear Title", win, new Vector2(0f, 143f), new Vector2(1200f, 88f), "SECURITY SECTOR CLEARED", 58f, s_paper);
            CombatHudLayout.StyleHeading(heading, new Color(0.42f, 0.92f, 0.86f));
            var stats = CenterLabel("Sector Statistics", win.transform, new Vector2(0f, -9f), new Vector2(760f, 185f), string.Empty, 25f, s_cyan);
            CenterLabel("Replay Instructions", win.transform, new Vector2(0f, -158f), new Vector2(480f, 38f), "F5  /  RETRY SECTOR", 17f, s_orange);

            var debugPanel = (RectTransform)root.Find("Debug Overlay");
            debugPanel.sizeDelta += Vector2.up * 49f;
            var telemetry = (RectTransform)debugPanel.Find("Telemetry");
            telemetry.anchoredPosition += Vector2.up * 49f;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(debugPanel);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(telemetry);
            var debug = Label("Projectile and Encounter Readout", debugPanel, new Vector2(16f, 5f), new Vector2(390f, 40f), string.Empty, 13f, s_muted);

            var sector = root.gameObject.AddComponent<SectorHud>();
            PlaygroundSceneBuilder.Wire(sector, "_route", route, "_director", director, "_player", player, "_view", view, "_roomNumber", room, "_roomTitle", title, "_status", state, "_exitArrow", arrow, "_intro", intro, "_introText", announcement, "_success", success, "_stats", stats, "_debug", debug);
        }

        internal void InstallDamageArcs(Transform root, CombatHud hud)
        {
            var path = "Assets/Resources/Damage Arc.png";
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
            if (importer.textureType != UnityEditor.TextureImporterType.Sprite)
            {
                importer.textureType = UnityEditor.TextureImporterType.Sprite;
                importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            PlaygroundSceneBuilder.Wire(hud, "_damageArc", UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        private Image Meter(string name, Transform parent, float y, float height, Color color)
        {
            var size = new Vector2(346f, height);
            var position = new Vector2(18f, y);
            Panel($"{name} Track", parent, Vector2.zero, position, size, new Color(0.16f, 0.24f, 0.30f));
            return Panel($"{name} Fill", parent, Vector2.zero, position, size, color);
        }

        private TMP_Text CenterLabel(string name, Transform parent, Vector2 position, Vector2 size, string text, float fontSize, Color color)
        {
            var label = Label(name, parent, position, size, text, fontSize, color);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.one * 0.5f;
            label.rectTransform.anchorMax = label.rectTransform.anchorMin;
            label.rectTransform.pivot = label.rectTransform.anchorMin;
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        private void AddDamageEdge(Transform parent, bool top)
        {
            var anchor = top ? Vector2.up : Vector2.zero;
            var image = Panel(top ? "Upper Impact Edge" : "Lower Impact Edge", parent, anchor, Vector2.zero, new Vector2(0f, 21f), new Color(0.90f, 0.17f, 0.10f, 0.52f));
            image.rectTransform.anchorMax = anchor + Vector2.right;
            image.rectTransform.pivot = anchor;
        }

        private static CanvasGroup AddOverlayGroup(GameObject host)
        {
            var group = host.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return group;
        }
    }
}
