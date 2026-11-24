namespace Overtime
{
    internal sealed class RunUpgradeState
    {
        private readonly int[] _levels = new int[(int)UpgradeId.Count];
        private readonly float[] _bonuses = new float[(int)UpgradeId.Count];
        private readonly RunUpgradeDefinition[] _definitions = new RunUpgradeDefinition[(int)UpgradeId.Count];
        private int _categories;

        internal int Revision { get; private set; }

        internal int Level(UpgradeId id)
        {
            return _levels[(int)id];
        }

        internal float Bonus(UpgradeId id)
        {
            return _bonuses[(int)id];
        }

        internal RunUpgradeDefinition Definition(UpgradeId id)
        {
            return _definitions[(int)id];
        }

        internal bool HasCategory(UpgradeCategory category)
        {
            return (_categories & (1 << (int)category)) != 0;
        }

        internal bool Acquire(RunUpgradeDefinition definition)
        {
            var index = (int)definition.Id;
            if (_levels[index] >= definition.MaxLevel)
            {
                return false;
            }

            _definitions[index] = definition;
            _bonuses[index] = definition.Magnitude * ++_levels[index];
            _categories |= 1 << (int)definition.Category;
            Revision++;
            return true;
        }
    }
}
