using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PlaceDieAtWellHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is PlaceDieAction a && a.Target is WellTarget;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (PlaceDieAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.DiceInHand.Count == 0)
            return ValidationResult.Fail("No die in hand to place.");

        var die         = player.DiceInHand[0];
        var placeholder = state.Board.Well;

        if (!placeholder.CanAccept(state.Players.Count))
            return ValidationResult.Fail("That placement slot is full.");

        int compareValue = placeholder.GetCompareValue(state.Players.Count);
        int delta        = die.Value - compareValue;
        if (delta < 0 && player.Coins < -delta)
            return ValidationResult.Fail(
                $"Not enough coins. Need {-delta}, have {player.Coins}.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (PlaceDieAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var die    = player.DiceInHand[0];

        var placeholder  = state.Board.Well;
        int compareValue = placeholder.GetCompareValue(state.Players.Count);
        int delta        = die.Value - compareValue;

        player.Coins += delta;
        placeholder.PlaceDie(die);
        player.DiceInHand.RemoveAt(0);

        events.Add(new DiePlacedEvent(state.GameId, player.Id, a.Target, die.Value, delta));

        // Well token effects — apply when die is placed in the well
        player.DaimyoSeals = Math.Min(player.DaimyoSeals + 1, 5);

        var resourcesGained = new ResourceBag();
        int coinsGained     = 0;
        int pendingChoices  = 0;

        foreach (var token in state.Board.Well.Tokens)
        {
            switch (token.ResourceSide)
            {
                case TokenResource.Food:           resourcesGained = resourcesGained.Add(ResourceType.Food,           1); break;
                case TokenResource.Iron:           resourcesGained = resourcesGained.Add(ResourceType.Iron,           1); break;
                case TokenResource.MotherOfPearls: resourcesGained = resourcesGained.Add(ResourceType.MotherOfPearls, 1); break;
                case TokenResource.Coin:           coinsGained++;    break;
                case TokenResource.AnyResource:    pendingChoices++; break;
            }
        }

        player.Resources = (player.Resources + resourcesGained).Clamp(7);
        player.Coins    += coinsGained;
        player.Pending.AnyResourceChoices += pendingChoices;

        events.Add(new WellEffectAppliedEvent(
            state.GameId, player.Id, 1, resourcesGained, coinsGained, pendingChoices));
    }
}
