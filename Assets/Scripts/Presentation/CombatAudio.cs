using UnityEngine;
using UnityEngine.Audio;

namespace Overtime
{
    internal sealed class CombatAudio : MonoBehaviour
    {
        private readonly AudioClip[,] _shots = new AudioClip[4, 3];
        private readonly AudioClip[,] _timeCues = new AudioClip[5, 2];
        private int _shotIndex;
        private readonly AudioClip[] _latches = new AudioClip[4];
        private readonly AudioClip[] _dry = new AudioClip[4];
        private AudioClip _sightIn;
        private AudioClip _sightOut;
        private bool _throughSight;
        private TemporalMode _mode;
        private AudioClip _weak;
        private AudioClip _kill;
        private AudioClip _wall;
        private AudioClip _landing;
        private AudioClip _jump;
        private AudioClip _magnet;

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
        private AudioClip _pickup;
        private AudioClip _knife;
        private AudioClip _knifeHit;
        private AudioClip _explosion;
        private AudioClip _supervisorCharge;
        private AudioClip _supervisorDischarge;
        private AudioClip _supervisorPhase;
        private AudioClip _supervisorDestruction;
        private AudioSource _handling;
        private AudioSource _interface;
        private AudioSource _atmosphere;
        private AudioSource _timeSwitch;
        private AudioSource _feet;
        private AudioSource[] _fieldVoices;
        private AudioMixerGroup _hostileWeapons;
        private AudioMixerGroup _hostileMotion;
        private AudioMixerGroup _environment;
        private AudioMixerGroup _temporal;
        private AudioClip[] _fieldClips;
        private readonly AudioClip[] _loops = new AudioClip[5];
        private float _stride;
        private float _lastFieldStep;
        private int _threatVoice;
        private int _foleyVoice;
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
            var mixer = Resources.Load<AudioMixer>("Meridian SFX");
            _hostileWeapons = mixer.FindMatchingGroups("Enemy Weapons")[0];
            _hostileMotion = mixer.FindMatchingGroups("Enemy Movement")[0];
            _environment = mixer.FindMatchingGroups("Environment")[0];
            _temporal = mixer.FindMatchingGroups("Temporal Effects")[0];
            _weapon.outputAudioMixerGroup = _handling.outputAudioMixerGroup = mixer.FindMatchingGroups("Player Weapons")[0];
            _interface = gameObject.AddComponent<AudioSource>();
            _interface.outputAudioMixerGroup = mixer.FindMatchingGroups("UI")[0];
            _feet = gameObject.AddComponent<AudioSource>();
            _feet.outputAudioMixerGroup = _environment;
            _atmosphere = gameObject.AddComponent<AudioSource>();
            _atmosphere.outputAudioMixerGroup = _temporal;
            _timeSwitch = gameObject.AddComponent<AudioSource>();
            _timeSwitch.outputAudioMixerGroup = _temporal;
            _atmosphere.loop = true;
            _atmosphere.volume = 0.075f;
            _voices = new AudioSource[6];
            _fieldVoices = new AudioSource[8];
            var impactGroup = mixer.FindMatchingGroups("Impacts")[0];
            _voicePool = new GameObject("Combat Audio Pool");
            _voicePool.transform.SetParent(_director.transform, false);
            for (var index = 0; index < _voices.Length + _fieldVoices.Length; index++)
            {
                var emitter = new GameObject("Combat Audio Voice");
                emitter.transform.SetParent(_voicePool.transform, false);
                var source = emitter.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.minDistance = 2f;
                source.maxDistance = 24f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                if (index < _voices.Length)
                {
                    source.outputAudioMixerGroup = impactGroup;
                    source.priority = 170;
                    _voices[index] = source;
                }
                else
                {
                    _fieldVoices[index - _voices.Length] = source;
                }
            }

            _fieldClips = new AudioClip[System.Enum.GetValues(typeof(FieldCue)).Length];
            for (var index = 0; index < _fieldClips.Length; index++)
            {
                _fieldClips[index] = Clip(((FieldCue)index).ToString());
            }

            foreach (WeaponId id in System.Enum.GetValues(typeof(WeaponId)))
            {
                _latches[(int)id] = Clip(id + "Latch");
                _dry[(int)id] = Clip(id + "Dry");
                for (var variant = 0; variant < 3; variant++)
                {
                    _shots[(int)id, variant] = Clip($"{id} {variant + 1}");
                }
            }

            for (var index = 1; index <= 4; index++)
            {
                var mode = (TemporalMode)index;
                _loops[index] = Clip(mode + "Loop");
                _timeCues[index, 0] = Clip(mode + "Enter");
                _timeCues[index, 1] = Clip(mode + "Exit");
            }

