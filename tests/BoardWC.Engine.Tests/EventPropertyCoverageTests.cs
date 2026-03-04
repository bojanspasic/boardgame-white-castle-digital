using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Exercises all public properties of domain event records to drive method coverage
/// on the auto-generated property getters.
/// </summary>
public class EventPropertyCoverageTests
{
    private static readonly Guid GameId   = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();

    // ── TrainingGroundsUsedEvent ──────────────────────────────────────────────

    [Fact]
    public void TrainingGroundsUsedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 3);
        var evt = new TrainingGroundsUsedEvent(
            GameId, PlayerId,
            AreaIndex:      1,
            IronSpent:      3,
            ResourcesGained: res,
            CoinsGained:    2,
            SealsGained:    1,
            LanternGained:  0,
            ActionTriggered: "Play castle");

        Assert.Equal(GameId,        evt.GameId);
        Assert.Equal(PlayerId,      evt.PlayerId);
        Assert.Equal(1,             evt.AreaIndex);
        Assert.Equal(3,             evt.IronSpent);
        Assert.Equal(1,             evt.ResourcesGained.Food);
        Assert.Equal(2,             evt.ResourcesGained.Iron);
        Assert.Equal(3,             evt.ResourcesGained.MotherOfPearls);
        Assert.Equal(2,             evt.CoinsGained);
        Assert.Equal(1,             evt.SealsGained);
        Assert.Equal(0,             evt.LanternGained);
        Assert.Equal("Play castle", evt.ActionTriggered);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(TrainingGroundsUsedEvent), evt.EventType);
    }

    [Fact]
    public void TrainingGroundsUsedEvent_NullActionTriggered()
    {
        var evt = new TrainingGroundsUsedEvent(
            GameId, PlayerId, -1, 0, new ResourceBag(), 0, 0, 0, null);
        Assert.Null(evt.ActionTriggered);
    }

    // ── FarmerPlacedEvent ─────────────────────────────────────────────────────

    [Fact]
    public void FarmerPlacedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 2);
        var evt = new FarmerPlacedEvent(
            GameId, PlayerId,
            BridgeColor: BridgeColor.Red,
            IsInland:    true,
            AreaIndex:   0,
            FoodSpent:   1,
            ResourcesGained: res,
            CoinsGained: 0,
            SealsGained: 0,
            LanternGained: 0,
            ActionTriggered: null);

        Assert.Equal(GameId,          evt.GameId);
        Assert.Equal(PlayerId,        evt.PlayerId);
        Assert.Equal(BridgeColor.Red, evt.BridgeColor);
        Assert.True(evt.IsInland);
        Assert.Equal(0,               evt.AreaIndex);
        Assert.Equal(1,               evt.FoodSpent);
        Assert.Equal(2,               evt.ResourcesGained.Food);
        Assert.Equal(0,               evt.CoinsGained);
        Assert.Equal(0,               evt.SealsGained);
        Assert.Equal(0,               evt.LanternGained);
        Assert.Null(evt.ActionTriggered);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(FarmerPlacedEvent), evt.EventType);
    }

    [Fact]
    public void FarmerPlacedEvent_WithActionTriggered()
    {
        var evt = new FarmerPlacedEvent(
            GameId, PlayerId, BridgeColor.Black, false, 0, 2,
            new ResourceBag(), 3, 0, 1, "Play castle");

        Assert.Equal("Play castle",      evt.ActionTriggered);
        Assert.Equal(3,                  evt.CoinsGained);
        Assert.Equal(1,                  evt.LanternGained);
        Assert.Equal(BridgeColor.Black,  evt.BridgeColor);
        Assert.False(evt.IsInland);
    }

    // ── FarmEffectFiredEvent ──────────────────────────────────────────────────

    [Fact]
    public void FarmEffectFiredEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Iron: 1);
        var evt = new FarmEffectFiredEvent(
            GameId, PlayerId,
            BridgeColor: BridgeColor.White,
            IsInland:    false,
            ResourcesGained: res,
            CoinsGained: 2,
            SealsGained: 1,
            LanternGained: 0,
            ActionTriggered: "Gain 3 coins");

        Assert.Equal(GameId,           evt.GameId);
        Assert.Equal(PlayerId,         evt.PlayerId);
        Assert.Equal(BridgeColor.White, evt.BridgeColor);
        Assert.False(evt.IsInland);
        Assert.Equal(1,                evt.ResourcesGained.Iron);
        Assert.Equal(2,                evt.CoinsGained);
        Assert.Equal(1,                evt.SealsGained);
        Assert.Equal(0,                evt.LanternGained);
        Assert.Equal("Gain 3 coins",   evt.ActionTriggered);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(FarmEffectFiredEvent), evt.EventType);
    }

    // ── WellEffectAppliedEvent ────────────────────────────────────────────────

    [Fact]
    public void WellEffectAppliedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1, Iron: 1);
        var evt = new WellEffectAppliedEvent(
            GameId, PlayerId,
            SealGained:      1,
            ResourcesGained: res,
            CoinsGained:     2,
            PendingChoices:  1);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(1,        evt.SealGained);
        Assert.Equal(1,        evt.ResourcesGained.Food);
        Assert.Equal(1,        evt.ResourcesGained.Iron);
        Assert.Equal(2,        evt.CoinsGained);
        Assert.Equal(1,        evt.PendingChoices);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(WellEffectAppliedEvent), evt.EventType);
    }

    // ── PersonalDomainActivatedEvent ──────────────────────────────────────────

    [Fact]
    public void PersonalDomainActivatedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(MotherOfPearls: 2);
        var evt = new PersonalDomainActivatedEvent(
            GameId, PlayerId,
            RowIndex:       2,
            DieColor:       BridgeColor.Black,
            UncoveredSpots: 3,
            ResourcesGained: res);

        Assert.Equal(GameId,          evt.GameId);
        Assert.Equal(PlayerId,        evt.PlayerId);
        Assert.Equal(2,               evt.RowIndex);
        Assert.Equal(BridgeColor.Black, evt.DieColor);
        Assert.Equal(3,               evt.UncoveredSpots);
        Assert.Equal(2,               evt.ResourcesGained.MotherOfPearls);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(PersonalDomainActivatedEvent), evt.EventType);
    }

    // ── LanternChainActivatedEvent ────────────────────────────────────────────

    [Fact]
    public void LanternChainActivatedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1, Iron: 2, MotherOfPearls: 1);
        var evt = new LanternChainActivatedEvent(
            GameId, PlayerId,
            Resources: res,
            Coins:     3,
            Seals:     1,
            VpGained:  2);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(1,        evt.Resources.Food);
        Assert.Equal(2,        evt.Resources.Iron);
        Assert.Equal(1,        evt.Resources.MotherOfPearls);
        Assert.Equal(3,        evt.Coins);
        Assert.Equal(1,        evt.Seals);
        Assert.Equal(2,        evt.VpGained);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(LanternChainActivatedEvent), evt.EventType);
    }

    // ── CardFieldGainActivatedEvent ───────────────────────────────────────────

    [Fact]
    public void CardFieldGainActivatedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1, Iron: 1, MotherOfPearls: 1);
        var evt = new CardFieldGainActivatedEvent(
            GameId, PlayerId,
            CardId:           "card-1",
            FieldIndex:       2,
            ResourcesGained:  res,
            CoinsGained:      3,
            SealsGained:      1,
            LanternGained:    1,
            VpGained:         2,
            InfluenceGained:  5);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal("card-1", evt.CardId);
        Assert.Equal(2,        evt.FieldIndex);
        Assert.Equal(1,        evt.ResourcesGained.Food);
        Assert.Equal(1,        evt.ResourcesGained.Iron);
        Assert.Equal(1,        evt.ResourcesGained.MotherOfPearls);
        Assert.Equal(3,        evt.CoinsGained);
        Assert.Equal(1,        evt.SealsGained);
        Assert.Equal(1,        evt.LanternGained);
        Assert.Equal(2,        evt.VpGained);
        Assert.Equal(5,        evt.InfluenceGained);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(CardFieldGainActivatedEvent), evt.EventType);
    }

    // ── SeedPairChosenEvent ───────────────────────────────────────────────────

    [Fact]
    public void SeedPairChosenEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Iron: 1);
        var evt = new SeedPairChosenEvent(
            GameId, PlayerId,
            ActionCardId:     "action-1",
            ActionType:       "PlayCastle",
            ResourcesGained:  res,
            CoinsGained:      2,
            SealsGained:      1,
            PendingAnyChoices: 0);

        Assert.Equal(GameId,      evt.GameId);
        Assert.Equal(PlayerId,    evt.PlayerId);
        Assert.Equal("action-1",  evt.ActionCardId);
        Assert.Equal("PlayCastle", evt.ActionType);
        Assert.Equal(1,           evt.ResourcesGained.Iron);
        Assert.Equal(2,           evt.CoinsGained);
        Assert.Equal(1,           evt.SealsGained);
        Assert.Equal(0,           evt.PendingAnyChoices);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(SeedPairChosenEvent), evt.EventType);
    }

    // ── OutsideActivationChosenEvent ──────────────────────────────────────────

    [Fact]
    public void OutsideActivationChosenEvent_AllPropertiesReadable()
    {
        var evt = new OutsideActivationChosenEvent(
            GameId, PlayerId,
            SlotIndex: 1,
            Choice:    OutsideActivation.TrainingGrounds);

        Assert.Equal(GameId,                         evt.GameId);
        Assert.Equal(PlayerId,                       evt.PlayerId);
        Assert.Equal(1,                              evt.SlotIndex);
        Assert.Equal(OutsideActivation.TrainingGrounds, evt.Choice);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(OutsideActivationChosenEvent), evt.EventType);
    }

    // ── CardActionActivatedEvent ──────────────────────────────────────────────

    [Fact]
    public void CardActionActivatedEvent_AllPropertiesReadable()
    {
        var evt = new CardActionActivatedEvent(
            GameId, PlayerId,
            CardId:            "c1",
            FieldIndex:        1,
            ActionDescription: "Play farm");

        Assert.Equal(GameId,       evt.GameId);
        Assert.Equal(PlayerId,     evt.PlayerId);
        Assert.Equal("c1",         evt.CardId);
        Assert.Equal(1,            evt.FieldIndex);
        Assert.Equal("Play farm",  evt.ActionDescription);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(CardActionActivatedEvent), evt.EventType);
    }

    // ── RoomCardAcquiredEvent ─────────────────────────────────────────────────

    [Fact]
    public void RoomCardAcquiredEvent_AllPropertiesReadable()
    {
        var evt = new RoomCardAcquiredEvent(
            GameId, PlayerId,
            CardId:   "room-1",
            CardName: "Grand Room",
            Floor:    0);

        Assert.Equal(GameId,       evt.GameId);
        Assert.Equal(PlayerId,     evt.PlayerId);
        Assert.Equal("room-1",     evt.CardId);
        Assert.Equal("Grand Room", evt.CardName);
        Assert.Equal(0,            evt.Floor);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(RoomCardAcquiredEvent), evt.EventType);
    }

    // ── PersonalDomainCardFieldActivatedEvent ─────────────────────────────────

    [Fact]
    public void PersonalDomainCardFieldActivatedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1);
        var evt = new PersonalDomainCardFieldActivatedEvent(
            GameId, PlayerId,
            CardId:          "pd-1",
            FieldIndex:      0,
            ResourcesGained: res,
            CoinsGained:     1,
            SealsGained:     0,
            LanternGained:   0,
            VpGained:        0,
            InfluenceGained: 0);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal("pd-1",   evt.CardId);
        Assert.Equal(0,        evt.FieldIndex);
        Assert.Equal(1,        evt.ResourcesGained.Food);
        Assert.Equal(1,        evt.CoinsGained);
        Assert.Equal(0,        evt.SealsGained);
        Assert.Equal(0,        evt.LanternGained);
        Assert.Equal(0,        evt.VpGained);
        Assert.Equal(0,        evt.InfluenceGained);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(PersonalDomainCardFieldActivatedEvent), evt.EventType);
    }

    // ── Other low-coverage events ─────────────────────────────────────────────

    [Fact]
    public void InfluenceGainPendingEvent_AllPropertiesReadable()
    {
        var evt = new InfluenceGainPendingEvent(GameId, PlayerId, InfluenceGain: 5, SealCost: 1);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(5,        evt.InfluenceGain);
        Assert.Equal(1,        evt.SealCost);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(InfluenceGainPendingEvent), evt.EventType);
    }

    [Fact]
    public void InfluenceGainResolvedEvent_AllPropertiesReadable()
    {
        var evt = new InfluenceGainResolvedEvent(GameId, PlayerId, InfluenceGain: 3, SealsPaid: 1, Accepted: true);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(3,        evt.InfluenceGain);
        Assert.Equal(1,        evt.SealsPaid);
        Assert.True(evt.Accepted);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(InfluenceGainResolvedEvent), evt.EventType);
    }

    [Fact]
    public void TopFloorSlotFilledEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(MotherOfPearls: 1);
        var evt = new TopFloorSlotFilledEvent(GameId, PlayerId, SlotIndex: 0,
            ResourcesGained: res, CoinsGained: 1, SealsGained: 0, LanternGained: 0);

        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(0,        evt.SlotIndex);
        Assert.Equal(1,        evt.ResourcesGained.MotherOfPearls);
        Assert.Equal(1,        evt.CoinsGained);
        Assert.Equal(0,        evt.SealsGained);
        Assert.Equal(0,        evt.LanternGained);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(TopFloorSlotFilledEvent), evt.EventType);
    }

    [Fact]
    public void LanternEffectFiredEvent_AllPropertiesReadable()
    {
        var evt = new LanternEffectFiredEvent(GameId, PlayerId);
        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(LanternEffectFiredEvent), evt.EventType);
    }

    [Fact]
    public void LanternChainItemAddedEvent_AllPropertiesReadable()
    {
        IReadOnlyList<(string GainType, int Amount)> gains =
            [("Food", 1), ("Coin", 2)];

        var evt = new LanternChainItemAddedEvent(
            GameId, PlayerId,
            SourceCardId:   "src-1",
            SourceCardType: "StewardFloor",
            Gains:          gains);

        Assert.Equal(GameId,        evt.GameId);
        Assert.Equal(PlayerId,      evt.PlayerId);
        Assert.Equal("src-1",       evt.SourceCardId);
        Assert.Equal("StewardFloor", evt.SourceCardType);
        Assert.Equal(2,             evt.Gains.Count);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(LanternChainItemAddedEvent), evt.EventType);
    }

    [Fact]
    public void SeedCardActivatedEvent_AllPropertiesReadable()
    {
        var evt = new SeedCardActivatedEvent(
            GameId, PlayerId,
            ActionCardId: "seed-1",
            ActionType:   "PlayFarm",
            RowIndex:     1);

        Assert.Equal(GameId,    evt.GameId);
        Assert.Equal(PlayerId,  evt.PlayerId);
        Assert.Equal("seed-1",  evt.ActionCardId);
        Assert.Equal("PlayFarm", evt.ActionType);
        Assert.Equal(1,          evt.RowIndex);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(SeedCardActivatedEvent), evt.EventType);
    }

    [Fact]
    public void GameStartedEvent_AllPropertiesReadable()
    {
        var evt = new GameStartedEvent(GameId);
        Assert.Equal(GameId, evt.GameId);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(GameStartedEvent), evt.EventType);
    }

    [Fact]
    public void DieTakenFromBridgeEvent_AllPropertiesReadable()
    {
        var evt = new DieTakenFromBridgeEvent(GameId, PlayerId, BridgeColor.Red, DiePosition.High, 5);
        Assert.Equal(GameId,          evt.GameId);
        Assert.Equal(PlayerId,        evt.PlayerId);
        Assert.Equal(BridgeColor.Red, evt.BridgeColor);
        Assert.Equal(DiePosition.High, evt.Position);
        Assert.Equal(5,               evt.DieValue);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(DieTakenFromBridgeEvent), evt.EventType);
    }

    [Fact]
    public void DiePlacedEvent_AllPropertiesReadable()
    {
        var evt = new DiePlacedEvent(GameId, PlayerId, new WellTarget(), 4, 3);
        Assert.Equal(GameId,        evt.GameId);
        Assert.Equal(PlayerId,      evt.PlayerId);
        Assert.IsType<WellTarget>(evt.Target);
        Assert.Equal(4,             evt.DieValue);
        Assert.Equal(3,             evt.CoinDelta);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(DiePlacedEvent), evt.EventType);
    }

    [Fact]
    public void ResourcesCollectedEvent_AllPropertiesReadable()
    {
        var res = new ResourceBag(Food: 1);
        var evt = new ResourcesCollectedEvent(GameId, PlayerId, res);
        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(1,        evt.Gained.Food);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(ResourcesCollectedEvent), evt.EventType);
    }

    [Fact]
    public void LanternsGainedEvent_AllPropertiesReadable()
    {
        var evt = new LanternsGainedEvent(GameId, PlayerId, 3);
        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.Equal(3,        evt.Amount);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(LanternsGainedEvent), evt.EventType);
    }

    [Fact]
    public void PlayerPassedEvent_AllPropertiesReadable()
    {
        var evt = new PlayerPassedEvent(GameId, PlayerId);
        Assert.Equal(GameId,   evt.GameId);
        Assert.Equal(PlayerId, evt.PlayerId);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(PlayerPassedEvent), evt.EventType);
    }

    [Fact]
    public void RoundEndedEvent_AllPropertiesReadable()
    {
        var evt = new RoundEndedEvent(GameId, RoundNumber: 2);
        Assert.Equal(GameId, evt.GameId);
        Assert.Equal(2,      evt.RoundNumber);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(RoundEndedEvent), evt.EventType);
    }

    [Fact]
    public void GameOverEvent_AllPropertiesReadable()
    {
        IReadOnlyList<PlayerScore> scores = [];
        var evt = new GameOverEvent(GameId, scores);
        Assert.Equal(GameId, evt.GameId);
        Assert.Empty(evt.FinalScores);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(GameOverEvent), evt.EventType);
    }

    [Fact]
    public void AnyResourceChosenEvent_AllPropertiesReadable()
    {
        var evt = new AnyResourceChosenEvent(GameId, PlayerId, ResourceType.Iron);
        Assert.Equal(GameId,           evt.GameId);
        Assert.Equal(PlayerId,         evt.PlayerId);
        Assert.Equal(ResourceType.Iron, evt.Choice);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(AnyResourceChosenEvent), evt.EventType);
    }

    [Fact]
    public void CastlePlayExecutedEvent_AllPropertiesReadable()
    {
        var evt = new CastlePlayExecutedEvent(
            GameId, PlayerId, PlacedAtGate: false,
            AdvancedFrom: CourtierPosition.Gate, LevelsAdvanced: 1);

        Assert.Equal(GameId,                evt.GameId);
        Assert.Equal(PlayerId,              evt.PlayerId);
        Assert.False(evt.PlacedAtGate);
        Assert.Equal(CourtierPosition.Gate, evt.AdvancedFrom);
        Assert.Equal(1,                     evt.LevelsAdvanced);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(nameof(CastlePlayExecutedEvent), evt.EventType);
    }
}
