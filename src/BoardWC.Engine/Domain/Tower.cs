namespace BoardWC.Engine.Domain;

internal sealed class TowerLevel
{
    public int Level { get; }
    public TowerAction Action { get; }
    internal Guid? OccupiedBy { get; set; }

    public TowerLevel(int level, TowerAction action)
    {
        Level  = level;
        Action = action;
    }

    public bool IsOccupied => OccupiedBy.HasValue;

    public TowerLevelSnapshot ToSnapshot() =>
        new(Level, Action.ToSnapshot(), OccupiedBy);
}

internal sealed class Tower
{
    public TowerZone Zone { get; }
    private readonly TowerLevel[] _levels;
    public IReadOnlyList<TowerLevel> Levels => _levels;

    private const int LevelCount = 4;

    public Tower(TowerZone zone)
    {
        Zone   = zone;
        _levels = Enumerable.Range(0, LevelCount)
            .Select(i => new TowerLevel(i, TowerActionFactory.Create(zone, i)))
            .ToArray();
    }

    public TowerLevel GetLevel(int level) => _levels[level];

    public void ReturnWorkers() { foreach (var l in _levels) l.OccupiedBy = null; }

    public TowerSnapshot ToSnapshot() =>
        new(Zone, _levels.Select(l => l.ToSnapshot()).ToList().AsReadOnly());
}
