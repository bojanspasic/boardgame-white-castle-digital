using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Exercises every auto-property getter on every public snapshot record type.
/// A getter that no test ever reads is likely unused and could be removed.
/// </summary>
public class SnapshotCoverageTests
{
    private static IGameEngine StartedGame()
    {
        var engine = GameEngineFactory.Create(
        [
            new PlayerSetup("Alice", PlayerColor.White, IsAI: false),
            new PlayerSetup("Bob",   PlayerColor.Black, IsAI: false),
        ]);
        engine.ProcessAction(new StartGameAction());
        return engine;
    }

    [Fact]
    public void AllSnapshotGetters_AreReadable()
    {
        var engine = StartedGame();
        var s = engine.GetCurrentState();

        // ── GameStateSnapshot ─────────────────────────────────────────────────
        _ = s.GameId;
        _ = s.CurrentPhase;
        _ = s.CurrentRound;
        _ = s.MaxRounds;
        _ = s.ActivePlayerIndex;
        _ = s.Players;
        _ = s.Board;
        _ = s.SeedPairs;

        // ── SeedPairSnapshot / SeedActionCardSnapshot / SeedResourceCardSnapshot ─
        foreach (var pair in s.SeedPairs)
        {
            _ = pair.Action;
            _ = pair.Resource;
            _ = pair.Action.Id;
            _ = pair.Action.ActionType;
            _ = pair.Action.Back;
            _ = pair.Resource.Id;
            _ = pair.Resource.Gains;
            _ = pair.Resource.Back;
            _ = pair.Resource.DecreeCardId;
            _ = pair.Resource.DecreeGain;
            foreach (var g in pair.Resource.Gains) { _ = g.GainType; _ = g.Amount; }
            { var ab = pair.Action.Back;   _ = ab.GainType; _ = ab.Amount; }
            { var rb = pair.Resource.Back; _ = rb.GainType; _ = rb.Amount; }
            if (pair.Resource.DecreeGain is { } dg) { _ = dg.GainType; _ = dg.Amount; }
        }

        // ── PlayerSnapshot ────────────────────────────────────────────────────
        foreach (var p in s.Players)
        {
            _ = p.Id;
            _ = p.Name;
            _ = p.Color;
            _ = p.IsAI;
            _ = p.Resources;
            _ = p.LanternScore;
            _ = p.Influence;
            _ = p.Coins;
            _ = p.DaimyoSeals;
            _ = p.SoldiersAvailable;
            _ = p.CourtiersAvailable;
            _ = p.FarmersAvailable;
            _ = p.PendingAnyResourceChoices;
            _ = p.PendingTrainingGroundsActions;
            _ = p.PendingFarmActions;
            _ = p.CastlePlaceRemaining;
            _ = p.CastleAdvanceRemaining;
            _ = p.PendingOutsideActivationSlot;
            _ = p.PendingInfluenceGain;
            _ = p.PendingInfluenceSealCost;
            _ = p.PendingCastleCardFieldFilter;
            _ = p.PendingPersonalDomainRowChoice;
            _ = p.PendingNewCardActivation;
            _ = p.CourtiersAtGate;
            _ = p.CourtiersOnStewardFloor;
            _ = p.CourtiersOnDiplomatFloor;
            _ = p.CourtiersOnTopFloor;
            _ = p.DiceInHand;
            _ = p.SeedCard;
            _ = p.LanternChain;
            _ = p.PersonalDomainCards;
            _ = p.PersonalDomainRows;

            if (p.SeedCard is { } sc) { _ = sc.Id; _ = sc.ActionType; }

            // PersonalDomainRowSnapshot + PersonalDomainSpotSnapshot
            foreach (var row in p.PersonalDomainRows)
            {
                _ = row.DieColor;
                _ = row.CompareValue;
                _ = row.FigureType;
                _ = row.DefaultGainType;
                _ = row.DefaultGainAmount;
                _ = row.Spots;
                _ = row.PlacedDie;
                if (row.PlacedDie is { } d) { _ = d.Value; _ = d.Color; }
                foreach (var spot in row.Spots)
                {
                    _ = spot.GainType;
                    _ = spot.GainAmount;
                    _ = spot.IsUncovered;
                }
            }

            // LanternChainItemSnapshot (iterates over empty list in fresh game — see below)
            foreach (var item in p.LanternChain)
            {
                _ = item.SourceCardId;
                _ = item.SourceCardType;
                _ = item.Gains;
                foreach (var g in item.Gains) { _ = g.GainType; _ = g.Amount; }
            }

            foreach (var card in p.PersonalDomainCards)
                ReadRoomCardSnapshot(card);
        }

        // ── BoardSnapshot ─────────────────────────────────────────────────────
        var b = s.Board;
        _ = b.Bridges;
        _ = b.Castle;
        _ = b.Well;
        _ = b.Outside;
        _ = b.StewardFloorDeckRemaining;
        _ = b.DiplomatFloorDeckRemaining;
        _ = b.TrainingGrounds;
        _ = b.FarmingLands;
        _ = b.TotalDiceRemaining;

        // BridgeSnapshot / DieSnapshot
        foreach (var bridge in b.Bridges)
        {
            _ = bridge.Color;
            _ = bridge.High;
            _ = bridge.Middle;
            _ = bridge.Low;
            if (bridge.High is { } h) { _ = h.Value; _ = h.Color; }
            foreach (var m in bridge.Middle) { _ = m.Value; _ = m.Color; }
            if (bridge.Low is { } l) { _ = l.Value; _ = l.Color; }
        }

        // CastleSnapshot / TopFloorRoomSnapshot / TopFloorSlotSnapshot
        var castle = b.Castle;
        _ = castle.Floors;
        _ = castle.TopFloor;
        _ = castle.TopFloor.CardId;
        _ = castle.TopFloor.Slots;
        foreach (var slot in castle.TopFloor.Slots)
        {
            _ = slot.SlotIndex;
            _ = slot.Gains;
            _ = slot.OccupantName;
            foreach (var g in slot.Gains) { _ = g.GainType; _ = g.Amount; }
        }

        // DicePlaceholderSnapshot / TokenSnapshot (castle rooms)
        foreach (var floor in castle.Floors)
            foreach (var room in floor)
                ReadDicePlaceholderSnapshot(room);

        // WellSnapshot
        _ = b.Well.Placeholder;
        ReadDicePlaceholderSnapshot(b.Well.Placeholder);

        // OutsideSnapshot
        _ = b.Outside.Slots;
        foreach (var slot in b.Outside.Slots)
            ReadDicePlaceholderSnapshot(slot);

        // TrainingGroundsSnapshot / TgAreaSnapshot
        _ = b.TrainingGrounds.Areas;
        foreach (var area in b.TrainingGrounds.Areas)
        {
            _ = area.AreaIndex;
            _ = area.IronCost;
            _ = area.ResourceGain;
            _ = area.ActionDescription;
            _ = area.SoldierOwners;
            foreach (var g in area.ResourceGain) { _ = g.GainType; _ = g.Amount; }
        }

        // FarmingLandsSnapshot / FarmFieldSnapshot
        _ = b.FarmingLands.Fields;
        foreach (var field in b.FarmingLands.Fields)
        {
            _ = field.BridgeColor;
            _ = field.IsInland;
            _ = field.FoodCost;
            _ = field.GainItems;
            _ = field.ActionDescription;
            _ = field.VictoryPoints;
            _ = field.FarmerOwners;
            foreach (var g in field.GainItems) { _ = g.GainType; _ = g.Amount; }
        }

        // ── Direct construction for types not populated in a fresh game ────────

        // LanternChainGainSnapshot + LanternChainItemSnapshot
        var chainGain = new LanternChainGainSnapshot("Food", 1);
        _ = chainGain.GainType;
        _ = chainGain.Amount;
        var chainItem = new LanternChainItemSnapshot(
            "card-1", "StewardFloor",
            new[] { chainGain }.ToList().AsReadOnly());
        _ = chainItem.SourceCardId;
        _ = chainItem.SourceCardType;
        _ = chainItem.Gains;

        // CardCostItemSnapshot (action fields may have empty cost lists in JSON data)
        var costItem = new CardCostItemSnapshot("Coin", 2);
        _ = costItem.CostType;
        _ = costItem.Amount;

        // PlayerScore (only returned after game-over — read all properties directly)
        var score = new PlayerScore(
            Guid.NewGuid(), "Alice", 42, 10, 8, 3, 2, 7, 5, 4, 3);
        _ = score.PlayerId;
        _ = score.PlayerName;
        _ = score.Total;
        _ = score.LanternPoints;
        _ = score.CourtierPoints;
        _ = score.CoinPoints;
        _ = score.SealPoints;
        _ = score.ResourcePoints;
        _ = score.FarmPoints;
        _ = score.TrainingGroundsPoints;
        _ = score.InfluencePoints;
    }

    private static void ReadRoomCardSnapshot(RoomCardSnapshot card)
    {
        _ = card.Id;
        _ = card.Fields;
        _ = card.Layout;
        foreach (var field in card.Fields)
        {
            _ = field.IsGain;
            _ = field.Gains;
            _ = field.ActionDescription;
            _ = field.ActionCost;
            if (field.Gains != null)
                foreach (var gi in field.Gains) { _ = gi.GainType; _ = gi.Amount; }
            if (field.ActionCost != null)
                foreach (var ci in field.ActionCost) { _ = ci.CostType; _ = ci.Amount; }
        }
    }

    private static void ReadDicePlaceholderSnapshot(DicePlaceholderSnapshot ph)
    {
        _ = ph.BaseValue;
        _ = ph.UnlimitedCapacity;
        _ = ph.PlacedDice;
        _ = ph.Tokens;
        _ = ph.Card;
        foreach (var token in ph.Tokens)
        {
            _ = token.DieColor;
            _ = token.ResourceSide;
            _ = token.IsResourceSideUp;
        }
        if (ph.Card is { } c) ReadRoomCardSnapshot(c);
    }
}
