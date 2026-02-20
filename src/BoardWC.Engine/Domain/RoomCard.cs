using System.Text.Json;

namespace BoardWC.Engine.Domain;

// ── Enums ─────────────────────────────────────────────────────────────────────

internal enum CardGainType { Food, Iron, ValueItem, Coin, MonarchialSeal, Lantern }
internal enum CardCostType { Coin, MonarchialSeal }

// ── Value objects ─────────────────────────────────────────────────────────────

internal sealed record CardGainItem(CardGainType Type, int Amount);
internal sealed record CardCostItem(CardCostType Type, int Amount);

// ── Card fields (discriminated union) ────────────────────────────────────────

internal abstract record CardField
{
    public CardFieldSnapshot ToSnapshot() => this switch
    {
        GainCardField g => new CardFieldSnapshot(
            IsGain: true,
            Gains: g.Gains.Select(i => new CardGainItemSnapshot(i.Type.ToString(), i.Amount))
                          .ToList().AsReadOnly(),
            ActionDescription: null,
            ActionCost: null),
        ActionCardField a => new CardFieldSnapshot(
            IsGain: false,
            Gains: null,
            ActionDescription: a.Description,
            ActionCost: a.Cost.Select(c => new CardCostItemSnapshot(c.Type.ToString(), c.Amount))
                              .ToList().AsReadOnly()),
        _ => throw new InvalidOperationException("Unknown card field type"),
    };
}

internal sealed record GainCardField(IReadOnlyList<CardGainItem> Gains) : CardField;

internal sealed record ActionCardField(
    string Description,
    IReadOnlyList<CardCostItem> Cost) : CardField;

// ── Room card ─────────────────────────────────────────────────────────────────

internal sealed class RoomCard
{
    public string Id { get; }
    public string Name { get; }

    /// <summary>
    /// Ordered fields — same count as the room's token list.
    /// Field[i] corresponds to Token[i] in the DicePlaceholder.
    /// </summary>
    public IReadOnlyList<CardField> Fields { get; }

    /// <summary>null for ground-floor cards; "DoubleTop" or "DoubleBottom" for mid-floor.</summary>
    public string? Layout { get; }

    internal RoomCard(string id, string name, IReadOnlyList<CardField> fields, string? layout = null)
    {
        Id     = id;
        Name   = name;
        Fields = fields;
        Layout = layout;
    }

    public RoomCardSnapshot ToSnapshot() => new(
        Id, Name,
        Fields.Select(f => f.ToSnapshot()).ToList().AsReadOnly(),
        Layout);
}

// ── Floor card deck ───────────────────────────────────────────────────────────

internal sealed class FloorCardDeck
{
    private readonly Queue<RoomCard> _drawPile;

    public int Remaining => _drawPile.Count;

    private FloorCardDeck(IEnumerable<RoomCard> cards) =>
        _drawPile = new Queue<RoomCard>(cards);

    public RoomCard? Deal() =>
        _drawPile.TryDequeue(out var card) ? card : null;

    public static FloorCardDeck LoadGroundFloor(Random rng) =>
        Load("BoardWC.Engine.Data.ground-floor-cards.json", isMidFloor: false, rng);

    public static FloorCardDeck LoadMidFloor(Random rng) =>
        Load("BoardWC.Engine.Data.mid-floor-cards.json", isMidFloor: true, rng);

    private static FloorCardDeck Load(string resourceName, bool isMidFloor, Random rng)
    {
        var assembly = typeof(FloorCardDeck).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        var cards = doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(el => isMidFloor ? ParseMidFloorCard(el) : ParseGroundFloorCard(el))
            .OrderBy(_ => rng.Next())
            .ToList();

        return new FloorCardDeck(cards);
    }

    // ── Ground floor: gain1(i=0), gain2(i=1), action(i=2) ────────────────────

    private static RoomCard ParseGroundFloorCard(JsonElement el)
    {
        var id   = el.GetProperty("id").GetString()!;
        var name = el.GetProperty("name").GetString()!;

        var fields = new CardField[]
        {
            ParseGainField(el.GetProperty("gain1")),
            ParseGainField(el.GetProperty("gain2")),
            ParseActionField(el.GetProperty("action")),
        };

        return new RoomCard(id, name, fields.AsReadOnly());
    }

    // ── Mid floor: field1(i=0), field2(i=1), plus layout ─────────────────────

    private static RoomCard ParseMidFloorCard(JsonElement el)
    {
        var id     = el.GetProperty("id").GetString()!;
        var name   = el.GetProperty("name").GetString()!;
        var layout = el.GetProperty("layout").GetString();

        var fields = new CardField[]
        {
            ParseAnyField(el.GetProperty("field1")),
            ParseAnyField(el.GetProperty("field2")),
        };

        return new RoomCard(id, name, fields.AsReadOnly(), layout);
    }

    // ── Field parsers ─────────────────────────────────────────────────────────

    /// <summary>Parses either a gain array or an action object.</summary>
    private static CardField ParseAnyField(JsonElement el) =>
        el.ValueKind == JsonValueKind.Array ? ParseGainField(el) : ParseActionField(el);

    private static GainCardField ParseGainField(JsonElement el)
    {
        var items = el.EnumerateArray()
            .Select(item => new CardGainItem(
                Enum.Parse<CardGainType>(item.GetProperty("type").GetString()!),
                item.GetProperty("amount").GetInt32()))
            .ToList()
            .AsReadOnly();
        return new GainCardField(items);
    }

    private static ActionCardField ParseActionField(JsonElement el)
    {
        var description = el.GetProperty("description").GetString()!;

        var cost = el.TryGetProperty("cost", out var costEl)
            ? costEl.EnumerateArray()
                    .Select(item => new CardCostItem(
                        Enum.Parse<CardCostType>(item.GetProperty("type").GetString()!),
                        item.GetProperty("amount").GetInt32()))
                    .ToList()
                    .AsReadOnly()
            : (IReadOnlyList<CardCostItem>)Array.Empty<CardCostItem>();

        return new ActionCardField(description, cost);
    }
}
