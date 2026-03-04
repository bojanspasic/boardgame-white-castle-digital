using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Actions;

/// <summary>Transition Setup -> WorkerPlacement and roll initial dice.</summary>
public sealed record StartGameAction() : IGameAction
{
    public Guid PlayerId => Guid.Empty;
}

/// <summary>Take a die from the High or Low position of a bridge.</summary>
public sealed record TakeDieFromBridgeAction(
    Guid PlayerId,
    BridgeColor BridgeColor,
    DiePosition DiePosition
) : IGameAction;

/// <summary>Place the die currently held in hand onto a placement area.</summary>
public sealed record PlaceDieAction(
    Guid PlayerId,
    PlacementTarget Target
) : IGameAction;

/// <summary>Player passes their turn.</summary>
public sealed record PassAction(Guid PlayerId) : IGameAction;

/// <summary>Resolve a pending AnyResource token choice gained from the well.</summary>
public sealed record ChooseResourceAction(
    Guid PlayerId,
    ResourceType Choice
) : IGameAction;

/// <summary>
/// "From" positions for courtier advancement.
/// Gate is the entry point; courtiers can advance up to MidFloor via this enum.
/// TopFloor is the destination only (no further advancement possible).
/// </summary>
public enum CourtierPosition { Gate, GroundFloor, MidFloor }

/// <summary>Place one courtier from hand to the gate (costs 2 coins).</summary>
public sealed record CastlePlaceCourtierAction(Guid PlayerId) : IGameAction;

/// <summary>Advance one courtier up the castle (costs 2 VI for 1 level, 5 VI for 2 levels).</summary>
/// <param name="RoomIndex">Which room to enter when landing on ground (0–2) or mid (0–1) floor; -1 when going directly to top floor.</param>
public sealed record CastleAdvanceCourtierAction(
    Guid PlayerId,
    CourtierPosition From,
    int Levels,
    int RoomIndex = -1
) : IGameAction;

/// <summary>Skip all remaining pending castle play options (place and/or advance).</summary>
public sealed record CastleSkipAction(Guid PlayerId) : IGameAction;

/// <summary>Place a soldier in one of the three training grounds areas, paying iron.</summary>
public sealed record TrainingGroundsPlaceSoldierAction(
    Guid PlayerId,
    int AreaIndex   // 0, 1, or 2
) : IGameAction;

/// <summary>Skip the pending training grounds action.</summary>
public sealed record TrainingGroundsSkipAction(Guid PlayerId) : IGameAction;

/// <summary>Place a farmer on an inland or outside farm field of a specific bridge, paying food.</summary>
public sealed record PlaceFarmerAction(
    Guid PlayerId,
    BridgeColor BridgeColor,
    bool IsInland
) : IGameAction;

/// <summary>Skip the pending farm action.</summary>
public sealed record FarmSkipAction(Guid PlayerId) : IGameAction;

/// <summary>Which area to activate after placing a die at an outside slot.</summary>
public enum OutsideActivation { Farm, Castle, TrainingGrounds }

/// <summary>Resolve the activation choice after placing a die in an outside slot.</summary>
public sealed record ChooseOutsideActivationAction(
    Guid PlayerId,
    OutsideActivation Choice
) : IGameAction;

/// <summary>Choose one of the available seed card pairs at the start of the game.</summary>
public sealed record ChooseSeedPairAction(
    Guid PlayerId,
    int PairIndex
) : IGameAction;

/// <summary>Accept or refuse a pending influence gain that crosses a Monarchial Seal threshold.</summary>
public sealed record ChooseInfluencePayAction(
    Guid PlayerId,
    bool WillPay
) : IGameAction;

/// <summary>
/// Activate one field on a placed castle room card matching the pending color filter.
/// Use Floor = -1 (and RoomIndex = -1, FieldIndex = -1) to skip without activating.
/// </summary>
public sealed record ChooseCastleCardFieldAction(
    Guid PlayerId,
    int Floor,
    int RoomIndex,
    int FieldIndex
) : IGameAction;

/// <summary>Activate a personal domain row of the chosen color without placing a die.</summary>
public sealed record ChoosePersonalDomainRowAction(
    Guid PlayerId,
    BridgeColor RowColor
) : IGameAction;

/// <summary>
/// Select a field on the just-acquired room card to play before placing it in the personal domain.
/// Use FieldIndex = -1 to skip field activation.
/// </summary>
public sealed record ChooseNewCardFieldAction(
    Guid PlayerId,
    int FieldIndex
) : IGameAction;
