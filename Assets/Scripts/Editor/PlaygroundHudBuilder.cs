using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed class PlaygroundHudBuilder
    {
        private static readonly Color s_ink = new(0.035f, 0.054f, 0.077f, 0.91f);
        private static readonly Color s_paper = new(0.91f, 0.95f, 0.97f);
        private static readonly Color s_muted = new(0.64f, 0.78f, 0.85f);
        private static readonly Color s_cyan = new(0.27f, 0.88f, 0.90f);
        private static readonly Color s_orange = new(1f, 0.42f, 0.17f);

        private readonly TMP_FontAsset _font;

        internal PlaygroundHudBuilder(TMP_FontAsset font)
        {
            _font = font;
        }

        internal void Build(Transform parent, PlaygroundInput input, GravityMotor motor, GravityCamera rig)
        {
            var root = new GameObject("MERIDIAN HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var reticle = BuildReticle(root.transform);
            var movement = Panel("Movement", root.transform, Vector2.zero, new Vector2(44f, 45f), new Vector2(382f, 142f), s_ink).rectTransform;
            var indicator = Panel("Magnet Indicator", movement, Vector2.zero, new Vector2(18f, 111f), new Vector2(8f, 8f), s_orange);
            Label("Identity", movement, new Vector2(34f, 104f), new Vector2(300f, 23f), "M E R I D I A N   /   LOCOMOTION", 15f, s_cyan);
            var speed = Label("Speed", movement, new Vector2(16f, 51f), new Vector2(154f, 46f), "0.0 <size=50%>M/S</size>", 38f, s_paper);
            var state = Label("State", movement, new Vector2(174f, 68f), new Vector2(190f, 20f), "GROUNDED  /  STANDARD", 15f, s_paper);
            var gravity = Label("Gravity", movement, new Vector2(174f, 48f), new Vector2(190f, 20f), "UP  [0.0  1.0  0.0]", 15f, s_muted);
            Panel("Speed Track", movement, Vector2.zero, new Vector2(18f, 22f), new Vector2(346f, 3f), new Color(0.19f, 0.32f, 0.39f));
            var fill = Panel("Speed Fill", movement, Vector2.zero, new Vector2(18f, 22f), new Vector2(346f, 3f), s_cyan);
            fill.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            var hints = Panel("Controls", root.transform, Vector2.one, Vector2.zero, new Vector2(418f, 134f), s_ink);
            hints.rectTransform.pivot = Vector2.one;
            hints.rectTransform.anchoredPosition = new Vector2(-44f, -44f);
            var group = hints.gameObject.AddComponent<CanvasGroup>();
            var hintText = "WASD   MOVE     MOUSE   LOOK\nSPACE   TAP JUMP / HOLD  ATTACH/DROP\nLMB   CAPTURE / FIRE     ESC   RELEASE\nR   RESET    H   HINTS    F3   DEBUG    F4   MOTION";
            Label("Bindings", hints.transform, new Vector2(16f, 10f), new Vector2(386f, 114f), hintText, 16f, s_paper);

            var debugPanel = Panel("Debug Overlay", root.transform, Vector2.up, Vector2.zero, new Vector2(420f, 234f), s_ink);
            debugPanel.rectTransform.pivot = Vector2.up;
            debugPanel.rectTransform.anchoredPosition = new Vector2(44f, -44f);
            var debug = Label("Telemetry", debugPanel.transform, new Vector2(18f, 12f), new Vector2(384f, 210f), "MOTOR TELEMETRY", 17f, s_cyan);
            debugPanel.gameObject.SetActive(false);

            var hud = root.AddComponent<PlaygroundHud>();
            PlaygroundSceneBuilder.Wire(hud, "_input", input, "_motor", motor, "_cameraRig", rig, "_movementPanel", movement, "_reticleArms", reticle, "_gravityIndicator", indicator, "_speedFill", fill, "_speed", speed, "_state", state, "_gravity", gravity, "_debug", debug, "_hints", group, "_debugPanel", debugPanel.gameObject);
        }

        internal CanvasGroup BuildTemporal(PlaygroundHud movementHud, TemporalDirector director, GravityCamera rig)
        {
            var root = movementHud.transform;
            var lowerRight = Vector2.right;
            var panel = Panel("Temporal Timeline", root, lowerRight, Vector2.zero, new Vector2(386f, 116f), s_ink).rectTransform;
            panel.pivot = lowerRight;
            panel.anchoredPosition = new Vector2(-44f, 45f);
            Label("Rewind Binding", panel, new Vector2(18f, 73f), new Vector2(214f, 32f), "Q — REWIND", 25f, s_paper);
            var time = Label("History Seconds", panel, new Vector2(236f, 73f), new Vector2(132f, 32f), "0.00 s", 20f, s_paper);
            time.alignment = TextAlignmentOptions.MidlineRight;
            var status = Label("Playback Status", panel, new Vector2(18f, 15f), new Vector2(260f, 22f), "RECORDING", 14f, s_cyan);

            var track = Panel("History Track", panel, Vector2.zero, new Vector2(18f, 49f), new Vector2(350f, 7f), new Color(0.13f, 0.23f, 0.28f)).rectTransform;
            var right = new Vector2(1f, 0.5f);
            var recorded = Panel("Recorded History", track, right, Vector2.zero, new Vector2(0f, 7f), new Color(0.18f, 0.55f, 0.63f)).rectTransform;
            recorded.pivot = right;
            var depth = Panel("Rewind Depth", track, right, Vector2.zero, new Vector2(0f, 7f), s_cyan).rectTransform;
            depth.pivot = right;
            var cursor = Panel("Timeline Cursor", track, right, Vector2.zero, new Vector2(3f, 17f), s_paper).rectTransform;
            cursor.pivot = Vector2.one * 0.5f;
            var accent = Panel("Playback Accent", panel, Vector2.zero, Vector2.zero, new Vector2(386f, 2f), s_cyan).gameObject.AddComponent<CanvasGroup>();
            accent.alpha = 0f;

            var hud = panel.gameObject.AddComponent<TemporalHud>();
            PlaygroundSceneBuilder.Wire(hud, "_director", director, "_cameraRig", rig, "_track", track, "_recorded", recorded, "_depth", depth, "_cursor", cursor, "_status", status, "_time", time, "_activeLabel", accent);
            PlaygroundSceneBuilder.Wire(movementHud, "_temporalDirector", director);
            ConfigureTemporalReadouts(movementHud, hud);

            var flash = Panel("RESET Screen Tint", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.27f, 0.82f, 1f, 0.13f)).rectTransform;
            flash.SetAsFirstSibling();
            flash.anchorMax = Vector2.one;
            var pulse = flash.gameObject.AddComponent<CanvasGroup>();
            pulse.alpha = 0f;
            pulse.blocksRaycasts = false;
            return pulse;
        }

        internal void ConfigureTemporalReadouts(PlaygroundHud movementHud, TemporalHud hud)
        {
            var panel = (RectTransform)hud.transform;
            if (panel.Find("World Track") != null)
            {
                return;
            }

            Undo.RecordObject(panel, "Resize Temporal HUD");
            panel.sizeDelta = new Vector2(386f, 154f);
            var status = panel.Find("Playback Status").GetComponent<TMP_Text>();
            var difference = panel.Find("History Seconds").GetComponent<TMP_Text>();
            var player = panel.Find("Rewind Binding").GetComponent<TMP_Text>();
            PlaceReadout(status, new Vector2(18f, 118f), new Vector2(234f, 26f), 19f, "NORMAL");
            PlaceReadout(difference, new Vector2(253f, 118f), new Vector2(115f, 26f), 14f, "P-W  0.00 s");
            PlaceReadout(player, new Vector2(18f, 91f), new Vector2(350f, 22f), 17f, "PLAYER  FWD   0.00 s");
            var world = Label("World Readout", panel, new Vector2(18f, 47f), new Vector2(350f, 22f), "WORLD   FWD   0.00 s", 17f, s_paper);
            var playerTrack = (RectTransform)panel.Find("History Track");
            Undo.RecordObject(playerTrack, "Place Player History");
            playerTrack.anchoredPosition = new Vector2(18f, 78f);
            var worldTrack = Object.Instantiate(playerTrack, panel);
            worldTrack.name = "World Track";
            worldTrack.anchoredPosition = new Vector2(18f, 34f);
            Undo.RegisterCreatedObjectUndo(world.gameObject, "Add World Readout");
            Undo.RegisterCreatedObjectUndo(worldTrack.gameObject, "Add World History");
            PlaygroundSceneBuilder.Wire(hud, "_playerReadout", player, "_worldReadout", world, "_worldTrack", worldTrack);

            Label("Temporal Bindings", panel, new Vector2(18f, 7f), new Vector2(350f, 18f), "Q RESET   E OVERRIDE   C RECALL   SHIFT BULLET TIME", 13f, s_muted);
            var debugPanel = (RectTransform)movementHud.transform.Find("Debug Overlay");
            var debugText = (RectTransform)debugPanel.Find("Telemetry");
            Undo.RecordObjects(new Object[] { debugPanel, debugText }, "Resize Temporal Diagnostics");
            debugPanel.sizeDelta = new Vector2(490f, 422f);
            debugText.sizeDelta = new Vector2(454f, 398f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(debugPanel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(debugText);
        }

        private void PlaceReadout(TMP_Text label, Vector2 position, Vector2 size, float fontSize, string text)
        {
            Undo.RecordObjects(new Object[] { label, label.rectTransform }, "Update Temporal Readout");
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.fontSize = fontSize;
            label.text = text;
        }

        private RectTransform[] BuildReticle(Transform parent)
        {
            var center = Vector2.one * 0.5f;
            var root = Rect("Reticle", parent, center, Vector2.zero, Vector2.zero);
            root.pivot = center;
            var dot = Panel("Aim Point", root, center, Vector2.zero, Vector2.one * 3f, s_paper);
            dot.rectTransform.pivot = center;
            var size = new Vector2(6f, 2.5f);
            var arms = new[]
            {
                Panel("Reticle Left", root, center, Vector2.left * 8f, size, s_paper).rectTransform,
                Panel("Reticle Right", root, center, Vector2.right * 8f, size, s_paper).rectTransform,
                Panel("Reticle Top", root, center, Vector2.up * 8f, new Vector2(size.y, size.x), s_paper).rectTransform,
                Panel("Reticle Bottom", root, center, Vector2.down * 8f, new Vector2(size.y, size.x), s_paper).rectTransform
            };
            foreach (var arm in arms)
            {
                arm.pivot = center;
            }

            return arms;
        }

        private Image Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string content, float fontSize, Color color)
        {
            var label = Rect(name, parent, Vector2.zero, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = content;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
