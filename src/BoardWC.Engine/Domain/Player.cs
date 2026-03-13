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

    /// <summary>
    /// Value of <see cref="GameState.InfluenceGainCounter"/> when this player last gained influence.
    /// Higher = more recent. Breaks ties in round-start player order.
    /// </summary>
    internal int InfluenceGainOrder { get; set; }

    internal int DaimyoSeals { get; set; }
    internal int SoldiersAvailable { get; set; } = 5;
    internal int CourtiersAvailable { get; set; } = 5;
    internal int FarmersAvailable { get; set; } = 5;

    /// <summary>All pending-state flags that block turn advance until resolved.</summary>
    internal PlayerPendingState Pending { get; } = new();

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
        Pending.AnyResourceChoices, Pending.TrainingGroundsActions, Pending.FarmActions,
        Pending.CastlePlaceRemaining, Pending.CastleAdvanceRemaining,
        Pending.OutsideActivationSlot, Pending.InfluenceGain, Pending.InfluenceSealCost,
        Pending.CastleCardFieldFilter, Pending.PersonalDomainRowChoice, Pending.NewCardActivation?.ToSnapshot(),
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
