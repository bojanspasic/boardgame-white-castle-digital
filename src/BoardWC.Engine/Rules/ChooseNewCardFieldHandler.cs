using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChooseNewCardFieldHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseNewCardFieldAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (ChooseNewCardFieldAction)action;

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingNewCardActivation is null)
            return ValidationResult.Fail("No pending new card field choice to resolve.");

        if (a.FieldIndex == -1)
            return ValidationResult.Ok(); // skip is always valid

        var card = player.PendingNewCardActivation;
        if (a.FieldIndex < 0 || a.FieldIndex >= card.Fields.Count)
            return ValidationResult.Fail("Invalid field index.");

        // Check that the player can afford the cost if this is an action field
        if (card.Fields[a.FieldIndex] is ActionCardField af)
        {
            foreach (var cost in af.Cost)
            {
                switch (cost.Type)
                {
                    case CardCostType.Coin when player.Coins < cost.Amount:
                        return ValidationResult.Fail(
                            $"Need {cost.Amount} coins to activate this field; have {player.Coins}.");
                    case CardCostType.DaimyoSeal when player.DaimyoSeals < cost.Amount:
                        return ValidationResult.Fail(
                            $"Need {cost.Amount} seals to activate this field; have {player.DaimyoSeals}.");
                }
            }
        }

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseNewCardFieldAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        var card = player.PendingNewCardActivation!;
        player.PendingNewCardActivation = null;
        player.PersonalDomainCards.Add(card);

        if (a.FieldIndex == -1)
        {
            events.Add(new NewCardFieldChosenEvent(
                state.GameId, player.Id, card.Id, -1,
                new ResourceBag(), 0, 0, 0, 0, 0, null));
            return;
        }

        var field = card.Fields[a.FieldIndex];
        var resources = new ResourceBag();
        int coins = 0, seals = 0, lantern = 0, vp = 0, influence = 0;
        string? actionTriggered = null;

        if (field is GainCardField gf)
        {
            (resources, coins, seals, lantern, vp, influence) =
                CardFieldHelper.ApplyGainField(gf, player, state, events);
        }
        else if (field is ActionCardField af)
        {
            // Pay any costs
            foreach (var cost in af.Cost)
            {
                switch (cost.Type)
                {
                    case CardCostType.Coin:       player.Coins       -= cost.Amount; break;
                    case CardCostType.DaimyoSeal: player.DaimyoSeals -= cost.Amount; break;
                }
            }

            actionTriggered = af.Description;
            CardFieldHelper.ApplyActionDescription(af.Description, player);
        }

        events.Add(new NewCardFieldChosenEvent(
            state.GameId, player.Id, card.Id, a.FieldIndex,
            resources, coins, seals, lantern, vp, influence, actionTriggered));
    }

}
