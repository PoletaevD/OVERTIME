using DG.Tweening;
using UnityEngine;

namespace Overtime
{
    internal sealed class SupervisorPresentation : MonoBehaviour
    {
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer[] _coreLights;
        [SerializeField] private Renderer[] _gunLights;
        [SerializeField] private Renderer[] _statusLights;
        [SerializeField] private LineRenderer _beam;
        [SerializeField] private Transform _violation;
        [SerializeField] private LineRenderer[] _violationRings;
        [SerializeField] private Transform[] _breakaway;
        [SerializeField] private ParticleSystem _sparks;

        private MaterialPropertyBlock _properties;
        private Tween _impact;
        private Tween _destruction;
        private float _hitFlash;
        private bool _hitCore;
        private bool _destroying;
        private bool _sparking;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _impact = DOVirtual.Float(0.8f, 0f, 0.19f, value => _hitFlash = value).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
        }

        private void OnDestroy()
        {
            _impact?.Kill();
            _destruction?.Kill();
        }

        internal void Present(in SupervisorBoss.Snapshot state, SupervisorAttacks attacks, bool alive, bool reduced)
        {
            if (_destroying)
            {
                return;
            }

            var attack = state.Attack;
            var warning = attack.Stage == SupervisorAttackStage.Warning;
            var active = attack.Stage == SupervisorAttackStage.Active;
            var charge = warning && attack.Duration > 0f ? Mathf.Clamp01(1f - attack.Remaining / attack.Duration) : 0f;
            var cyan = new Color(0.15f, 0.92f, 1f);
            var amber = new Color(1f, 0.42f, 0.09f);
            var red = new Color(1f, 0.21f, 0.06f);
            var status = state.Phase == SupervisorPhase.EmergencyOverride ? red : state.Phase == SupervisorPhase.CorrectiveAction ? amber : cyan;

            for (var index = 0; index < _coreLights.Length; index++)
            {
                var exposed = index < _coreLights.Length - 1 || state.TopExposed;
                SetGlow(_coreLights[index], cyan, alive && exposed ? 1.15f + (_hitCore ? _hitFlash * 2.5f : 0f) : 0.12f);
            }

            var armed = alive && state.Started;
            var chargingWeapons = armed && (attack.Attack is SupervisorAttack.SuppressionBurst or SupervisorAttack.SecuritySweep);
            foreach (var gun in _gunLights)
            {
                SetGlow(gun, chargingWeapons && (active || warning) ? amber : status, chargingWeapons ? 0.35f + charge * 2f + (active ? 0.7f : 0f) : 0.2f);
            }

            foreach (var indicator in _statusLights)
            {
                SetGlow(indicator, _hitCore ? status : Color.Lerp(status, Color.white, _hitFlash), armed ? 0.65f + _hitFlash * 2f : 0.08f);
            }

            _beam.enabled = armed && attack.Attack == SupervisorAttack.SecuritySweep && (warning || active);
            if (_beam.enabled)
            {
                _beam.SetPosition(0, attacks.BeamOrigin);
                _beam.SetPosition(1, attacks.BeamEnd(attack));
                _beam.startColor = _beam.endColor = active ? red : Color.Lerp(new Color(1f, 0.65f, 0.12f, 0.42f), amber, charge);
                _beam.widthMultiplier = active ? 0.19f : 0.045f + charge * 0.06f;
            }

            var marking = armed && attack.Attack == SupervisorAttack.RecordedViolation && (warning || active);
            if (_violation.gameObject.activeSelf != marking)
            {
                _violation.gameObject.SetActive(marking);
            }

            if (marking)
            {
                _violation.position = attack.Point;
                _violation.localScale = Vector3.one * attacks.ViolationRadius;
                foreach (var ring in _violationRings)
                {
                    ring.startColor = ring.endColor = active ? red : Color.Lerp(new Color(0.99f, 0.58f, 0.18f, 0.45f), amber, charge);
                    ring.widthMultiplier = active ? 0.105f : 0.035f + charge * 0.03f;
                }
            }

            var sparking = armed && state.Phase == SupervisorPhase.EmergencyOverride && !reduced;
            if (sparking != _sparking)
            {
                _sparking = sparking;
                if (sparking)
                {
                    _sparks.Play();
                }
                else
                {
                    _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void SetGlow(Renderer target, Color color, float intensity)
        {
            _properties.SetColor(s_baseColor, color);
            _properties.SetColor(s_emission, color * intensity);
            target.SetPropertyBlock(_properties);
        }

        internal void Impact(bool core, bool reduced)
        {
            if (reduced)
            {
                return;
            }

            _hitCore = core;
            _hitFlash = 0.8f;
            _impact.Restart();
        }

        internal void ClearImpact()
        {
            _impact.Pause();
            _hitFlash = 0f;
        }

        internal void DestroyChassis(bool reduced)
        {
            _destroying = true;
            ClearImpact();
            _beam.enabled = false;
            _violation.gameObject.SetActive(false);
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var indicator in _statusLights)
            {
                SetGlow(indicator, new Color(0.27f, 0.18f, 0.14f), 0.03f);
            }

            foreach (var light in _gunLights)
            {
                SetGlow(light, Color.black, 0f);
            }

            foreach (var light in _coreLights)
            {
                SetGlow(light, Color.black, 0f);
            }

            if (reduced)
            {
                return;
            }

            var sequence = DOTween.Sequence();
            sequence.SetUpdate(true).SetLink(gameObject);
            var emission = _sparks.emission;
            emission.enabled = false;
            _sparks.Play();
            sequence.AppendCallback(() => _sparks.Emit(45));
            for (var index = 0; index < _breakaway.Length; index++)
            {
                var part = _breakaway[index];
                var side = index % 2 == 0 ? -1f : 1f;
                var landing = part.localPosition + new Vector3(side * (0.5f + index * 0.16f), -1.15f, -0.35f + index * 0.18f);
                sequence.Insert(0.07f + index * 0.07f, part.DOLocalMove(landing, 0.72f).SetEase(Ease.InQuad));
                sequence.Insert(0.07f + index * 0.07f, part.DOLocalRotate(new Vector3(23f, side * 22f, side * 51f), 0.72f, RotateMode.LocalAxisAdd));
            }

            sequence.InsertCallback(0.65f, () => _sparks.Emit(32));
            _destruction = sequence;
        }
    }
}
