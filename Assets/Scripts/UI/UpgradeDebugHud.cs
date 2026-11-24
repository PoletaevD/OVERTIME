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
        private Vector2 _scroll;

        internal void Initialize(SectorRoute route, TemporalDirector director, PlaygroundPlayer player)
        {
            _route = route;
            _director = director;
            _player = player;
            _seed = route.Progression.Seed.ToString();
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
            var rect = new Rect(14f, _route.Generated ? 320f : Screen.height * 0.32f, 540f, 220f);
            GUI.Box(rect, "WEAPONS & RUN PROTOCOLS   /   QA");
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 25f, rect.width - 24f, rect.height - 26f));
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
            GUILayout.EndArea();
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

            for (var index = 0; index < RoutePlan.RoomCount; index++)
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
            GUILayout.Label($"{room.Title}   /   {variant.Title} ({variant.Difficulty})");

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
            GUILayout.Label($"Actors {_director.WorldActorCount}   Colliders {_director.DynamicColliderCount}   Bolts {_director.PlayerProjectiles.ActiveCount + _route.EnemyProjectiles.ActiveCount}");

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
