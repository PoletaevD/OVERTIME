using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Overtime
{
    internal sealed class DamageIndicators
    {
        private const int Capacity = 10;
        private readonly Indicator[] _arcs = new Indicator[Capacity];

        internal DamageIndicators(Transform parent, Sprite sprite)
        {
            var container = new GameObject("Directional Damage Arcs", typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(parent, false);
            container.anchorMin = container.anchorMax = Vector2.one * 0.5f;
            container.anchoredPosition = Vector2.zero;
            container.sizeDelta = Vector2.zero;
            container.SetSiblingIndex(Mathf.Min(3, parent.childCount - 1));
            for (var index = 0; index < _arcs.Length; index++)
            {
                var image = new GameObject($"Damage Arc {index + 1}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(container, false);
                image.rectTransform.sizeDelta = new Vector2(250f, 250f);
                image.sprite = sprite;
                image.raycastTarget = false;
                _arcs[index] = new Indicator(image);
            }
        }

        internal void Signal(GravityCamera camera, Vector3 source, int damage, bool reducedMotion)
        {
            var bearing = Angle(camera, source);
            var slot = _arcs[0];
            Indicator free = null;
            foreach (var arc in _arcs)
            {
                if (arc.Strength <= 0f)
                {
                    free ??= arc;
                }
                else if (Mathf.Abs(Mathf.DeltaAngle(Angle(camera, arc.Source), bearing)) < 17f)
                {
                    slot = arc;
                    free = null;
                    break;
                }

                if (arc.Strength < slot.Strength)
                {
                    slot = arc;
                }
            }

            (free ?? slot).Show(source, bearing, Mathf.Lerp(0.54f, 1f, Mathf.Clamp01(damage / 28f)), reducedMotion);
        }

        internal void Orient(GravityCamera camera)
        {
            foreach (var arc in _arcs)
            {
                if (arc.Strength > 0f)
                {
                    arc.Orient(Angle(camera, arc.Source));
                }
            }
        }

        internal void Clear()
        {
            foreach (var arc in _arcs)
            {
                arc.Clear();
            }
        }

        internal void Dispose()
        {
            foreach (var arc in _arcs)
            {
                arc.Dispose();
            }
        }

        private float Angle(GravityCamera camera, Vector3 source)
        {
            var ray = camera.AimRay;
            return Bearing(Quaternion.LookRotation(ray.direction, camera.AimUp), source - ray.origin);
        }

        internal float Bearing(Quaternion view, Vector3 direction)
        {
            var local = Quaternion.Inverse(view) * direction;
            return -Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }

        private sealed class Indicator
        {
            private const float FadeIn = 0.085f;
            private readonly Image _image;
            private readonly Sequence _fade;
            private float _initial;
            private float _peak;

            internal Vector3 Source { get; private set; }
            internal float Strength { get; private set; }

            internal Indicator(Image image)
            {
                _image = image;
                SetStrength(0f);
                _fade = DOTween.Sequence().SetAutoKill(false).SetUpdate(true)
                    .Append(DOVirtual.Float(0f, 1f, FadeIn, value => SetStrength(Mathf.Lerp(_initial, _peak, value))).SetEase(Ease.OutQuad))
                    .Append(DOVirtual.Float(1f, 0f, 0.91f, value => SetStrength(_peak * value)).SetEase(Ease.InQuad))
                    .Pause();
            }

            internal void Show(Vector3 source, float bearing, float strength, bool reducedMotion)
            {
                Source = source;
                Orient(bearing);
                _peak = strength;
                _initial = reducedMotion ? strength : Mathf.Max(Strength, strength * 0.5f);
                _fade.Restart();
                if (reducedMotion)
                {
                    _fade.Goto(FadeIn, true);
                }

                SetStrength(_initial);
            }

            internal void Orient(float bearing)
            {
                _image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, bearing);
            }

            internal void Clear()
            {
                if (Strength > 0f)
                {
                    _fade.Pause();
                    SetStrength(0f);
                }
            }

            internal void Dispose()
            {
                _fade.Kill();
            }

            private void SetStrength(float value)
            {
                Strength = value;
                _image.color = new Color(1f, 0.16f + value * 0.09f, 0.105f, value);
                _image.enabled = value > 0f;
            }
        }
    }
}
