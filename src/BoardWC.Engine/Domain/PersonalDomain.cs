using System.Text.Json;

namespace BoardWC.Engine.Domain;

// ── Row configuration (loaded from JSON, shared across all players) ───────────

internal sealed class PersonalDomainRowConfig
{
    public BridgeColor DieColor        { get; init; }
    public int CompareValue            { get; init; }
    public string FigureType          { get; init; } = string.Empty;  // "Courtier" | "Farmer" | "Soldier"
    public ResourceType DefaultGainType  { get; init; }
    public int DefaultGainAmount         { get; init; }
    public (ResourceType Type, int Amount)[] SpotGains { get; init; } = [];  // always length 5

    private static IReadOnlyList<PersonalDomainRowConfig>? _cache;

    /// <summary>Load row configs from embedded JSON. Result is cached after the first call.</summary>
    public static IReadOnlyList<PersonalDomainRowConfig> Load()
    {
        if (_cache is not null) return _cache;

        const string resourceName = "BoardWC.Engine.Data.personal-domain-rows.json";
        var assembly = typeof(PersonalDomainRowConfig).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        var rows = doc.RootElement
            .GetProperty("rows")
            .EnumerateArray()
            .Select(ParseRow)
            .ToList()
            .AsReadOnly();

        return _cache = rows;
    }

    private static PersonalDomainRowConfig ParseRow(JsonElement el)
    {
        var dieColor    = Enum.Parse<BridgeColor>(el.GetProperty("dieColor").GetString()!);
        var compareValue = el.GetProperty("compareValue").GetInt32();
        var figureType  = el.GetProperty("figureType").GetString()!;
        var defaultGain = el.GetProperty("defaultGain");
        var defaultType = ParseResource(defaultGain.GetProperty("type").GetString()!);
        var defaultAmt  = defaultGain.GetProperty("amount").GetInt32();

        var spots = el.GetProperty("spots")
            .EnumerateArray()
            .Select(s => (
                Type:   ParseResource(s.GetProperty("type").GetString()!),
                Amount: s.GetProperty("amount").GetInt32()))
            .ToArray();

        return new PersonalDomainRowConfig
        {
            DieColor         = dieColor,
            CompareValue     = compareValue,
            FigureType       = figureType,
            DefaultGainType  = defaultType,
            DefaultGainAmount = defaultAmt,
            SpotGains        = spots,
        };
    }

    private static ResourceType ParseResource(string s) => s switch
    {
        "Food"      => ResourceType.Food,
        "Iron"      => ResourceType.Iron,
        "ValueItem" => ResourceType.ValueItem,
        _           => throw new InvalidOperationException($"Unknown resource type in personal-domain-rows.json: '{s}'")
    };
}

// ── Per-player runtime state for one row ─────────────────────────────────────

internal sealed class PersonalDomainRow
{
    public PersonalDomainRowConfig Config { get; }
    public Die? PlacedDie { get; set; }

    public PersonalDomainRow(PersonalDomainRowConfig config) => Config = config;

    /// <summary>Called at round end to allow placing a die again next round.</summary>
    public void ClearForRound() => PlacedDie = null;

    /// <param name="uncoveredCount">Number of spots (left-to-right) whose figure has been deployed.</param>
    public PersonalDomainRowSnapshot ToSnapshot(int uncoveredCount) => new(
        Config.DieColor,
        Config.CompareValue,
        Config.FigureType,
        Config.DefaultGainType,
        Config.DefaultGainAmount,
        Config.SpotGains
            .Select((sg, i) => new PersonalDomainSpotSnapshot(sg.Type, sg.Amount, i < uncoveredCount))
            .ToList()
            .AsReadOnly(),
        PlacedDie?.ToSnapshot());
}
