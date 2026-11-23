using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed partial class PlaygroundHudBuilder
    {
        internal void BuildCombat(PlaygroundHud movement, TemporalDirector director, PlaygroundPlayer player)
        {
            var root = movement.transform;
            var controls = root.Find("Controls");
            var hints = controls.Find("Bindings").GetComponent<TMP_Text>();
            hints.text = "WASD  MOVE   MOUSE  LOOK\nSPACE  JUMP / GRAVITY   LMB  FIRE\nR  RELOAD   F  CHARGE   F5  RESTART\nQ  RESET   E  OVERRIDE   C  RECALL\nSHIFT  BULLET TIME   ESC  RELEASE\nF3  DEBUG   F4  MOTION   H  HINTS";

            var left = Panel("Combat Vitals", root, Vector2.zero, new Vector2(44f, 202f), new Vector2(382f, 124f), s_ink).rectTransform;
            var health = Label("Health", left, new Vector2(18f, 83f), new Vector2(346f, 28f), "VITALS  100 / 100", 24f, s_paper);
            var healthFill = Meter("Health", left, 75f, 6f, s_paper);
            var energy = Label("Chrono Energy", left, new Vector2(18f, 44f), new Vector2(346f, 25f), "CHRONO  100 / 100", 20f, s_cyan);
            var energyFill = Meter("Chrono", left, 36f, 5f, s_cyan);
            var cost = Label("Energy Cost", left, new Vector2(18f, 9f), new Vector2(346f, 20f), string.Empty, 14f, s_muted);

            var right = Panel("ARC-01 Readout", root, Vector2.right, Vector2.zero, new Vector2(386f, 104f), s_ink).rectTransform;
            right.pivot = Vector2.right;
            right.anchoredPosition = new Vector2(-44f, 214f);
            var reload = Label("Weapon Status", right, new Vector2(18f, 70f), new Vector2(346f, 23f), "ARC-01  /  AUTO", 16f, s_cyan);
            var ammo = Label("Magazine and Reserve", right, new Vector2(18f, 19f), new Vector2(346f, 46f), "30  <size=55%>/ 180</size>", 38f, s_paper);
            var reloadFill = Meter("Reload", right, 11f, 4f, s_orange);

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

            var death = Panel("Recovery Screen", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.12f, 0.022f, 0.033f, 0.65f));
            death.rectTransform.anchorMax = Vector2.one;
            var deathGroup = AddOverlayGroup(death.gameObject);
            var deathTitle = CenterLabel("Recovery Status", death.transform, new Vector2(0f, 105f), new Vector2(920f, 72f), "TEMPORAL RECOVERY AVAILABLE", 44f, s_paper);
            var deathInfo = CenterLabel("Recovery Instructions", death.transform, new Vector2(0f, 46f), new Vector2(680f, 36f), string.Empty, 21f, s_orange);

            var restartPanel = Panel("Retry Button", death.transform, center, new Vector2(0f, -28f), new Vector2(270f, 50f), s_ink);
            restartPanel.rectTransform.pivot = center;
            restartPanel.raycastTarget = true;
            var restart = restartPanel.gameObject.AddComponent<Button>();
            restart.targetGraphic = restartPanel;
            CenterLabel("Retry Label", restart.transform, Vector2.zero, new Vector2(260f, 44f), "RESTART DRILL", 22f, s_paper);
            restart.gameObject.SetActive(false);
            root.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("Combat UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var hud = root.gameObject.AddComponent<CombatHud>();
            PlaygroundSceneBuilder.Wire(hud, "_director", director, "_player", player, "_healthLabel", health, "_healthFill", healthFill, "_damageFlash", damageGroup, "_ammoLabel", ammo, "_reloadLabel", reload, "_reloadFill", reloadFill, "_energyLabel", energy, "_costLabel", cost, "_energyFill", energyFill, "_stationHint", station, "_hitMarker", hitMarker, "_hitLabel", hitLabel, "_hitGroup", hitGroup, "_deathGroup", deathGroup, "_deathTitle", deathTitle, "_deathInfo", deathInfo, "_restart", restart);
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
