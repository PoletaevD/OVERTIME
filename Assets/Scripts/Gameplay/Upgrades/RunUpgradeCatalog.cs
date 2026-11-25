using System;
using UnityEngine;

namespace Overtime
{
    internal enum UpgradeId
    {
        OverclockedReceiver = 0,
        ExtendedMagazine = 1,
        RapidService = 2,
        KineticCharge = 3,
        MagneticAcceleration = 4,
        AutoFeed = 5,
        Freefall = 6,
        SecondDraft = 7,
        QualityAssurance = 8,
        ExploitWindow = 9,
        ResidualMemory = 10,
        EfficientRecall = 11,
        SafeReturn = 12,
        BadTake = 13,
        AcceleratedProcessing = 14,
        Hypervelocity = 15,
        ExtendedBattery = 16,
        PerformanceBonus = 17,
        PenetrationProtocol = 18,
        RicochetMatrix = 19,
        TwinOutput = 20,
        TerminalBallistics = 21,
        CloseQuarters = 22,
        VerticalAdvantage = 23,
        TacticalExchange = 24,
        LastWord = 25,
        SurgicalStrike = 26,
        BloodBattery = 27,
        ExtendedEdge = 28,
        TimeSever = 29,
        FocusProtocol = 30,
        PrecisionLock = 31,
        SteadyProcessing = 32,
        Scavenger = 33,
        EmergencySupply = 34,
        QuickChamber = 35,
        BladeAndBullet = 36,
        MagneticExecution = 37,
        Count = 38
    }

    internal enum UpgradeCategory
    {
        Weapon,
        Gravity,
        Reset,
        Override,
        Recall,
        BulletTime,
        Energy,
        Melee,
        Aim,
        Scavenging
    }

    internal enum UpgradeRarity
    {
        Common,
        Uncommon,
        Rare
    }

    [Serializable]
    internal sealed class RunUpgradeDefinition
    {
        [SerializeField] private UpgradeId _id;
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private string _numbers;
        [SerializeField] private UpgradeCategory _category;
        [SerializeField] private UpgradeRarity _rarity;
        [SerializeField, Min(1)] private int _maxLevel = 1;
        [SerializeField, Min(0f)] private float _magnitude;
        [SerializeField, Min(0f)] private float _seconds;
        [SerializeField, Min(0f)] private float _radius;
        [SerializeField, Min(0f)] private float _limit;
        [SerializeField] private Sprite _icon;

        internal UpgradeId Id => _id;
        internal string Title => _title;
        internal string Description => _description;
        internal string Numbers => string.Format(_numbers, Magnitude, Seconds, Radius);
        internal UpgradeCategory Category => _category;
        internal UpgradeRarity Rarity => _rarity;
        internal int MaxLevel => Mathf.Max(1, _maxLevel);
        internal float Magnitude => Mathf.Max(0f, _magnitude);
        internal float Seconds => Mathf.Max(0f, _seconds);
        internal float Radius => Mathf.Max(0f, _radius);
        internal float Limit => Mathf.Max(0f, _limit);
        internal Sprite Icon => _icon;
    }

    internal sealed class RunUpgradeCatalog : ScriptableObject
    {
        [SerializeField] private RunUpgradeDefinition[] _definitions;
        [SerializeField] private Vector3 _rarityWeights = new(55f, 35f, 10f);
        [SerializeField, Min(1f)] private float _synergyWeight = 1.35f;
        [SerializeField, Min(1f)] private float _maxSynergyWeight = 1.5f;

        internal RunUpgradeDefinition[] Definitions => _definitions;
        internal float SynergyWeight => Mathf.Clamp(_synergyWeight, 1f, Mathf.Max(1f, _maxSynergyWeight));

        internal float RarityWeight(UpgradeRarity rarity)
        {
            return Mathf.Max(0f, _rarityWeights[(int)rarity]);
        }
    }
}