            _sightIn = Clip("SightIn");
            _sightOut = Clip("SightOut");
            _weak = Clip("Weak");
            _kill = Clip("Kill");
            _wall = Clip("Wall");
            _jump = Clip("Jump");
            _landing = Clip("Landing");
            _magnet = Clip("Magnet");
            _shot = Clip("Compliance 1");
            _impact = Clip("Metal");
            _shield = Clip("Shield");
            _charge = Clip("Charge");
            _feed = Clip("Feed");
            _swap = Clip("Exchange");
            _ricochet = Clip("Ricochet");
            _pierce = Clip("Pierce");
            _pickup = Clip("Pickup");
            _knife = Clip("Knife");
            _knifeHit = Clip("KnifeHit");
            _explosion = Clip("Blast");
            _supervisorCharge = Clip("BossCharge");
            _supervisorDischarge = Clip("BossFire");
            _supervisorPhase = Clip("BossPhase");
            _supervisorDestruction = Clip("Collapse");
        }

        private void OnEnable()
        {
            _player.Combat.Weapon.ShotFired += OnShot;
            _player.Combat.Weapon.EmptyTriggered += OnEmpty;
            _player.Combat.Weapon.CollectedWeapon += OnSwitch;
            _player.Combat.Weapon.AutoFed += OnFeed;
            _player.Combat.Weapon.SwitchStarted += OnSwitch;
            _player.Combat.Weapon.AmmoCollected += OnPickup;
            _player.Combat.Knife.Started += OnKnife;
            _director.FieldSound += PlayField;
            _player.Combat.Health.Damaged += OnWound;
            _director.ModeChanged += OnMode;
            _player.Motor.Jumped += OnJump;
            _player.Motor.Landed += OnLand;
            _player.Motor.GravityChanged += OnMagnet;
            _director.ProjectileHit += OnImpact;
            _director.AuditorCharging += OnCharge;
            _director.SupervisorSound += OnSupervisor;
        }

        private void OnDisable()
        {
            _player.Combat.Weapon.ShotFired -= OnShot;
            _player.Combat.Weapon.EmptyTriggered -= OnEmpty;
            _player.Combat.Weapon.CollectedWeapon -= OnSwitch;
            _player.Combat.Weapon.AutoFed -= OnFeed;
            _player.Combat.Weapon.SwitchStarted -= OnSwitch;
            _player.Combat.Weapon.AmmoCollected -= OnPickup;
            _player.Combat.Knife.Started -= OnKnife;
            _handling.Stop();
            _director.FieldSound -= PlayField;
            _player.Combat.Health.Damaged -= OnWound;
            _atmosphere.Stop();
            _director.ModeChanged -= OnMode;
            _player.Motor.Jumped -= OnJump;
            _player.Motor.Landed -= OnLand;
            _player.Motor.GravityChanged -= OnMagnet;
            _director.ProjectileHit -= OnImpact;
            _director.AuditorCharging -= OnCharge;
            _director.SupervisorSound -= OnSupervisor;
        }

        private void OnDestroy()
        {
            Destroy(_voicePool);
        }

        private void OnWound(DamageResult damage)
        {
            var health = _player.Combat.Health;
            PlayInterface(health.Alive && health.Current * 4 <= health.Maximum ? FieldCue.Critical : FieldCue.Wound);
        }

        internal void PlayInterface(FieldCue cue)
        {
            _interface.PlayOneShot(_fieldClips[(int)cue], 0.36f);
        }

        private void PlayField(FieldCue cue, Vector3 position)
        {
            if (cue >= FieldCue.GuardHit && cue <= FieldCue.EnforcerHit)
            {
                PlayAt(_fieldClips[(int)cue], position, 0.24f);
                return;
            }

            var movement = cue >= FieldCue.GuardMove && cue <= FieldCue.EnforcerMove;
            if (movement && Time.unscaledTime < _lastFieldStep + 0.075f)
            {
                return;
            }

            var voice = movement ? _fieldVoices[5 + _foleyVoice++ % 3] : _fieldVoices[_threatVoice++ % 5];
            if (movement)
            {
                _lastFieldStep = Time.unscaledTime;
            }

            voice.Stop();
            voice.transform.position = position;
            voice.outputAudioMixerGroup = movement ? _hostileMotion : cue <= FieldCue.EnforcerDown ? _hostileWeapons : _environment;
            voice.priority = movement ? 180 : 68;
            voice.pitch = _director.Mode == TemporalMode.BulletTime ? 0.72f : 1f;
            voice.clip = _fieldClips[(int)cue];
            voice.volume = movement ? 0.23f : 0.46f;
            voice.Play();
        }

        private void OnPickup(int amount)
        {
            _weapon.PlayOneShot(_pickup, 0.31f);
        }

        private void OnKnife()
        {
            _handling.Stop();
            _weapon.PlayOneShot(_knife, 0.43f);
        }

        private void OnShot()
        {
            var gun = _player.Combat.Weapon;
            var gain = gun.ActiveType == WeaponType.Scatter ? 0.59f : gun.ActiveType == WeaponType.Charged ? 0.52f : 0.31f;
            var id = _player.Combat.Weapon.ActiveDefinition?.Id ?? WeaponId.Compliance;
            _weapon.PlayOneShot(_shots[(int)id, _shotIndex++ % 3] ?? _shot, gain);
        }

