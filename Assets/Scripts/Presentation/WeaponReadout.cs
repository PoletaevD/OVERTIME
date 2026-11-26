using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Overtime
{
    internal sealed class WeaponReadout
    {
        private readonly TextMeshPro _digits;
        private readonly Transform _anchor;
        private int _magazine = -1;
        private int _reserve = -1;
        private int _progress = -2;
        private float _pulse;

        internal WeaponReadout(Transform parent, int layer)
        {
            _anchor = new GameObject("AmmoDisplayAnchor").transform;
            _anchor.SetParent(parent, false);
            var face = new GameObject("Ammunition", typeof(RectTransform));
            face.layer = layer;
            face.transform.SetParent(_anchor, false);
            _digits = face.AddComponent<TextMeshPro>();
            _digits.fontSize = 3.5f;
            _digits.alignment = TextAlignmentOptions.MidlineRight;
            _digits.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _digits.rectTransform.pivot = new Vector2(1f, 0.5f);
            _digits.rectTransform.sizeDelta = new Vector2(2.3f, 1f);
            _digits.renderer.shadowCastingMode = ShadowCastingMode.Off;
            _digits.renderer.receiveShadows = false;
        }

        internal void Present(CombatWeapon gun, Camera camera, bool reduced)
        {
            var type = gun.ActiveDefinition?.Id ?? WeaponId.Compliance;
            var scoped = type == WeaponId.Deadline && DeadlineScope.Blend(gun.AimBlend) >= 0.68f;
            _digits.enabled = !scoped;
            var offset = type switch
            {
                WeaponId.Directive => new Vector3(-0.066f, -0.012f, 0.085f),
                WeaponId.Deadline => new Vector3(-0.14f, -0.032f, 0.18f),
                WeaponId.Termination => new Vector3(-0.145f, -0.04f, 0.19f),
                _ => new Vector3(-0.11f, -0.035f, 0.145f)
            };

            var aim = gun.AimBlend;
            var outboard = gun.Instance(gun.ActiveSlot)?.Affixes.Count > 0 ? 0.062f : 0.025f;
            _anchor.localPosition = offset + new Vector3(-outboard - aim * 0.026f, 0.018f + aim * 0.061f, aim * 0.044f);
            var viewport = camera.WorldToViewportPoint(_anchor.position);
            viewport.x = Mathf.Clamp(viewport.x, 0.16f, 0.86f);
            viewport.y = Mathf.Clamp(viewport.y, 0.235f, 0.78f);
            _anchor.position = camera.ViewportToWorldPoint(viewport);
            _anchor.rotation = camera.transform.rotation;
            var progress = gun.IsReloading ? Mathf.FloorToInt(100f * gun.ReloadProgress) : gun.IsCharging ? Mathf.FloorToInt(100f * gun.ChargeProgress) : -1;
            if (_magazine != gun.Magazine || _reserve != gun.Reserve || _progress != progress)
            {
                if (_magazine != gun.Magazine || _reserve != gun.Reserve)
                {
                    _pulse = reduced ? 0f : 1f;
                }

                _magazine = gun.Magazine;
                _reserve = gun.Reserve;
                _progress = progress;
                if (progress >= 0)
                {
                    _digits.SetText("<mark=#13252CD8>{0:00} <size=60%>{1:000}</size></mark>\n<size=45%>{2:0}%</size>", _magazine, _reserve, progress);
                }
                else
                {
                    _digits.SetText("<mark=#13252CD8>{0:00} <size=60%>{1:000}</size></mark>", _magazine, _reserve);
                }
            }

            _pulse *= Mathf.Exp(-Time.unscaledDeltaTime * 16f);
            _digits.rectTransform.localScale = Vector3.one * (0.185f * Mathf.Clamp(viewport.z, 0.13f, 0.75f) * (1f + _pulse * 0.12f));
            var color = gun.Magazine <= Mathf.Max(1, gun.MagazineCapacity / 5) ? new Color(1f, 0.5f, 0.16f) : new Color(0.55f, 0.99f, 1f);
            _digits.color = Color.Lerp(color, Color.white, _pulse);
        }

        internal void SetVisible(bool visible)
        {
            _anchor.gameObject.SetActive(visible);
        }
    }
}
