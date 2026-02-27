namespace BoardWC.Engine.Domain;

internal sealed class Board
{
    private readonly Bridge[] _bridges;

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

    private FloorCardDeck?  _groundDeck;
    private FloorCardDeck?  _midDeck;
    private TrainingGrounds? _trainingGrounds;
    public int GroundFloorDeckRemaining => _groundDeck?.Remaining ?? 0;
    public int MidFloorDeckRemaining    => _midDeck?.Remaining   ?? 0;

    public IReadOnlyList<Bridge> Bridges => _bridges;

    public IReadOnlyList<IReadOnlyList<DicePlaceholder>> CastleFloors =>
        _castleRooms.Select(row => (IReadOnlyList<DicePlaceholder>)row).ToArray();

    public DicePlaceholder                Well         => _well;
    public IReadOnlyList<DicePlaceholder> OutsideSlots => _outsideSlots;

    public TrainingGrounds TrainingGrounds =>
        _trainingGrounds ?? throw new InvalidOperationException("Training grounds not yet set up.");

    public int TotalDiceRemaining => _bridges.Sum(b => b.DiceCount);

    public Board()
    {
        _bridges =
        [
            new Bridge(BridgeColor.Red),
            new Bridge(BridgeColor.Black),
            new Bridge(BridgeColor.White),
        ];
    }

    public Bridge          GetBridge(BridgeColor color)       => _bridges.First(b => b.Color == color);
    public DicePlaceholder GetCastleRoom(int floor, int room) => _castleRooms[floor][room];
    public DicePlaceholder GetOutsideSlot(int slot)           => _outsideSlots[slot];

    /// <summary>Roll and arrange dice on all bridges for a new round.</summary>
    public void RollAllDice(int playerCount, Random rng)
    {
        foreach (var bridge in _bridges)
            bridge.RollAndArrange(playerCount, rng);
    }

    /// <summary>
    /// Place all 15 tokens on the board at game start using a constrained random algorithm.
    /// Ground rooms get 3 tokens each (≥2 different die colors per room).
    /// Mid rooms get 2 tokens each (different die colors per room).
    /// Remaining 2 tokens go to the well, resource side up.
    /// </summary>
    public void PlaceTokens(Random rng)
    {
        var bag = CreateAllTokens().OrderBy(_ => rng.Next()).ToList();

        // Step 1: seed each ground floor room with 1 token, one die color per room
        var colorOrder = Enum.GetValues<BridgeColor>().OrderBy(_ => rng.Next()).ToArray();
        for (int r = 0; r < 3; r++)
        {
            var token = bag.First(t => t.DieColor == colorOrder[r]);
            bag.Remove(token);
            _castleRooms[0][r].AddToken(token);
        }

        // Step 2: mid floor rooms — 2 tokens each, must have different die colors
        for (int r = 0; r < _castleRooms[1].Length; r++)
        {
            var t1 = bag[rng.Next(bag.Count)]; bag.Remove(t1);
            var t2 = bag.First(t => t.DieColor != t1.DieColor); bag.Remove(t2);
            foreach (var t in new[] { t1, t2 }.OrderBy(_ => rng.Next()))
                _castleRooms[1][r].AddToken(t);
        }

        // Step 3: add 2 more tokens to each ground floor room
        //         constraint: final 3 tokens per room must contain ≥2 different die colors
        for (int r = 0; r < 3; r++)
        {
            var seedColor = _castleRooms[0][r].Tokens[0].DieColor;
            var t1 = bag[rng.Next(bag.Count)]; bag.Remove(t1);
            Token t2;
            // If t1 has same color as seed token and a different-color option exists, force t2 to differ
            if (t1.DieColor == seedColor && bag.Any(t => t.DieColor != seedColor))
                t2 = bag.First(t => t.DieColor != seedColor);
            else
                t2 = bag[rng.Next(bag.Count)];
            bag.Remove(t2);
            foreach (var t in new[] { t1, t2 }.OrderBy(_ => rng.Next()))
                _castleRooms[0][r].AddToken(t);
        }

        // Step 4: remaining 2 tokens → well, resource side up
        foreach (var t in bag)
            _well.AddToken(t with { IsResourceSideUp = true });
    }

    /// <summary>
    /// Load and shuffle both floor card decks, deal one card to each castle room.
    /// Called once at game start; cards remain for the entire game.
    /// </summary>
    public void PlaceCards(Random rng)
    {
        _groundDeck = FloorCardDeck.LoadGroundFloor(rng);
        _midDeck    = FloorCardDeck.LoadMidFloor(rng);

        for (int r = 0; r < _castleRooms[0].Length; r++)
            if (_groundDeck.Deal() is { } c) _castleRooms[0][r].SetCard(c);

        for (int r = 0; r < _castleRooms[1].Length; r++)
            if (_midDeck.Deal() is { } c) _castleRooms[1][r].SetCard(c);
    }

    /// <summary>
    /// Load training grounds token pool (first call) and draw 4 tokens for this round.
    /// Call at game start and at the start of each subsequent round.
    /// </summary>
    public void SetupTrainingGrounds(Random rng)
    {
        _trainingGrounds ??= TrainingGrounds.Load();
        _trainingGrounds.SetupForRound(rng);
    }

    private static IEnumerable<Token> CreateAllTokens() =>
        Enum.GetValues<BridgeColor>()
            .SelectMany(color => Enum.GetValues<TokenResource>()
                .Select(res => new Token(color, res)));

    /// <summary>Clear all placed dice from castle rooms, well, and outside slots at round end.</summary>
    public void ClearPlacementAreas()
    {
        foreach (var row in _castleRooms) foreach (var room in row) room.Clear();
        _well.Clear();
        foreach (var slot in _outsideSlots) slot.Clear();
    }

    public BoardSnapshot ToSnapshot() => new(
        _bridges.Select(b => b.ToSnapshot()).ToList().AsReadOnly(),
        new CastleSnapshot(
            _castleRooms
                .Select(row => (IReadOnlyList<DicePlaceholderSnapshot>)
                    row.Select(r => r.ToSnapshot()).ToArray())
                .ToArray()),
        new WellSnapshot(_well.ToSnapshot()),
        new OutsideSnapshot(_outsideSlots.Select(s => s.ToSnapshot()).ToArray()),
        GroundFloorDeckRemaining,
        MidFloorDeckRemaining,
        _trainingGrounds?.ToSnapshot() ?? new TrainingGroundsSnapshot([])
    );
}
