using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PlaceDieHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is PlaceDieAction;

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

        var placeholder = Resolve(a.Target, state.Board);
        if (placeholder is null)
            return ValidationResult.Fail("Invalid placement target.");
        if (!placeholder.CanAccept(state.Players.Count))
            return ValidationResult.Fail("That placement slot is full.");

        var die = player.DiceInHand[0];

        // Castle rooms only accept dice whose color matches at least one room token
        if (a.Target is CastleRoomTarget)
        {
            if (!placeholder.Tokens.Any(t => t.DieColor == die.Color))
                return ValidationResult.Fail(
                    $"A {die.Color} die cannot be placed in this room (no matching token).");
        }
        int compareValue = placeholder.GetCompareValue(state.Players.Count);
        int delta = die.Value - compareValue;
        if (delta < 0 && player.Coins < -delta)
            return ValidationResult.Fail(
                $"Not enough coins. Need {-delta}, have {player.Coins}.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a    = (PlaceDieAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var die    = player.DiceInHand[0];

        var placeholder  = Resolve(a.Target, state.Board)!;
        int compareValue = placeholder.GetCompareValue(state.Players.Count);
        int delta        = die.Value - compareValue;

        player.Coins += delta;
        placeholder.PlaceDie(die);
        player.DiceInHand.RemoveAt(0);

        events.Add(new DiePlacedEvent(state.GameId, player.Id, a.Target, die.Value, delta));

        // Castle room card effects — activate fields whose token color matches the die's color
        if (a.Target is CastleRoomTarget && placeholder.Card is { } card)
        {
            var tokens = placeholder.Tokens;
            int limit  = Math.Min(tokens.Count, card.Fields.Count);

            for (int i = 0; i < limit; i++)
            {
                if (tokens[i].DieColor != die.Color) continue;

                if (card.Fields[i] is GainCardField gainField)
                {
                    var resources = new ResourceBag();
                    int coins = 0, seals = 0, lantern = 0;

                    foreach (var item in gainField.Gains)
                    {
                        switch (item.Type)
                        {
                            case CardGainType.Food:
                                resources = resources.Add(ResourceType.Food, item.Amount); break;
                            case CardGainType.Iron:
                                resources = resources.Add(ResourceType.Iron, item.Amount); break;
                            case CardGainType.ValueItem:
                                resources = resources.Add(ResourceType.ValueItem, item.Amount); break;
                            case CardGainType.Coin:
                                coins += item.Amount; break;
                            case CardGainType.MonarchialSeal:
                                seals += item.Amount; break;
                            case CardGainType.Lantern:
                                lantern += item.Amount; break;
                        }
                    }

                    player.Resources = (player.Resources + resources).Clamp(7);
                    player.Coins += coins;
                    player.MonarchialSeals = Math.Min(player.MonarchialSeals + seals, 5);
                    player.LanternScore += lantern;

                    events.Add(new CardFieldGainActivatedEvent(
                        state.GameId, player.Id, card.Id, i,
                        resources, coins, seals, lantern));
                }
                else if (card.Fields[i] is ActionCardField actionField)
                {
                    events.Add(new CardActionActivatedEvent(
                        state.GameId, player.Id, card.Id, i, actionField.Description));

                    if (actionField.Description == "Play castle")
                    {
                        player.CastlePlaceRemaining++;
                        player.CastleAdvanceRemaining++;
                    }
                    else if (actionField.Description == "Play training grounds")
                    {
                        player.PendingTrainingGroundsActions++;
                    }
                }
            }
        }

        // Well token effects — apply when die is placed in the well
        if (a.Target is WellTarget)
        {
            player.MonarchialSeals = Math.Min(player.MonarchialSeals + 1, 5);

            var resourcesGained = new ResourceBag();
            int coinsGained     = 0;
            int pendingChoices  = 0;

            foreach (var token in state.Board.Well.Tokens)
            {
                switch (token.ResourceSide)
                {
                    case TokenResource.Food:        resourcesGained = resourcesGained.Add(ResourceType.Food,      1); break;
                    case TokenResource.Iron:        resourcesGained = resourcesGained.Add(ResourceType.Iron,      1); break;
                    case TokenResource.ValueItem:   resourcesGained = resourcesGained.Add(ResourceType.ValueItem, 1); break;
                    case TokenResource.Coin:        coinsGained++;    break;
                    case TokenResource.AnyResource: pendingChoices++; break;
                }
            }

            player.Resources = (player.Resources + resourcesGained).Clamp(7);
            player.Coins    += coinsGained;
            player.PendingAnyResourceChoices += pendingChoices;

            events.Add(new WellEffectAppliedEvent(
                state.GameId, player.Id, 1, resourcesGained, coinsGained, pendingChoices));
        }
    }

    private static DicePlaceholder? Resolve(PlacementTarget target, Board board) =>
        target switch
        {
            CastleRoomTarget  c => board.GetCastleRoom(c.Floor, c.RoomIndex),
            WellTarget          => board.Well,
            OutsideSlotTarget o => board.GetOutsideSlot(o.SlotIndex),
            _                   => null,
        };
}
