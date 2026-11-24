#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        internal void Initialize(SectorRoute route, TemporalDirector director, PlaygroundPlayer player)
        {
            _route = route;
            _director = director;
            _player = player;
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

        private void OnGUI()
        {
            if (!_visible || _director.RewardSelection)
            {
                return;
            }

            var progression = _route.Progression;
            var definition = progression.DebugDefinitions[_index];
            var gun = _player.Combat.Weapon;
            var energy = _director.Energy;
            var buffs = progression.PlayerEffects.State;
            var rect = new Rect(14f, Screen.height * 0.35f, 440f, 161f);
            GUI.Box(rect, "RUN PROTOCOLS   /   QA");
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 25f, rect.width - 24f, rect.height - 26f));
            GUILayout.Label($"{definition.Title}   {progression.Build.Level(definition.Id)}/{definition.MaxLevel}    SEED {progression.Seed}");
            GUILayout.Label($"DMG {gun.EffectiveDamage}   RPM {gun.RoundsPerMinute(_director.Mode):0}   MAG {gun.MagazineCapacity}   RELOAD {gun.ReloadSeconds:0.00}s   BOLT {gun.ProjectileSpeed(_director.Mode):0.0}m/s");
            GUILayout.Label($"CHRONO {energy.Current:0.0}/{energy.Maximum:0}   Q {energy.Cost(TemporalMode.Reset):0.##}   E {energy.Cost(TemporalMode.Override):0.##}   C {energy.Cost(TemporalMode.Recall):0.##}   SHIFT {energy.Cost(TemporalMode.BulletTime):0.##}");
            GUILayout.Label($"KIN {buffs.Kinetic:0.0}   FEED {buffs.AutoFeed:0.0}   DRAFT {buffs.SecondDraft:0.0}   QA {buffs.QualityAssurance:0.0}   EXP {buffs.ExploitWindow:0.0}   SAFE {buffs.SafeReturn:0.0}");
            GUILayout.Label("PGUP/DOWN  Browse   ENTER  Grant   O  Offer   F5  Restart");
            GUILayout.EndArea();
        }
    }
}
#endif
