using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed partial class PlaygroundHudBuilder
    {
        internal void BuildRouteHud(PlaygroundHud movement, SectorRoute route, TemporalDirector director)
        {
            var root = movement.transform;
            var overlay = Panel("Route Selection", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.017f, 0.029f, 0.045f, 0.98f));
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.raycastTarget = true;
            var group = AddOverlayGroup(overlay.gameObject);
            var header = CenterLabel("Assignment Title", overlay.transform, new Vector2(0f, 405f), new Vector2(1140f, 73f), "SELECT NEXT ASSIGNMENT", 55f, s_paper);
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 2.7f;
            CenterLabel("Dispatch", overlay.transform, new Vector2(0f, 456f), new Vector2(900f, 30f), "MERIDIAN   /   SECURITY DISPATCH", 19f, s_cyan).characterSpacing = 2.3f;

            var map = RouteMap(overlay.transform);
            var controller = root.gameObject.AddComponent<RouteSelectionHud>();
            PlaygroundSceneBuilder.Wire(controller, "_route", route, "_director", director, "_overlay", group, "_mapLabels", map);
            var serialized = new SerializedObject(controller);
            var entries = serialized.FindProperty("_cards");
            entries.arraySize = 2;
            for (var index = 0; index < 2; index++)
            {
                BuildRouteCard(overlay.transform, entries.GetArrayElementAtIndex(index), index);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            CenterLabel("Controls", overlay.transform, new Vector2(0f, -407f), new Vector2(900f, 32f), "CLICK TO ASSIGN   /   PRESS 1 OR 2", 18f, s_muted).characterSpacing = 2f;

            var transition = Panel("Airlock Blackout", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.012f, 0.021f, 0.032f));
            transition.rectTransform.anchorMax = Vector2.one;
            var transferGroup = transition.gameObject.AddComponent<CanvasGroup>();
            transferGroup.alpha = 0f;
            transferGroup.blocksRaycasts = false;
            var transfer = route.gameObject.AddComponent<RouteTransfer>();
            PlaygroundSceneBuilder.Wire(transfer, "_overlay", transferGroup);
            PlaygroundSceneBuilder.Wire(route, "_transfer", transfer);
        }

        private TMP_Text[] RouteMap(Transform root)
        {
            var positions = new[]
            {
                new Vector2(-456f, 290f), new Vector2(-228f, 323f), new Vector2(-228f, 257f), new Vector2(0f, 290f),
                new Vector2(228f, 323f), new Vector2(228f, 257f), new Vector2(456f, 290f)
            };
            var edges = new[] { (0, 1), (0, 2), (1, 3), (2, 3), (3, 4), (3, 5), (4, 6), (5, 6) };
            var mapColor = new Color(0.13f, 0.28f, 0.35f);
            var center = Vector2.one * 0.5f;
            foreach (var (from, to) in edges)
            {
                var delta = positions[to] - positions[from];
                var line = Panel("Route Link", root, center, Vector2.zero, new Vector2(delta.magnitude, 2f), mapColor).rectTransform;
                line.pivot = center;
                line.anchoredPosition = (positions[from] + positions[to]) * 0.5f;
                line.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.right, delta));
            }

            var labels = new TMP_Text[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                var plate = Panel($"Route Node {index + 1}", root, center, Vector2.zero, new Vector2(204f, 52f), new Color(0.061f, 0.103f, 0.137f));
                plate.rectTransform.pivot = center;
                plate.rectTransform.anchoredPosition = positions[index];
                labels[index] = CenterLabel($"Route Node Label {index + 1}", plate.transform, Vector2.zero, new Vector2(200f, 52f), string.Empty, 15f, s_muted);
            }

            return labels;
        }

        private void BuildRouteCard(Transform root, SerializedProperty card, int index)
        {
            var center = Vector2.one * 0.5f;
            var panel = Panel($"Assignment {index + 1}", root, center, Vector2.zero, new Vector2(535f, 558f), new Color(0.065f, 0.105f, 0.143f));
            panel.rectTransform.pivot = center;
            panel.rectTransform.anchoredPosition = new Vector2((2 * index - 1) * 280f, -85f);
            var body = panel.transform;
            var group = body.gameObject.AddComponent<CanvasGroup>();
            var accent = Panel("Threat Accent", body, Vector2.zero, new Vector2(0f, 554f), new Vector2(535f, 4f), s_cyan);
            var thumbnail = Rect("Arena Survey", body, Vector2.zero, new Vector2(18f, 330f), new Vector2(499f, 210f)).gameObject.AddComponent<RawImage>();
            thumbnail.raycastTarget = false;
            var title = Label("Assignment Name", body, new Vector2(22f, 283f), new Vector2(490f, 42f), string.Empty, 31f, s_paper);
            title.fontStyle = FontStyles.Bold;
            var description = Label("Field Notes", body, new Vector2(22f, 237f), new Vector2(490f, 46f), string.Empty, 19f, s_muted);
            description.textWrappingMode = TextWrappingModes.Normal;
            var geometry = Label("Geometry", body, new Vector2(22f, 206f), new Vector2(490f, 27f), string.Empty, 17f, s_cyan);
            var threat = Label("Threat", body, new Vector2(22f, 172f), new Vector2(490f, 26f), string.Empty, 19f, s_cyan);
            var hostiles = Label("Hostiles", body, new Vector2(22f, 129f), new Vector2(490f, 44f), string.Empty, 17f, s_muted);
            hostiles.textWrappingMode = TextWrappingModes.Normal;
            var rewardPlate = Panel("Supply Bonus", body, Vector2.zero, new Vector2(18f, 72f), new Vector2(499f, 47f), new Color(0.11f, 0.18f, 0.22f));
            var rewardIcon = Rect("Reward Icon", rewardPlate.transform, Vector2.zero, new Vector2(7f, 2f), new Vector2(43f, 43f)).gameObject.AddComponent<RawImage>();
            rewardIcon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Security Route/MERIDIAN Reward Icons.png");
            rewardIcon.raycastTarget = false;
            var reward = Label("Reward", rewardPlate.transform, new Vector2(56f, 0f), new Vector2(428f, 47f), string.Empty, 18f, s_paper);
            var choice = Panel("Confirm Assignment", body, Vector2.zero, new Vector2(18f, 16f), new Vector2(499f, 45f), new Color(0.18f, 0.33f, 0.38f));
            choice.raycastTarget = true;
            var button = choice.gameObject.AddComponent<Button>();
            button.targetGraphic = choice;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.95f, 0.97f);
            colors.pressedColor = s_cyan;
            button.colors = colors;
            CenterLabel("Confirm Label", choice.transform, Vector2.zero, new Vector2(492f, 42f), $"{index + 1}   /   ACCEPT ASSIGNMENT", 18f, s_paper).characterSpacing = 1.3f;

            card.FindPropertyRelative("_button").objectReferenceValue = button;
            card.FindPropertyRelative("_group").objectReferenceValue = group;
            card.FindPropertyRelative("_accent").objectReferenceValue = accent;
            card.FindPropertyRelative("_thumbnail").objectReferenceValue = thumbnail;
            card.FindPropertyRelative("_title").objectReferenceValue = title;
            card.FindPropertyRelative("_description").objectReferenceValue = description;
            card.FindPropertyRelative("_geometry").objectReferenceValue = geometry;
            card.FindPropertyRelative("_threat").objectReferenceValue = threat;
            card.FindPropertyRelative("_hostiles").objectReferenceValue = hostiles;
            card.FindPropertyRelative("_reward").objectReferenceValue = reward;
            card.FindPropertyRelative("_rewardIcon").objectReferenceValue = rewardIcon;
        }
    }
}
