namespace BoardWC.Engine.Domain;

public enum ResourceType { Iron, Rice, Flower }

public enum Phase { Setup, WorkerPlacement, EndOfRound, GameOver }

public enum TowerZone { Left, Center, Right }

public enum PlayerColor { White, Black, Red, Blue }

public enum BridgeColor { Red, Black, White }

public enum DiePosition { High, Low }

public enum TowerActionType
{
    GainResources,
    AdvanceTower,
    AcquireClanCard,
    GainLanterns,
}
