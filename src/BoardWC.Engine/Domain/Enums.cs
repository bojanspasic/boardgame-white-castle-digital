namespace BoardWC.Engine.Domain;

public enum ResourceType { Food, Iron, MotherOfPearls }

public enum Phase { Setup, SeedCardSelection, WorkerPlacement, EndOfRound, GameOver }

public enum PlayerColor { White, Black, Red, Blue }

public enum BridgeColor { Red, Black, White }

public enum DiePosition { High, Low }

public enum TokenResource { Food, Iron, MotherOfPearls, AnyResource, Coin }
