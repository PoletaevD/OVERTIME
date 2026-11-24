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
        private AudioSource _weapon;
        private AudioSource[] _voices;
        private GameObject _voicePool;
        private int _nextVoice;

        private void Awake()
        {
            _weapon = gameObject.AddComponent<AudioSource>();
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
        }

        private void OnEnable()
        {
            _player.Combat.Weapon.ShotFired += OnShot;
            _director.ProjectileHit += OnImpact;
            _director.AuditorCharging += OnCharge;
        }

        private void OnDisable()
        {
            _player.Combat.Weapon.ShotFired -= OnShot;
            _director.ProjectileHit -= OnImpact;
            _director.AuditorCharging -= OnCharge;
        }

        private void OnDestroy()
        {
            Destroy(_shot);
            Destroy(_impact);
            Destroy(_shield);
            Destroy(_charge);
            Destroy(_voicePool);
        }

        private void OnShot()
        {
            _weapon.PlayOneShot(_shot, 0.31f);
        }

        private void OnImpact(ProjectileImpact impact)
        {
            PlayAt(impact.Shielded ? _shield : _impact, impact.Point, impact.Shielded ? 0.50f : 0.27f);
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
