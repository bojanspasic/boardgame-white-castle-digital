using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Pure predicate: determines whether the active player has any pending state
/// that should block turn advance.
/// </summary>
internal static class TurnAdvancePolicy
{
    /// <summary>
    /// Returns true when the player still has unresolved state that must be
    /// handled before their turn can be advanced to the next player.
    /// Checks both <see cref="Player.DiceInHand"/> (turn state) and
    /// <see cref="PlayerPendingState.HasAny"/> (pending-state flags).
    /// </summary>
    internal static bool HasPendingState(Player player) =>
        player.DiceInHand.Count > 0 || player.Pending.HasAny;
}
