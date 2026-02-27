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

        // Player must resolve pending farm actions before acting
        if (player.PendingFarmActions > 0)
        {
            actions.Add(new FarmSkipAction(playerId));
            var fl = state.Board.FarmingLands;
            foreach (BridgeColor color in Enum.GetValues<BridgeColor>())
            {
                foreach (bool isInland in new[] { true, false })
                {
                    var field = fl.GetField(color, isInland);
                    if (player.FarmersAvailable > 0
                        && player.Resources.Food >= field.Card.FoodCost
                        && !field.HasFarmer(player.Name))
                        actions.Add(new PlaceFarmerAction(playerId, color, isInland));
                }
            }
            return actions.AsReadOnly();
        }

        // Player must resolve pending training grounds actions before acting
        if (player.PendingTrainingGroundsActions > 0)
        {
            actions.Add(new TrainingGroundsSkipAction(playerId));
            var tgAreas = state.Board.TrainingGrounds.Areas;
            for (int i = 0; i < tgAreas.Length; i++)
            {
                if (player.SoldiersAvailable > 0 && player.Resources.Iron >= tgAreas[i].IronCost)
                    actions.Add(new TrainingGroundsPlaceSoldierAction(playerId, i));
            }
            return actions.AsReadOnly();
        }

        // Player must resolve pending castle actions before acting
        if (player.CastlePlaceRemaining > 0 || player.CastleAdvanceRemaining > 0)
        {
            actions.Add(new CastleSkipAction(playerId)); // always offered

            if (player.CastlePlaceRemaining > 0
                && player.CourtiersAvailable > 0 && player.Coins >= 2)
                actions.Add(new CastlePlaceCourtierAction(playerId));

            if (player.CastleAdvanceRemaining > 0)
                foreach (var (from, lvl) in ValidAdvances(player))
                    actions.Add(new CastleAdvanceCourtierAction(playerId, from, lvl));

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

    private static IEnumerable<(CourtierPosition From, int Levels)> ValidAdvances(Domain.Player player)
    {
        int vi = player.Resources.ValueItem;

        if (player.CourtiersAtGate > 0)
        {
            if (vi >= 2) yield return (CourtierPosition.Gate, 1);
            if (vi >= 5) yield return (CourtierPosition.Gate, 2);
        }
        if (player.CourtiersOnGroundFloor > 0)
        {
            if (vi >= 2) yield return (CourtierPosition.GroundFloor, 1);
            if (vi >= 5) yield return (CourtierPosition.GroundFloor, 2);
        }
        if (player.CourtiersOnMidFloor > 0)
        {
            if (vi >= 2) yield return (CourtierPosition.MidFloor, 1);
            // MidFloor + 2 is invalid (exceeds top floor)
        }
    }
}
