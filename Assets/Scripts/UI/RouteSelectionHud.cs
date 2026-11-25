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
        [SerializeField] private Image _accent;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private RawImage _rewardIcon;

        private Tween _entry;
        private Tween _hover;
        private bool _hovered;

        internal Button Button => _button;

        internal void Present(RoomReward reward, CombatWeapon weapon, int index, bool reduced)
        {
            Stop();
            _title.SetText(RewardTitle(reward));
            _reward.SetText(RewardEffect(reward, weapon));
            _rewardIcon.enabled = reward != RoomReward.None;
            var frame = (int)reward - 1;
            _rewardIcon.uvRect = new Rect((frame & 1) * 0.5f, frame < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            _accent.color = reward switch
            {
                RoomReward.MedicalSupplies => new Color(0.43f, 0.91f, 0.66f),
                RoomReward.ChronoCache => new Color(0.25f, 0.85f, 0.88f),
                RoomReward.AmmunitionCache => new Color(1f, 0.65f, 0.31f),
                RoomReward.PriorityProtocol => new Color(0.75f, 0.57f, 1f),
                _ => Color.white
            };
            var buttonColors = _button.colors;
            buttonColors.highlightedColor = _accent.color * 0.72f;
            buttonColors.selectedColor = buttonColors.highlightedColor;
            _button.colors = buttonColors;
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

        internal static string RewardText(RoomReward reward, CombatWeapon weapon = null)
        {
            return $"{RewardTitle(reward)}   {RewardEffect(reward, weapon)}";
        }

        private static string RewardTitle(RoomReward reward)
        {
            return reward switch
            {
                RoomReward.MedicalSupplies => "MEDICAL SUPPLIES",
                RoomReward.ChronoCache => "CHRONO CACHE",
                RoomReward.AmmunitionCache => "AMMUNITION CACHE",
                RoomReward.PriorityProtocol => "PRIORITY PROTOCOL",
                _ => "STANDARD RESUPPLY"
            };
        }

        private static string RewardEffect(RoomReward reward, CombatWeapon weapon)
        {
            return reward switch
            {
                RoomReward.MedicalSupplies => "+25 HP",
                RoomReward.ChronoCache => "+35 ENERGY",
                RoomReward.AmmunitionCache when weapon?.Instance(0) != null => $"+{EncounterLoot.CacheSize(weapon.ActiveDefinition.Id)} {EncounterLoot.AmmoName(weapon.ActiveDefinition.Id)}",
                RoomReward.AmmunitionCache when weapon?.HasSecondary == true => $"+{weapon.CacheRounds(0)} PRIMARY / +{weapon.CacheRounds(1)} SECONDARY",
                RoomReward.AmmunitionCache => "+45 PRIMARY RESERVE",
                RoomReward.PriorityProtocol => "Increased upgrade rarity",
                _ => "Standard room reward"
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
                _cards[index].Present(_route.RouteOptions[index].Reward, _route.Weapon, index, reduced);
            }

            DrawMap();
        }

        private void DrawMap()
        {
            foreach (var label in _mapLabels)
            {
                label.gameObject.SetActive(false);
            }

            for (var depth = 0; depth < _route.Plan.Count; depth++)
            {
                var nodes = _route.Plan.At(depth);
                foreach (var node in nodes)
                {
                    var label = _mapLabels[node.Id - 1];
                    var visited = _route.Visited[depth] == node;
                    var status = depth == _route.RoomIndex && visited ? "CURRENT" : visited ? "CLEARED" : depth == _route.RoomIndex + 1 ? "AVAILABLE" : depth < _route.RoomIndex ? "BYPASSED" : "UPCOMING";
                    label.gameObject.SetActive(true);
                    var name = visited ? node.Room.Title.ToUpperInvariant() : "UNKNOWN ROOM";
                    label.SetText($"{depth + 1:00}  /  {status}\n{name}");
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
