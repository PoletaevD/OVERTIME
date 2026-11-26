using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal static class CombatHudLayout
    {
        internal static void SetCombatDefaults(Transform root, TMP_Text health, TMP_Text energy, PlaygroundPlayer player, TemporalDirector director)
        {
            var vitals = player.Combat.Health.Maximum;
            health.text = $"<b><i>{vitals}</i></b> <size=38%> / {vitals}  HP</size>";
            energy.text = $"<b>{director.Energy.Maximum:0}</b> <size=70%>CHRONO</size>";

            root.Find("Movement").gameObject.SetActive(false);
            root.Find("Controls").GetComponent<CanvasGroup>().alpha = 0f;

            var reticle = root.Find("Reticle");
            foreach (Transform arm in reticle)
            {
                if (!arm.name.StartsWith("Reticle ", StringComparison.Ordinal) || arm.GetComponent<Outline>() != null)
                {
                    continue;
                }

                var outline = arm.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1f, -1f);
                outline.effectColor = new Color(0.03f, 0.06f, 0.08f, 0.95f);
            }

            if (root.Find("Temporal Timeline") is RectTransform timeline)
            {
                var anchor = new Vector2(0.5f, 0f);
                timeline.anchorMin = timeline.anchorMax = timeline.pivot = anchor;
                timeline.anchoredPosition = new Vector2(0f, 55f);
            }
        }

        internal static void AddHealthDivisions(RectTransform vitals)
        {
            var track = (RectTransform)vitals.Find("Health Track");
            var size = track.sizeDelta;
            for (var index = 1; index < 10; index++)
            {
                var notch = new GameObject("Health Division", typeof(RectTransform)).AddComponent<Image>();
                var rect = notch.rectTransform;
                rect.SetParent(vitals, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = track.anchoredPosition + new Vector2(index * size.x / 10f, size.y * 0.5f);
                rect.sizeDelta = new Vector2(3f, size.y + 2f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -18f);
                notch.raycastTarget = false;
                notch.color = new Color(0.08f, 0.11f, 0.13f);
            }
        }

        internal static void SetSectorDefaults(Transform root, SectorRoute route, TMP_Text room, TMP_Text title, TMP_Text status)
        {
            room.text = "ROOM 01";
            var roomTitle = route.Generated
                ? Array.Find(route.DebugLibrary, definition => definition != null && definition.Category == RoomCategory.Entry && definition.AllowsDepth(1))?.Title
                : route.Current.RoomName;
            if (route.Generated)
            {
                var hasSupervisor = route.Seamless && route.DebugHasSupervisor;
                room.text = $"ROOM 01 / {RoutePlan.StandardRoomCount + (hasSupervisor ? 1 : 0):00}";
            }

            title.text = roomTitle?.ToUpperInvariant() ?? string.Empty;
            title.fontStyle |= FontStyles.Bold | FontStyles.Italic;
            status.text = "ENTER COMBAT ZONE";

            var timeline = root.Find("Temporal Timeline");
            var playback = timeline.Find("Playback Status").GetComponent<TMP_Text>();
            playback.fontStyle |= FontStyles.Bold | FontStyles.Italic;
            playback.text = "CHRONO BUFFER";
            var seconds = timeline.Find("History Seconds").GetComponent<TMP_Text>();
            seconds.fontStyle |= FontStyles.Bold;
            seconds.text = "0.0 s";
            timeline.Find("Rewind Binding").GetComponent<TMP_Text>().text = "PERSONAL HISTORY   0.0 s";
            timeline.Find("World Readout").GetComponent<TMP_Text>().text = "WORLD HISTORY   0.0 s";
        }

        internal static void BuildTemporalGlyphs(TemporalHud hud)
        {
            var texture = Resources.Load<Texture2D>("Temporal Glyphs");
            if (texture == null)
            {
                return;
            }

            var panel = hud.transform;
            var font = panel.Find("Playback Status").GetComponent<TMP_Text>().font;
            var labels = new[] { "Q   RESET", "E   OVERRIDE", "C   RECALL", "SHIFT   SLOW" };
            var glyphs = new RawImage[labels.Length];
            for (var index = 0; index < glyphs.Length; index++)
            {
                var glyph = new GameObject(labels[index], typeof(RectTransform)).AddComponent<RawImage>();
                var rect = glyph.rectTransform;
                rect.SetParent(panel, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = new Vector2(index * 96f + 49f, -1f);
                rect.sizeDelta = new Vector2(42f, 42f);
                glyph.texture = texture;
                glyph.uvRect = new Rect(index % 2 * 0.5f, index < 2 ? 0.5f : 0f, 0.5f, 0.5f);
                glyph.color = new Color(0.86f, 0.90f, 0.93f);
                glyph.raycastTarget = false;
                glyphs[index] = glyph;

                var key = new GameObject("Binding", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                key.rectTransform.SetParent(rect, false);
                key.rectTransform.anchoredPosition = new Vector2(0f, -32f);
                key.rectTransform.sizeDelta = new Vector2(94f, 22f);
                key.font = font;
                key.fontSize = 12.5f;
                key.fontStyle = FontStyles.Bold;
                key.alignment = TextAlignmentOptions.Center;
                key.raycastTarget = false;
                key.text = labels[index];
            }

            PlaygroundSceneBuilder.Wire(hud, "_glyphs", glyphs);
        }

        internal static void StyleHeading(TMP_Text heading, Color color)
        {
            heading.fontStyle |= FontStyles.Bold | FontStyles.Italic;
            heading.characterSpacing = 4f;
            heading.color = color;
        }
    }
}

