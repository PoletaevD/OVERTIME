using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overtime
{
    [Serializable]
    internal sealed class RouteCard
    {
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RawImage _thumbnail;
        [SerializeField] private Image _accent;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _geometry;
        [SerializeField] private TMP_Text _threat;
        [SerializeField] private TMP_Text _hostiles;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private RawImage _rewardIcon;

        private Tween _entry;
        private Tween _hover;
        private bool _hovered;

        internal Button Button => _button;

        internal void Present(RouteNode node, int index, bool reduced)
        {
            Stop();
            var room = node.Room;
            var threat = room.Threat(node.Variant);
            var variant = room.Variants[node.Variant];
            _thumbnail.texture = room.Thumbnail;
            _title.SetText(room.Title.ToUpperInvariant());
            _description.SetText(room.Description);
            _geometry.SetText(GeometryLabel(room.Geometry));
            _hostiles.SetText(HostileLabel(room, variant));
            _threat.SetText($"{(threat < 4.5f ? "MODERATE" : threat < 5.5f ? "ELEVATED" : "HIGH")}  /  TIER {variant.Difficulty}  /  {threat:0.#}");
            _reward.SetText(RewardText(node.Reward));
            _rewardIcon.enabled = node.Reward != RoomReward.None;
            var frame = (int)node.Reward - 1;
            _rewardIcon.uvRect = new Rect((frame & 1) * 0.5f, frame < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            _accent.color = threat < 5.5f ? new Color(0.25f, 0.85f, 0.88f) : new Color(1f, 0.48f, 0.22f);
            _threat.color = _accent.color;
            _group.transform.localScale = Vector3.one;
            _group.alpha = reduced ? 1f : 0f;
            if (!reduced)
            {
                _entry = DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, 0.22f).SetDelay(index * 0.045f).SetUpdate(true);
            }
        }

        internal void UpdateHover(Vector2 pointer, bool reduced)
        {
            var rect = (RectTransform)_group.transform;
            var hovered = RectTransformUtility.RectangleContainsScreenPoint(rect, pointer);
            if (_hovered == hovered)
            {
                return;
            }

            _hovered = hovered;
            _hover?.Kill();
            if (!reduced)
            {
                _hover = rect.DOScale(hovered ? 1.018f : 1f, 0.14f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        internal void Stop()
        {
            _entry?.Kill();
            _hover?.Kill();
            _hovered = false;
        }

        private string GeometryLabel(RoomGeometry tags)
        {
            var text = tags.ToString().Replace("CloseQuarters", "Close Quarters").Replace("CoverHeavy", "Cover Heavy").Replace("LongRange", "Long Range").Replace("DroneHeavy", "Drone Heavy");
            return text.Replace(", ", "  /  ").ToUpperInvariant();
        }

        private string HostileLabel(RoomDefinition room, EncounterVariant variant)
        {
            Span<bool> kinds = stackalloc bool[4];
            kinds.Clear();
            foreach (var index in variant.Markers)
            {
                kinds[(int)room.Prefab.Markers[index].Enemy.Kind] = true;
            }

            var names = new System.Collections.Generic.List<string>(4);
            var labels = new[] { "Security Unit", "Surveillance Drone", "Laser Auditor", "Compliance Enforcer" };
            for (var index = 0; index < labels.Length; index++)
            {
                if (kinds[index])
                {
                    names.Add(labels[index]);
                }
            }

            return string.Join("  /  ", names);
        }

        internal static string RewardText(RoomReward reward)
        {
            return reward switch
            {
                RoomReward.MedicalSupplies => "MEDICAL SUPPLIES   +25 HP",
                RoomReward.ChronoCache => "CHRONO CACHE   +35 ENERGY",
                RoomReward.AmmunitionCache => "AMMUNITION CACHE   +45 / +12 / +6",
                RoomReward.PriorityProtocol => "PRIORITY PROTOCOL   RARITY BOOST",
                _ => "STANDARD RESUPPLY"
            };
        }
    }

    internal sealed class RouteSelectionHud : MonoBehaviour
    {
        [SerializeField] private SectorRoute _route;
        [SerializeField] private TemporalDirector _director;
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private TMP_Text[] _mapLabels;
        [SerializeField] private RouteCard[] _cards;

        private Tween _fade;

        private void Awake()
        {
            for (var index = 0; index < _cards.Length; index++)
            {
                var choice = index;
                _cards[index].Button.onClick.AddListener(() => Choose(choice));
            }

            _overlay.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
            foreach (var card in _cards)
            {
                card.Stop();
            }
        }

        private void Update()
        {
            if (!_route.SelectingRoute)
            {
                if (_overlay.gameObject.activeSelf && _overlay.interactable)
                {
                    Hide();
                }

                return;
            }

            if (!_overlay.interactable || !_overlay.gameObject.activeSelf)
            {
                Show();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    Choose(0);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    Choose(1);
                }
            }

            if (_overlay.interactable && Mouse.current is { } mouse)
            {
                foreach (var card in _cards)
                {
                    card.UpdateHover(mouse.position.ReadValue(), _director.ReducedMotion);
                }
            }
        }

        private void Show()
        {
            var reduced = _director.ReducedMotion;
            _fade?.Kill();
            _overlay.gameObject.SetActive(true);
            _overlay.interactable = true;
            _overlay.blocksRaycasts = true;
            _overlay.alpha = reduced ? 1f : 0f;
            if (!reduced)
            {
                _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, 1f, 0.18f).SetUpdate(true);
            }

            for (var index = 0; index < _cards.Length; index++)
            {
                _cards[index].Present(_route.RouteOptions[index], index, reduced);
            }

            DrawMap();
        }

        private void DrawMap()
        {
            foreach (var label in _mapLabels)
            {
                label.gameObject.SetActive(false);
            }

            for (var depth = 0; depth < RoutePlan.RoomCount; depth++)
            {
                var nodes = _route.Plan.At(depth);
                foreach (var node in nodes)
                {
                    var label = _mapLabels[node.Id - 1];
                    var visited = _route.Visited[depth] == node;
                    var status = depth == _route.RoomIndex && visited ? "CURRENT" : visited ? "CLEARED" : depth == _route.RoomIndex + 1 ? "AVAILABLE" : depth < _route.RoomIndex ? "BYPASSED" : depth == 4 ? "FINAL" : "UPCOMING";
                    label.gameObject.SetActive(true);
                    label.SetText($"{status}\n{node.Room.Title.ToUpperInvariant()}");
                    label.color = status switch
                    {
                        "CURRENT" => new Color(1f, 0.60f, 0.32f),
                        "AVAILABLE" => new Color(0.30f, 0.93f, 0.94f),
                        "CLEARED" => new Color(0.68f, 0.86f, 0.88f),
                        _ => new Color(0.49f, 0.64f, 0.73f)
                    };
                }
            }
        }

        private void Choose(int choice)
        {
            if (!_overlay.interactable || !_route.ConfirmRoute(choice))
            {
                return;
            }

            Hide(choice);
        }

        private void Hide(int choice = -1)
        {
            _overlay.interactable = false;
            _overlay.blocksRaycasts = false;
            _fade?.Kill();
            if (_director.ReducedMotion)
            {
                _overlay.gameObject.SetActive(false);
            }
            else
            {
                if (choice >= 0)
                {
                    _cards[choice].Button.transform.DOPunchScale(Vector3.one * 0.035f, 0.14f, 1).SetUpdate(true).SetLink(_overlay.gameObject);
                }

                _fade = DOTween.To(() => _overlay.alpha, value => _overlay.alpha = value, 0f, 0.16f).SetUpdate(true).OnComplete(() => _overlay.gameObject.SetActive(false));
            }
        }
    }
}
