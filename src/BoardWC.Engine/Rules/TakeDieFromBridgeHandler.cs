using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class TakeDieFromBridgeHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is TakeDieFromBridgeAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (TakeDieFromBridgeAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");

        var bridge = state.Board.Bridges.FirstOrDefault(b => b.Color == a.BridgeColor);
        if (bridge is null)
            return ValidationResult.Fail($"Unknown bridge color '{a.BridgeColor}'.");

        if (a.DiePosition == DiePosition.High && !bridge.CanTakeFromHigh)
            return ValidationResult.Fail($"The High position on the {a.BridgeColor} bridge is empty.");

        if (a.DiePosition == DiePosition.Low && !bridge.CanTakeFromLow)
            return ValidationResult.Fail($"The Low position on the {a.BridgeColor} bridge is empty.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (TakeDieFromBridgeAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var bridge = state.Board.GetBridge(a.BridgeColor);

        var die = a.DiePosition == DiePosition.High
            ? bridge.TakeFromHigh()!
            : bridge.TakeFromLow()!;

        player.DiceInHand.Add(die);

        events.Add(new DieTakenFromBridgeEvent(
            state.GameId, player.Id, a.BridgeColor, a.DiePosition, die.Value));

        if (a.DiePosition == DiePosition.Low)
        {
            events.Add(new LanternEffectFiredEvent(state.GameId, player.Id));
            LanternHelper.Trigger(player, state.GameId, events);
        }
    }
}
