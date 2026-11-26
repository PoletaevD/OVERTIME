using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Overtime
{
    internal sealed class DamageNumbers
    {
        private readonly Slot[] _slots = new Slot[28];
        private readonly RectTransform _root;
        private readonly Camera _camera;
        private readonly Material _ink;
        private int _next;

        internal DamageNumbers(RectTransform root, Camera camera, TMP_Text source)
        {
            _root = root;
            _camera = camera;
            _ink = new Material(source.fontSharedMaterial);
            _ink.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.07f, 0.09f));
            _ink.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            _ink.EnableKeyword("OUTLINE_ON");
            for (var index = 0; index < _slots.Length; index++)
            {
                var label = new GameObject("Impact Readout", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(root, false);
                label.font = source.font;
                label.fontSharedMaterial = _ink;
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold | FontStyles.Italic;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(200f, 60f);
                label.gameObject.SetActive(false);
                _slots[index] = new Slot(label);
            }
        }

        internal void Block(Vector3 point)
        {
            foreach (var slot in _slots)
            {
                if (slot.Open && slot.Target == null && Time.unscaledTime - slot.Start < 0.075f && (point - slot.Point).sqrMagnitude < 0.8f)
                {
                    return;
                }
            }

            var entry = Begin(null, point);
            entry.Label.fontSize = 23f;
            entry.Label.color = new Color(0.65f, 0.81f, 0.96f);
            entry.Label.SetText("BLOCKED");
        }

        internal void Show(ICombatDamageable target, Vector3 point, DamageResult damage, bool weak, Color color)
        {
            Slot entry = null;
            foreach (var slot in _slots)
            {
                if (slot.Open && slot.Target == target && Time.unscaledTime - slot.Start <= 0.075f)
                {
                    entry = slot;
                    break;
                }
            }

            entry ??= Begin(target, point);
            entry.Amount += damage.Amount;
            entry.Weak |= weak;
            entry.Label.color = entry.Weak ? new Color(1f, 0.81f, 0.37f) : color;
            entry.Label.fontSize = entry.Weak ? 41f : 32f;
            entry.Label.SetText(damage.Killed ? "{0:0}!" : "{0:0}", entry.Amount);
        }

        private Slot Begin(ICombatDamageable target, Vector3 point)
        {
            var slot = _slots[_next];
            _next = (_next + 1) % _slots.Length;
            slot.Start = Time.unscaledTime;
            slot.Point = point;
            slot.Target = target;
            slot.Amount = 0;
            slot.Weak = false;
            slot.Open = true;
            slot.Label.gameObject.SetActive(true);
            slot.Animation.Restart();
            return slot;
        }

        internal void Project(bool reduced)
        {
            foreach (var slot in _slots)
            {
                if (!slot.Open)
                {
                    continue;
                }

                var screen = _camera.WorldToScreenPoint(slot.Point);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
                var age = Mathf.Clamp01((Time.unscaledTime - slot.Start) / 0.76f);
                var lift = reduced ? 9f : 46f;
                slot.Label.rectTransform.localPosition = local + new Vector2(17f + age * 12f, 16f + age * lift);
                slot.Label.rectTransform.localScale = Vector3.one * (reduced ? 1f : DOVirtual.EasedValue(1.34f, 0.93f, age, Ease.OutExpo));
                slot.Label.alpha = screen.z > 0f ? 1f - Mathf.InverseLerp(0.49f, 1f, age) : 0f;
            }
        }

        internal void Clear()
        {
            foreach (var slot in _slots)
            {
                slot.Animation.Pause();
                slot.Hide();
            }
        }

        internal void Dispose()
        {
            foreach (var slot in _slots)
            {
                slot.Animation.Kill();
            }

            Object.Destroy(_ink);
        }

        private sealed class Slot
        {
            internal readonly TextMeshProUGUI Label;
            internal readonly Tween Animation;
            internal ICombatDamageable Target;
            internal Vector3 Point;
            internal float Start;
            internal int Amount;
            internal bool Weak;
            internal bool Open;

            internal Slot(TextMeshProUGUI label)
            {
                Label = label;
                Animation = DOVirtual.DelayedCall(0.76f, Hide).SetUpdate(true).SetAutoKill(false).Pause();
            }

            internal void Hide()
            {
                Open = false;
                Target = null;
                Label.gameObject.SetActive(false);
            }
        }
    }
}
