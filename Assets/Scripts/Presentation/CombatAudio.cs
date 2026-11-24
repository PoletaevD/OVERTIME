using UnityEngine;

namespace Overtime
{
    internal sealed class CombatAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;

        [SerializeField] private TemporalDirector _director;
        [SerializeField] private PlaygroundPlayer _player;

        private AudioClip _shot;
        private AudioClip _impact;
        private AudioClip _shield;
        private AudioClip _charge;
        private AudioClip _feed;
        private AudioClip _swap;
        private AudioClip _ricochet;
        private AudioClip _pierce;
        private AudioSource _handling;
        private Vector3 _lastImpactPoint;
        private float _lastImpactTime = -1f;
        private bool _lastImpactShielded;
        private AudioSource _weapon;
        private AudioSource[] _voices;
        private GameObject _voicePool;
        private int _nextVoice;

        private void Awake()
        {
            _weapon = gameObject.AddComponent<AudioSource>();
            _handling = gameObject.AddComponent<AudioSource>();
            _voices = new AudioSource[6];
            _voicePool = new GameObject("Combat Audio Pool");
            _voicePool.transform.SetParent(_director.transform, false);
            for (var index = 0; index < _voices.Length; index++)
            {
                var emitter = new GameObject("Combat Audio Voice");
                emitter.transform.SetParent(_voicePool.transform, false);
                var source = emitter.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.minDistance = 2f;
                source.maxDistance = 24f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                _voices[index] = source;
            }

            _shot = BuildClip("ARC-01 Discharge", 0.095f, 108f, 56f, 0.53f, 5f);
            _impact = BuildClip("Projectile Impact", 0.07f, 450f, 175f, 0.38f, 3f);
            _shield = BuildClip("Shield Deflection", 0.19f, 620f, 395f, 0.03f, 3.2f);
            _charge = BuildClip("Auditor Charge", 0.55f, 170f, 510f, 0.01f, 1.3f);
            _feed = BuildClip("Magnetic Feed", 0.10f, 840f, 1250f, 0.04f, 2.5f);
            _swap = BuildClip("Loadout Exchange", 0.19f, 210f, 480f, 0.61f, 3.1f);
            _ricochet = BuildClip("Bolt Ricochet", 0.14f, 1570f, 850f, 0.11f, 2.8f);
            _pierce = BuildClip("Piercing Impact", 0.12f, 319f, 121f, 0.6f, 3.6f);
        }

        private void OnEnable()
        {
            _player.Combat.Weapon.ShotFired += OnShot;
            _player.Combat.Weapon.AutoFed += OnFeed;
            _player.Combat.Weapon.SwitchStarted += OnSwitch;
            _director.ProjectileHit += OnImpact;
            _director.AuditorCharging += OnCharge;
        }

        private void OnDisable()
        {
            _player.Combat.Weapon.ShotFired -= OnShot;
            _player.Combat.Weapon.AutoFed -= OnFeed;
            _player.Combat.Weapon.SwitchStarted -= OnSwitch;
            _handling.Stop();
            _director.ProjectileHit -= OnImpact;
            _director.AuditorCharging -= OnCharge;
        }

        private void OnDestroy()
        {
            Destroy(_shot);
            Destroy(_impact);
            Destroy(_shield);
            Destroy(_charge);
            Destroy(_feed);
            Destroy(_swap);
            Destroy(_ricochet);
            Destroy(_pierce);
            Destroy(_voicePool);
        }

        private void OnShot()
        {
            var gun = _player.Combat.Weapon;
            var gain = gun.ActiveType == WeaponType.Scatter ? 0.59f : gun.ActiveType == WeaponType.Charged ? 0.52f : 0.31f;
            _weapon.PlayOneShot(gun.ActiveDefinition?.Shot ?? _shot, gain);
        }

        private void OnSwitch()
        {
            _handling.Stop();
            _weapon.PlayOneShot(_swap, 0.29f);
        }

        private void LateUpdate()
        {
            var gun = _player.Combat.Weapon;
            var definition = gun.ActiveDefinition;
            var recharge = gun.IsReloading;
            var progress = recharge ? gun.ReloadProgress : gun.ChargeProgress;
            var clip = recharge ? definition?.Reload : gun.IsCharging ? definition?.Charge : null;
            if (clip == null || _director.IsRewinding || _director.RewardSelection || !_director.HasLivingPlayer || progress >= 0.998f)
            {
                if (_handling.isPlaying)
                {
                    _handling.Stop();
                }

                return;
            }

            var duration = recharge ? gun.State(gun.ActiveSlot).ReloadDuration : gun.ChargeSeconds(gun.ActiveSlot, _director.Mode);
            _handling.pitch = clip.length / Mathf.Max(0.05f, duration);
            if (_handling.clip != clip || !_handling.isPlaying || Mathf.Abs(_handling.time - progress * clip.length) > 0.09f)
            {
                _handling.clip = clip;
                _handling.time = progress * clip.length;
                _handling.volume = recharge ? 0.54f : 0.38f;
                _handling.Play();
            }
        }

        private void OnFeed()
        {
            _weapon.PlayOneShot(_feed, 0.24f);
        }

        private void OnImpact(ProjectileImpact impact)
        {
            var now = Time.unscaledTime;
            if (impact.FromPlayer && !impact.Ricochet && !impact.Piercing && now - _lastImpactTime < 0.055f && impact.Shielded == _lastImpactShielded && (impact.Point - _lastImpactPoint).sqrMagnitude < 0.8f)
            {
                return;
            }

            if (impact.FromPlayer)
            {
                _lastImpactTime = now;
                _lastImpactPoint = impact.Point;
                _lastImpactShielded = impact.Shielded;
            }

            var clip = impact.Shielded ? _shield : impact.Ricochet ? _ricochet : impact.Piercing ? _pierce : _impact;
            PlayAt(clip, impact.Point, impact.Shielded ? 0.50f : 0.27f);
        }

        private void OnCharge(Vector3 position)
        {
            PlayAt(_charge, position, 0.42f);
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.Stop();
            voice.transform.position = position;
            voice.PlayOneShot(clip, volume);
        }

        private AudioClip BuildClip(string clipName, float duration, float startFrequency, float endFrequency, float noise, float decay)
        {
            var samples = new float[Mathf.RoundToInt(duration * SampleRate)];
            var phase = 0f;
            var random = new System.Random(71);
            for (var index = 0; index < samples.Length; index++)
            {
                var time = (float)index / (samples.Length - 1);
                phase += Mathf.Lerp(startFrequency, endFrequency, time) * (2f * Mathf.PI / SampleRate);
                var edge = Mathf.Min(1f, time * 38f) * Mathf.Pow(1f - time, decay);
                var grain = ((float)random.NextDouble() * 2f - 1f) * noise;
                samples[index] = (Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 2.77f) * 0.19f + grain) * edge;
            }

            var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
