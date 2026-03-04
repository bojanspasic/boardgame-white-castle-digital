namespace BoardWC.Engine.Domain;

internal sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public PlayerColor Color { get; init; }
    public bool IsAI { get; init; }

    internal ResourceBag Resources { get; set; }
    internal int LanternScore { get; set; }
    internal int Influence { get; set; }
    internal int Coins { get; set; }

    /// <summary>Influence amount pending a threshold-payment decision; 0 = no pending gain.</summary>
    internal int PendingInfluenceGain { get; set; }

    /// <summary>Daimyo seals owed if the player accepts the pending influence gain.</summary>
    internal int PendingInfluenceSealCost { get; set; }

    /// <summary>
    /// Value of <see cref="GameState.InfluenceGainCounter"/> when this player last gained influence.
    /// Higher = more recent. Breaks ties in round-start player order.
    /// </summary>
    internal int InfluenceGainOrder { get; set; }

    internal int DaimyoSeals { get; set; }
    internal int SoldiersAvailable { get; set; } = 5;
    internal int CourtiersAvailable { get; set; } = 5;
    internal int FarmersAvailable { get; set; } = 5;

    /// <summary>Number of unresolved AnyResource token choices from the well.</summary>
    internal int PendingAnyResourceChoices { get; set; }

    /// <summary>Remaining soldier placements from pending "Play training grounds" actions.</summary>
    internal int PendingTrainingGroundsActions { get; set; }

    /// <summary>Remaining farmer placements from pending "Play farm" actions.</summary>
    internal int PendingFarmActions { get; set; }

    /// <summary>Remaining "place courtier at gate" uses from pending "Play castle" actions.</summary>
    internal int CastlePlaceRemaining { get; set; }

    /// <summary>Remaining "advance courtier" uses from pending "Play castle" actions.</summary>
    internal int CastleAdvanceRemaining { get; set; }

    /// <summary>-1 = no pending choice; 0 = slot 0 (Farm/Castle); 1 = slot 1 (TG/Castle).</summary>
    internal int PendingOutsideActivationSlot { get; set; } = -1;

    /// <summary>Filter for castle card field choice. "Red"/"Black"/"White"/"Any"/"GainOnly"; null = none pending.</summary>
    internal string? PendingCastleCardFieldFilter { get; set; }

    /// <summary>Whether the player must choose a personal domain row to activate for free.</summary>
    internal bool PendingPersonalDomainRowChoice { get; set; }

    /// <summary>Card just acquired by a courtier advance; player chooses a field before it enters the personal domain.</summary>
    internal RoomCard? PendingNewCardActivation { get; set; }

    internal int CourtiersAtGate { get; set; }
    internal int CourtiersOnStewardFloor { get; set; }
    internal int CourtiersOnDiplomatFloor { get; set; }
    internal int CourtiersOnTopFloor { get; set; }

    /// <summary>Dice the player has taken from bridges this round.</summary>
    internal List<Die> DiceInHand { get; } = new();

    /// <summary>The three personal domain rows (Red/Courtier, White/Farmer, Black/Soldier).</summary>
    internal PersonalDomainRow[] PersonalDomainRows { get; set; } = [];

    /// <summary>The seed action card chosen at game start; activates on each personal domain placement.</summary>
    internal SeedActionCard? SeedCard { get; set; }

    /// <summary>Ordered list of chain entries; fires left-to-right whenever a Lantern gain triggers.</summary>
    internal List<LanternChainItem> LanternChain { get; } = new();

    /// <summary>Room cards acquired from steward/diplomat floor castle rooms; fields activate on personal domain row placement.</summary>
    internal List<RoomCard> PersonalDomainCards { get; } = new();

    public PlayerSnapshot ToSnapshot() => new(
        Id, Name, Color, IsAI,
        Resources, LanternScore, Influence, Coins,
        DaimyoSeals, SoldiersAvailable, CourtiersAvailable, FarmersAvailable,
        PendingAnyResourceChoices, PendingTrainingGroundsActions, PendingFarmActions, CastlePlaceRemaining, CastleAdvanceRemaining,
        PendingOutsideActivationSlot, PendingInfluenceGain, PendingInfluenceSealCost,
        PendingCastleCardFieldFilter, PendingPersonalDomainRowChoice, PendingNewCardActivation?.ToSnapshot(),
        CourtiersAtGate, CourtiersOnStewardFloor, CourtiersOnDiplomatFloor, CourtiersOnTopFloor,
        DiceInHand.Select(d => d.ToSnapshot()).ToList().AsReadOnly(),
        PersonalDomainRows.Select(r => r.ToSnapshot(UncoveredCount(r.Config.FigureType))).ToList().AsReadOnly(),
        SeedCard?.ToSnapshot(),
        LanternChain.Select(i => i.ToSnapshot()).ToList().AsReadOnly(),
        PersonalDomainCards.Select(c => c.ToSnapshot()).ToList().AsReadOnly()
    );

    private int UncoveredCount(string figureType) => figureType switch
    {
        "Courtier" => 5 - CourtiersAvailable,
        "Farmer"   => 5 - FarmersAvailable,
        "Soldier"  => 5 - SoldiersAvailable,
        _          => 0
    };
}
