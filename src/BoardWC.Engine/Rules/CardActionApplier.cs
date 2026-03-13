using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Applies named action descriptions (from <see cref="ActionCardField.Description"/>) to the
/// player. Covers all "Play …" action types shared across castle rooms, farm, training grounds,
/// and personal domain card fields.
/// </summary>
internal static class CardActionApplier
{
    /// <summary>
    /// Applies a named action description to the player.
    /// Returns true if the description was handled.
    /// </summary>
    internal static bool ApplyAction(Player player, string description) =>
        ApplyAction(player, description, null!, null!);

    /// <summary>
    /// Applies a named action description to the player.
    /// Returns true if the description was handled.
    /// </summary>
    internal static bool ApplyAction(Player player, string description, GameState state, List<IDomainEvent> events)
    {
        switch (description)
        {
            case "Play castle":
                player.Pending.CastlePlaceRemaining++;
                player.Pending.CastleAdvanceRemaining++;
                return true;
            case "Play training grounds":
                player.Pending.TrainingGroundsActions++;
                return true;
            case "Play farm":
                player.Pending.FarmActions++;
                return true;
            case "Play red castle card field":
                player.Pending.CastleCardFieldFilter = "Red";
                return true;
            case "Play black castle card field":
                player.Pending.CastleCardFieldFilter = "Black";
                return true;
            case "Play white castle card field":
                player.Pending.CastleCardFieldFilter = "White";
                return true;
            case "Play any castle card field":
                player.Pending.CastleCardFieldFilter = "Any";
                return true;
            case "Play castle gain field":
                player.Pending.CastleCardFieldFilter = "GainOnly";
                return true;
            case "Play personal domain row":
                player.Pending.PersonalDomainRowChoice = true;
                return true;
            default:
                return false;
        }
    }
}
