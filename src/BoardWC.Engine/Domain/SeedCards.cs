using System.Text.Json;

namespace BoardWC.Engine.Domain;

// ── Seed action types ─────────────────────────────────────────────────────────

internal enum SeedActionType { PlayCastle, PlayFarm, PlayTrainingGrounds }

// ── Domain types ──────────────────────────────────────────────────────────────

internal sealed class SeedActionCard
{
    public string         Id         { get; init; } = "";
    public SeedActionType ActionType { get; init; }

    public SeedActionCardSnapshot ToSnapshot() => new(Id, ActionType.ToString());
}

internal sealed record SeedResourceGain(CardGainType Type, int Amount);

internal sealed class SeedResourceCard
{
    public string                        Id    { get; init; } = "";
    public IReadOnlyList<SeedResourceGain> Gains { get; init; } = [];

    public SeedResourceCardSnapshot ToSnapshot() => new(
        Id,
        Gains.Select(g => new SeedResourceGainSnapshot(g.Type.ToString(), g.Amount))
             .ToList().AsReadOnly());
}

internal sealed record SeedCardPair(SeedActionCard Action, SeedResourceCard Resource)
{
    public SeedPairSnapshot ToSnapshot() => new(Action.ToSnapshot(), Resource.ToSnapshot());
}

// ── Deck loading ──────────────────────────────────────────────────────────────

internal static class SeedCardDecks
{
    /// <summary>
    /// Shuffle both decks independently, pair by position, and return the first
    /// <paramref name="count"/> pairs.  Always called with nPlayers + 1.
    /// </summary>
    public static List<SeedCardPair> DrawPairs(int count, Random rng)
    {
        var actions   = LoadActionCards().OrderBy(_ => rng.Next()).ToList();
        var resources = LoadResourceCards().OrderBy(_ => rng.Next()).ToList();
        return actions.Zip(resources, (a, r) => new SeedCardPair(a, r))
                      .Take(count)
                      .ToList();
    }

    private static IEnumerable<SeedActionCard> LoadActionCards()
    {
        var assembly = typeof(SeedCardDecks).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "BoardWC.Engine.Data.starting-action-cards.json")
            ?? throw new InvalidOperationException("Embedded resource 'starting-action-cards.json' not found.");
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(el => new SeedActionCard
            {
                Id         = el.GetProperty("id").GetString()!,
                ActionType = Enum.Parse<SeedActionType>(el.GetProperty("actionType").GetString()!),
            })
            .ToList();
    }

    private static IEnumerable<SeedResourceCard> LoadResourceCards()
    {
        var assembly = typeof(SeedCardDecks).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "BoardWC.Engine.Data.starting-resource-cards.json")
            ?? throw new InvalidOperationException("Embedded resource 'starting-resource-cards.json' not found.");
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(el => new SeedResourceCard
            {
                Id    = el.GetProperty("id").GetString()!,
                Gains = el.GetProperty("gains")
                          .EnumerateArray()
                          .Select(g => new SeedResourceGain(
                              Enum.Parse<CardGainType>(g.GetProperty("type").GetString()!),
                              g.GetProperty("amount").GetInt32()))
                          .ToList()
                          .AsReadOnly(),
            })
            .ToList();
    }
}
