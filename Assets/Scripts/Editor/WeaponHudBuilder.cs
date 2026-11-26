using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed partial class PlaygroundHudBuilder
    {
        internal void BuildWeaponHud(PlaygroundHud movement, SectorRoute route, TemporalDirector director, WeaponDefinition[] options)
        {
            var root = movement.transform;
            var reticle = root.Find("Reticle");
            var precision = Rect("Charge Reticle", reticle, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            Panel("Precision Point", precision, Vector2.one * 0.5f, new Vector2(-1.5f, -1.5f), new Vector2(3f, 3f), s_paper);
            var ticks = new Image[12];
            for (var index = 0; index < ticks.Length; index++)
            {
                var angle = (float)index / ticks.Length * Mathf.PI * 2f;
                var point = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 22f;
                ticks[index] = Panel($"Charge Tick {index + 1}", precision, Vector2.one * 0.5f, point - new Vector2(1.5f, 3f), new Vector2(3f, 6f), s_cyan);
                ticks[index].rectTransform.pivot = Vector2.one * 0.5f;
                ticks[index].rectTransform.anchoredPosition = point;
                ticks[index].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }

            precision.gameObject.SetActive(false);
            PlaygroundSceneBuilder.Wire(movement, "_chargeReticle", precision, "_chargeTicks", ticks);
            BuildSecondarySelection(root, route, director, options);
        }

        internal void BuildSecondarySelection(Transform root, SectorRoute route, TemporalDirector director, WeaponDefinition[] options, WeaponSelectionHud existing = null)
        {
            var overlay = Panel("Secondary Weapon Selection", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.018f, 0.031f, 0.049f, 0.96f));
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.raycastTarget = true;
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            var center = Vector2.one * 0.5f;
            var header = CenterLabel("Secondary Selection Title", overlay.transform, new Vector2(0f, 290f), new Vector2(1320f, 80f), "SELECT SECONDARY WEAPON", 60f, s_paper);
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 3.5f;
            CenterLabel("Loadout Summary", overlay.transform, new Vector2(0f, 226f), new Vector2(900f, 38f), "SLOT 1  /  ARC-01 COMPLIANCE EQUIPPED", 21f, s_muted).characterSpacing = 2f;
            var buttons = new Button[options.Length];
            for (var index = 0; index < buttons.Length; index++)
            {
                var definition = options[index];
                var tint = definition.Type == WeaponType.Scatter ? s_orange : s_cyan;
                var card = Panel(definition.DisplayName, overlay.transform, center, new Vector2((index - (options.Length - 1) * 0.5f) * 564f, -36f), new Vector2(540f, 430f), new Color(0.064f, 0.11f, 0.148f));
                card.rectTransform.pivot = center;
                Panel("Accent", card.transform, Vector2.zero, new Vector2(0f, 427f), new Vector2(540f, 3f), tint);
                Panel("Inspection Plate", card.transform, Vector2.zero, new Vector2(22f, 198f), new Vector2(496f, 207f), new Color(0.037f, 0.078f, 0.105f));
                var image = Panel("Weapon Portrait", card.transform, Vector2.zero, new Vector2(44f, 204f), new Vector2(452f, 192f), Color.white);
                image.sprite = definition.Icon;
                image.preserveAspect = true;
                Label("Weapon Name", card.transform, new Vector2(24f, 154f), new Vector2(492f, 40f), definition.DisplayName, 32f, s_paper).fontStyle = FontStyles.Bold;
                var tuning = definition.Tuning;
                var feature = definition.Type == WeaponType.Scatter ? $"{tuning.Pellets} Р“вЂ” {tuning.Damage} DMG    /    {tuning.Magazine} MAG    /    {tuning.Spread:0}Р’В° SPREAD" : definition.Type == WeaponType.SemiAutomatic ? $"{tuning.Damage} DMG    /    {tuning.Magazine} MAG    /    SEMI-AUTO" : $"{tuning.Damage} DMG    /    {tuning.Magazine} MAG    /    {tuning.Targets} TARGETS";
                Label("Weapon Specs", card.transform, new Vector2(24f, 117f), new Vector2(492f, 32f), feature, 20f, tint);
                var description = Label("Weapon Role", card.transform, new Vector2(24f, 67f), new Vector2(492f, 46f), definition.Description, 19f, s_muted);
                description.textWrappingMode = TextWrappingModes.Normal;
                var plate = Panel("Equip Weapon", card.transform, Vector2.zero, new Vector2(24f, 15f), new Vector2(492f, 42f), new Color(0.13f, 0.24f, 0.29f));
                plate.raycastTarget = true;
                buttons[index] = plate.gameObject.AddComponent<Button>();
                buttons[index].targetGraphic = plate;
                var colors = buttons[index].colors;
                colors.highlightedColor = tint;
                colors.selectedColor = tint;
                colors.pressedColor = Color.white;
                buttons[index].colors = colors;
                CenterLabel("Equip Label", plate.transform, Vector2.zero, new Vector2(480f, 39f), $"{index + 1}   /   EQUIP IN SLOT 2", 18f, s_paper).characterSpacing = 2f;
            }

            var controller = existing != null ? existing : root.gameObject.AddComponent<WeaponSelectionHud>();
            PlaygroundSceneBuilder.Wire(controller, "_route", route, "_director", director, "_overlay", group, "_buttons", buttons);
            overlay.gameObject.SetActive(false);
        }
    }
}
