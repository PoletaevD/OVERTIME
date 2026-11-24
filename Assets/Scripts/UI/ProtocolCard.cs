using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class ProtocolCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _category;
        [SerializeField] private TMP_Text _rarity;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _numbers;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _key;
        [SerializeField] private TMP_Text _synergy;

        private RectTransform _rect;
        private Vector2 _rest;
        private Sequence _entry;
        private Tween _hoverTween;
        private bool _hovered;
        private bool _selected;
        private bool _reducedMotion;

        internal Button Button => _button;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _rest = _rect.anchoredPosition;
        }

        private void OnDestroy()
        {
            _entry?.Kill();
            _hoverTween?.Kill();
        }

        internal void Present(RunUpgradeDefinition definition, RunUpgradeState build, int index, int count, bool reducedMotion)
        {
            gameObject.SetActive(true);
            _reducedMotion = reducedMotion;
            _hovered = false;
            _selected = false;
            _hoverTween?.Kill();
            _entry?.Kill();
            _rect.localScale = Vector3.one;
            _rect.localRotation = Quaternion.identity;
            _rest = new Vector2((index - (count - 1) * 0.5f) * 428f, -35f);
            _rect.anchoredPosition = _rest;
            var accent = CategoryColor(definition.Category);
            var rarity = definition.Rarity switch
            {
                UpgradeRarity.Rare => new Color(0.84f, 0.65f, 1f),
                UpgradeRarity.Uncommon => new Color(0.45f, 0.89f, 0.83f),
                _ => new Color(0.56f, 0.68f, 0.76f)
            };
            _frame.color = rarity;
            _icon.sprite = definition.Icon;
            _icon.color = accent;
            _title.SetText(definition.Title);
            _category.SetText(CategoryName(definition.Category));
            _category.color = accent;
            _rarity.SetText(definition.Rarity.ToString().ToUpperInvariant());
            _rarity.color = rarity;
            _description.SetText(definition.Description);
            _numbers.SetText(definition.Numbers);
            _numbers.color = accent;
            var level = build.Level(definition.Id);
            _level.SetText(definition.MaxLevel > 1 ? $"LEVEL {level + 1} / {definition.MaxLevel}" : string.Empty);
            _synergy.SetText(build.HasCategory(definition.Category) ? "SYNERGY  /  CATEGORY IN BUILD" : string.Empty);
            _key.SetText("{0:0}   SELECT", index + 1);
            _group.alpha = reducedMotion ? 1f : 0f;
            if (reducedMotion)
            {
                return;
            }

            _rect.anchoredPosition -= Vector2.up * 22f;
            _entry = DOTween.Sequence().SetUpdate(true).SetDelay(index * 0.047f)
                .Append(DOTween.To(() => _group.alpha, value => _group.alpha = value, 1f, 0.2f))
                .Join(DOTween.To(() => _rect.anchoredPosition, value => _rect.anchoredPosition = value, _rest, 0.22f).SetEase(Ease.OutCubic));
        }

        internal void MarkSelected()
        {
            _entry?.Kill();
            _rect.anchoredPosition = _rest;
            _group.alpha = 1f;
            _frame.color = Color.white;
            _hovered = false;
            _selected = true;
            _hoverTween?.Kill();
            if (!_reducedMotion)
            {
                _hoverTween = _rect.DOPunchScale(Vector3.one * 0.035f, 0.16f, 3, 0.35f).SetUpdate(true);
            }
        }

        internal void Tilt(Vector2 pointer, bool reducedMotion)
        {
            if (reducedMotion || !_hovered)
            {
                _rect.localRotation = Quaternion.Slerp(_rect.localRotation, Quaternion.identity, reducedMotion ? 1f : Time.unscaledDeltaTime * 18f);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, pointer, null, out var local);
            var pitch = Mathf.Clamp(local.y / 240f, -1f, 1f) * -3f;
            var yaw = Mathf.Clamp(local.x / 200f, -1f, 1f) * 3f;
            _rect.localRotation = Quaternion.Slerp(_rect.localRotation, Quaternion.Euler(pitch, yaw, 0f), Time.unscaledDeltaTime * 15f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Hover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Hover(false);
        }

        private void Hover(bool active)
        {
            if (_selected)
            {
                return;
            }

            _hoverTween?.Kill();
            if (!_reducedMotion)
            {
                _hoverTween = _rect.DOScale(active ? 1.022f : 1f, 0.13f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        internal static string CategoryName(UpgradeCategory category)
        {
            return category == UpgradeCategory.BulletTime ? "BULLET TIME" : category.ToString().ToUpperInvariant();
        }

        internal static Color CategoryColor(UpgradeCategory category)
        {
            return category switch
            {
                UpgradeCategory.Gravity => new Color(1f, 0.52f, 0.23f),
                UpgradeCategory.Reset => new Color(0.40f, 0.90f, 1f),
                UpgradeCategory.Override => new Color(0.79f, 0.63f, 1f),
                UpgradeCategory.Recall => new Color(1f, 0.72f, 0.40f),
                UpgradeCategory.BulletTime => new Color(0.36f, 0.93f, 0.81f),
                UpgradeCategory.Energy => new Color(0.47f, 0.73f, 1f),
                _ => new Color(0.90f, 0.94f, 0.96f)
            };
        }
    }
}
