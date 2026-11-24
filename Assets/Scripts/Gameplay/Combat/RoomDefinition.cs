using System;
using UnityEngine;

namespace Overtime
{
    internal enum RoomCategory
    {
        Entry,
        Operations,
        Logistics,
        Assessment,
        Containment
    }

    [Flags]
    internal enum RoomGeometry
    {
        None = 0,
        CloseQuarters = 1,
        Open = 2,
        Vertical = 4,
        CoverHeavy = 8,
        LongRange = 16,
        Shielded = 32,
        DroneHeavy = 64
    }

    internal enum RoomReward
    {
        None,
        MedicalSupplies,
        ChronoCache,
        AmmunitionCache,
        PriorityProtocol
    }

    [Serializable]
    internal sealed class EncounterVariant
    {
        [SerializeField] private string _title;
        [SerializeField] private int _difficulty;
        [SerializeField] private int[] _markers;

        internal string Title => _title;
        internal int Difficulty => _difficulty;
        internal int[] Markers => _markers;
    }

    internal sealed class RoomDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private RoomRuntime _prefab;
        [SerializeField] private Texture2D _thumbnail;
        [SerializeField] private RoomCategory _category;
        [SerializeField] private RoomGeometry _geometry;
        [SerializeField] private int _minDepth;
        [SerializeField] private int _maxDepth;
        [SerializeField] private int _difficulty;
        [SerializeField] private EncounterVariant[] _variants;
        [SerializeField] private RoomReward[] _rewards;

        internal string Id => _id;
        internal string Title => _title;
        internal string Description => _description;
        internal RoomRuntime Prefab => _prefab;
        internal Texture2D Thumbnail => _thumbnail;
        internal RoomCategory Category => _category;
        internal RoomGeometry Geometry => _geometry;
        internal int Difficulty => _difficulty;
        internal EncounterVariant[] Variants => _variants;
        internal RoomReward[] Rewards => _rewards;

        internal bool AllowsDepth(int depth)
        {
            return depth >= _minDepth && depth <= _maxDepth;
        }

        internal int HostileKinds(int variant)
        {
            var kinds = 0;
            foreach (var index in _variants[variant].Markers)
            {
                kinds |= 1 << (int)_prefab.Markers[index].Enemy.Kind;
            }

            return kinds;
        }

        internal float Threat(int variant)
        {
            var threat = 0f;
            foreach (var index in _variants[variant].Markers)
            {
                threat += _prefab.Markers[index].Enemy.Kind switch
                {
                    SecurityEnemyKind.Auditor => 1.5f,
                    SecurityEnemyKind.Enforcer => 2f,
                    _ => 1f
                };
            }

            return threat;
        }
    }
}
