using System.Diagnostics.CodeAnalysis;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PlaceDieAtCastleHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is PlaceDieAction a && a.Target is CastleRoomTarget;

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

        var die = player.DiceInHand[0];
        var placeholder = state.Board.GetCastleRoom(
            ((CastleRoomTarget)a.Target).Floor,
            ((CastleRoomTarget)a.Target).RoomIndex);

        if (placeholder is null)
            return ValidationResult.Fail("Invalid placement target.");
        if (!placeholder.CanAccept(state.Players.Count))
            return ValidationResult.Fail("That placement slot is full.");

        if (!placeholder.Tokens.Any(t => t.DieColor == die.Color))
            return ValidationResult.Fail(
                $"A {die.Color} die cannot be placed in this room (no matching token).");

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
        var target = (CastleRoomTarget)a.Target;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var die    = player.DiceInHand[0];

        var placeholder  = state.Board.GetCastleRoom(target.Floor, target.RoomIndex)!;
        int compareValue = placeholder.GetCompareValue(state.Players.Count);
        int delta        = die.Value - compareValue;

        player.Coins += delta;
        placeholder.PlaceDie(die);
        player.DiceInHand.RemoveAt(0);

        events.Add(new DiePlacedEvent(state.GameId, player.Id, a.Target, die.Value, delta));

        // Castle room card effects — activate fields whose token color matches the die's color
        if (placeholder.Card is { } card)
        {
            var tokens = placeholder.Tokens;
            int limit  = Math.Min(tokens.Count, card.Fields.Count);

            for (int i = 0; i < limit; i++)
            {
                if (tokens[i].DieColor != die.Color) continue;

                if (card.Fields[i] is GainCardField gainField)
                {
                    var (resources, coins, seals, lantern, vp, influence) =
                        CardGainApplier.ApplyGain(player, gainField, state, events);

                    events.Add(new CardFieldGainActivatedEvent(
                        state.GameId, player.Id, card.Id, i,
                        resources, coins, seals, lantern, vp, influence));
                }
                else if (card.Fields[i] is ActionCardField actionField)
                {
                    events.Add(new CardActionActivatedEvent(
                        state.GameId, player.Id, card.Id, i, actionField.Description));
                    CardActionApplier.ApplyAction(player, actionField.Description, state, events);
                }
            }
        }
    }
}
