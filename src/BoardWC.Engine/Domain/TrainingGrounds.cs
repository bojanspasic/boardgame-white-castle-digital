using System.Text.Json;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Domain;

// ── Token data ────────────────────────────────────────────────────────────────

internal sealed record TgGainItem(string Type, int Amount);

internal sealed class TrainingGroundsToken(
    string id,
    IReadOnlyList<TgGainItem> resourceSide,
    string actionSide)
{
    internal string                    Id           { get; } = id;
    internal IReadOnlyList<TgGainItem> ResourceSide { get; } = resourceSide;
    internal string                    ActionSide   { get; } = actionSide;
}

// ── Area ──────────────────────────────────────────────────────────────────────

internal sealed class TrainingGroundsArea
{
    internal int IronCost { get; }
    internal IReadOnlyList<TgGainItem> ResourceGain { get; private set; } = [];
    internal string ActionDescription { get; private set; } = string.Empty;
    private readonly List<string> _soldierOwners = new();
    internal IReadOnlyList<string> SoldierOwners => _soldierOwners;

    internal TrainingGroundsArea(int ironCost) => IronCost = ironCost;

    internal void AssignResourceSide(IReadOnlyList<TgGainItem> gains) =>
        ResourceGain = gains;

    internal void AssignActionSide(string description) =>
        ActionDescription = description;

    internal void AddSoldier(string playerName) => _soldierOwners.Add(playerName);

    internal void Reset()
    {
        ResourceGain      = [];
        ActionDescription = string.Empty;
        _soldierOwners.Clear();
    }

    internal TgAreaSnapshot ToSnapshot(int areaIndex) => new(
        areaIndex, IronCost,
        ResourceGain.Select(g => new CardGainItemSnapshot(g.Type, g.Amount)).ToList().AsReadOnly(),
        ActionDescription,
        _soldierOwners.ToList().AsReadOnly());
}

// ── Training grounds board component ─────────────────────────────────────────

internal sealed class TrainingGrounds
{
    private readonly IReadOnlyList<TrainingGroundsToken> _pool;

    internal TrainingGroundsArea[] Areas { get; } =
    [
        new TrainingGroundsArea(1),
        new TrainingGroundsArea(3),
        new TrainingGroundsArea(5),
    ];

    private TrainingGrounds(IReadOnlyList<TrainingGroundsToken> pool) => _pool = pool;

    /// <summary>Load token pool from embedded JSON. Called once (first SetupForRound).</summary>
    internal static TrainingGrounds Load()
    {
        const string resourceName = "BoardWC.Engine.Data.training-grounds-tokens.json";
        var assembly = typeof(TrainingGrounds).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        var tokens = doc.RootElement
            .GetProperty("tokens")
            .EnumerateArray()
            .Select(ParseToken)
            .ToList()
            .AsReadOnly();

        return new TrainingGrounds(tokens);
    }

    /// <summary>Draw 4 tokens randomly and assign them to the 3 areas for the new round.</summary>
    internal void SetupForRound(Random rng)
    {
        foreach (var area in Areas) area.Reset();

        var drawn = _pool.OrderBy(_ => rng.Next()).Take(4).ToArray();

        // Area 0: resource side of drawn[0]
        Areas[0].AssignResourceSide(drawn[0].ResourceSide);

        // Area 1: action side of drawn[1]
        Areas[1].AssignActionSide(drawn[1].ActionSide);

        // Area 2: resource side of drawn[2] + action side of drawn[3]
        Areas[2].AssignResourceSide(drawn[2].ResourceSide);
        Areas[2].AssignActionSide(drawn[3].ActionSide);
    }

    internal TrainingGroundsSnapshot ToSnapshot() => new(
        Areas.Select((a, i) => a.ToSnapshot(i)).ToList().AsReadOnly());

    // ── JSON parsing ─────────────────────────────────────────────────────────

    private static TrainingGroundsToken ParseToken(JsonElement el)
    {
        var id = el.GetProperty("id").GetString()!;
        var actionSide = el.GetProperty("actionSide").GetString()!;
        var resourceSide = el.GetProperty("resourceSide")
            .EnumerateArray()
            .Select(g => new TgGainItem(
                g.GetProperty("type").GetString()!,
                g.GetProperty("amount").GetInt32()))
            .ToList()
            .AsReadOnly();
        return new TrainingGroundsToken(id, resourceSide, actionSide);
    }
}
