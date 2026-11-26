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

        private Breakaway[] _panels;
        private bool _separated;
        private Transform[] _legs;
        private Vector3[] _legRest;
        private LineRenderer _aperture;
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
            var panels = new System.Collections.Generic.List<Breakaway>();
            var legs = new System.Collections.Generic.List<Transform>();
            foreach (var part in _model.GetComponentsInChildren<Transform>())
            {
                if (part.name is "Sensor Brow" or "Weapon Shroud" or "Stabilizer" or "Thrust Strip" or "Cooling Cheek" or "Emitter Cheek Inlay" or "Load Bearing Shoulder" or "Containment Harness")
                {
                    if (part.GetComponentInChildren<Collider>(true) == null)
                    {
                        panels.Add(new Breakaway(part, _model));
                    }
                }

                if (part.name is "Shin Guard" or "Traction Boot" or "Actuator" or "Armored Greave")
                {
                    legs.Add(part);
                }
            }

            _panels = panels.ToArray();
            _legs = legs.ToArray();
            _legRest = new Vector3[_legs.Length];
            for (var index = 0; index < _legs.Length; index++)
            {
                _legRest[index] = _legs[index].localPosition;
            }

            _shellProperties = new MaterialPropertyBlock();
            _lensProperties = new MaterialPropertyBlock();
            _shellColor = _shell.sharedMaterial.GetColor(s_baseColor);
            _lensEmission = _lens.sharedMaterial.GetColor(s_emission);
            var iris = new GameObject("Charge Aperture");
            iris.transform.SetParent(_model, false);
            _aperture = iris.AddComponent<LineRenderer>();
            _aperture.useWorldSpace = false;
            _aperture.loop = true;
            _aperture.enabled = false;
            _aperture.sharedMaterial = _lens.sharedMaterial;
            _aperture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aperture.receiveShadows = false;
            _aperture.widthMultiplier = 0.13f;
            _aperture.positionCount = _kind == SecurityEnemyKind.Guard ? 4 : _kind == SecurityEnemyKind.Drone ? 3 : _kind == SecurityEnemyKind.Enforcer ? 6 : 18;
            for (var index = 0; index < _aperture.positionCount; index++)
            {
                var angle = Mathf.PI * 2f * index / _aperture.positionCount;
                _aperture.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
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

        internal void Present(Vector3 travel, float time, Vector3 aim, Vector3 origin, float recoil, float deathTime, SecurityEnemyState state, bool aiming, bool firing, float charge, bool locked, float beamDistance, bool unstable)
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

            var stride = Mathf.Clamp01(travel.magnitude) * Mathf.Sin(time * (_kind == SecurityEnemyKind.Enforcer ? 4.6f : 7.2f));
            for (var index = 0; index < _legs.Length; index++)
            {
                var step = stride * Mathf.Sign(_legRest[index].x);
                _legs[index].localPosition = _legRest[index] + new Vector3(0f, Mathf.Max(0f, step) * 0.085f, step * 0.16f);
            }

            if (dead || _separated)
            {
                var progress = dead ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(deathTime / 0.42f)) : 0f;
                foreach (var panel in _panels)
                {
                    panel.Set(progress);
                }

                _separated = dead;
            }

            if (dead)
            {
                PresentCollapse(deathTime);
            }
            else
            {
                var hover = _kind == SecurityEnemyKind.Drone ? Mathf.Sin(time * 5f) * 0.075f : _kind == SecurityEnemyKind.Guard ? Mathf.Sin(time * 6f) * 0.022f : 0f;
                _model.localPosition = Vector3.up * hover;
                _model.localRotation = _kind == SecurityEnemyKind.Drone ? Quaternion.Euler(Mathf.Clamp(travel.z * 3f, -12f, 12f), 0f, Mathf.Clamp(-travel.x * 4f, -16f, 16f)) : Quaternion.identity;
                _aimPivot.rotation = AimRotation(aim);
                _recoilPivot.localPosition = _recoilRest - Vector3.forward * (recoil * 0.085f);
            }

            var telegraph = state == SecurityEnemyState.Alert || state == SecurityEnemyState.Aiming;
            var beamColor = firing ? s_beam : Color.Lerp(s_scan, s_locked, charge);
            var emission = aiming || firing ? beamColor * (1.1f + charge * 3.5f + (firing ? 5f : 0f)) : telegraph ? Color.Lerp(_lensEmission, s_scan * 2.5f, 0.65f) : _lensEmission * (1f + recoil * 1.4f);
            _lensProperties.SetColor(s_emission, dead ? Color.black : emission);
            _lens.SetPropertyBlock(_lensProperties);
            _aperture.enabled = telegraph && !dead;
            if (_aperture.enabled)
            {
                _aperture.transform.SetPositionAndRotation(origin + aim * 0.09f, AimRotation(aim) * Quaternion.AngleAxis(45f * charge, Vector3.forward));
                var radius = _kind == SecurityEnemyKind.Enforcer ? 0.33f : _kind == SecurityEnemyKind.Auditor ? 0.24f : 0.18f;
                _aperture.transform.localScale = Vector3.one * Mathf.Lerp(radius, radius * 0.51f, charge);
                _aperture.widthMultiplier = Mathf.Lerp(0.015f, 0.029f, charge);
                _aperture.SetPropertyBlock(_lensProperties);
            }

            if (_beam != null)
            {
                PresentBeam(aim, origin, aiming, firing, charge, locked, beamDistance, beamColor);
            }
        }

        private readonly struct Breakaway
        {
            private readonly Transform _part;
            private readonly Vector3 _rest;
            private readonly Quaternion _orientation;
            private readonly Vector3 _throw;

            internal Breakaway(Transform part, Transform model)
            {
                _part = part;
                _rest = part.localPosition;
                _orientation = part.localRotation;
                var radial = (part.position - model.position).normalized;
                _throw = part.parent.InverseTransformVector(radial * 0.36f + model.up * 0.12f);
            }

            internal void Set(float amount)
            {
                _part.localPosition = _rest + _throw * amount;
                _part.localRotation = _orientation * Quaternion.Euler(42f * amount, 16f * amount, Mathf.Sign(_throw.x) * 57f * amount);
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
            _model.localRotation = _kind switch
            {
                SecurityEnemyKind.Drone => Quaternion.Euler(27f * fall, 146f * fall * fall, 74f * fall),
                SecurityEnemyKind.Auditor => Quaternion.Euler(6f * fall, -35f * fall, 13f * fall),
                SecurityEnemyKind.Enforcer => Quaternion.Euler(-44f * fall * fall, 0f, 12f * fall),
                _ => Quaternion.Euler(fall * fall * 26f, 0f, fall * fall * 58f)
            };
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
