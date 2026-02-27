using System.Text.Json;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Domain;

// ── Gain item ─────────────────────────────────────────────────────────────────

internal sealed record FarmGainItem(string Type, int Amount);

// ── Card ──────────────────────────────────────────────────────────────────────

internal sealed class FarmCard(
    string id,
    int foodCost,
    IReadOnlyList<FarmGainItem> gainItems,
    string actionDescription)
{
    internal string                    Id                { get; } = id;
    internal int                       FoodCost          { get; } = foodCost;
    internal IReadOnlyList<FarmGainItem> GainItems       { get; } = gainItems;
    internal string                    ActionDescription { get; } = actionDescription;
}

// ── Field ─────────────────────────────────────────────────────────────────────

internal sealed class FarmField(FarmCard card)
{
    internal FarmCard Card { get; } = card;
    private readonly List<string> _farmerOwners = new();
    internal IReadOnlyList<string> FarmerOwners => _farmerOwners;

    internal bool HasFarmer(string playerName) => _farmerOwners.Contains(playerName);
    internal void AddFarmer(string playerName) => _farmerOwners.Add(playerName);

    internal FarmFieldSnapshot ToSnapshot(BridgeColor color, bool isInland) => new(
        color, isInland,
        Card.FoodCost,
        Card.GainItems.Select(g => new CardGainItemSnapshot(g.Type, g.Amount)).ToList().AsReadOnly(),
        Card.ActionDescription,
        _farmerOwners.ToList().AsReadOnly());
}

// ── FarmingLands board component ──────────────────────────────────────────────

internal sealed class FarmingLands
{
    // [BridgeColor index][0=inland, 1=outside]
    private readonly FarmField[,] _fields;
    private readonly IReadOnlyList<FarmCard> _inlandDeck;
    private readonly IReadOnlyList<FarmCard> _outsideDeck;

    private static readonly BridgeColor[] _colors =
        [BridgeColor.Red, BridgeColor.Black, BridgeColor.White];

    private FarmingLands(
        IReadOnlyList<FarmCard> inlandDeck,
        IReadOnlyList<FarmCard> outsideDeck)
    {
        _inlandDeck  = inlandDeck;
        _outsideDeck = outsideDeck;
        _fields      = new FarmField[3, 2];
    }

    internal FarmField GetField(BridgeColor color, bool isInland)
    {
        int ci = ColorIndex(color);
        return _fields[ci, isInland ? 0 : 1];
    }

    internal IEnumerable<(BridgeColor Color, bool IsInland, FarmField Field)> AllFields()
    {
        foreach (var color in _colors)
        {
            int ci = ColorIndex(color);
            yield return (color, true,  _fields[ci, 0]);
            yield return (color, false, _fields[ci, 1]);
        }
    }

    /// <summary>Load both card decks from embedded JSON. Called once at game start.</summary>
    internal static FarmingLands Load()
    {
        var assembly = typeof(FarmingLands).Assembly;
        var inland  = LoadDeck(assembly, "BoardWC.Engine.Data.inland-farm-cards.json");
        var outside = LoadDeck(assembly, "BoardWC.Engine.Data.outside-farm-cards.json");
        return new FarmingLands(inland, outside);
    }

    /// <summary>Draw 1 card per field from the appropriate deck. Called once at game start.</summary>
    internal void SetupForGame(Random rng)
    {
        var shuffledInland  = _inlandDeck.OrderBy(_ => rng.Next()).ToArray();
        var shuffledOutside = _outsideDeck.OrderBy(_ => rng.Next()).ToArray();

        for (int i = 0; i < _colors.Length; i++)
        {
            _fields[i, 0] = new FarmField(shuffledInland[i]);   // inland
            _fields[i, 1] = new FarmField(shuffledOutside[i]);  // outside
        }
    }

    internal FarmingLandsSnapshot ToSnapshot() => new(
        AllFields()
            .Select(f => f.Field.ToSnapshot(f.Color, f.IsInland))
            .ToList()
            .AsReadOnly());

    // ── JSON parsing ──────────────────────────────────────────────────────────

    private static IReadOnlyList<FarmCard> LoadDeck(System.Reflection.Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(ParseCard)
            .ToList()
            .AsReadOnly();
    }

    private static FarmCard ParseCard(JsonElement el)
    {
        var id          = el.GetProperty("id").GetString()!;
        var foodCost    = el.GetProperty("foodCost").GetInt32();
        var actionDesc  = el.GetProperty("actionDescription").GetString() ?? string.Empty;
        var gainItems   = el.GetProperty("gainItems")
            .EnumerateArray()
            .Select(g => new FarmGainItem(
                g.GetProperty("type").GetString()!,
                g.GetProperty("amount").GetInt32()))
            .ToList()
            .AsReadOnly();
        return new FarmCard(id, foodCost, gainItems, actionDesc);
    }

    private static int ColorIndex(BridgeColor color) => color switch
    {
        BridgeColor.Red   => 0,
        BridgeColor.Black => 1,
        BridgeColor.White => 2,
        _                 => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}
