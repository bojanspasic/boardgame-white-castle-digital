namespace BoardWC.Engine.Domain;

public sealed class TowerAction
{
    public string Description { get; }
    public ResourceBag Cost { get; }
    public ResourceBag ResourceGain { get; }
    public int LanternsGained { get; }
    public TowerActionType ActionType { get; }
    public TowerZone? TowerToAdvance { get; }

    public TowerAction(
        string description,
        ResourceBag cost,
        ResourceBag resourceGain,
        int lanternsGained,
        TowerActionType actionType,
        TowerZone? towerToAdvance = null)
    {
        Description    = description;
        Cost           = cost;
        ResourceGain   = resourceGain;
        LanternsGained = lanternsGained;
        ActionType     = actionType;
        TowerToAdvance = towerToAdvance;
    }

    public TowerActionSnapshot ToSnapshot() =>
        new(Description, Cost, ResourceGain, LanternsGained, ActionType);
}

public static class TowerActionFactory
{
    public static TowerAction Create(TowerZone zone, int level) => (zone, level) switch
    {
        // Left Tower — Iron themed
        (TowerZone.Left, 0) => new("Gain 1 Iron",
            ResourceBag.Empty, new ResourceBag(Iron: 1), 0, TowerActionType.GainResources),

        (TowerZone.Left, 1) => new("Gain 2 Iron",
            new ResourceBag(Rice: 1), new ResourceBag(Iron: 2), 0, TowerActionType.GainResources),

        (TowerZone.Left, 2) => new("Advance Left Tower + 1 Lantern",
            new ResourceBag(Iron: 1), ResourceBag.Empty, 1, TowerActionType.AdvanceTower, TowerZone.Left),

        (TowerZone.Left, 3) => new("Gain 3 Iron + 1 Lantern",
            new ResourceBag(Rice: 2), new ResourceBag(Iron: 3), 1, TowerActionType.GainResources),

        // Center Tower — Rice themed
        (TowerZone.Center, 0) => new("Gain 1 Rice",
            ResourceBag.Empty, new ResourceBag(Rice: 1), 0, TowerActionType.GainResources),

        (TowerZone.Center, 1) => new("Gain 2 Rice",
            new ResourceBag(Iron: 1), new ResourceBag(Rice: 2), 0, TowerActionType.GainResources),

        (TowerZone.Center, 2) => new("Advance Center Tower + 1 Lantern",
            new ResourceBag(Rice: 1), ResourceBag.Empty, 1, TowerActionType.AdvanceTower, TowerZone.Center),

        (TowerZone.Center, 3) => new("Gain 3 Rice + 1 Lantern",
            new ResourceBag(Iron: 2), new ResourceBag(Rice: 3), 1, TowerActionType.GainResources),

        // Right Tower — Flower themed
        (TowerZone.Right, 0) => new("Gain 1 Flower",
            ResourceBag.Empty, new ResourceBag(Flower: 1), 0, TowerActionType.GainResources),

        (TowerZone.Right, 1) => new("Gain 2 Flower",
            new ResourceBag(Iron: 1), new ResourceBag(Flower: 2), 0, TowerActionType.GainResources),

        (TowerZone.Right, 2) => new("Acquire Clan Card",
            new ResourceBag(Flower: 1), ResourceBag.Empty, 0, TowerActionType.AcquireClanCard),

        (TowerZone.Right, 3) => new("Gain 3 Lanterns",
            new ResourceBag(Iron: 1, Rice: 1), ResourceBag.Empty, 3, TowerActionType.GainLanterns),

        _ => throw new ArgumentOutOfRangeException($"Invalid tower zone/level: {zone}/{level}")
    };
}
