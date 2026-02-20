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