        private void OnEmpty()
        {
            var id = _player.Combat.Weapon.ActiveDefinition?.Id ?? WeaponId.Compliance;
            _weapon.PlayOneShot(_dry[(int)id], 0.32f);
        }

        private void OnSwitch()
        {
            _handling.Stop();
            var id = _player.Combat.Weapon.ActiveDefinition?.Id ?? WeaponId.Compliance;
            _weapon.PlayOneShot(_latches[(int)id] ?? _swap, 0.29f);
        }

        private void LateUpdate()
        {
            var gun = _player.Combat.Weapon;
            var definition = gun.ActiveDefinition;
            if (_throughSight != gun.Aiming)
            {
                _throughSight = gun.Aiming;
                if (!_director.IsPlayerProtected && !_director.IsRewinding && !_director.RewardSelection && _director.HasLivingPlayer)
                {
                    _weapon.PlayOneShot(_throughSight ? _sightIn : _sightOut, 0.13f);
                }
            }

            if (!_director.IsPlayerProtected && _player.Motor.Grounded && _director.HasLivingPlayer)
            {
                _stride += _player.Motor.Speed * Time.unscaledDeltaTime;
                if (_stride >= 1.85f)
                {
                    _stride %= 1.85f;
                    _feet.PlayOneShot(_fieldClips[(int)FieldCue.Footstep], 0.22f);
                }
            }

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
            if (impact.Explosion)
            {
                PlayAt(_explosion, impact.Point, 0.39f);
                return;
            }

            var now = Time.unscaledTime;
            if (impact.FromPlayer && !impact.Damage.Killed && !impact.WeakPoint && !impact.Ricochet && !impact.Piercing && now - _lastImpactTime < 0.055f && impact.Shielded == _lastImpactShielded && (impact.Point - _lastImpactPoint).sqrMagnitude < 0.8f)
            {
                return;
            }

            if (impact.FromPlayer)
            {
                _lastImpactTime = now;
                _lastImpactPoint = impact.Point;
                _lastImpactShielded = impact.Shielded;
            }

            var clip = impact.Shielded ? _shield : impact.Ricochet ? _ricochet : impact.Piercing ? _pierce : impact.Melee ? _knifeHit : impact.Damage.Killed ? _kill : impact.WeakPoint ? _weak : impact.Damage.Amount > 0 ? _impact : _wall;
            PlayAt(clip, impact.Point, impact.Shielded ? 0.50f : 0.27f);
        }

        private void OnCharge(Vector3 position)
        {
            PlayWarning(_charge, position, 0.42f);
        }

        private void OnSupervisor(SupervisorCue cue, Vector3 position)
        {
            var clip = cue switch
            {
                SupervisorCue.Charge => _supervisorCharge,
                SupervisorCue.Discharge => _supervisorDischarge,
                SupervisorCue.Phase => _supervisorPhase,
                _ => _supervisorDestruction
            };
            PlayWarning(clip, position, cue == SupervisorCue.Discharge ? 0.48f : 0.72f);
        }

        private void PlayWarning(AudioClip clip, Vector3 position, float volume)
        {
            var voice = _fieldVoices[_threatVoice++ % 5];
            voice.Stop();
            voice.transform.position = position;
            voice.outputAudioMixerGroup = _hostileWeapons;
            voice.priority = 40;
            voice.pitch = _director.Mode == TemporalMode.BulletTime ? 0.72f : 1f;
            voice.clip = clip;
            voice.volume = volume;
            voice.Play();
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.Stop();
            voice.transform.position = position;
            voice.PlayOneShot(clip, volume);
        }

        private void OnMode(TemporalMode mode)
        {
            if (!_director.CanDamageWorld)
            {
                foreach (var voice in _fieldVoices)
                {
                    voice.Stop();
                }

                foreach (var voice in _voices)
                {
                    voice.Stop();
                }
            }

            var clip = mode == TemporalMode.Normal ? _timeCues[(int)_mode, 1] : _timeCues[(int)mode, 0];
            _mode = mode;
            _atmosphere.Stop();
            if (mode != TemporalMode.Normal)
            {
                _atmosphere.clip = _loops[(int)mode];
                _atmosphere.Play();
            }
            else if (_director.Energy?.Empty == true)
            {
                PlayInterface(FieldCue.Depleted);
            }
            if (clip != null)
            {
                _timeSwitch.PlayOneShot(clip, 0.42f);
            }
        }

        private void OnLand(float strength)
        {
            _feet.PlayOneShot(_landing, Mathf.Lerp(0.15f, 0.44f, strength));
        }

        private void OnJump()
        {
            _feet.PlayOneShot(_jump, 0.20f);
        }

        private void OnMagnet(Quaternion rotation)
        {
            _feet.PlayOneShot(_magnet, 0.31f);
        }

        private AudioClip Clip(string name)
        {
            return Resources.Load<AudioClip>("Meridian Audio/" + name);
        }
    }
}
