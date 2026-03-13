using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Tests;

/// <summary>
/// Unit tests for ChooseSeedPairHandler — validation, resource gain per type,
/// lantern chain setup from resource card back, and decree card chain entry.
/// </summary>
public class ChooseSeedPairHandlerTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (Player Alice, Player Bob, GameState State, ChooseSeedPairHandler Handler)
        MakeState(SeedCardPair[]? pairs = null, Action<Player>? setup = null)
    {
        var alice = new Player { Name = "Alice" };
        setup?.Invoke(alice);
        var bob   = new Player { Name = "Bob" };
        var state = new GameState(new List<Player> { alice, bob });
        state.CurrentPhase = Phase.SeedCardSelection;

        foreach (var p in pairs ?? DefaultPairs())
            state.SeedCardPairs.Add(p);

        return (alice, bob, state, new ChooseSeedPairHandler());
    }

    private static SeedCardPair[] DefaultPairs() =>
    [
        MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 1)]),
        MakePair(SeedActionType.PlayCastle, [new SeedResourceGain(CardGainType.Iron, 1)]),
    ];

    private static SeedCardPair MakePair(
        SeedActionType actionType,
        SeedResourceGain[] gains,
        DecreeCard? decree = null)
    {
        var action = new SeedActionCard
        {
            Id         = Guid.NewGuid().ToString(),
            ActionType = actionType,
            Back       = new LanternChainGain(CardGainType.Lantern, 1),
        };
        var resource = new SeedResourceCard
        {
            Id     = Guid.NewGuid().ToString(),
            Gains  = gains.ToList().AsReadOnly(),
            Back   = new LanternChainGain(CardGainType.Food, 1),
            Decree = decree,
        };
        return new SeedCardPair(action, resource);
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_ChooseSeedPairAction_ReturnsTrue()
    {
        var handler = new ChooseSeedPairHandler();
        Assert.True(handler.CanHandle(new ChooseSeedPairAction(Guid.NewGuid(), 0)));
    }

    [Fact]
    public void CanHandle_OtherAction_ReturnsFalse()
    {
        var handler = new ChooseSeedPairHandler();
        Assert.False(handler.CanHandle(new StartGameAction()));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WrongPhase_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        state.CurrentPhase = Phase.WorkerPlacement;
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("seed card selection", result.Reason);
    }

    [Fact]
    public void Validate_UnknownPlayer_Fails()
    {
        var (_, _, state, handler) = MakeState();
        var result = handler.Validate(new ChooseSeedPairAction(Guid.NewGuid(), 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown player", result.Reason);
    }

    [Fact]
    public void Validate_NotActivePlayer_Fails()
    {
        var (_, bob, state, handler) = MakeState();
        var result = handler.Validate(new ChooseSeedPairAction(bob.Id, 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("not this player's turn", result.Reason);
    }

    [Fact]
    public void Validate_PendingAnyResourceChoices_Fails()
    {
        var (alice, _, state, handler) = MakeState(setup: a => a.Pending.AnyResourceChoices = 1);
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("pending resource choices", result.Reason);
    }

    [Fact]
    public void Validate_AlreadyHasSeedCard_Fails()
    {
        var (alice, _, state, handler) = MakeState(setup: a =>
            a.SeedCard = new SeedActionCard { Id = "existing", ActionType = SeedActionType.PlayFarm,
                Back = new LanternChainGain(CardGainType.Lantern, 1) });
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, 0), state);
        Assert.False(result.IsValid);
        Assert.Contains("Already chose", result.Reason);
    }

    [Fact]
    public void Validate_InvalidPairIndex_Negative_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, -1), state);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid pair index", result.Reason);
    }

    [Fact]
    public void Validate_InvalidPairIndex_TooLarge_Fails()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, 99), state);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid pair index", result.Reason);
    }

    [Fact]
    public void Validate_ValidChoice_Succeeds()
    {
        var (alice, _, state, handler) = MakeState();
        var result = handler.Validate(new ChooseSeedPairAction(alice.Id, 0), state);
        Assert.True(result.IsValid);
    }

    // ── Apply — action card ────────────────────────────────────────────────────

    [Fact]
    public void Apply_SetsSeedCard()
    {
        var pair = MakePair(SeedActionType.PlayTrainingGrounds, [new SeedResourceGain(CardGainType.Food, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.NotNull(alice.SeedCard);
        Assert.Equal(SeedActionType.PlayTrainingGrounds, alice.SeedCard.ActionType);
    }

    [Fact]
    public void Apply_RemovesPairFromList()
    {
        var (alice, _, state, handler) = MakeState();
        int countBefore = state.SeedCardPairs.Count;

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(countBefore - 1, state.SeedCardPairs.Count);
    }

    // ── Apply — resource gains ─────────────────────────────────────────────────

    [Fact]
    public void Apply_FoodGain_GrantsFood()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 2)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(2, alice.Resources.Food);
    }

    [Fact]
    public void Apply_IronGain_GrantsIron()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Iron, 3)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(3, alice.Resources.Iron);
    }

    [Fact]
    public void Apply_MotherOfPearlsGain_GrantsMoP()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.MotherOfPearls, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(1, alice.Resources.MotherOfPearls);
    }

    [Fact]
    public void Apply_CoinGain_GrantsCoins()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Coin, 2)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(2, alice.Coins);
    }

    [Fact]
    public void Apply_DaimyoSealGain_GrantsSeals()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.DaimyoSeal, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(1, alice.DaimyoSeals);
    }

    [Fact]
    public void Apply_DaimyoSealGain_CappedAt5()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.DaimyoSeal, 10)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(5, alice.DaimyoSeals);
    }

    [Fact]
    public void Apply_AnyResourceGain_SetsPendingChoices()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.AnyResource, 2)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(2, alice.Pending.AnyResourceChoices);
    }

    [Fact]
    public void Apply_ResourceGain_CappedAt7()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 10)]);
        var (alice, _, state, handler) = MakeState([pair], setup: a => a.Resources = new ResourceBag(6, 0, 0));

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(7, alice.Resources.Food);
    }

    [Fact]
    public void Apply_MultipleGains_AllApplied()
    {
        var pair = MakePair(SeedActionType.PlayFarm,
        [
            new SeedResourceGain(CardGainType.Food,  1),
            new SeedResourceGain(CardGainType.Iron,  1),
            new SeedResourceGain(CardGainType.Coin,  2),
        ]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Equal(1, alice.Resources.Food);
        Assert.Equal(1, alice.Resources.Iron);
        Assert.Equal(2, alice.Coins);
    }

    // ── Apply — lantern chain ─────────────────────────────────────────────────

    [Fact]
    public void Apply_AddsResourceCardBackToLanternChain()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, events);

        Assert.Single(alice.LanternChain);
        var item = alice.LanternChain[0];
        Assert.Equal("ResourceSeed", item.SourceCardType);
        Assert.Equal(CardGainType.Food, item.Gains[0].Type); // back is Food,1 from MakePair
    }

    [Fact]
    public void Apply_EmitsLanternChainItemAddedEvent()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, events);

        Assert.Contains(events, e => e is LanternChainItemAddedEvent lce
            && lce.PlayerId == alice.Id && lce.SourceCardType == "ResourceSeed");
    }

    [Fact]
    public void Apply_WithDecreeCard_AddsDecreeToLanternChain()
    {
        var decree = new DecreeCard { Id = "decree-1", GainType = CardGainType.Iron, Amount = 1 };
        var pair   = MakePair(SeedActionType.PlayFarm,
            [new SeedResourceGain(CardGainType.Food, 1)], decree);
        var (alice, _, state, handler) = MakeState([pair]);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, events);

        // Two chain items: resource back + decree
        Assert.Equal(2, alice.LanternChain.Count);
        var decreeItem = alice.LanternChain[1];
        Assert.Equal("Decree", decreeItem.SourceCardType);
        Assert.Equal("decree-1", decreeItem.SourceCardId);
        Assert.Equal(CardGainType.Iron, decreeItem.Gains[0].Type);
    }

    [Fact]
    public void Apply_WithoutDecreeCard_OnlyOneChainItem()
    {
        var pair = MakePair(SeedActionType.PlayFarm, [new SeedResourceGain(CardGainType.Food, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, []);

        Assert.Single(alice.LanternChain);
    }

    // ── Apply — SeedPairChosenEvent ───────────────────────────────────────────

    [Fact]
    public void Apply_EmitsSeedPairChosenEvent()
    {
        var pair = MakePair(SeedActionType.PlayCastle, [new SeedResourceGain(CardGainType.Food, 1)]);
        var (alice, _, state, handler) = MakeState([pair]);
        var events = new List<IDomainEvent>();

        handler.Apply(new ChooseSeedPairAction(alice.Id, 0), state, events);

        var evt = Assert.Single(events.OfType<SeedPairChosenEvent>());
        Assert.Equal(alice.Id, evt.PlayerId);
        Assert.Equal("PlayCastle", evt.ActionType);
        Assert.Equal(1, evt.ResourcesGained.Food);
    }

    [Fact]
    public void Apply_SecondPairIndex_PicksCorrectPair()
    {
        var pair0 = MakePair(SeedActionType.PlayFarm,           [new SeedResourceGain(CardGainType.Food, 1)]);
        var pair1 = MakePair(SeedActionType.PlayTrainingGrounds, [new SeedResourceGain(CardGainType.Iron, 2)]);
        var (alice, _, state, handler) = MakeState([pair0, pair1]);

        handler.Apply(new ChooseSeedPairAction(alice.Id, 1), state, []);

        Assert.Equal(SeedActionType.PlayTrainingGrounds, alice.SeedCard!.ActionType);
        Assert.Equal(2, alice.Resources.Iron);
    }
}
