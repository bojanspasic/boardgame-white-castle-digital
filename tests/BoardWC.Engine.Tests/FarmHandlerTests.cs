using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for FarmHandler — ApplyCardEffect action-card path and all ApplyNamedAction cases.
/// </summary>
public class FarmHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// Build a two-player GameState with FarmingLands loaded.
    private static (Player Alice, GameState State, FarmHandler Handler)
        MakeState(Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice", PendingFarmActions = 1 };
        setup?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.WorkerPlacement;
        state.Board.SetupFarmingLands(state.Rng);
        return (alice, state, new FarmHandler());
    }

    // ── FarmSkipAction ────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Skip_ClearsPendingActions()
    {
        var (alice, state, handler) = MakeState();
        var events = new List<IDomainEvent>();

        handler.Apply(new FarmSkipAction(alice.Id), state, events);

        Assert.Equal(0, alice.PendingFarmActions);

        var evt = Assert.Single(events.OfType<FarmerPlacedEvent>());
        Assert.Equal(state.GameId, evt.GameId);
        Assert.Equal(alice.Id,     evt.PlayerId);
        Assert.Equal(-1,           evt.AreaIndex);  // -1 = skipped
        Assert.Equal(0,            evt.FoodSpent);
        Assert.Equal(new ResourceBag(), evt.ResourcesGained);
        Assert.Equal(0,            evt.CoinsGained);
        Assert.Equal(0,            evt.SealsGained);
        Assert.Equal(0,            evt.LanternGained);
        Assert.Null(evt.ActionTriggered);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
    }

    // ── ApplyCardEffect — gain-type paths ─────────────────────────────────────

    [Fact]
    public void ApplyCardEffect_GainCard_Food_GrantsFood()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f1", foodCost: 0,
            gainItems: new[] { new FarmGainItem("Food", 3) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (resources, coins, seals, lantern, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(3, resources.Food);
        Assert.Equal(3, player.Resources.Food);
        Assert.Equal(0, coins);
        Assert.Equal(0, seals);
        Assert.Equal(0, lantern);
        Assert.Null(action);
    }

    [Fact]
    public void ApplyCardEffect_GainCard_Iron_GrantsIron()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f2", foodCost: 0,
            gainItems: new[] { new FarmGainItem("Iron", 2) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (resources, _, _, _, _) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(2, resources.Iron);
        Assert.Equal(2, player.Resources.Iron);
    }

    [Fact]
    public void ApplyCardEffect_GainCard_ValueItem_GrantsValueItem()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f3", foodCost: 0,
            gainItems: new[] { new FarmGainItem("ValueItem", 2) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (resources, _, _, _, _) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(2, resources.ValueItem);
        Assert.Equal(2, player.Resources.ValueItem);
    }

    [Fact]
    public void ApplyCardEffect_GainCard_Coin_GrantsCoin()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f4", foodCost: 0,
            gainItems: new[] { new FarmGainItem("Coin", 3) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (_, coins, _, _, _) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(3, coins);
        Assert.Equal(3, player.Coins);
    }

    [Fact]
    public void ApplyCardEffect_GainCard_MonarchialSeal_GrantsSeal()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f5", foodCost: 0,
            gainItems: new[] { new FarmGainItem("MonarchialSeal", 1) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (_, _, seals, _, _) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(1, seals);
        Assert.Equal(1, player.MonarchialSeals);
    }

    [Fact]
    public void ApplyCardEffect_GainCard_Lantern_GrantsLantern()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "f6", foodCost: 0,
            gainItems: new[] { new FarmGainItem("Lantern", 1) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        var (_, _, _, lantern, _) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(1, lantern);
        // player.LanternScore is updated by LanternHelper.Apply inside Apply(), not here
    }

    // ── ApplyCardEffect — action-card path ────────────────────────────────────

    [Fact]
    public void ApplyCardEffect_ActionCard_PlayCastle_SetsCastlePending()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "act-castle", foodCost: 0,
            gainItems: Array.Empty<FarmGainItem>().AsReadOnly(),
            actionDescription: "Play castle",
            victoryPoints: 0);

        var (_, _, _, _, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal("Play castle", action);
        Assert.Equal(1, player.CastlePlaceRemaining);
        Assert.Equal(1, player.CastleAdvanceRemaining);
    }

    [Fact]
    public void ApplyCardEffect_ActionCard_PlayTrainingGrounds_SetsTgPending()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "act-tg", foodCost: 0,
            gainItems: Array.Empty<FarmGainItem>().AsReadOnly(),
            actionDescription: "Play training grounds",
            victoryPoints: 0);

        var (_, _, _, _, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal("Play training grounds", action);
        Assert.Equal(1, player.PendingTrainingGroundsActions);
    }

    [Fact]
    public void ApplyCardEffect_ActionCard_Gain3Coins_GrantsCoins()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "act-coins", foodCost: 0,
            gainItems: Array.Empty<FarmGainItem>().AsReadOnly(),
            actionDescription: "Gain 3 coins",
            victoryPoints: 0);

        var (_, coins, _, _, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal("Gain 3 coins", action);
        Assert.Equal(3, coins);
        Assert.Equal(3, player.Coins);
    }

    [Fact]
    public void ApplyCardEffect_ActionCard_Gain1MonarchialSeal_GrantsSeal()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "act-seal", foodCost: 0,
            gainItems: Array.Empty<FarmGainItem>().AsReadOnly(),
            actionDescription: "Gain 1 monarchial seal",
            victoryPoints: 0);

        var (_, _, seals, _, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal("Gain 1 monarchial seal", action);
        Assert.Equal(1, seals);
        Assert.Equal(1, player.MonarchialSeals);
    }

    [Fact]
    public void ApplyCardEffect_ActionCard_Gain1Lantern_GrantsLantern()
    {
        var player = new Player { Name = "Alice" };
        var card   = new FarmCard(
            "act-lantern", foodCost: 0,
            gainItems: Array.Empty<FarmGainItem>().AsReadOnly(),
            actionDescription: "Gain 1 lantern",
            victoryPoints: 0);

        var (_, _, _, lantern, action) = FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal("Gain 1 lantern", action);
        Assert.Equal(1, lantern);
    }

    // ── Apply() via game state — full Apply path with FarmerPlacedEvent ───────

    [Fact]
    public void Apply_PlaceFarmer_EmitsFarmerPlacedEvent()
    {
        var (alice, state, handler) = MakeState(p =>
        {
            p.Resources = new ResourceBag(Food: 10);
        });

        // Find a field with a gain-type card that doesn't cost more food than we have
        var field = state.Board.FarmingLands.GetField(BridgeColor.Red, isInland: true);
        var events = new List<IDomainEvent>();

        // Skip if food cost exceeds available food
        if (field.Card.FoodCost > alice.Resources.Food)
            return;

        handler.Apply(
            new PlaceFarmerAction(alice.Id, BridgeColor.Red, IsInland: true),
            state, events);

        var evt = Assert.Single(events.OfType<FarmerPlacedEvent>());
        Assert.Equal(state.GameId,   evt.GameId);
        Assert.Equal(alice.Id,       evt.PlayerId);
        Assert.Equal(BridgeColor.Red, evt.BridgeColor);
        Assert.True(evt.IsInland);
        Assert.Equal(0,              evt.AreaIndex);       // Always 0 in Apply
        Assert.True(evt.FoodSpent >= 0);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        _ = evt.ResourcesGained;
        _ = evt.CoinsGained;
        _ = evt.SealsGained;
        _ = evt.LanternGained;
        _ = evt.ActionTriggered;
    }

    // ── Validation paths ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoPendingFarmAction_Fails()
    {
        var (alice, state, handler) = MakeState(p => p.PendingFarmActions = 0);

        var result = handler.Validate(new PlaceFarmerAction(alice.Id, BridgeColor.Red, true), state);

        Assert.False(result.IsValid);
        Assert.Contains("farm action", result.Reason);
    }

    [Fact]
    public void Validate_NoFarmersAvailable_Fails()
    {
        var (alice, state, handler) = MakeState(p =>
        {
            p.FarmersAvailable = 0;
            p.Resources        = new ResourceBag(Food: 10);
        });

        var result = handler.Validate(new PlaceFarmerAction(alice.Id, BridgeColor.Red, true), state);

        Assert.False(result.IsValid);
        Assert.Contains("farmer", result.Reason);
    }

    [Fact]
    public void Validate_AlreadyHasFarmer_Fails()
    {
        var (alice, state, handler) = MakeState(p =>
        {
            p.Resources = new ResourceBag(Food: 10);
        });

        // Manually place alice's farmer on the field
        var field = state.Board.FarmingLands.GetField(BridgeColor.Red, isInland: true);
        field.AddFarmer(alice.Name);

        var result = handler.Validate(new PlaceFarmerAction(alice.Id, BridgeColor.Red, true), state);

        Assert.False(result.IsValid);
        Assert.Contains("farmer", result.Reason);
    }

    // ── MonarchialSeal cap ────────────────────────────────────────────────────

    [Fact]
    public void ApplyCardEffect_GainCard_MonarchialSeal_CappedAtFive()
    {
        var player = new Player { Name = "Alice", MonarchialSeals = 4 };
        var card   = new FarmCard(
            "f-cap", foodCost: 0,
            gainItems: new[] { new FarmGainItem("MonarchialSeal", 3) }.AsReadOnly(),
            actionDescription: "",
            victoryPoints: 0);

        FarmHandler.ApplyCardEffect(card, player);

        Assert.Equal(5, player.MonarchialSeals); // capped
    }
}
