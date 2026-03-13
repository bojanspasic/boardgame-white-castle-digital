using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class CastlePlaceCourtierHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is CastlePlaceCourtierAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a      = (CastlePlaceCourtierAction)action;
        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");

        bool anyPending = player.Pending.CastlePlaceRemaining > 0 || player.Pending.CastleAdvanceRemaining > 0;
        if (!anyPending)
            return ValidationResult.Fail("No pending castle action to resolve.");

        return ValidatePlace(player);
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (CastlePlaceCourtierAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        player.Pending.CastlePlaceRemaining--;
        player.CourtiersAvailable--;
        player.CourtiersAtGate++;
        player.Coins -= 2;
        events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, true, null, 0));
    }

    private static ValidationResult ValidatePlace(Player player)
    {
        if (player.Pending.CastlePlaceRemaining <= 0)
            return ValidationResult.Fail("No place-at-gate use remaining.");
        if (player.CourtiersAvailable <= 0)
            return ValidationResult.Fail("No courtiers in hand to place at the gate.");
        if (player.Coins < 2)
            return ValidationResult.Fail("Need 2 coins to place a courtier at the gate.");
        return ValidationResult.Ok();
    }
}
