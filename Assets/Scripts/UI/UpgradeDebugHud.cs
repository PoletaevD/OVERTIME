#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overtime
{
    internal sealed class UpgradeDebugHud : MonoBehaviour
    {
        private SectorRoute _route;
        private TemporalDirector _director;
        private PlaygroundPlayer _player;

        private bool _visible;
        private int _index;
        private int _room;
        private int _variant;
        private string _seed;
        private bool _showActors;
        private bool _showMarkers;
        private bool _showBossZones;
        private string _bossHealth = "1800";
        private int _bossAttack;
        private Vector2 _scroll;
        private Vector2 _weaponScroll;
        private bool _tuneRecoil;
        private int _lootType;
        private int _lootRarity;
        private int _lootModifiers;
        private string _lootSeed;
        private string _rounds = "12";
        private bool _showLoot;
        private int _previewCue;
        private static readonly string[] s_cues = System.Enum.GetNames(typeof(FieldCue));
        private CombatAudio _previewAudio;
        private static readonly string[] s_weaponTypes = System.Enum.GetNames(typeof(WeaponId));
        private static readonly string[] s_rarities = System.Enum.GetNames(typeof(WeaponRarity));
        private static readonly string[] s_modifiers = System.Enum.GetNames(typeof(WeaponModifier));

        internal void Initialize(SectorRoute route, TemporalDirector director, PlaygroundPlayer player)
        {
            _previewAudio = player.GetComponentInChildren<CombatAudio>(true);
            _route = route;
            _director = director;
            _player = player;
            _seed = route.Progression.Seed.ToString();
            _lootSeed = _seed;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _route.Progression.Build == null)
            {
                return;
            }

            if (keyboard.f8Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }

            if (!_visible || _director.RewardSelection)
            {
                return;
            }

            if (_showBossZones)
            {
                _route.Current.Boss?.DebugDraw();
            }

            UpdateWeapons(keyboard);
            var definitions = _route.Progression.DebugDefinitions;
            if (keyboard.pageUpKey.wasPressedThisFrame)
            {
                _index = (_index + definitions.Length - 1) % definitions.Length;
            }
            else if (keyboard.pageDownKey.wasPressedThisFrame)
            {
                _index = (_index + 1) % definitions.Length;
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                _route.Progression.DebugGrant(definitions[_index]);
            }
            else if (keyboard.oKey.wasPressedThisFrame)
            {
                _route.DebugOffer();
            }
        }

        private void UpdateWeapons(Keyboard keyboard)
        {
            if (!_director.HasLivingPlayer || _route.Finished)
            {
                return;
            }

            var gun = _player.Combat.Weapon;
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                gun.DebugActivate(0);
            }
            else if (keyboard.f10Key.wasPressedThisFrame && _route.SecondaryOptions is { Length: > 0 })
            {
                gun.DebugEquip(_route.SecondaryOptions[0]);
            }
            else if (keyboard.f11Key.wasPressedThisFrame && _route.SecondaryOptions is { Length: > 1 })
            {
                gun.DebugEquip(_route.SecondaryOptions[1]);
            }
            else if (keyboard.homeKey.wasPressedThisFrame)
            {
                gun.DebugActivate(1 - gun.ActiveSlot);
            }
            else if (keyboard.endKey.wasPressedThisFrame)
            {
                gun.DebugRefill();
            }
            else
            {
                return;
            }

            _director.SealHistory();
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            if (_route.Generated)
            {
                DrawRoute();
            }

            if (_director.RewardSelection)
            {
                return;
            }

            var progression = _route.Progression;
            var definition = progression.DebugDefinitions[_index];
            var gun = _player.Combat.Weapon;
            var energy = _director.Energy;
            var buffs = progression.PlayerEffects.State;
            var y = _route.Generated ? 320f : Screen.height * 0.32f;
            var rect = new Rect(14f, y, Mathf.Min(540f, Screen.width - 28f), Mathf.Min(470f, Screen.height - y - 10f));
            GUI.Box(rect, "WEAPONS & RUN PROTOCOLS   /   QA");
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 25f, rect.width - 24f, rect.height - 26f));
            _weaponScroll = GUILayout.BeginScrollView(_weaponScroll);
            GUILayout.Label($"{definition.Title}   {progression.Build.Level(definition.Id)}/{definition.MaxLevel}    SEED {progression.Seed}");
            GUILayout.Label($"DMG {gun.EffectiveDamage}   RPM {gun.RoundsPerMinute(_director.Mode):0}   MAG {gun.MagazineCapacity}   RELOAD {gun.ReloadSeconds:0.00}s   BOLT {gun.ProjectileSpeed(_director.Mode):0.0}m/s");
            GUILayout.Label($"CHRONO {energy.Current:0.0}/{energy.Maximum:0}   Q {energy.Cost(TemporalMode.Reset):0.##}   E {energy.Cost(TemporalMode.Override):0.##}   C {energy.Cost(TemporalMode.Recall):0.##}   SHIFT {energy.Cost(TemporalMode.BulletTime):0.##}");
            GUILayout.Label($"KIN {buffs.Kinetic:0.0}   FEED {buffs.AutoFeed:0.0}   DRAFT {buffs.SecondDraft:0.0}   QA {buffs.QualityAssurance:0.0}   EXP {buffs.ExploitWindow:0.0}   SAFE {buffs.SafeReturn:0.0}");
            var pool = _director.PlayerProjectiles;
            var penetrations = 0;
            var ricochets = 0;
            for (var slot = 0; slot < pool.PoolCapacity; slot++)
            {
                if (pool.TryReadProjectile(slot, out var pierce, out var bounce, out _, out _))
                {
                    penetrations = Mathf.Max(penetrations, pierce);
                    ricochets = Mathf.Max(ricochets, bounce);
                }
            }

            GUILayout.Label($"SLOT {gun.ActiveSlot + 1}   {gun.ActiveName}   CHARGE {gun.ChargeProgress:P0}");
            GUILayout.Label($"POOL {pool.ActiveCount}/{pool.PoolCapacity}   MAX PENETRATIONS {penetrations}   MAX RICOCHETS {ricochets}");
            GUILayout.Label("F9 / F10 / F11  Equip   HOME  Slot   END  Restock");
            GUILayout.Label("PGUP/DOWN  Browse   ENTER  Grant   O  Offer   F5  Restart");
            if (_previewAudio != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< SFX"))
                {
                    _previewCue = (_previewCue + s_cues.Length - 1) % s_cues.Length;
                }

                if (GUILayout.Button(((FieldCue)_previewCue).ToString()))
                {
                    _previewAudio.PlayInterface((FieldCue)_previewCue);
                }

                if (GUILayout.Button("SFX >"))
                {
                    _previewCue = (_previewCue + 1) % s_cues.Length;
                }

                GUILayout.EndHorizontal();
            }

            DrawRecoil(gun);
            if (_route.Seamless)
            {
                DrawLoot(gun);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawLoot(CombatWeapon gun)
        {
            _showLoot = GUILayout.Toggle(_showLoot, "Stage 05A.2 loot, aim and knife");
            if (!_showLoot)
            {
                return;
            }

            var frame = gun.Capture();
            var knife = _player.Combat.Knife.State;
            var aim = _player.AimRay;
            GUILayout.Label($"Player frame {frame.PrimaryId} / {frame.SecondaryId}    BURST {frame.Active.BurstLeft}    ADS {frame.AimBlend:F2} / {frame.AimSeconds:F2}s");
            GUILayout.Label($"KNIFE {knife.Elapsed:F3}s   active {knife.Active}   resolved {knife.Resolved}");
            GUILayout.Label($"AIM {aim.origin:F2} → {aim.direction:F3}");
            var pool = _director.PlayerProjectiles;
            for (var slot = 0; slot < pool.PoolCapacity; slot++)
            {
                if (pool.TryReadFlight(slot, out var shot, out var speed, out var split))
                {
                    GUILayout.Label($"WORLD BOLT {slot}   {speed:F1} m/s   split {split}   {shot.direction:F3}");
                    Debug.DrawRay(shot.origin, shot.direction, Color.magenta);
                    break;
                }
            }

            Debug.DrawRay(aim.origin, aim.direction * 10f, Color.cyan);
            var definition = gun.ActiveDefinition;
            var fov = definition.Aim.FieldOfView(definition.Type);
            var adjusted = Slider("ADS field of view", fov, 38f, 72f);
            if (!Mathf.Approximately(adjusted, fov))
            {
                definition.Aim.DebugFov = adjusted;
            }

            _lootType = GUILayout.SelectionGrid(_lootType, s_weaponTypes, 4);
            _lootRarity = GUILayout.SelectionGrid(_lootRarity, s_rarities, 4);
            GUILayout.Label("Select modifiers, or leave all clear for a random roll");
            for (var index = 0; index < s_modifiers.Length; index++)
            {
                var bit = 1 << index;
                var enabled = GUILayout.Toggle((_lootModifiers & bit) != 0, s_modifiers[index]);
                _lootModifiers = enabled ? _lootModifiers | bit : _lootModifiers & ~bit;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Loot seed");
            _lootSeed = GUILayout.TextField(_lootSeed);
            if (int.TryParse(_lootSeed, out var seed))
            {
                if (GUILayout.Button("Spawn"))
                {
                    _route.DebugLootWeapon((WeaponId)_lootType, (WeaponRarity)_lootRarity, _lootModifiers, seed, false);
                }

                if (GUILayout.Button("Equip"))
                {
                    _route.DebugLootWeapon((WeaponId)_lootType, (WeaponRarity)_lootRarity, _lootModifiers, seed, true);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rounds");
            _rounds = GUILayout.TextField(_rounds);
            if (int.TryParse(_rounds, out var rounds))
            {
                if (GUILayout.Button("Spawn ammo"))
                {
                    _route.DebugLootAmmo((WeaponId)_lootType, Mathf.Clamp(rounds, 1, 999));
                }

                if (GUILayout.Button("Set reserve"))
                {
                    gun.DebugSetAmmo((WeaponId)_lootType, Mathf.Clamp(rounds, 0, 999));
                    _director.SealHistory();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawRecoil(CombatWeapon gun)
        {
            var camera = _player.CameraRig;
            GUILayout.Label($"YAW {camera.Yaw:0.00}   PITCH {camera.Pitch:0.00}   IMPULSE {camera.PendingRecoil}   PATTERN {gun.State(gun.ActiveSlot).RecoilIndex}");
            GUILayout.BeginHorizontal();
            if (_route.SecondaryOptions is { Length: > 2 } && GUILayout.Button("Equip ARC-00"))
            {
                foreach (var option in _route.SecondaryOptions)
                {
                    if (option.Id == WeaponId.Directive)
                    {
                        gun.DebugEquip(option);
                        _director.SealHistory();
                        break;
                    }
                }
            }

            var ray = camera.AimRay;
            var right = Vector3.Cross(camera.AimUp, ray.direction).normalized;
            DamageButton("Front", ray.origin + ray.direction * 7f);
            DamageButton("Right", ray.origin + right * 7f);
            DamageButton("Rear", ray.origin - ray.direction * 7f);
            DamageButton("Left", ray.origin - right * 7f);
            GUILayout.EndHorizontal();

            _tuneRecoil = GUILayout.Toggle(_tuneRecoil, "Tune recoil in Play Mode");
            if (!_tuneRecoil || gun.ActiveDefinition is not { } definition)
            {
                return;
            }

            var gameplay = definition.GameplayRecoil;
            gameplay.DebugVertical = Slider("Camera vertical (degrees)", gameplay.DebugVertical, 0f, 8f);
            gameplay.DebugHorizontal = Slider("Camera horizontal (degrees)", gameplay.DebugHorizontal, 0f, 2f);
            gameplay.DebugDuration = Slider("Impulse duration (s)", gameplay.DebugDuration, 0.008f, 0.14f);
            var visual = definition.VisualRecoil;
            visual.DebugKick = Slider("Viewmodel kick (m/s)", visual.DebugKick, 0f, 12f);
            visual.DebugPitch = Slider("Viewmodel pitch (degrees/s)", visual.DebugPitch, 0f, 850f);
            visual.DebugStiffness = Slider("Position stiffness", visual.DebugStiffness, 75f, 1400f);
            visual.DebugDamping = Slider("Position damping", visual.DebugDamping, 7f, 70f);
        }

        private void DamageButton(string label, Vector3 source)
        {
            if (GUILayout.Button(label))
            {
                _director.DamagePlayer(1, source);
            }
        }

        private float Slider(string label, float value, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}  {value:0.###}", GUILayout.Width(238f));
            var result = GUILayout.HorizontalSlider(value, minimum, maximum);
            GUILayout.EndHorizontal();
            return result;
        }

        private void DrawSupervisor(EncounterController encounter)
        {
            if (encounter.Boss is not { } boss)
            {
                if (GUILayout.Button("Load Supervisor Chamber"))
                {
                    _route.DebugTravelToSupervisor();
                }

                return;
            }

            var state = boss.State;
            var snapshot = boss.DebugSnapshot(_director.WorldTimeline);
            GUILayout.Label($"SUPERVISOR  {state.Health}/{boss.Health.Maximum}   {state.Phase}   WAVES {state.ReinforcementWaves}");
            GUILayout.Label($"{state.Attack.Attack}  /  {state.Attack.Stage}  {state.Attack.Remaining:0.00}s   PANEL {state.PanelOpening:0.00}");
            GUILayout.Label($"SNAPSHOT  {_director.WorldTimeline.Cursor}  HP {snapshot.Health}  {snapshot.Phase}  {snapshot.Attack.Attack}  {snapshot.Attack.Remaining:0.00}s");
            _showBossZones = GUILayout.Toggle(_showBossZones, "Draw boss cores and attack bounds");
            var wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && encounter.State == EncounterState.Active && !_route.Finished;
            GUILayout.BeginHorizontal();
            _bossHealth = GUILayout.TextField(_bossHealth, 6, GUILayout.Width(72f));
            if (GUILayout.Button("Set HP") && int.TryParse(_bossHealth, out var health))
            {
                _route.DebugSupervisor(health, null);
            }

            if (GUILayout.Button("Phase 1"))
            {
                _route.DebugSupervisor(boss.Health.Maximum, null);
            }

            if (GUILayout.Button("Phase 2"))
            {
                _route.DebugSupervisor(Mathf.FloorToInt(boss.Health.Maximum * 0.55f), null);
            }

            if (GUILayout.Button("Phase 3"))
            {
                _route.DebugSupervisor(Mathf.FloorToInt(boss.Health.Maximum * 0.24f), null);
            }

            GUILayout.EndHorizontal();
            _bossAttack = GUILayout.Toolbar(_bossAttack, new[] { "Burst", "Sweep", "Violation", "Reinforce" });
            if (GUILayout.Button("Force selected attack"))
            {
                _route.DebugSupervisor(0, (SupervisorAttack)_bossAttack);
            }

            GUI.enabled = wasEnabled;
        }

        private void DrawRoute()
        {
            var width = Mathf.Min(540f, Screen.width - 28f);
            GUI.Box(new Rect(14f, 14f, width, 298f), "SECURITY ROUTE   /   QA");
            GUILayout.BeginArea(new Rect(26f, 39f, width - 24f, 263f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            _seed = GUILayout.TextField(_seed, 12, GUILayout.Width(124f));
            if (GUILayout.Button("New run from seed") && int.TryParse(_seed, out var seed))
            {
                _route.Progression.DebugRestartWithSeed(seed);
            }

            GUILayout.Label("ESC releases cursor");
            GUILayout.EndHorizontal();

            for (var index = 0; index < _route.Plan.Count; index++)
            {
                var options = _route.Plan.At(index);
                var left = options[0];
                var right = options.Count > 1 ? $"  /  {options[1].Room.Title}" : string.Empty;
                GUILayout.Label($"{index + 1}  {left.Room.Title}{right}");
            }

            var library = _route.DebugLibrary;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous room"))
            {
                _room = (_room + library.Length - 1) % library.Length;
                _variant = 0;
            }

            if (GUILayout.Button("Next room"))
            {
                _room = (_room + 1) % library.Length;
                _variant = 0;
            }

            var room = library[_room];
            if (GUILayout.Button("Change variant"))
            {
                _variant = (_variant + 1) % room.Variants.Length;
            }

            GUILayout.EndHorizontal();
            var variant = room.Variants[_variant];
            GUILayout.Label($"{room.Title}   /   {variant.Title} ({variant.Difficulty})   PLAN {variant.Markers.Length} / START {variant.InitialActive}");
            var encounter = _route.Current;
            GUILayout.Label($"CURRENT   ACTIVE {encounter.ActiveEnemies}  /  PENDING {encounter.PendingReinforcements}  /  TOTAL ALIVE {encounter.LivingEnemies}");

            GUILayout.BeginHorizontal();
            var enabled = GUI.enabled;
            GUI.enabled = enabled && !_director.RewardSelection && !_route.Transferring && !_route.Finished;
            if (GUILayout.Button("Load"))
            {
                _route.DebugTravel(_room, _variant);
            }

            if (GUILayout.Button("Next in plan"))
            {
                _route.DebugNextRoom();
            }

            if (GUILayout.Button("Retry fight"))
            {
                _route.DebugRestartRoom();
            }

            if (GUILayout.Button("Clear fight"))
            {
                _route.DebugClearRoom();
            }

            GUI.enabled = enabled;
            GUILayout.EndHorizontal();

            var node = _route.CurrentNode;
            var paid = _route.RewardClaimed(node.Id) ? "PAID" : "PENDING";
            GUILayout.Label($"{paid}   {RouteCard.RewardText(node.Reward)}");
            GUILayout.Label($"History  P {_director.PlayerTimeline.Count} / W {_director.WorldTimeline.Count} / Max {_director.WorldTimeline.Capacity}");
            var enemyPool = _route.EnemyProjectiles;
            GUILayout.Label($"Actors {_director.WorldActorCount}   Colliders {_director.DynamicColliderCount}   Hostile bolts {enemyPool.ActiveCount}/{enemyPool.PoolCapacity}");

            if (_route.DebugHasSupervisor)
            {
                DrawSupervisor(encounter);
            }

            _showActors = GUILayout.Toggle(_showActors, "World actors and Reward IDs");
            if (_showActors)
            {
                var ids = new HashSet<int>();
                foreach (var actor in _director.WorldActors)
                {
                    if (actor is SecurityEnemy enemy)
                    {
                        var id = ((ICombatDamageable)enemy).RewardId;
                        GUILayout.Label($"{enemy.Kind}   {id}{(ids.Add(id) ? string.Empty : "  DUPLICATE")}");
                    }
                    else if (actor is SupervisorBoss boss)
                    {
                        var id = ((ICombatDamageable)boss).RewardId;
                        GUILayout.Label($"Supervisor   {id}{(ids.Add(id) ? string.Empty : "  DUPLICATE")}");
                    }
                    else if (actor is ArenaHazard hazard)
                    {
                        GUILayout.Label($"{hazard.Kind}   {hazard.Phase}   COOLDOWN {hazard.DamageCooldown:0.00}");
                    }
                    else if (actor is ArenaMechanism mechanism)
                    {
                        GUILayout.Label($"{mechanism.name}   PHASE {mechanism.Phase:0.00}");
                    }
                    else
                    {
                        GUILayout.Label(actor.name);
                    }
                }
            }

            _showMarkers = GUILayout.Toggle(_showMarkers, "Selected variant markers");
            if (_showMarkers)
            {
                foreach (var index in variant.Markers)
                {
                    var marker = room.Prefab.Markers[index];
                    GUILayout.Label($"{index:00}  {marker.Enemy.Kind}   {marker.Point.localPosition}   Patrol {marker.Patrol.Length}");
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
#endif
