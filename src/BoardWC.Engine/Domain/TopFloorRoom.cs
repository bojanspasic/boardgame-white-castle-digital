using System.Text.Json;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Domain;

// ── Gain item ─────────────────────────────────────────────────────────────────

internal sealed record TopFloorGainItem(string Type, int Amount);

// ── Slot ──────────────────────────────────────────────────────────────────────

internal sealed class TopFloorSlot(IReadOnlyList<TopFloorGainItem> gains)
{
    internal IReadOnlyList<TopFloorGainItem> Gains { get; } = gains;
    internal string? OccupantName { get; private set; }
    internal bool IsEmpty => OccupantName is null;

    internal void Occupy(string playerName) => OccupantName = playerName;
}

// ── Card ──────────────────────────────────────────────────────────────────────

internal sealed class TopFloorCard(string id, IReadOnlyList<TopFloorSlot> slots)
{
    internal string                    Id    { get; } = id;
    internal IReadOnlyList<TopFloorSlot> Slots { get; } = slots;
}

// ── Top-floor room ────────────────────────────────────────────────────────────

internal sealed class TopFloorRoom
{
    private readonly IReadOnlyList<TopFloorCard> _pool;
    private TopFloorCard? _card;

    private TopFloorRoom(IReadOnlyList<TopFloorCard> pool) => _pool = pool;

    internal TopFloorCard Card =>
        _card ?? throw new InvalidOperationException("Top floor card not yet set up.");

    /// <summary>
    /// Finds the first empty slot and occupies it.
    /// Returns true and sets <paramref name="slotIndex"/> + <paramref name="gains"/> if successful.
    /// Returns false (no slot available) if all 3 positions are already taken.
    /// </summary>
    internal bool TryTakeSlot(
        string playerName,
        out int slotIndex,
        out IReadOnlyList<TopFloorGainItem> gains)
    {
        for (int i = 0; i < Card.Slots.Count; i++)
        {
            if (!Card.Slots[i].IsEmpty) continue;
            Card.Slots[i].Occupy(playerName);
            slotIndex = i;
            gains     = Card.Slots[i].Gains;
            return true;
        }
        slotIndex = -1;
        gains     = [];
        return false;
    }

    /// <summary>Load card pool from embedded JSON. Called once.</summary>
    internal static TopFloorRoom Load()
    {
        const string resourceName = "BoardWC.Engine.Data.top-floor-cards.json";
        var assembly = typeof(TopFloorRoom).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var doc = JsonDocument.Parse(stream);

        var cards = doc.RootElement
            .GetProperty("cards")
            .EnumerateArray()
            .Select(ParseCard)
            .ToList()
            .AsReadOnly();

        return new TopFloorRoom(cards);
    }

    /// <summary>Pick one card randomly for the game. Called once at game start.</summary>
    internal void SetupForGame(Random rng)
    {
        _card = _pool[rng.Next(_pool.Count)];
        // Slots are new objects from the JSON load — occupants start empty.
    }

    internal TopFloorRoomSnapshot ToSnapshot() => new(
        Card.Id,
        Card.Slots
            .Select((s, i) => new TopFloorSlotSnapshot(
                i,
                s.Gains.Select(g => new CardGainItemSnapshot(g.Type, g.Amount))
                        .ToList().AsReadOnly(),
                s.OccupantName))
            .ToList().AsReadOnly());

    // ── JSON parsing ──────────────────────────────────────────────────────────

    private static TopFloorCard ParseCard(JsonElement el)
    {
        var id = el.GetProperty("id").GetString()!;
        var slots = el.GetProperty("slots")
            .EnumerateArray()
            .Select(s =>
            {
                var gains = s.GetProperty("gains")
                    .EnumerateArray()
                    .Select(g => new TopFloorGainItem(
                        g.GetProperty("type").GetString()!,
                        g.GetProperty("amount").GetInt32()))
                    .ToList()
                    .AsReadOnly();
                return new TopFloorSlot(gains);
            })
            .ToList()
            .AsReadOnly();
        return new TopFloorCard(id, slots);
    }
}
