namespace BoardWC.Engine.Domain;

internal sealed class Board
{
    private readonly Bridge[] _bridges;
    private readonly Tower[] _towers;

    // Castle die-placement rooms: Floor 0 = 3 rooms (value 3), Floor 1 = 2 rooms (value 4)
    private readonly DicePlaceholder[][] _castleRooms =
    [
        [new DicePlaceholder(3), new DicePlaceholder(3), new DicePlaceholder(3)],
        [new DicePlaceholder(4), new DicePlaceholder(4)],
    ];

    // Well: 1 slot, unlimited capacity, compare value always = 1
    private readonly DicePlaceholder _well = new(1, unlimitedCapacity: true);

    // Outside: 2 slots, compare value = 5
    private readonly DicePlaceholder[] _outsideSlots =
        [new DicePlaceholder(5), new DicePlaceholder(5)];

    public IReadOnlyList<Bridge> Bridges => _bridges;
    public IReadOnlyList<Tower>  Towers  => _towers;

    public IReadOnlyList<IReadOnlyList<DicePlaceholder>> CastleFloors =>
        _castleRooms.Select(row => (IReadOnlyList<DicePlaceholder>)row).ToArray();

    public DicePlaceholder                Well         => _well;
    public IReadOnlyList<DicePlaceholder> OutsideSlots => _outsideSlots;

    public int TotalDiceRemaining => _bridges.Sum(b => b.DiceCount);

    public Board()
    {
        _bridges =
        [
            new Bridge(BridgeColor.Red),
            new Bridge(BridgeColor.Black),
            new Bridge(BridgeColor.White),
        ];
        _towers =
        [
            new Tower(TowerZone.Left),
            new Tower(TowerZone.Center),
            new Tower(TowerZone.Right),
        ];
    }

    public Bridge          GetBridge(BridgeColor color)      => _bridges.First(b => b.Color == color);
    public Tower           GetTower(TowerZone zone)          => _towers.First(t => t.Zone == zone);
    public DicePlaceholder GetCastleRoom(int floor, int room) => _castleRooms[floor][room];
    public DicePlaceholder GetOutsideSlot(int slot)          => _outsideSlots[slot];

    /// <summary>Roll and arrange dice on all bridges for a new round.</summary>
    public void RollAllDice(int playerCount, Random rng)
    {
        foreach (var bridge in _bridges)
            bridge.RollAndArrange(playerCount, rng);
    }

    /// <summary>Return workers from tower levels to their owners.</summary>
    public void ReturnAllWorkers()
    {
        foreach (var t in _towers) t.ReturnWorkers();
    }

    /// <summary>Clear all placed dice from castle rooms, well, and outside slots at round end.</summary>
    public void ClearPlacementAreas()
    {
        foreach (var row in _castleRooms) foreach (var room in row) room.Clear();
        _well.Clear();
        foreach (var slot in _outsideSlots) slot.Clear();
    }

    public BoardSnapshot ToSnapshot() => new(
        _bridges.Select(b => b.ToSnapshot()).ToList().AsReadOnly(),
        _towers.Select(t => t.ToSnapshot()).ToList().AsReadOnly(),
        new CastleSnapshot(
            _castleRooms
                .Select(row => (IReadOnlyList<DicePlaceholderSnapshot>)
                    row.Select(r => r.ToSnapshot()).ToArray())
                .ToArray()),
        new WellSnapshot(_well.ToSnapshot()),
        new OutsideSnapshot(_outsideSlots.Select(s => s.ToSnapshot()).ToArray())
    );
}
