using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime.Editor
{
    internal sealed partial class PlaygroundHudBuilder
    {
        internal void BuildUpgrades(PlaygroundHud movement, SectorRoute route, TemporalDirector director, PlaygroundPlayer player, RunProgression progression)
        {
            var root = movement.transform;
            BuildRunInspection(root, progression, director, player);
            var overlay = Panel("Protocol Selection", root, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.018f, 0.031f, 0.049f, 0.96f));
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.raycastTarget = true;
            var group = AddOverlayGroup(overlay.gameObject);
            var header = CenterLabel("Protocol Selection Title", overlay.transform, new Vector2(0f, 303f), new Vector2(1200f, 80f), "SELECT A PROTOCOL", 64f, s_paper);
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 4f;
            CenterLabel("Protocol Selection Subtitle", overlay.transform, new Vector2(0f, 244f), new Vector2(1000f, 44f), "Choose one upgrade for the remainder of this run", 29f, s_muted);
            var center = Vector2.one * 0.5f;
            Panel("Header Divider", overlay.transform, center, new Vector2(-628f, 206f), new Vector2(1256f, 2f), new Color(0.16f, 0.35f, 0.42f));
            Panel("Header Accent", overlay.transform, center, new Vector2(-29f, 205f), new Vector2(58f, 4f), s_cyan);
            var cards = new ProtocolCard[3];
            for (var index = 0; index < cards.Length; index++)
            {
                cards[index] = BuildProtocolCard(overlay.transform, index);
            }

            CenterLabel("Protocol Selection Controls", overlay.transform, new Vector2(0f, -292f), new Vector2(850f, 30f), "CLICK A CARD   /   PRESS 1, 2 OR 3", 16f, s_muted).characterSpacing = 2.2f;
            var reward = root.gameObject.AddComponent<UpgradeRewardHud>();
            PlaygroundSceneBuilder.Wire(reward, "_route", route, "_director", director, "_overlay", group, "_cards", cards);
        }

        private ProtocolCard BuildProtocolCard(Transform root, int index)
        {
            var center = Vector2.one * 0.5f;
            var border = Panel($"Protocol Card {index + 1}", root, center, new Vector2((index - 1) * 428f, -35f), new Vector2(398f, 390f), s_muted);
            border.rectTransform.pivot = center;
            var group = border.gameObject.AddComponent<CanvasGroup>();
            var face = Panel("Plate", border.transform, Vector2.zero, new Vector2(2f, 2f), new Vector2(394f, 386f), new Color(0.072f, 0.114f, 0.155f));
            border.raycastTarget = true;
            var button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.48f, 1.48f, 1.48f);
            colors.pressedColor = new Color(1.8f, 1.8f, 1.8f);
            colors.colorMultiplier = 1.4f;
            button.colors = colors;

            Panel("Icon Backplate", border.transform, Vector2.zero, new Vector2(258f, 230f), new Vector2(108f, 108f), new Color(0.042f, 0.072f, 0.108f));
            var icon = Panel("Protocol Pictogram", border.transform, Vector2.zero, new Vector2(273f, 245f), new Vector2(78f, 78f), s_paper);
            icon.preserveAspect = true;
            var category = Label("Protocol Category", border.transform, new Vector2(27f, 336f), new Vector2(214f, 25f), string.Empty, 19f, s_cyan);
            category.characterSpacing = 2f;
            var rarity = Label("Protocol Rarity", border.transform, new Vector2(27f, 306f), new Vector2(214f, 22f), string.Empty, 16f, s_muted);
            rarity.characterSpacing = 2f;
            var level = Label("Upgrade Level", border.transform, new Vector2(27f, 258f), new Vector2(214f, 22f), string.Empty, 16f, s_muted);
            var title = Label("Protocol Name", border.transform, new Vector2(27f, 172f), new Vector2(343f, 52f), string.Empty, 33f, s_paper);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f;
            title.fontSizeMax = 33f;
            var description = Label("Protocol Description", border.transform, new Vector2(27f, 111f), new Vector2(343f, 66f), string.Empty, 22f, s_muted);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.alignment = TextAlignmentOptions.TopLeft;
            var numbers = Label("Protocol Effect", border.transform, new Vector2(27f, 84f), new Vector2(343f, 30f), string.Empty, 20f, s_paper);
            numbers.fontStyle = FontStyles.Bold;
            var synergy = Label("Synergy", border.transform, new Vector2(27f, 56f), new Vector2(343f, 25f), string.Empty, 13f, s_muted);
            synergy.characterSpacing = 1f;
            Panel("Selection Divider", border.transform, Vector2.zero, new Vector2(27f, 48f), new Vector2(344f, 2f), new Color(0.22f, 0.37f, 0.45f));
            var key = Label("Protocol Selection Shortcut", border.transform, new Vector2(27f, 13f), new Vector2(344f, 30f), string.Empty, 17f, s_paper);
            key.characterSpacing = 2.5f;
            var card = border.gameObject.AddComponent<ProtocolCard>();
            PlaygroundSceneBuilder.Wire(card, "_button", button, "_group", group, "_frame", border, "_icon", icon, "_title", title, "_category", category, "_rarity", rarity, "_description", description, "_numbers", numbers, "_level", level, "_key", key, "_synergy", synergy);
            return card;
        }

        private void BuildRunInspection(Transform root, RunProgression progression, TemporalDirector director, PlaygroundPlayer player)
        {
            var strip = Rect("Acquired Run Protocols", root, Vector2.zero, new Vector2(44f, 348f), new Vector2(382f, 65f));
            var icons = new Image[(int)UpgradeId.Count];
            var levels = new TMP_Text[icons.Length];
            for (var index = 0; index < icons.Length; index++)
            {
                var position = new Vector2(index % 9 * 42f, index / 9 * 35f);
                icons[index] = Panel($"Run Protocol {index + 1}", strip, Vector2.zero, position, new Vector2(28f, 28f), s_paper);
                levels[index] = Label("Level", icons[index].transform, new Vector2(27f, 0f), new Vector2(12f, 17f), string.Empty, 13f, s_paper);
            }

            var timers = new TMP_Text[6];
            for (var index = 0; index < timers.Length; index++)
            {
                timers[index] = Label($"Protocol Timer {index + 1}", strip, new Vector2(index % 3 * 110f, 75f + index / 3 * 20f), new Vector2(108f, 19f), string.Empty, 14f, s_cyan);
            }

            var protection = Rect("Temporal Protection", root, Vector2.zero, Vector2.zero, Vector2.zero);
            protection.anchorMax = Vector2.one;
            Panel("Left Protection", protection, new Vector2(0f, 0.5f), new Vector2(14f, -67f), new Vector2(3f, 134f), s_cyan);
            Panel("Right Protection", protection, new Vector2(1f, 0.5f), new Vector2(-17f, -67f), new Vector2(3f, 134f), s_cyan);
            var protectionGroup = AddOverlayGroup(protection.gameObject);

            var noticeText = CenterLabel("Protocol Acquired", root, new Vector2(0f, -290f), new Vector2(1000f, 40f), string.Empty, 21f, s_cyan);
            var notice = AddOverlayGroup(noticeText.gameObject);

            var top = new Vector2(0.5f, 1f);
            var panel = Panel("Current Protocol Build", root, top, new Vector2(0f, -177f), new Vector2(1240f, 732f), new Color(0.035f, 0.059f, 0.084f, 0.97f));
            panel.rectTransform.pivot = top;
            var group = AddOverlayGroup(panel.gameObject);
            Panel("Build Accent", panel.transform, Vector2.zero, new Vector2(0f, 728f), new Vector2(1240f, 4f), s_cyan);
            Label("Current Build Title", panel.transform, new Vector2(28f, 661f), new Vector2(530f, 48f), "RUN PROTOCOLS", 38f, s_paper).fontStyle = FontStyles.Bold;
            var weapon = Label("Effective ARC-01 Stats", panel.transform, new Vector2(28f, 625f), new Vector2(1184f, 31f), string.Empty, 20f, s_cyan);
            var chrono = Label("Effective Chrono Stats", panel.transform, new Vector2(28f, 591f), new Vector2(1184f, 31f), string.Empty, 20f, s_muted);
            Panel("Build Divider", panel.transform, Vector2.zero, new Vector2(28f, 579f), new Vector2(1184f, 2f), new Color(0.18f, 0.32f, 0.38f));
            var columns = new TMP_Text[2];
            for (var index = 0; index < columns.Length; index++)
            {
                var column = Label($"Acquired Protocols Column {index + 1}", panel.transform, new Vector2(28f + index * 595f, 16f), new Vector2(585f, 545f), string.Empty, 16f, s_muted);
                column.alignment = TextAlignmentOptions.TopLeft;
                columns[index] = column;
            }

            var hud = root.gameObject.AddComponent<RunBuildHud>();
            PlaygroundSceneBuilder.Wire(hud, "_progression", progression, "_director", director, "_player", player, "_icons", icons, "_levels", levels, "_timers", timers, "_protection", protectionGroup, "_notice", notice, "_noticeText", noticeText, "_inspection", group, "_weaponStats", weapon, "_chronoStats", chrono, "_columns", columns);
        }
    }
}
