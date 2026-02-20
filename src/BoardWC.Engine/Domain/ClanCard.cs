namespace BoardWC.Engine.Domain;

public sealed class ClanCard
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string Effect { get; }
    public int VictoryPoints { get; }

    public ClanCard(string name, string effect, int victoryPoints)
    {
        Name = name;
        Effect = effect;
        VictoryPoints = victoryPoints;
    }

    public ClanCardSnapshot ToSnapshot() => new(Id, Name, Effect, VictoryPoints);
}

public sealed class ClanCardDeck
{
    private readonly Queue<ClanCard> _drawPile;
    private readonly List<ClanCard> _visibleRow = new();
    private const int RowSize = 3;

    public bool IsEmpty => _drawPile.Count == 0 && _visibleRow.Count == 0;
    public IReadOnlyList<ClanCard> VisibleCards => _visibleRow.AsReadOnly();

    public ClanCardDeck()
    {
        var deck = BuildDeck().OrderBy(_ => Guid.NewGuid()).ToList();
        _drawPile = new Queue<ClanCard>(deck);
        RefillRow();
    }

    public ClanCard? TakeFirstAvailable()
    {
        if (_visibleRow.Count == 0) return null;
        var card = _visibleRow[0];
        _visibleRow.RemoveAt(0);
        RefillRow();
        return card;
    }

    public CardRowSnapshot ToSnapshot() =>
        new(_visibleRow.Select(c => c.ToSnapshot()).ToList().AsReadOnly());

    private void RefillRow()
    {
        while (_visibleRow.Count < RowSize && _drawPile.Count > 0)
            _visibleRow.Add(_drawPile.Dequeue());
    }

    private static IEnumerable<ClanCard> BuildDeck() =>
    [
        new("Samurai",      "Master of the sword",         3),
        new("Merchant",     "Trades resources for lanterns", 2),
        new("Scholar",      "Knowledge brings honor",       2),
        new("Farmer",       "Feeds the clan",               1),
        new("Blacksmith",   "Forges iron into glory",       2),
        new("Archer",       "Eyes of the hawk",             2),
        new("Ninja",        "Moves unseen",                 3),
        new("Herbalist",    "Nature's medicine",            1),
        new("Daimyo",       "Lord of the land",             4),
        new("Monk",         "Inner peace",                  2),
        new("Geisha",       "Art and elegance",             2),
        new("Carpenter",    "Builds towers",                1),
        new("Guard",        "Protects the castle",          2),
        new("Fisherman",    "Feeds the castle",             1),
        new("Poet",         "Words of wisdom",              2),
        new("Spy",          "Gathers secrets",              3),
        new("Chef",         "Master of rice",               2),
        new("Gardener",     "Tends the flowers",            2),
        new("Swordsmith",   "Finest blades",                3),
        new("Strategist",   "Commands the army",            4),
    ];
}
