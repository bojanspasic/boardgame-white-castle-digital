namespace BoardWC.Engine.Domain;

/// <summary>
/// A double-sided token placed in castle rooms or the well at game start.
/// DieColor = the die-indicator side; ResourceSide = the reward side.
/// IsResourceSideUp = true only for well tokens (resource side face-up).
/// </summary>
internal sealed record Token(
    BridgeColor DieColor,
    TokenResource ResourceSide,
    bool IsResourceSideUp = false
);
