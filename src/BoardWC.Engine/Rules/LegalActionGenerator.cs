using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Engine.Rules;

internal static class LegalActionGenerator
{
    public static IReadOnlyList<IGameAction> Generate(Guid playerId, GameState state)
    {
        var actions = new List<IGameAction>();

        if (state.CurrentPhase == Phase.Setup)
        {
            actions.Add(new StartGameAction());
            return actions.AsReadOnly();
        }

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return actions.AsReadOnly();

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null || state.ActivePlayer.Id != playerId)
            return actions.AsReadOnly();

        // Player must resolve pending AnyResource choices before acting
        if (player.PendingAnyResourceChoices > 0)
        {
            actions.Add(new ChooseResourceAction(playerId, ResourceType.Food));
            actions.Add(new ChooseResourceAction(playerId, ResourceType.Iron));
            actions.Add(new ChooseResourceAction(playerId, ResourceType.ValueItem));
            return actions.AsReadOnly();
        }

        // Player has taken a die and must place it before doing anything else
        if (player.DiceInHand.Count > 0)
        {
            var die = player.DiceInHand[0];
            int pc  = state.Players.Count;

            // Castle rooms
            var castleFloors = state.Board.CastleFloors;
            for (int floor = 0; floor < castleFloors.Count; floor++)
            {
                var rooms = castleFloors[floor];
                for (int room = 0; room < rooms.Count; room++)
                {
                    var ph    = rooms[room];
                    int delta = die.Value - ph.GetCompareValue(pc);
                    if (ph.CanAccept(pc)
                        && (delta >= 0 || player.Coins >= -delta)
                        && ph.Tokens.Any(t => t.DieColor == die.Color))
                        actions.Add(new PlaceDieAction(playerId, new CastleRoomTarget(floor, room)));
                }
            }

            // Well (always available as long as player can afford the delta)
            {
                var ph    = state.Board.Well;
                int delta = die.Value - ph.GetCompareValue(pc);
                if (delta >= 0 || player.Coins >= -delta)
                    actions.Add(new PlaceDieAction(playerId, new WellTarget()));
            }

            // Outside slots
            var outsideSlots = state.Board.OutsideSlots;
            for (int s = 0; s < outsideSlots.Count; s++)
            {
                var ph    = outsideSlots[s];
                int delta = die.Value - ph.GetCompareValue(pc);
                if (ph.CanAccept(pc) && (delta >= 0 || player.Coins >= -delta))
                    actions.Add(new PlaceDieAction(playerId, new OutsideSlotTarget(s)));
            }

            return actions.AsReadOnly();
        }

        // Bridge die takes
        foreach (var bridge in state.Board.Bridges)
        {
            if (bridge.CanTakeFromHigh)
                actions.Add(new TakeDieFromBridgeAction(playerId, bridge.Color, DiePosition.High));
            if (bridge.CanTakeFromLow)
                actions.Add(new TakeDieFromBridgeAction(playerId, bridge.Color, DiePosition.Low));
        }

        // Can always pass
        actions.Add(new PassAction(playerId));

        return actions.AsReadOnly();
    }
}
