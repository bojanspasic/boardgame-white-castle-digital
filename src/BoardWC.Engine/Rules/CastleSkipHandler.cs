using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class CastleSkipHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is CastleSkipAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a      = (CastleSkipAction)action;
        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");

        bool anyPending = player.Pending.CastlePlaceRemaining > 0 || player.Pending.CastleAdvanceRemaining > 0;
        if (!anyPending)
            return ValidationResult.Fail("No pending castle action to resolve.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (CastleSkipAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        player.Pending.CastlePlaceRemaining   = 0;
        player.Pending.CastleAdvanceRemaining = 0;
        events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, false, null, 0));
    }
}
