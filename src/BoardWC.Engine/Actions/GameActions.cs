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

/// <summary>Place a worker in a tower level to perform its action.</summary>
public sealed record PlaceWorkerInTowerAction(
    Guid PlayerId,
    TowerZone Zone,
    int Level
) : IGameAction;

/// <summary>Place the die currently held in hand onto a placement area.</summary>
public sealed record PlaceDieAction(
    Guid PlayerId,
    PlacementTarget Target
) : IGameAction;

/// <summary>Player passes their turn.</summary>
public sealed record PassAction(Guid PlayerId) : IGameAction;
