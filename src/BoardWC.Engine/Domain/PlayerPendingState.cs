namespace BoardWC.Engine.Domain;

/// <summary>
/// Holds all pending-state flags on a <see cref="Player"/> — fields that block turn advance
/// until the corresponding handler resolves them.
/// </summary>
internal sealed class PlayerPendingState
{
    /// <summary>Number of unresolved AnyResource token choices from the well.</summary>
    internal int AnyResourceChoices { get; set; }

    /// <summary>Influence amount pending a threshold-payment decision; 0 = no pending gain.</summary>
    internal int InfluenceGain { get; set; }

    /// <summary>Daimyo seals owed if the player accepts the pending influence gain.</summary>
    internal int InfluenceSealCost { get; set; }

    /// <summary>Remaining "place courtier at gate" uses from pending "Play castle" actions.</summary>
    internal int CastlePlaceRemaining { get; set; }

    /// <summary>Remaining "advance courtier" uses from pending "Play castle" actions.</summary>
    internal int CastleAdvanceRemaining { get; set; }

    /// <summary>Remaining soldier placements from pending "Play training grounds" actions.</summary>
    internal int TrainingGroundsActions { get; set; }

    /// <summary>Remaining farmer placements from pending "Play farm" actions.</summary>
    internal int FarmActions { get; set; }

    /// <summary>-1 = no pending choice; 0 = slot 0 (Farm/Castle); 1 = slot 1 (TG/Castle).</summary>
    internal int OutsideActivationSlot { get; set; } = -1;

    /// <summary>Filter for castle card field choice. "Red"/"Black"/"White"/"Any"/"GainOnly"; null = none pending.</summary>
    internal string? CastleCardFieldFilter { get; set; }

    /// <summary>Whether the player must choose a personal domain row to activate for free.</summary>
    internal bool PersonalDomainRowChoice { get; set; }

    /// <summary>Card just acquired by a courtier advance; player chooses a field before it enters the personal domain.</summary>
    internal RoomCard? NewCardActivation { get; set; }

    /// <summary>True when any pending state exists that blocks turn advance.</summary>
    internal bool HasAny =>
        AnyResourceChoices > 0 ||
        InfluenceGain > 0 ||
        CastlePlaceRemaining > 0 ||
        CastleAdvanceRemaining > 0 ||
        TrainingGroundsActions > 0 ||
        FarmActions > 0 ||
        OutsideActivationSlot >= 0 ||
        CastleCardFieldFilter is not null ||
        PersonalDomainRowChoice ||
        NewCardActivation is not null;
}
