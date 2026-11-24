using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class SecurityEnemyPresentation : MonoBehaviour
    {
        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color s_scan = new(1f, 0.44f, 0.1f, 0.8f);
        private static readonly Color s_locked = new(1f, 0.12f, 0.045f, 1f);
        private static readonly Color s_beam = new(1f, 0.83f, 0.52f, 1f);

        [SerializeField] private SecurityEnemyKind _kind;
        [SerializeField] private Transform _model;
        [SerializeField] private Transform _aimPivot;
        [SerializeField] private Transform _recoilPivot;
        [SerializeField] private Transform _wreck;
        [SerializeField] private Transform _rupture;
        [SerializeField] private Renderer _shell;
        [SerializeField] private Renderer _lens;
        [SerializeField] private LineRenderer _beam;
        [SerializeField, Min(0f)] private float _beamMuzzleOffset = 0.72f;

        private Tween _hitFlash;
        private MaterialPropertyBlock _shellProperties;
        private MaterialPropertyBlock _lensProperties;
        private Color _shellColor;
        private Color _lensEmission;
        private Vector3 _recoilRest;
        private Vector3 _ruptureScale;
        private Vector3 _wreckRest;
        private float _flashPeak;
        private bool _unstable;
        private float _flash;

        private void Awake()
        {
            _shellProperties = new MaterialPropertyBlock();
            _lensProperties = new MaterialPropertyBlock();
            _shellColor = _shell.sharedMaterial.GetColor(s_baseColor);
            _lensEmission = _lens.sharedMaterial.GetColor(s_emission);
            _recoilRest = _recoilPivot.localPosition;
            _ruptureScale = _rupture.localScale;
            _wreckRest = _wreck.localPosition;
            _hitFlash = DOVirtual.Float(1f, 0f, 0.18f, ApplyHitFlash).SetUpdate(true).SetEase(Ease.OutQuad).SetAutoKill(false).Pause();
        }

        private void OnDestroy()
        {
            _hitFlash?.Kill();
        }

        internal void SetDeployed(bool deployed)
        {
            Toggle(_model, deployed);
            if (!deployed)
            {
                Toggle(_wreck, false);
                Toggle(_rupture, false);
                if (_beam != null)
                {
                    _beam.enabled = false;
                }
            }
        }

        internal void ReactToHit(bool reducedMotion)
        {
            _flashPeak = reducedMotion ? 0.42f : 0.86f;
            ApplyHitFlash(1f);
            _hitFlash.Restart();
        }

        internal void ClearHit()
        {
            _hitFlash.Pause();
            ApplyHitFlash(0f);
        }

        internal void Present(float time, Vector3 aim, Vector3 origin, float recoil, float deathTime, SecurityEnemyState state, bool aiming, bool firing, float charge, bool locked, float beamDistance, bool unstable)
        {
            var dead = state == SecurityEnemyState.Dead;
            var collapsing = dead && deathTime < 0.42f;
            var showInstability = !dead && unstable;
            if (_unstable != showInstability)
            {
                _unstable = showInstability;
                ApplyHitFlash(_flash);
            }
            Toggle(_model, !dead || collapsing);
            Toggle(_wreck, dead && !collapsing && deathTime < 1.3f);
            Toggle(_rupture, dead && deathTime < 0.30f);

            if (dead)
            {
                PresentCollapse(deathTime);
            }
            else
            {
                var hover = _kind == SecurityEnemyKind.Drone ? Mathf.Sin(time * 5f) * 0.075f : _kind == SecurityEnemyKind.Guard ? Mathf.Sin(time * 6f) * 0.022f : 0f;
                _model.localPosition = Vector3.up * hover;
                _model.localRotation = Quaternion.identity;
                _aimPivot.rotation = AimRotation(aim);
                _recoilPivot.localPosition = _recoilRest - Vector3.forward * (recoil * 0.085f);
            }

            var telegraph = state == SecurityEnemyState.Alert || state == SecurityEnemyState.Aiming;
            var beamColor = firing ? s_beam : Color.Lerp(s_scan, s_locked, charge);
            var emission = aiming || firing ? beamColor * (1.1f + charge * 3.5f + (firing ? 5f : 0f)) : telegraph ? Color.Lerp(_lensEmission, s_scan * 2.5f, 0.65f) : _lensEmission * (1f + recoil * 1.4f);
            _lensProperties.SetColor(s_emission, dead ? Color.black : emission);
            _lens.SetPropertyBlock(_lensProperties);

            if (_beam != null)
            {
                PresentBeam(aim, origin, aiming, firing, charge, locked, beamDistance, beamColor);
            }
        }

        private Quaternion AimRotation(Vector3 aim)
        {
            var right = transform.right;
            return Quaternion.LookRotation(aim, Vector3.Cross(aim, right));
        }

        private void PresentCollapse(float time)
        {
            var fall = Mathf.Clamp01(time / 0.42f);
            _model.localPosition = Vector3.down * (fall * fall * (_kind == SecurityEnemyKind.Drone ? 0.68f : 0.19f));
            _model.localRotation = Quaternion.Euler(fall * fall * 26f, 0f, fall * fall * 58f);
            var burst = Mathf.Clamp01(time / 0.30f);
            _rupture.localRotation = Quaternion.Euler(burst * 85f, 45f + burst * 80f, 40f);
            _rupture.localScale = _ruptureScale * ((1f - burst) * (0.35f + burst * 3.8f));
            var wreckAge = Mathf.Max(0f, time - 0.42f);
            var drop = _kind == SecurityEnemyKind.Drone ? Mathf.Min(wreckAge * wreckAge * 2f, Mathf.Max(0f, transform.position.y - 1.65f)) : 0f;
            _wreck.localPosition = _wreckRest - Vector3.up * drop;
            _wreck.localScale = Vector3.one * (1f - Mathf.InverseLerp(0.94f, 1.30f, time));
        }

        private void PresentBeam(Vector3 aim, Vector3 origin, bool aiming, bool firing, float charge, bool locked, float distance, Color color)
        {
            _beam.enabled = (aiming || firing) && distance > _beamMuzzleOffset;
            if (!_beam.enabled)
            {
                return;
            }

            var width = firing ? 0.23f : Mathf.Lerp(0.026f, locked ? 0.085f : 0.048f, charge);
            _beam.startWidth = width;
            _beam.endWidth = firing ? width * 0.8f : width * 0.65f;
            _beam.startColor = color;
            _beam.endColor = color;
            _beam.SetPosition(0, origin + aim * _beamMuzzleOffset);
            _beam.SetPosition(1, origin + aim * distance);
        }

        private void ApplyHitFlash(float value)
        {
            _flash = value;
            var color = _unstable ? Color.Lerp(_shellColor, new Color(0.63f, 0.32f, 0.98f), 0.55f) : _shellColor;
            _shellProperties.SetColor(s_baseColor, Color.Lerp(color, new Color(1f, 0.52f, 0.26f), value * _flashPeak));
            _shell.SetPropertyBlock(_shellProperties);
        }

        private void Toggle(Transform part, bool active)
        {
            if (part.gameObject.activeSelf != active)
            {
                part.gameObject.SetActive(active);
            }
        }
    }
}
