using System.Text.Json;

namespace BoardWC.Engine.Domain;

// ── Seed action types ─────────────────────────────────────────────────────────

internal enum SeedActionType { PlayCastle, PlayFarm, PlayTrainingGrounds }

// ── Decree cards ──────────────────────────────────────────────────────────────

/// <summary>
/// A decree card grants one repeating gain each time the lantern chain fires.
/// Decree cards are obtained through certain resource seed cards and are placed
/// directly into the player's lantern chain when the seed pair is chosen.
/// </summary>
internal sealed class DecreeCard
{
    public string       Id       { get; init; } = "";
    public CardGainType GainType { get; init; }
    public int          Amount   { get; init; }
}

// ── Domain types ──────────────────────────────────────────────────────────────

internal sealed class SeedActionCard
{
    public string         Id         { get; init; } = "";
    public SeedActionType ActionType { get; init; }
    public LanternChainGain Back     { get; init; } = null!;

    public SeedActionCardSnapshot ToSnapshot() => new(
        Id,
        ActionType.ToString(),
        new LanternChainGainSnapshot(Back.Type.ToString(), Back.Amount));
}

internal sealed record SeedResourceGain(CardGainType Type, int Amount);

internal sealed class SeedResourceCard
{
    public string                        Id      { get; init; } = "";
    public IReadOnlyList<SeedResourceGain> Gains { get; init; } = [];
    public LanternChainGain              Back    { get; init; } = null!;
    /// <summary>Optional decree card granted when this seed pair is chosen; null if none.</summary>
    public DecreeCard?                   Decree  { get; init; }

    public SeedResourceCardSnapshot ToSnapshot() => new(
        Id,
        Gains.Select(g => new SeedResourceGainSnapshot(g.Type.ToString(), g.Amount))
             .ToList().AsReadOnly(),
        new LanternChainGainSnapshot(Back.Type.ToString(), Back.Amount),
        Decree?.Id,
        Decree is null ? null : new LanternChainGainSnapshot(Decree.GainType.ToString(), Decree.Amount));
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
                Back       = new LanternChainGain(
                    Enum.Parse<CardGainType>(el.GetProperty("back").GetProperty("type").GetString()!),
                    el.GetProperty("back").GetProperty("amount").GetInt32()),
            })
            .ToList();
    }

    private static IEnumerable<SeedResourceCard> LoadResourceCards()
    {
        var decreeById = LoadDecreeCards().ToDictionary(d => d.Id);

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
                Back  = new LanternChainGain(
                    Enum.Parse<CardGainType>(el.GetProperty("back").GetProperty("type").GetString()!),
                    el.GetProperty("back").GetProperty("amount").GetInt32()),
                Decree = el.TryGetProperty("decree", out var decreeEl)
                    ? decreeById[decreeEl.GetString()!]
                    : null,
            })
            .ToList();
    }

    private static IEnumerable<DecreeCard> LoadDecreeCards()
    {
        var assembly = typeof(SeedCardDecks).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "BoardWC.Engine.Data.decree-cards.json")
            ?? throw new InvalidOperationException("Embedded resource 'decree-cards.json' not found.");
        using var doc = JsonDocument.Parse(stream);

        return doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(el => new DecreeCard
            {
                Id       = el.GetProperty("id").GetString()!,
                GainType = Enum.Parse<CardGainType>(el.GetProperty("gainType").GetString()!),
                Amount   = el.GetProperty("amount").GetInt32(),
            })
            .ToList();
    }
}
