namespace BoardWC.Engine.Domain;

public enum ResourceType { Food, Iron, ValueItem }

public enum Phase { Setup, WorkerPlacement, EndOfRound, GameOver }

public enum PlayerColor { White, Black, Red, Blue }

public enum BridgeColor { Red, Black, White }

public enum DiePosition { High, Low }

public enum TokenResource { Food, Iron, ValueItem, AnyResource, Coin }
