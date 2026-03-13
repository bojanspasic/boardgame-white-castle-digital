using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class OutsideActivationHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseOutsideActivationAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a      = (ChooseOutsideActivationAction)action;
        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("Not this player's turn.");
        if (player.Pending.OutsideActivationSlot < 0)
            return ValidationResult.Fail("No pending outside activation choice.");

        int slot = player.Pending.OutsideActivationSlot;
        if (slot == 0 && a.Choice == OutsideActivation.TrainingGrounds)
            return ValidationResult.Fail("Slot 0 only offers Farm or Castle.");
        if (slot == 1 && a.Choice == OutsideActivation.Farm)
            return ValidationResult.Fail("Slot 1 only offers Training Grounds or Castle.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseOutsideActivationAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        int slot   = player.Pending.OutsideActivationSlot;

        player.Pending.OutsideActivationSlot = -1;

        switch (a.Choice)
        {
            case OutsideActivation.Farm:
                player.Pending.FarmActions++;
                break;
            case OutsideActivation.Castle:
                player.Pending.CastlePlaceRemaining++;
                player.Pending.CastleAdvanceRemaining++;
                break;
            case OutsideActivation.TrainingGrounds:
                player.Pending.TrainingGroundsActions++;
                break;
        }

        events.Add(new OutsideActivationChosenEvent(state.GameId, player.Id, slot, a.Choice));
    }
}
