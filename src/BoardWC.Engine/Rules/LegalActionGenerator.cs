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

        if (state.CurrentPhase == Phase.SeedCardSelection)
        {
            var seedPlayer = state.Players.FirstOrDefault(p => p.Id == playerId);
            if (seedPlayer is null || state.ActivePlayer.Id != playerId)
                return actions.AsReadOnly();

            // Resolve pending AnyResource choices from a resource card with wildcards
            if (seedPlayer.Pending.AnyResourceChoices > 0)
            {
                GenerateChooseResourceActions(playerId, actions);
                return actions.AsReadOnly();
            }

            for (int i = 0; i < state.SeedCardPairs.Count; i++)
                actions.Add(new ChooseSeedPairAction(playerId, i));
            return actions.AsReadOnly();
        }

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return actions.AsReadOnly();

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null || state.ActivePlayer.Id != playerId)
            return actions.AsReadOnly();

        // Player must resolve a pending influence threshold payment before acting
        if (player.Pending.InfluenceGain > 0)
        {
            GenerateInfluencePayActions(playerId, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending AnyResource choices before acting
        if (player.Pending.AnyResourceChoices > 0)
        {
            GenerateChooseResourceActions(playerId, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending new card field choice before acting
        if (player.Pending.NewCardActivation is { } pendingCard)
        {
            GenerateNewCardFieldActions(playerId, pendingCard, player, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending outside slot activation choice before acting
        if (player.Pending.OutsideActivationSlot >= 0)
        {
            GenerateOutsideActivationActions(playerId, player.Pending.OutsideActivationSlot, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending farm actions before acting
        if (player.Pending.FarmActions > 0)
        {
            GenerateFarmActions(playerId, player, state, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending training grounds actions before acting
        if (player.Pending.TrainingGroundsActions > 0)
        {
            GenerateTrainingGroundsActions(playerId, player, state, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending castle card field choice before acting
        if (player.Pending.CastleCardFieldFilter is { } filter)
        {
            GenerateCastleCardFieldActions(playerId, filter, player, state, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending personal domain row choice before acting
        if (player.Pending.PersonalDomainRowChoice)
        {
            GeneratePersonalDomainRowActions(playerId, player, actions);
            return actions.AsReadOnly();
        }

        // Player must resolve pending castle actions before acting
        if (player.Pending.CastlePlaceRemaining > 0 || player.Pending.CastleAdvanceRemaining > 0)
        {
            GenerateCastleActions(playerId, player, actions);
            return actions.AsReadOnly();
        }

        // Player has taken a die and must place it before doing anything else
        if (player.DiceInHand.Count > 0)
        {
            GeneratePlaceDieActions(playerId, player, state, actions);
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

    // ── pending-state generators ───────────────────────────────────────────────

    private static void GenerateInfluencePayActions(Guid playerId, List<IGameAction> actions)
    {
        actions.Add(new ChooseInfluencePayAction(playerId, WillPay: true));
        actions.Add(new ChooseInfluencePayAction(playerId, WillPay: false));
    }

    private static void GenerateChooseResourceActions(Guid playerId, List<IGameAction> actions)
    {
        actions.Add(new ChooseResourceAction(playerId, ResourceType.Food));
        actions.Add(new ChooseResourceAction(playerId, ResourceType.Iron));
        actions.Add(new ChooseResourceAction(playerId, ResourceType.MotherOfPearls));
    }

    private static void GenerateNewCardFieldActions(
        Guid playerId, RoomCard pendingCard, Domain.Player player, List<IGameAction> actions)
    {
        actions.Add(new ChooseNewCardFieldAction(playerId, -1)); // skip
        for (int fi = 0; fi < pendingCard.Fields.Count; fi++)
        {
            if (CanAffordField(pendingCard.Fields[fi], player))
                actions.Add(new ChooseNewCardFieldAction(playerId, fi));
        }
    }

    private static void GenerateOutsideActivationActions(
        Guid playerId, int slot, List<IGameAction> actions)
    {
        if (slot == 0)
        {
            actions.Add(new ChooseOutsideActivationAction(playerId, OutsideActivation.Farm));
            actions.Add(new ChooseOutsideActivationAction(playerId, OutsideActivation.Castle));
        }
        else
        {
            actions.Add(new ChooseOutsideActivationAction(playerId, OutsideActivation.TrainingGrounds));
            actions.Add(new ChooseOutsideActivationAction(playerId, OutsideActivation.Castle));
        }
    }

    private static void GenerateFarmActions(
        Guid playerId, Domain.Player player, GameState state, List<IGameAction> actions)
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
    }

    private static void GenerateTrainingGroundsActions(
        Guid playerId, Domain.Player player, GameState state, List<IGameAction> actions)
    {
        actions.Add(new TrainingGroundsSkipAction(playerId));
        var tgAreas = state.Board.TrainingGrounds.Areas;
        for (int i = 0; i < tgAreas.Length; i++)
        {
            if (player.SoldiersAvailable > 0 && player.Resources.Iron >= tgAreas[i].IronCost)
                actions.Add(new TrainingGroundsPlaceSoldierAction(playerId, i));
        }
    }

    private static void GenerateCastleCardFieldActions(
        Guid playerId, string filter, Domain.Player player, GameState state, List<IGameAction> actions)
    {
        actions.Add(new ChooseCastleCardFieldAction(playerId, -1, -1, -1)); // skip
        var castleFloors = state.Board.CastleFloors;
        for (int floor = 0; floor < castleFloors.Count; floor++)
        {
            var rooms = castleFloors[floor];
            for (int room = 0; room < rooms.Count; room++)
            {
                var ph = rooms[room];
                var card = ph.Card;
                if (card is null) continue;

                if (filter == "Red"   && !ph.Tokens.Any(t => t.DieColor == BridgeColor.Red))   continue;
                if (filter == "Black" && !ph.Tokens.Any(t => t.DieColor == BridgeColor.Black)) continue;
                if (filter == "White" && !ph.Tokens.Any(t => t.DieColor == BridgeColor.White)) continue;

                for (int fi = 0; fi < card.Fields.Count; fi++)
                {
                    if (filter == "GainOnly" && card.Fields[fi] is not GainCardField) continue;
                    if (CanAffordField(card.Fields[fi], player))
                        actions.Add(new ChooseCastleCardFieldAction(playerId, floor, room, fi));
                }
            }
        }
    }

    private static void GeneratePersonalDomainRowActions(
        Guid playerId, Domain.Player player, List<IGameAction> actions)
    {
        foreach (var row in player.PersonalDomainRows)
            actions.Add(new ChoosePersonalDomainRowAction(playerId, row.Config.DieColor));
    }

    private static void GenerateCastleActions(
        Guid playerId, Domain.Player player, List<IGameAction> actions)
    {
        actions.Add(new CastleSkipAction(playerId)); // always offered

        if (player.Pending.CastlePlaceRemaining > 0
            && player.CourtiersAvailable > 0 && player.Coins >= 2)
            actions.Add(new CastlePlaceCourtierAction(playerId));

        if (player.Pending.CastleAdvanceRemaining > 0)
            foreach (var (from, lvl, roomIdx) in ValidAdvances(player))
                actions.Add(new CastleAdvanceCourtierAction(playerId, from, lvl, roomIdx));
    }

    private static void GeneratePlaceDieActions(
        Guid playerId, Domain.Player player, GameState state, List<IGameAction> actions)
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

        // Personal domain rows (die color must match; row must be empty this round)
        for (int r = 0; r < player.PersonalDomainRows.Length; r++)
        {
            var row = player.PersonalDomainRows[r];
            if (row.PlacedDie is not null) continue;
            if (die.Color != row.Config.DieColor) continue;
            int delta = die.Value - row.Config.CompareValue;
            if (delta >= 0 || player.Coins >= -delta)
                actions.Add(new PlaceDieAction(playerId, new PersonalDomainTarget(r)));
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns true if the player can afford any cost on the given field.</summary>
    private static bool CanAffordField(CardField field, Domain.Player player)
    {
        if (field is not ActionCardField af) return true; // gain fields are always free
        foreach (var cost in af.Cost)
        {
            switch (cost.Type)
            {
                case CardCostType.Coin          when player.Coins          < cost.Amount: return false;
                case CardCostType.DaimyoSeal when player.DaimyoSeals < cost.Amount: return false;
            }
        }
        return true;
    }

    private static IEnumerable<(CourtierPosition From, int Levels, int RoomIndex)> ValidAdvances(Domain.Player player)
    {
        int vi = player.Resources.MotherOfPearls;

        if (player.CourtiersAtGate > 0)
        {
            // Gate + 1 → GroundFloor: player picks one of 3 steward-floor rooms
            if (vi >= 2)
                for (int r = 0; r < 3; r++)
                    yield return (CourtierPosition.Gate, 1, r);
            // Gate + 2 → MidFloor: player picks one of 2 diplomat-floor rooms
            if (vi >= 5)
                for (int r = 0; r < 2; r++)
                    yield return (CourtierPosition.Gate, 2, r);
        }
        if (player.CourtiersOnStewardFloor > 0)
        {
            // GroundFloor + 1 → MidFloor: player picks one of 2 diplomat-floor rooms
            if (vi >= 2)
                for (int r = 0; r < 2; r++)
                    yield return (CourtierPosition.StewardFloor, 1, r);
            // GroundFloor + 2 → TopFloor: no room choice
            if (vi >= 5) yield return (CourtierPosition.StewardFloor, 2, -1);
        }
        if (player.CourtiersOnDiplomatFloor > 0)
        {
            // MidFloor + 1 → TopFloor: no room choice
            if (vi >= 2) yield return (CourtierPosition.DiplomatFloor, 1, -1);
            // MidFloor + 2 is invalid (exceeds top floor)
        }
    }
}
