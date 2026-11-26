using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class CombatHudLayout
    {
        internal void Apply(Transform root)
        {
            StyleSheet(root, "Recovery Screen", "Recovery Status", new Color(1f, 0.39f, 0.20f));
            StyleSheet(root, "Sector Success", "Sector Clear Title", new Color(0.42f, 0.92f, 0.86f));
            StyleSheet(root, "Current Protocol Build", "Current Build Title", new Color(0.74f, 0.84f, 0.88f));
            var vitals = Panel(root, "Combat Vitals", Vector2.zero, new Vector2(40f, 29f), 0.40f);
            if (vitals != null)
            {
                var hp = vitals.Find("Health").GetComponent<TMP_Text>();
                hp.fontSize = 46f;
                hp.rectTransform.sizeDelta = new Vector2(345f, 57f);
                hp.rectTransform.anchoredPosition = new Vector2(15f, 79f);
                var track = (RectTransform)vitals.Find("Health Track");
                var fill = (RectTransform)vitals.Find("Health Fill");
                track.sizeDelta = fill.sizeDelta = new Vector2(346f, 11f);
                for (var index = 1; index < 10; index++)
                {
                    var notch = new GameObject("Health Division", typeof(RectTransform)).AddComponent<Image>();
                    notch.rectTransform.SetParent(vitals, false);
                    notch.raycastTarget = false;
                    notch.color = new Color(0.08f, 0.11f, 0.13f);
                    notch.rectTransform.anchorMax = notch.rectTransform.anchorMin = Vector2.zero;
                    notch.rectTransform.anchoredPosition = track.anchoredPosition + new Vector2(index * 34.6f, 5.5f);
                    notch.rectTransform.sizeDelta = new Vector2(3f, 13f);
                    notch.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                }
            }

            Panel(root, "Temporal Timeline", new Vector2(0.5f, 0f), new Vector2(0f, 55f), 0.67f);
            var sector = Panel(root, "Sector Encounter", Vector2.up, new Vector2(40f, -32f), 0.45f);
            if (sector != null)
            {
                sector.Find("Room Title").GetComponent<TMP_Text>().fontStyle |= FontStyles.Bold | FontStyles.Italic;
            }

            var gun = Panel(root, "ARC-01 Readout", Vector2.right, new Vector2(-40f, 29f), 0.24f);
            if (gun != null)
            {
                gun.localScale = Vector3.one * 0.78f;
            }
        }

        internal RawImage[] CreateModulePlates(RectTransform hint)
        {
            var texture = Resources.Load<Texture2D>("Module Plates");
            var plates = new RawImage[3];
            for (var index = 0; index < plates.Length; index++)
            {
                var plate = new GameObject("Attachment Profile", typeof(RectTransform)).AddComponent<RawImage>();
                plate.rectTransform.SetParent(hint, false);
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                plate.rectTransform.pivot = new Vector2(0.5f, 1f);
                plate.rectTransform.sizeDelta = new Vector2(44f, 44f);
                plate.texture = texture;
                plate.raycastTarget = false;
                plate.enabled = false;
                plates[index] = plate;
            }

            return plates;
        }

        private void StyleSheet(Transform root, string panelName, string headingName, Color accent)
        {
            var panel = root.Find(panelName);
            var heading = panel != null ? panel.Find(headingName)?.GetComponent<TMP_Text>() : null;
            if (heading == null)
            {
                return;
            }

            heading.fontStyle |= FontStyles.Bold | FontStyles.Italic;
            heading.characterSpacing = 4f;
            heading.color = accent;
            var engraving = new GameObject("Meridian Engraving", typeof(RectTransform)).AddComponent<RawImage>();
            engraving.rectTransform.SetParent(panel, false);
            engraving.rectTransform.SetAsFirstSibling();
            engraving.rectTransform.anchorMin = new Vector2(0.53f, 0.05f);
            engraving.rectTransform.anchorMax = new Vector2(0.98f, 0.91f);
            engraving.rectTransform.offsetMin = engraving.rectTransform.offsetMax = Vector2.zero;
            engraving.texture = Resources.Load<Texture2D>("Protocol Print");
            engraving.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            engraving.raycastTarget = false;
        }

        private RectTransform Panel(Transform root, string name, Vector2 anchor, Vector2 position, float alpha)
        {
            var rect = root.Find(name) as RectTransform;
            if (rect == null)
            {
                return null;
            }

            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            if (rect.TryGetComponent<Image>(out var surface))
            {
                surface.color = new Color(0.035f, 0.051f, 0.061f, alpha);
            }

            return rect;
        }
    }
}
