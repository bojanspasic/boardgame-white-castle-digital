using BoardWC.Console.UI;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;

namespace BoardWC.Console.Tests;

public class GameScreenRendererTests
{
    // ── Fake console ──────────────────────────────────────────────────────────

    private sealed class FakeConsole : IConsoleIO
    {
        public int WindowWidth  { get; set; } = 80;
        public int WindowHeight { get; set; } = 25;
        public List<string> Written { get; } = new();
        public List<(string Text, ConsoleColor Color)> Colored { get; } = new();

        public void SetCursorPosition(int left, int top) { }
        public void Clear() { }
        public void Write(string text)                        => Written.Add(text);
        public void WriteColored(string text, ConsoleColor c) => Colored.Add((text, c));
        public void WriteLine(string text)                    => Written.Add(text + "\n");
        public ConsoleKeyInfo ReadKey(bool intercept)         => throw new NotSupportedException();
    }

    // ── Helper: started 2-player game ─────────────────────────────────────────

    private static GameStateSnapshot StartedGame()
    {
        var engine = GameEngineFactory.Create(
            [new PlayerSetup("Alice", PlayerColor.White, false, null),
             new PlayerSetup("Bob",   PlayerColor.Black, false, null)]);
        engine.ProcessAction(new StartGameAction());
        while (engine.GetCurrentState().CurrentPhase == Phase.SeedCardSelection)
        {
            var state = engine.GetCurrentState();
            var pid   = state.Players[state.ActivePlayerIndex].Id;
            var legal = engine.GetLegalActions(pid);
            var seed  = legal.OfType<ChooseSeedPairAction>().FirstOrDefault();
            if (seed is not null)
                engine.ProcessAction(seed);
            else
                engine.ProcessAction(new ChooseResourceAction(pid, ResourceType.Food));
        }
        return engine.GetCurrentState();
    }

    private static string ExpectedTokenAbbreviation(TokenSnapshot t)
    {
        var side = t.IsResourceSideUp ? t.ResourceSide.ToString() : "Seal";
        return $"{t.DieColor.ToString()[0]}{side[0]}";
    }

    // ── Helper: engine + place a die ──────────────────────────────────────────

    private static IGameEngine StartedEngine()
    {
        var engine = GameEngineFactory.Create(
            [new PlayerSetup("Alice", PlayerColor.White, false, null),
             new PlayerSetup("Bob",   PlayerColor.Black, false, null)]);
        engine.ProcessAction(new StartGameAction());
        while (engine.GetCurrentState().CurrentPhase == Phase.SeedCardSelection)
        {
            var s   = engine.GetCurrentState();
            var pid = s.Players[s.ActivePlayerIndex].Id;
            var legal = engine.GetLegalActions(pid);
            var seed = legal.OfType<ChooseSeedPairAction>().FirstOrDefault();
            if (seed is not null) engine.ProcessAction(seed);
            else engine.ProcessAction(new ChooseResourceAction(pid, ResourceType.Food));
        }
        return engine;
    }

    /// Takes a die then places it at the first legal target matching <paramref name="filter"/>.
    /// Returns the snapshot immediately after placement (before pending choices resolved).
    private static GameStateSnapshot? StateAfterPlace(Func<PlaceDieAction, bool> filter)
    {
        var engine = StartedEngine();
        var state  = engine.GetCurrentState();
        var pid    = state.Players[state.ActivePlayerIndex].Id;
        var take   = engine.GetLegalActions(pid).OfType<TakeDieFromBridgeAction>().First();
        engine.ProcessAction(take);
        var place = engine.GetLegalActions(pid).OfType<PlaceDieAction>().FirstOrDefault(filter);
        if (place == null) return null;
        engine.ProcessAction(place);
        return engine.GetCurrentState();
    }

    /// Drains pending choices for the active player until take-die or pass becomes available.
    private static void DrainPendingState(IGameEngine engine)
    {
        for (int safety = 0; safety < 50; safety++)
        {
            var state = engine.GetCurrentState();
            if (state.CurrentPhase != Phase.WorkerPlacement) break;
            var pid   = state.Players[state.ActivePlayerIndex].Id;
            var legal = engine.GetLegalActions(pid).ToList();
            if (legal.Any(a => a is TakeDieFromBridgeAction || a is PassAction)) break;
            if (legal.Count == 0) break;
            engine.ProcessAction(legal[0]);
        }
    }

    /// Plays n complete turns (take + place + drain pending). Returns state after all turns.
    private static GameStateSnapshot StateAfterNTurns(int n)
    {
        var engine = StartedEngine();
        for (int i = 0; i < n; i++)
        {
            var state = engine.GetCurrentState();
            if (state.CurrentPhase != Phase.WorkerPlacement) break;
            var pid  = state.Players[state.ActivePlayerIndex].Id;
            var take = engine.GetLegalActions(pid).OfType<TakeDieFromBridgeAction>().FirstOrDefault();
            if (take == null) break;
            engine.ProcessAction(take);
            // After taking, die is in hand — place it directly (do NOT drain pending here)
            state = engine.GetCurrentState();
            pid   = state.Players[state.ActivePlayerIndex].Id;
            var place = engine.GetLegalActions(pid).OfType<PlaceDieAction>().First();
            engine.ProcessAction(place);
            DrainPendingState(engine);
        }
        return engine.GetCurrentState();
    }

    /// Takes a die from every bridge at least once to deplete Middle dice and potentially null High/Low.
    private static GameStateSnapshot StateWithDepletedBridges()
    {
        // Play enough turns that at least one bridge has Middle.Count == 0
        // After 1 take from a bridge: Middle becomes empty for that bridge
        return StateAfterNTurns(1);
    }

    /// After 2 takes from the first bridge (Red), High becomes null.
    private static GameStateSnapshot StateWithNullHighBridge() => StateAfterNTurns(2);

    /// After 3 takes, Low of the first bridge also becomes null.
    private static GameStateSnapshot StateWithNullLowBridge() => StateAfterNTurns(3);

    /// Retries fresh engines until a bridge has a High die of value 6,
    /// takes that die and places it in the matching personal domain row (compareValue=6, needs exactly 6).
    private static GameStateSnapshot? StateAfterPDPlace()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var engine = StartedEngine();
            var state  = engine.GetCurrentState();
            var pid    = state.Players[state.ActivePlayerIndex].Id;
            var bridge6 = state.Board.Bridges.FirstOrDefault(b => b.High?.Value == 6);
            if (bridge6 == null) continue;
            engine.ProcessAction(new TakeDieFromBridgeAction(pid, bridge6.Color, DiePosition.High));
            var place = engine.GetLegalActions(pid).OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is PersonalDomainTarget);
            if (place == null) continue;
            engine.ProcessAction(place);
            DrainPendingState(engine);
            return engine.GetCurrentState();
        }
        return null;
    }

    /// Retries until a High die ≥ 5 is available (Outside compare value = 5), places it at an outside slot.
    private static GameStateSnapshot? StateAfterOutsidePlace()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var engine = StartedEngine();
            var state  = engine.GetCurrentState();
            var pid    = state.Players[state.ActivePlayerIndex].Id;
            var bridge5 = state.Board.Bridges.FirstOrDefault(b => b.High?.Value >= 5);
            if (bridge5 == null) continue;
            engine.ProcessAction(new TakeDieFromBridgeAction(pid, bridge5.Color, DiePosition.High));
            var place = engine.GetLegalActions(pid).OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is OutsideSlotTarget);
            if (place == null) continue;
            engine.ProcessAction(place);
            DrainPendingState(engine);
            return engine.GetCurrentState();
        }
        return null;
    }

    /// Both players place at the well; returns state where the well has 2 placed dice, or null if not achievable.
    private static GameStateSnapshot? StateWithTwoWellDice()
    {
        var engine = StartedEngine();
        for (int i = 0; i < 2; i++)
        {
            var state = engine.GetCurrentState();
            if (state.CurrentPhase != Phase.WorkerPlacement) return null;
            var pid  = state.Players[state.ActivePlayerIndex].Id;
            var take = engine.GetLegalActions(pid).OfType<TakeDieFromBridgeAction>().FirstOrDefault();
            if (take == null) return null;
            engine.ProcessAction(take);
            // Die is in hand — place it at well (do NOT drain pending before placing)
            state = engine.GetCurrentState();
            pid   = state.Players[state.ActivePlayerIndex].Id;
            var well = engine.GetLegalActions(pid).OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is WellTarget);
            if (well == null) return null;
            engine.ProcessAction(well);
            DrainPendingState(engine);
        }
        return engine.GetCurrentState();
    }

    // ── Fake-state helpers (build minimal snapshots to exercise uncovered branches) ──

    private static GameStateSnapshot FakeStateWithWellNoTokens()
    {
        var s = StartedGame();
        var fakeWell = new WellSnapshot(
            new DicePlaceholderSnapshot(1, true,
                Array.Empty<DieSnapshot>().ToList().AsReadOnly(),
                Array.Empty<TokenSnapshot>().ToList().AsReadOnly(),
                null));
        return s with { Board = s.Board with { Well = fakeWell } };
    }

    private static GameStateSnapshot FakeStateWithTopFloorNoGains()
    {
        var s = StartedGame();
        var fakeSlot = new TopFloorSlotSnapshot(0,
            Array.Empty<CardGainItemSnapshot>().ToList().AsReadOnly(), null);
        var fakeTop  = new TopFloorRoomSnapshot("t_fake",
            new[] { fakeSlot }.ToList().AsReadOnly());
        return s with { Board = s.Board with { Castle = s.Board.Castle with { TopFloor = fakeTop } } };
    }

    private static GameStateSnapshot FakeStateWithTopFloorTwoGains()
    {
        var s = StartedGame();
        var gains = new[] {
            new CardGainItemSnapshot("Food", 1),
            new CardGainItemSnapshot("Iron", 2) }.ToList().AsReadOnly();
        var fakeSlot = new TopFloorSlotSnapshot(0, gains, null);
        var fakeTop  = new TopFloorRoomSnapshot("t_fake",
            new[] { fakeSlot }.ToList().AsReadOnly());
        return s with { Board = s.Board with { Castle = s.Board.Castle with { TopFloor = fakeTop } } };
    }

    private static GameStateSnapshot FakeStateWithTopFloorOccupied()
    {
        var s = StartedGame();
        var gains = new[] { new CardGainItemSnapshot("Food", 1) }.ToList().AsReadOnly();
        var fakeSlot = new TopFloorSlotSnapshot(0, gains, "TestCourtier");
        var fakeTop  = new TopFloorRoomSnapshot("t_fake",
            new[] { fakeSlot }.ToList().AsReadOnly());
        return s with { Board = s.Board with { Castle = s.Board.Castle with { TopFloor = fakeTop } } };
    }

    private static GameStateSnapshot FakeStateWithCastleRoomTwoDice()
    {
        var s      = StartedGame();
        var floor0 = s.Board.Castle.Floors[0];
        var fakeRoom = floor0[0] with
        {
            PlacedDice = new[] {
                new DieSnapshot(3, BridgeColor.Red),
                new DieSnapshot(4, BridgeColor.White) }.ToList().AsReadOnly()
        };
        IReadOnlyList<DicePlaceholderSnapshot> fakeFloor0 =
            new[] { fakeRoom }.Concat(floor0.Skip(1)).ToList();
        IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> fakeFloors =
            new[] { fakeFloor0 }.Concat(s.Board.Castle.Floors.Skip(1)).ToList();
        return s with { Board = s.Board with { Castle = s.Board.Castle with { Floors = fakeFloors } } };
    }

    private static GameStateSnapshot FakeStateWithCastleRoomNoTokens()
    {
        var s      = StartedGame();
        var floor0 = s.Board.Castle.Floors[0];
        var fakeRoom = floor0[0] with
        {
            Tokens = Array.Empty<TokenSnapshot>().ToList().AsReadOnly()
        };
        IReadOnlyList<DicePlaceholderSnapshot> fakeFloor0 =
            new[] { fakeRoom }.Concat(floor0.Skip(1)).ToList();
        IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> fakeFloors =
            new[] { fakeFloor0 }.Concat(s.Board.Castle.Floors.Skip(1)).ToList();
        return s with { Board = s.Board with { Castle = s.Board.Castle with { Floors = fakeFloors } } };
    }

    private static GameStateSnapshot FakeStateWithCastleRoomNullCard()
    {
        var s      = StartedGame();
        var floor0 = s.Board.Castle.Floors[0];
        var fakeRoom = floor0[0] with { Card = null };
        IReadOnlyList<DicePlaceholderSnapshot> fakeFloor0 =
            new[] { fakeRoom }.Concat(floor0.Skip(1)).ToList();
        IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> fakeFloors =
            new[] { fakeFloor0 }.Concat(s.Board.Castle.Floors.Skip(1)).ToList();
        return s with { Board = s.Board with { Castle = s.Board.Castle with { Floors = fakeFloors } } };
    }

    private static GameStateSnapshot FakeStateWithCastleRoomTwoGainField()
    {
        var s      = StartedGame();
        var floor0 = s.Board.Castle.Floors[0];
        var room0  = floor0[0];
        var twoGainField = new CardFieldSnapshot(
            IsGain: true,
            Gains: new[] { new CardGainItemSnapshot("Food", 1), new CardGainItemSnapshot("Iron", 2) }
                .ToList().AsReadOnly(),
            ActionDescription: null, ActionCost: null);
        var fakeCard  = room0.Card != null
            ? room0.Card with { Fields = new[] { twoGainField }.ToList().AsReadOnly() }
            : new RoomCardSnapshot("fake_card", new[] { twoGainField }.ToList().AsReadOnly(), null);
        var fakeRoom  = room0 with { Card = fakeCard };
        IReadOnlyList<DicePlaceholderSnapshot> fakeFloor0 =
            new[] { fakeRoom }.Concat(floor0.Skip(1)).ToList();
        IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> fakeFloors =
            new[] { fakeFloor0 }.Concat(s.Board.Castle.Floors.Skip(1)).ToList();
        return s with { Board = s.Board with { Castle = s.Board.Castle with { Floors = fakeFloors } } };
    }

    private static GameStateSnapshot FakeStateWithTgAreaTwoGains()
    {
        var s = StartedGame();
        var fakeArea = new TgAreaSnapshot(0, 1,
            new[] { new CardGainItemSnapshot("Food", 2), new CardGainItemSnapshot("Iron", 1) }
                .ToList().AsReadOnly(),
            "", Array.Empty<string>().ToList().AsReadOnly());
        IReadOnlyList<TgAreaSnapshot> fakeAreas =
            new[] { fakeArea }.Concat(s.Board.TrainingGrounds.Areas.Skip(1)).ToList();
        return s with { Board = s.Board with {
            TrainingGrounds = new TrainingGroundsSnapshot(fakeAreas) } };
    }

    private static GameStateSnapshot FakeStateWithTgAreaSoldier()
    {
        var s    = StartedGame();
        var area = s.Board.TrainingGrounds.Areas[0];
        var fakeArea = area with { SoldierOwners = new[] { "Alice" }.ToList().AsReadOnly() };
        IReadOnlyList<TgAreaSnapshot> fakeAreas =
            new[] { fakeArea }.Concat(s.Board.TrainingGrounds.Areas.Skip(1)).ToList();
        return s with { Board = s.Board with {
            TrainingGrounds = new TrainingGroundsSnapshot(fakeAreas) } };
    }

    private static GameStateSnapshot FakeStateWithFarmFieldTwoGains()
    {
        var s     = StartedGame();
        var field = s.Board.FarmingLands.Fields[0];
        var fakeField = field with
        {
            GainItems = new[] {
                new CardGainItemSnapshot("Food", 3),
                new CardGainItemSnapshot("Iron", 2) }.ToList().AsReadOnly()
        };
        IReadOnlyList<FarmFieldSnapshot> fakeFields =
            new[] { fakeField }.Concat(s.Board.FarmingLands.Fields.Skip(1)).ToList();
        return s with { Board = s.Board with {
            FarmingLands = new FarmingLandsSnapshot(fakeFields) } };
    }

    private static GameStateSnapshot FakeStateWithFarmFieldFarmer()
    {
        var s     = StartedGame();
        var field = s.Board.FarmingLands.Fields[0];
        var fakeField = field with { FarmerOwners = new[] { "Alice" }.ToList().AsReadOnly() };
        IReadOnlyList<FarmFieldSnapshot> fakeFields =
            new[] { fakeField }.Concat(s.Board.FarmingLands.Fields.Skip(1)).ToList();
        return s with { Board = s.Board with {
            FarmingLands = new FarmingLandsSnapshot(fakeFields) } };
    }

    private static GameStateSnapshot FakeStateWithTgAreaTwoSoldiers()
    {
        var s    = StartedGame();
        var area = s.Board.TrainingGrounds.Areas[0];
        var fakeArea = area with { SoldierOwners = new[] { "Alice", "Bob" }.ToList().AsReadOnly() };
        IReadOnlyList<TgAreaSnapshot> fakeAreas =
            new[] { fakeArea }.Concat(s.Board.TrainingGrounds.Areas.Skip(1)).ToList();
        return s with { Board = s.Board with {
            TrainingGrounds = new TrainingGroundsSnapshot(fakeAreas) } };
    }

    private static GameStateSnapshot FakeStateWithFarmFieldTwoFarmers()
    {
        var s     = StartedGame();
        var field = s.Board.FarmingLands.Fields[0];
        var fakeField = field with { FarmerOwners = new[] { "Alice", "Bob" }.ToList().AsReadOnly() };
        IReadOnlyList<FarmFieldSnapshot> fakeFields =
            new[] { fakeField }.Concat(s.Board.FarmingLands.Fields.Skip(1)).ToList();
        return s with { Board = s.Board with {
            FarmingLands = new FarmingLandsSnapshot(fakeFields) } };
    }

    private static GameStateSnapshot FakeStateWithCastleRoomActionFieldWithGains()
    {
        // IsGain=false, Gains=non-null, ActionDescription="TestAction"
        // Kills: f.IsGain || f.Gains != null mutation on L238 (original: &&)
        var s      = StartedGame();
        var floor0 = s.Board.Castle.Floors[0];
        var room0  = floor0[0];
        var mixedField = new CardFieldSnapshot(
            IsGain: false,
            Gains: new[] { new CardGainItemSnapshot("Food", 9) }.ToList().AsReadOnly(),
            ActionDescription: "TestAction",
            ActionCost: null);
        var fakeCard  = new RoomCardSnapshot("fake_card",
            new[] { mixedField }.ToList().AsReadOnly(), null);
        var fakeRoom  = room0 with { Card = fakeCard };
        IReadOnlyList<DicePlaceholderSnapshot> fakeFloor0 =
            new[] { fakeRoom }.Concat(floor0.Skip(1)).ToList();
        IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>> fakeFloors =
            new[] { fakeFloor0 }.Concat(s.Board.Castle.Floors.Skip(1)).ToList();
        return s with { Board = s.Board with { Castle = s.Board.Castle with { Floors = fakeFloors } } };
    }

    private static GameStateSnapshot FakeStateWithFarmFieldNoGains()
    {
        var s     = StartedGame();
        var field = s.Board.FarmingLands.Fields[0];
        var fakeField = field with
        {
            GainItems        = Array.Empty<CardGainItemSnapshot>().ToList().AsReadOnly(),
            ActionDescription = "TestFarmAction"
        };
        IReadOnlyList<FarmFieldSnapshot> fakeFields =
            new[] { fakeField }.Concat(s.Board.FarmingLands.Fields.Skip(1)).ToList();
        return s with { Board = s.Board with {
            FarmingLands = new FarmingLandsSnapshot(fakeFields) } };
    }

    private static GameStateSnapshot FakeStateWithBridgeTwoMiddleDice()
    {
        var s      = StartedGame();
        var bridge = s.Board.Bridges[0];
        var fakeBridge = bridge with
        {
            Middle = new[] {
                new DieSnapshot(3, bridge.Color),
                new DieSnapshot(4, bridge.Color) }.ToList().AsReadOnly()
        };
        IReadOnlyList<BridgeSnapshot> fakeBridges =
            new[] { fakeBridge }.Concat(s.Board.Bridges.Skip(1)).ToList();
        return s with { Board = s.Board with { Bridges = fakeBridges } };
    }

    private static GameStateSnapshot FakeStateWithOutsideSlotTwoDice()
    {
        var s    = StartedGame();
        var slot = s.Board.Outside.Slots[0];
        var fakeSlot = slot with
        {
            PlacedDice = new[] {
                new DieSnapshot(3, BridgeColor.Red),
                new DieSnapshot(5, BridgeColor.White) }.ToList().AsReadOnly()
        };
        IReadOnlyList<DicePlaceholderSnapshot> fakeSlots =
            new[] { fakeSlot }.Concat(s.Board.Outside.Slots.Skip(1)).ToList();
        return s with { Board = s.Board with { Outside = new OutsideSnapshot(fakeSlots) } };
    }

    private static GameStateSnapshot FakeStateWithPdRowTwoUncoveredSpots()
    {
        var s      = StartedGame();
        var player = s.Players[s.ActivePlayerIndex];
        var row0   = player.PersonalDomainRows[0];
        var fakeSpots = new[]
        {
            new PersonalDomainSpotSnapshot(ResourceType.Food,           1, true),
            new PersonalDomainSpotSnapshot(ResourceType.Iron,           2, true),
            new PersonalDomainSpotSnapshot(ResourceType.MotherOfPearls, 1, false),
            new PersonalDomainSpotSnapshot(ResourceType.Food,           1, false),
            new PersonalDomainSpotSnapshot(ResourceType.Food,           1, false),
        }.ToList().AsReadOnly();
        var fakeRow = row0 with { Spots = fakeSpots };
        IReadOnlyList<PersonalDomainRowSnapshot> fakeRows =
            new[] { fakeRow }.Concat(player.PersonalDomainRows.Skip(1)).ToList();
        var fakePlayer = player with { PersonalDomainRows = fakeRows };
        IReadOnlyList<PlayerSnapshot> fakePlayers =
            s.Players.Select((p, i) => i == s.ActivePlayerIndex ? fakePlayer : p).ToList();
        return s with { Players = fakePlayers };
    }

    // ── RenderHeader tests ────────────────────────────────────────────────────

    [Fact]
    public void RenderHeader_WritesRoundLine()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        Assert.Contains(console.Colored, e => e.Text.Contains($"Round {state.CurrentRound}/{state.MaxRounds}"));
    }

    [Fact]
    public void RenderHeader_WritesRoundLineInYellow()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var roundEntry = console.Colored.First(e => e.Text.StartsWith("Round"));
        Assert.Equal(ConsoleColor.Yellow, roundEntry.Color);
    }

    [Fact]
    public void RenderHeader_ActivePlayerHasArrow()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var activePlayer = state.Players[state.ActivePlayerIndex];
        var activeEntry  = console.Colored.First(e => e.Text.Contains(activePlayer.Name));
        Assert.StartsWith("▲", activeEntry.Text);
    }

    [Fact]
    public void RenderHeader_InactivePlayerHasNoArrow()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var inactiveIndex  = state.ActivePlayerIndex == 0 ? 1 : 0;
        var inactivePlayer = state.Players[inactiveIndex];
        var inactiveEntry  = console.Colored.First(e => e.Text.Contains(inactivePlayer.Name));
        Assert.StartsWith("  ", inactiveEntry.Text);
    }

    [Fact]
    public void RenderHeader_PlayerColorUsed()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var player0      = state.Players[0];
        var player0Entry = console.Colored.First(e => e.Text.Contains(player0.Name));
        Assert.Equal(PlayerColors.Colors[0], player0Entry.Color);
    }

    [Fact]
    public void RenderHeader_FallbackGrayWhenNoColor()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, []);
        var playerEntry = console.Colored.First(e => e.Text.Contains(state.Players[0].Name));
        Assert.Equal(ConsoleColor.Gray, playerEntry.Color);
    }

    [Fact]
    public void RenderHeader_ShowsVpAndInfluence()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var activePlayer = state.Players[state.ActivePlayerIndex];
        var playerEntry  = console.Colored.First(e => e.Text.Contains(activePlayer.Name));
        Assert.Contains("VP:", playerEntry.Text);
        Assert.Contains("Inf:", playerEntry.Text);
    }

    [Fact]
    public void RenderHeader_ExactVpFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var p     = state.Players[state.ActivePlayerIndex];
        var entry = console.Colored.First(e => e.Text.Contains(p.Name));
        Assert.Contains($"VP:{p.LanternScore,3}", entry.Text);
    }

    [Fact]
    public void RenderHeader_ExactInfFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var p     = state.Players[state.ActivePlayerIndex];
        var entry = console.Colored.First(e => e.Text.Contains(p.Name));
        Assert.Contains($"Inf:{p.Influence,2}", entry.Text);
    }

    [Fact]
    public void RenderHeader_AllPlayersRendered()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        // 1 round entry + one entry per player
        Assert.Equal(1 + state.Players.Count, console.Colored.Count);
    }

    [Fact]
    public void RenderHeader_ActivePlayerArrowPrefixThenSpace()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var p     = state.Players[state.ActivePlayerIndex];
        var entry = console.Colored.First(e => e.Text.Contains(p.Name));
        Assert.StartsWith($"▲ {p.Name}", entry.Text.TrimEnd());
    }

    [Fact]
    public void RenderHeader_InactivePlayerTwoSpaceThenName()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var inactiveIndex  = state.ActivePlayerIndex == 0 ? 1 : 0;
        var p = state.Players[inactiveIndex];
        var entry = console.Colored.First(e => e.Text.Contains(p.Name));
        Assert.StartsWith($"  {p.Name}", entry.Text.TrimEnd());
    }

    [Fact]
    public void RenderHeader_Player1ColorFromArray()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderHeader(console, state, PlayerColors.Colors);
        var p1Entry = console.Colored.First(e => e.Text.Contains(state.Players[1].Name));
        Assert.Equal(PlayerColors.Colors[1], p1Entry.Color);
    }

    // ── RenderHotkeyBar tests ─────────────────────────────────────────────────

    [Fact]
    public void RenderHotkeyBar_ActiveAreaMarked()
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, GameAreaView.Castle);
        var line = console.Written[0];
        Assert.Contains("[1]*Castle", line);
    }

    [Fact]
    public void RenderHotkeyBar_InactiveAreaNoMark()
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, GameAreaView.Castle);
        var line = console.Written[0];
        Assert.Contains("[2] Training", line);
        Assert.Contains("[3] Bridges", line);
        Assert.Contains("[4] Well/Outside", line);
        Assert.Contains("[5] Personal Domain", line);
    }

    [Fact]
    public void RenderHotkeyBar_AllFiveAreasPresent()
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, GameAreaView.TrainingGrounds);
        var line = console.Written[0];
        Assert.Contains("[1]", line);
        Assert.Contains("[2]", line);
        Assert.Contains("[3]", line);
        Assert.Contains("[4]", line);
        Assert.Contains("[5]", line);
    }

    [Theory]
    [InlineData(1, "[1]*Castle")]
    [InlineData(2, "[2]*Training")]
    [InlineData(3, "[3]*Bridges")]
    [InlineData(4, "[4]*Well/Outside")]
    [InlineData(5, "[5]*Personal Domain")]
    public void RenderHotkeyBar_EachViewAsActive_MarkedWithStar(int activeInt, string expected)
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, (GameAreaView)activeInt);
        Assert.Contains(expected, console.Written[0]);
    }

    [Theory]
    [InlineData(2, "[1] Castle")]
    [InlineData(1, "[2] Training")]
    [InlineData(1, "[3] Bridges")]
    [InlineData(1, "[4] Well/Outside")]
    [InlineData(1, "[5] Personal Domain")]
    public void RenderHotkeyBar_InactiveViewHasSpaceNotStar(int activeInt, string expected)
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, (GameAreaView)activeInt);
        Assert.Contains(expected, console.Written[0]);
    }

    [Fact]
    public void RenderHotkeyBar_PartsJoinedWithTwoSpaces()
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, GameAreaView.Castle);
        // Verify double-space separator between adjacent parts
        Assert.Contains("[1]*Castle  [2] Training", console.Written[0]);
    }

    [Fact]
    public void RenderHotkeyBar_ProducesExactlyOneLine()
    {
        var console = new FakeConsole();
        GameScreenRenderer.RenderHotkeyBar(console, GameAreaView.Castle);
        Assert.Single(console.Written);
    }

    // ── TryParseHotkey tests ──────────────────────────────────────────────────

    [Theory]
    [InlineData('1', 1)]
    [InlineData('2', 2)]
    [InlineData('3', 3)]
    [InlineData('4', 4)]
    [InlineData('5', 5)]
    public void TryParseHotkey_ValidChars_ReturnsTrueWithCorrectView(char ch, int expectedInt)
    {
        Assert.True(GameScreenRenderer.TryParseHotkey(ch, out var view));
        Assert.Equal(expectedInt, (int)view);
    }

    [Theory]
    [InlineData('0')]
    [InlineData('6')]
    [InlineData('a')]
    public void TryParseHotkey_InvalidChar_ReturnsFalse(char ch)
    {
        Assert.False(GameScreenRenderer.TryParseHotkey(ch, out _));
    }

    // ── RenderArea routing tests ──────────────────────────────────────────────

    [Fact]
    public void RenderArea_Castle_WritesTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("CASTLE"));
    }

    [Fact]
    public void RenderArea_TrainingGrounds_WritesTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("TRAINING GROUNDS"));
    }

    [Fact]
    public void RenderArea_BridgesFarmlands_WritesBridgesTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("BRIDGES"));
    }

    [Fact]
    public void RenderArea_WellOutside_WritesWellTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("WELL"));
    }

    [Fact]
    public void RenderArea_PersonalDomain_WritesPlayerName()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var activeId = state.Players[state.ActivePlayerIndex].Id;
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, activeId);
        Assert.Contains(console.Written, l => l.Contains("PERSONAL DOMAIN"));
    }

    // ── RenderCastle precision tests ──────────────────────────────────────────

    [Fact]
    public void RenderCastle_WritesFloorInfo()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.True(allLines.Contains("Floor") || allLines.Contains("CASTLE"));
    }

    [Fact]
    public void RenderCastle_TopFloorCardId()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains($"[{state.Board.Castle.TopFloor.CardId}]"));
    }

    [Fact]
    public void RenderCastle_TopFloorHeaderFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains($"Top Floor [{state.Board.Castle.TopFloor.CardId}]:"));
    }

    [Fact]
    public void RenderCastle_TopFloorSlotIndices()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var slots = state.Board.Castle.TopFloor.Slots;
        for (int i = 0; i < slots.Count; i++)
            Assert.Contains(console.Written, l => l.Contains($"Slot {i}:"));
    }

    [Fact]
    public void RenderCastle_TopFloorSlotOccupantEmpty_AtStart()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        // No courtiers placed at start, so "(empty)" appears in slot occupant
        var slots = state.Board.Castle.TopFloor.Slots;
        if (slots.Any(s => s.OccupantName == null))
            Assert.Contains(console.Written, l => l.Contains("(empty)"));
    }

    [Fact]
    public void RenderCastle_TopFloorSlotGainsFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var slotWithGains = state.Board.Castle.TopFloor.Slots.FirstOrDefault(s => s.Gains.Count > 0);
        if (slotWithGains != null)
        {
            var g = slotWithGains.Gains[0];
            Assert.Contains($"+{g.Amount} {g.GainType}", allLines);
        }
    }

    [Fact]
    public void RenderCastle_TopFloorSlotNoGains_ShowsNone()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var slots = state.Board.Castle.TopFloor.Slots;
        if (slots.Any(s => s.Gains.Count == 0))
            Assert.Contains(console.Written, l => l.Contains("(none)"));
    }

    [Fact]
    public void RenderCastle_StewardFloorLabel()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("Floor Steward:"));
    }

    [Fact]
    public void RenderCastle_DiplomatFloorLabel()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("Floor Diplomat:"));
    }

    [Fact]
    public void RenderCastle_RoomIndicesForStewardFloor()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var stewardFloor = state.Board.Castle.Floors[0];
        for (int r = 0; r < stewardFloor.Count; r++)
            Assert.Contains(console.Written, l => l.Contains($"Room {r}:"));
    }

    [Fact]
    public void RenderCastle_RoomEmpty_WhenNoDicePlaced()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        // At game start, no dice are placed in castle rooms
        Assert.Contains(console.Written, l => l.Contains("Room 0: (empty)") || l.Contains("Room 0: (empty) "));
    }

    [Fact]
    public void RenderCastle_TokenAbbreviationFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
                foreach (var token in room.Tokens)
                {
                    var expected = ExpectedTokenAbbreviation(token);
                    Assert.Contains(expected, allLines);
                    return;
                }
    }

    [Fact]
    public void RenderCastle_RoomTokensExactJoinedFormat()
    {
        // Kills the " " separator mutation in string.Join(" ", room.Tokens.Select(FormatToken))
        // Rooms have 2-3 tokens; exact "T1 T2" format verifies separator is preserved
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
                if (room.Tokens.Count >= 2)
                {
                    var expected = string.Join(" ", room.Tokens.Select(ExpectedTokenAbbreviation));
                    Assert.Contains(expected, allLines);
                    return;
                }
    }

    [Fact]
    public void RenderCastle_CardFieldsWrappedInBrackets()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        // At game start all castle rooms have cards placed; their fields appear in [...]
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
            {
                if (room.Card == null) continue;
                Assert.Contains(console.Written, l => l.Contains("Room") && l.Contains("[") && l.Contains("]"));
                return;
            }
    }

    [Fact]
    public void RenderCastle_CardGainFieldNoSpaceBetweenAmountAndType()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
            {
                if (room.Card == null) continue;
                var gainField = room.Card.Fields.FirstOrDefault(f => f.IsGain && f.Gains?.Count > 0);
                if (gainField == null) continue;
                var g = gainField.Gains![0];
                // FormatCardFields: no space — "{amount}{gainType}"
                Assert.Contains($"{g.Amount}{g.GainType}", allLines);
                return;
            }
    }

    [Fact]
    public void RenderCastle_CardMultipleFieldsSeparatedByPipe()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
            {
                if (room.Card?.Fields.Count > 1)
                {
                    Assert.Contains("|", allLines);
                    return;
                }
            }
    }

    // ── RenderTrainingGrounds precision tests ──────────────────────────────────

    [Fact]
    public void RenderTrainingGrounds_WritesAreaInfo()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("iron"));
    }

    [Fact]
    public void RenderTrainingGrounds_AreaIndexInOutput()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        foreach (var area in state.Board.TrainingGrounds.Areas)
            Assert.Contains(console.Written, l => l.Contains($"Area {area.AreaIndex}"));
    }

    [Fact]
    public void RenderTrainingGrounds_IronCostFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        foreach (var area in state.Board.TrainingGrounds.Areas)
            Assert.Contains(console.Written, l => l.Contains($"({area.IronCost} iron)"));
    }

    [Fact]
    public void RenderTrainingGrounds_ResourceGainFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var areaWithGain = state.Board.TrainingGrounds.Areas.FirstOrDefault(a => a.ResourceGain.Count > 0);
        if (areaWithGain != null)
        {
            var g = areaWithGain.ResourceGain[0];
            Assert.Contains($"+{g.Amount} {g.GainType}", allLines);
        }
    }

    [Fact]
    public void RenderTrainingGrounds_ActionDescriptionWhenNoGain()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var areaWithAction = state.Board.TrainingGrounds.Areas.FirstOrDefault(a => a.ResourceGain.Count == 0);
        if (areaWithAction != null)
            Assert.Contains(areaWithAction.ActionDescription, allLines);
    }

    [Fact]
    public void RenderTrainingGrounds_NoSoldierBracketsAtStart()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        // No soldiers at start — no bracketed soldier list on area lines
        var allLines = string.Join("\n", console.Written);
        foreach (var area in state.Board.TrainingGrounds.Areas)
            if (area.SoldierOwners.Count == 0)
                Assert.DoesNotContain(console.Written,
                    l => l.Contains($"Area {area.AreaIndex}") && l.Contains("[Alice]"));
        // The no-soldier else branch must produce "", not any other string
        Assert.DoesNotContain("Stryker was here!", allLines);
    }

    // ── RenderBridgesFarmlands precision tests ────────────────────────────────

    [Fact]
    public void RenderBridgesFarmlands_WritesBridgeInfo()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.True(
            allLines.Contains("Red") || allLines.Contains("Black") || allLines.Contains("White"),
            "Expected bridge color names in output");
    }

    [Fact]
    public void RenderBridges_FarmingLandsTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("FARMING LANDS"));
    }

    [Fact]
    public void RenderBridges_BridgeColorInOutput()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            Assert.Contains(console.Written, l => l.Contains(bridge.Color.ToString()));
    }

    [Fact]
    public void RenderBridges_HighDieValueInOutput()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.High != null)
                Assert.Contains($"High [{bridge.High.Value}]", allLines);
    }

    [Fact]
    public void RenderBridges_LowDieValueInOutput()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.Low != null)
                Assert.Contains($"Low [{bridge.Low.Value}]", allLines);
    }

    [Fact]
    public void RenderBridges_MiddleDiceValues()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var bridge in state.Board.Bridges)
            foreach (var die in bridge.Middle)
                Assert.Contains($"[{die.Value}]", allLines);
    }

    [Fact]
    public void RenderBridges_FarmFieldFoodCostFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var field in state.Board.FarmingLands.Fields)
            Assert.Contains($"({field.FoodCost}):", allLines);
    }

    [Fact]
    public void RenderBridges_InlandLabel()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        if (state.Board.FarmingLands.Fields.Any(f => f.IsInland))
            Assert.Contains(console.Written, l => l.Contains("Inland("));
    }

    [Fact]
    public void RenderBridges_OutsideLabel()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        if (state.Board.FarmingLands.Fields.Any(f => !f.IsInland))
            Assert.Contains(console.Written, l => l.Contains("Outside("));
    }

    [Fact]
    public void RenderBridges_FarmGainFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var fieldWithGain = state.Board.FarmingLands.Fields.FirstOrDefault(f => f.GainItems.Count > 0);
        if (fieldWithGain != null)
        {
            var g = fieldWithGain.GainItems[0];
            Assert.Contains($"+{g.Amount} {g.GainType}", allLines);
        }
    }

    // ── RenderWellOutside precision tests ─────────────────────────────────────

    [Fact]
    public void RenderWellOutside_WritesSlotInfo()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("WELL"));
    }

    [Fact]
    public void RenderWellOutside_OutsideSlotsTitle()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("OUTSIDE SLOTS"));
    }

    [Fact]
    public void RenderWellOutside_WellDiceEmpty_AtStart()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        if (state.Board.Well.Placeholder.PlacedDice.Count == 0)
            Assert.Contains("Dice: (empty)", allLines);
    }

    [Fact]
    public void RenderWellOutside_WellTokens()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var well = state.Board.Well;
        if (well.Placeholder.Tokens.Count == 0)
            Assert.Contains("Tokens: (none)", allLines);
        else
        {
            var abbrev = ExpectedTokenAbbreviation(well.Placeholder.Tokens[0]);
            Assert.Contains(abbrev, allLines);
        }
    }

    [Fact]
    public void RenderWellOutside_OutsideSlotIndices()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        for (int i = 0; i < state.Board.Outside.Slots.Count; i++)
            Assert.Contains(console.Written, l => l.Contains($"Slot {i}:"));
    }

    [Fact]
    public void RenderWellOutside_OutsideSlotsEmpty_AtStart()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        for (int i = 0; i < state.Board.Outside.Slots.Count; i++)
        {
            var slot = state.Board.Outside.Slots[i];
            if (slot.PlacedDice.Count == 0)
                Assert.Contains(console.Written, l => l.Contains($"Slot {i}: (empty)"));
        }
    }

    // ── RenderPersonalDomain precision tests ──────────────────────────────────

    [Fact]
    public void RenderPersonalDomain_WritesRowInfo()
    {
        var state    = StartedGame();
        var console  = new FakeConsole();
        var activeId = state.Players[state.ActivePlayerIndex].Id;
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, activeId);
        Assert.Contains(console.Written, l => l.Contains("PERSONAL DOMAIN"));
    }

    [Fact]
    public void RenderPersonalDomain_ExactTitleContainsPlayerName()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        Assert.Contains(console.Written, l => l.Contains($"PERSONAL DOMAIN — {player.Name}"));
    }

    [Fact]
    public void RenderPersonalDomain_RowCount()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        for (int i = 0; i < player.PersonalDomainRows.Count; i++)
            Assert.Contains(console.Written, l => l.Contains($"Row {i}"));
    }

    [Fact]
    public void RenderPersonalDomain_RowDieColor()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        var allLines = string.Join("\n", console.Written);
        for (int i = 0; i < player.PersonalDomainRows.Count; i++)
            Assert.Contains($"Row {i} ({player.PersonalDomainRows[i].DieColor})", allLines);
    }

    [Fact]
    public void RenderPersonalDomain_FreeDie_AtStart()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        Assert.Contains(console.Written, l => l.Contains("(free)"));
    }

    [Fact]
    public void RenderPersonalDomain_UncoveredSpotCount()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var row in player.PersonalDomainRows)
        {
            var uncovered = row.Spots.Count(s => s.IsUncovered);
            Assert.Contains($"{uncovered} spots uncovered", allLines);
        }
    }

    [Fact]
    public void RenderPersonalDomain_GainFormat()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var row in player.PersonalDomainRows)
        {
            var spot = row.Spots.FirstOrDefault(s => s.IsUncovered);
            if (spot != null)
            {
                Assert.Contains($"+{spot.GainAmount} {spot.GainType}", allLines);
                return;
            }
        }
    }

    [Fact]
    public void RenderPersonalDomain_FallbackToFirstPlayer_WhenUnknownId()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, Guid.Empty);
        Assert.Contains(console.Written, l => l.Contains($"PERSONAL DOMAIN — {state.Players[0].Name}"));
    }

    [Fact]
    public void RenderPersonalDomain_ShowsCorrectPlayer_WhenPlayer1Id()
    {
        // Kills null-coalescing "remove left" mutation: always returning players[0]
        var state   = StartedGame();
        var console = new FakeConsole();
        var player1 = state.Players[1];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player1.Id);
        Assert.Contains(console.Written, l => l.Contains($"PERSONAL DOMAIN — {player1.Name}"));
        Assert.DoesNotContain(console.Written,
            l => l.Contains($"PERSONAL DOMAIN — {state.Players[0].Name}"));
    }

    // ── Floor ordering tests ──────────────────────────────────────────────────

    [Fact]
    public void RenderCastle_StewardFloorBeforeDiplomatFloor()
    {
        // Kills f != 0 mutation which swaps Steward/Diplomat labels
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var stewardIdx  = console.Written.FindIndex(l => l.Contains("Floor Steward:"));
        var diplomatIdx = console.Written.FindIndex(l => l.Contains("Floor Diplomat:"));
        Assert.True(stewardIdx >= 0, "Floor Steward: not found");
        Assert.True(diplomatIdx >= 0, "Floor Diplomat: not found");
        Assert.True(stewardIdx < diplomatIdx, "Steward should appear before Diplomat");
    }

    // ── Non-empty branch tests (dice placed) ──────────────────────────────────

    [Fact]
    public void RenderWellOutside_WellDiceValue_AfterPlacement()
    {
        var state = StateAfterPlace(a => a.Target is WellTarget);
        if (state == null) return;
        var well = state.Board.Well;
        if (well.Placeholder.PlacedDice.Count == 0) return;
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var die = well.Placeholder.PlacedDice[0];
        Assert.Contains($"[{die.Value}]", allLines);
        // "(empty)" must NOT appear for placed dice
        Assert.DoesNotContain("Dice: (empty)", allLines);
    }

    [Fact]
    public void RenderWellOutside_OutsideDiceValue_AfterPlacement()
    {
        // Use reliable helper: retries until High die >= 5 found, places at outside slot
        var state = StateAfterOutsidePlace();
        if (state == null) return;
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var slotWithDie = state.Board.Outside.Slots
            .Select((s, i) => (s, i))
            .FirstOrDefault(x => x.s.PlacedDice.Count > 0);
        if (slotWithDie.s == null) return;
        // Verifies exact "Slot N: [V]" format — kills the $"[{d.Value}]" string mutation
        Assert.Contains($"Slot {slotWithDie.i}: [{slotWithDie.s.PlacedDice[0].Value}]", allLines);
        Assert.DoesNotContain($"Slot {slotWithDie.i}: (empty)", allLines);
    }

    [Fact]
    public void RenderCastle_RoomDiceValue_AfterPlacement()
    {
        var state = StateAfterPlace(a => a.Target is CastleRoomTarget);
        if (state == null) return;
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        // Find any castle room with placed dice
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
                if (room.PlacedDice.Count > 0)
                {
                    Assert.Contains($"[{room.PlacedDice[0].Value}]", allLines);
                    return;
                }
    }

    [Fact]
    public void RenderPersonalDomain_PlacedDieValue_AfterPlacement()
    {
        // Use reliable helper: retries until a die of value 6 is available for PD (compareValue=6)
        var state = StateAfterPDPlace();
        if (state == null) return;
        foreach (var p in state.Players)
        {
            var rowWithDie = p.PersonalDomainRows.Select((r, i) => (r, i))
                .FirstOrDefault(x => x.r.PlacedDie != null);
            if (rowWithDie.r == null) continue;
            var console = new FakeConsole();
            GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, p.Id);
            // The specific row with a placed die must show its value, not "(free)"
            Assert.Contains(console.Written,
                l => l.Contains($"Row {rowWithDie.i}") && l.Contains($"[{rowWithDie.r.PlacedDie!.Value}]"));
            Assert.DoesNotContain(console.Written,
                l => l.Contains($"Row {rowWithDie.i}") && l.Contains("(free)"));
            return;
        }
    }

    // ── SoldierOwners / FarmerOwners empty-bracket tests ─────────────────────

    [Fact]
    public void RenderTrainingGrounds_EmptySoldierOwners_NoBrackets()
    {
        // Kills (true?...) mutation that always shows soldier brackets
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        foreach (var area in state.Board.TrainingGrounds.Areas)
            if (area.SoldierOwners.Count == 0)
                Assert.DoesNotContain(console.Written,
                    l => l.Contains($"Area {area.AreaIndex}") && l.Contains("[]"));
    }

    [Fact]
    public void RenderBridges_FarmFieldEmptyFarmers_NoBrackets()
    {
        // Kills (true?...) mutation that always shows farmer brackets
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        // At start no farmers; verify empty brackets [] don't appear after food cost
        var allLines = string.Join("\n", console.Written);
        foreach (var field in state.Board.FarmingLands.Fields)
            if (field.FarmerOwners.Count == 0)
                Assert.DoesNotContain(console.Written,
                    l => l.Contains($"({field.FoodCost}):") && l.Contains("[]"));
        // The no-farmer else branch must produce "", not any other string
        Assert.DoesNotContain("Stryker was here!", allLines);
    }

    // ── Training grounds: AreaIndex + IronCost on same line ──────────────────

    [Fact]
    public void RenderTrainingGrounds_AreaIndexAndIronCostOnSameLine()
    {
        // Kills string mutations and property substitution mutations
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        foreach (var area in state.Board.TrainingGrounds.Areas)
            Assert.Contains(console.Written,
                l => l.Contains($"Area {area.AreaIndex}") && l.Contains($"({area.IronCost} iron)"));
    }

    // ── Bridge: High/Low die value on same line as bridge color ──────────────

    [Fact]
    public void RenderBridges_HighDieOnSameLineAsColor()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.High != null)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains($"High [{bridge.High.Value}]"));
    }

    [Fact]
    public void RenderBridges_LowDieOnSameLineAsColor()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.Low != null)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains($"Low [{bridge.Low.Value}]"));
    }

    [Fact]
    public void RenderBridges_HighEmptyWhenNull()
    {
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.High == null)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains("High (empty)"));
    }

    [Fact]
    public void RenderBridges_MiddleEmptyWhenNoMiddleDice()
    {
        // After 1 take from the Red bridge, Middle becomes empty for that bridge
        var state   = StateWithDepletedBridges();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.Middle.Count == 0)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains("Mid [(empty)]"));
    }

    [Fact]
    public void RenderBridges_HighEmptyWhenNull_AfterTwoTakes()
    {
        // After 2 takes from same bridge: High becomes null → must render "High (empty)"
        var state   = StateWithNullHighBridge();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.High == null)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains("High (empty)"));
    }

    [Fact]
    public void RenderBridges_LowEmptyWhenNull_AfterThreeTakes()
    {
        // After 3 takes from same bridge: Low also becomes null → must render "Low (empty)"
        var state   = StateWithNullLowBridge();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
            if (bridge.Low == null)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains("Low (empty)"));
    }

    [Fact]
    public void RenderBridges_MiddleExactFormat_DieWrappedInBrackets()
    {
        // Kills string mutation on $"[{d.Value}]" — verifies "Mid [[N]]" format (outer [] from format string, inner [N] from die)
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var bridge in state.Board.Bridges)
            foreach (var die in bridge.Middle)
                Assert.Contains(console.Written,
                    l => l.Contains(bridge.Color.ToString()) && l.Contains($"Mid [[{die.Value}]"));
    }

    // ── Farm: BridgeColor filtering — field color appears on same line ────────

    [Fact]
    public void RenderBridges_FarmFieldBridgeColorOnSameLine()
    {
        // Kills BridgeColor != color mutation which would show wrong color fields
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var field in state.Board.FarmingLands.Fields)
        {
            var label = field.IsInland ? "Inland" : "Outside";
            Assert.Contains(console.Written,
                l => l.Contains(field.BridgeColor.ToString()) && l.Contains($"{label}({field.FoodCost}):"));
        }
    }

    // ── Well tokens: "(none)" assertion and token abbreviation ───────────────

    [Fact]
    public void RenderWellOutside_WellTokensNoneOrAbbrev()
    {
        // Kills Count >= 0 mutation (tokens always shown) or string "" mutation
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var well = state.Board.Well;
        if (well.Placeholder.Tokens.Count == 0)
        {
            Assert.Contains("Tokens: (none)", allLines);
            // Verify the empty brackets don't appear due to mutation
            Assert.DoesNotContain("Tokens: []", allLines);
        }
        else
        {
            // Token abbreviation appears, NOT "(none)"
            Assert.DoesNotContain("Tokens: (none)", allLines);
            foreach (var t in well.Placeholder.Tokens)
                Assert.Contains(ExpectedTokenAbbreviation(t), allLines);
        }
    }

    [Fact]
    public void RenderWellOutside_WellTokensExactJoinedFormat()
    {
        // Kills the " " separator mutation in string.Join(" ", tokens) — well always has 2 tokens
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var tokens = state.Board.Well.Placeholder.Tokens;
        if (tokens.Count >= 2)
        {
            var expected = string.Join(" ", tokens.Select(ExpectedTokenAbbreviation));
            Assert.Contains(expected, allLines);
        }
    }

    [Fact]
    public void RenderWellOutside_WellDiceExactFormat_WhenTwoDice()
    {
        // Kills the " " separator mutation in well.PlacedDice join — needs 2 dice in well
        var state = StateWithTwoWellDice();
        var well  = state.Board.Well.Placeholder;
        if (well.PlacedDice.Count < 2) return;
        var console  = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        var expected = string.Join(" ", well.PlacedDice.Select(d => $"[{d.Value}]"));
        Assert.Contains(expected, allLines);
    }

    // ── FormatCardFields: logical mutation && vs || ───────────────────────────

    [Fact]
    public void RenderCastle_CardField_ActionWithGains_UsesDescription_NotGains()
    {
        // Kills f.IsGain || f.Gains != null mutation on L238:
        // When IsGain=false but Gains!=null, original uses ActionDescription; mutant renders gains.
        var state   = FakeStateWithCastleRoomActionFieldWithGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("TestAction", allLines);
        // Gains (9Food) must NOT appear since IsGain=false
        Assert.DoesNotContain("9Food", allLines);
    }

    // ── FormatCardFields: action description field ────────────────────────────

    [Fact]
    public void RenderCastle_CardActionFieldDescription_InOutput()
    {
        // Kills ActionDescription == null mutation and string "" mutation in FormatCardFields
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        foreach (var floor in state.Board.Castle.Floors)
            foreach (var room in floor)
            {
                if (room.Card == null) continue;
                var actionField = room.Card.Fields.FirstOrDefault(
                    f => !f.IsGain && f.ActionDescription != null);
                if (actionField == null) continue;
                Assert.Contains(actionField.ActionDescription!, allLines);
                return;
            }
    }

    // ── Top floor slot: gains format and "(none)" ─────────────────────────────

    [Fact]
    public void RenderCastle_TopFloorSlotGainsOnSameLineAsSlotIndex()
    {
        // Kills string substitution mutations in gains format
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var slots = state.Board.Castle.TopFloor.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Gains.Count > 0)
            {
                var g = slot.Gains[0];
                Assert.Contains(console.Written,
                    l => l.Contains($"Slot {i}:") && l.Contains($"+{g.Amount} {g.GainType}"));
            }
            else
            {
                Assert.Contains(console.Written,
                    l => l.Contains($"Slot {i}:") && l.Contains("(none)"));
            }
        }
    }

    [Fact]
    public void RenderCastle_TopFloorSlot_OccupantNoneAtStart()
    {
        // Kills null coalescing "remove left" mutation
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        // At start no courtiers placed → occupant shows "(empty)" not null/crash
        var slots = state.Board.Castle.TopFloor.Slots;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].OccupantName == null)
                Assert.Contains(console.Written, l => l.Contains($"Slot {i}:") && l.Contains("(empty)"));
    }

    // ── Fake-state castle tests ───────────────────────────────────────────────

    [Fact]
    public void RenderCastle_TopFloor_SlotNoGains_ShowsNone()
    {
        var state   = FakeStateWithTopFloorNoGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        Assert.Contains(console.Written, l => l.Contains("Slot 0:") && l.Contains("(none)"));
    }

    [Fact]
    public void RenderCastle_TopFloor_SlotTwoGains_CommaFormat()
    {
        var state   = FakeStateWithTopFloorTwoGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("+1 Food, +2 Iron", allLines);
    }

    [Fact]
    public void RenderCastle_TopFloor_SlotOccupied_ShowsName()
    {
        var state   = FakeStateWithTopFloorOccupied();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("TestCourtier", allLines);
        Assert.DoesNotContain(console.Written, l => l.Contains("Slot 0:") && l.Contains("(empty)"));
    }

    [Fact]
    public void RenderCastle_Room_TwoDice_SpaceSeparator()
    {
        var state   = FakeStateWithCastleRoomTwoDice();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[3] [4]", allLines);
    }

    [Fact]
    public void RenderCastle_Room_NoTokens_RendersWithEmptyTokensSection()
    {
        var state   = FakeStateWithCastleRoomNoTokens();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        // Room 0 has 0 tokens — the room line should render without any token abbreviations
        var allLines = string.Join("\n", console.Written);
        Assert.Contains(console.Written, l => l.Contains("Room 0:") && l.Contains("(empty)"));
        // The empty-tokens else branch must produce "", not any other string
        Assert.DoesNotContain("Stryker was here!", allLines);
    }

    [Fact]
    public void RenderCastle_Room_NullCard_RendersWithoutCardFields()
    {
        var state   = FakeStateWithCastleRoomNullCard();
        var console = new FakeConsole();
        // Should not throw — null card means no card fields rendered for room 0
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains(console.Written, l => l.Contains("Room 0:"));
        // The null-card else branch must produce "", not any other string
        Assert.DoesNotContain("Stryker was here!", allLines);
    }

    [Fact]
    public void RenderCastle_CardField_TwoGains_PlusFormat()
    {
        var state   = FakeStateWithCastleRoomTwoGainField();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.Castle, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("1Food+2Iron", allLines);
    }

    // ── Fake-state training grounds tests ─────────────────────────────────────

    [Fact]
    public void RenderTrainingGrounds_Area_TwoResourceGains_CommaFormat()
    {
        var state   = FakeStateWithTgAreaTwoGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("+2 Food, +1 Iron", allLines);
    }

    [Fact]
    public void RenderTrainingGrounds_Area_WithSoldier_ShowsBracketed()
    {
        var state   = FakeStateWithTgAreaSoldier();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[Alice]", allLines);
    }

    [Fact]
    public void RenderTrainingGrounds_Area_TwoSoldiers_CommaFormat()
    {
        // Kills string mutation ","→"" in string.Join(", ", area.SoldierOwners)
        var state   = FakeStateWithTgAreaTwoSoldiers();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.TrainingGrounds, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[Alice, Bob]", allLines);
    }

    // ── Fake-state bridge/farming tests ──────────────────────────────────────

    [Fact]
    public void RenderBridges_MiddleTwoDice_CommaFormat()
    {
        var state   = FakeStateWithBridgeTwoMiddleDice();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[3], [4]", allLines);
    }

    [Fact]
    public void RenderBridges_FarmField_TwoGains_CommaFormat()
    {
        var state   = FakeStateWithFarmFieldTwoGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("+3 Food, +2 Iron", allLines);
    }

    [Fact]
    public void RenderBridges_FarmField_NoGains_ShowsActionDescription()
    {
        // Kills GainItems.Count > 0 → always-true mutation: when gains=0, ActionDescription must appear
        var state   = FakeStateWithFarmFieldNoGains();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        // ActionDescription must be shown when GainItems is empty (not replaced by empty string.Join result)
        Assert.Contains("TestFarmAction", allLines);
    }

    [Fact]
    public void RenderBridges_FarmField_WithFarmer_ShowsBracketed()
    {
        var state   = FakeStateWithFarmFieldFarmer();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[Alice]", allLines);
    }

    [Fact]
    public void RenderBridges_FarmField_TwoFarmers_CommaFormat()
    {
        // Kills string mutation ","→"" in string.Join(", ", f.FarmerOwners)
        var state   = FakeStateWithFarmFieldTwoFarmers();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[Alice, Bob]", allLines);
    }

    // ── Fake-state well/outside tests ────────────────────────────────────────

    [Fact]
    public void RenderWellOutside_WellNoTokens_ShowsNone()
    {
        var state   = FakeStateWithWellNoTokens();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("Tokens: (none)", allLines);
    }

    [Fact]
    public void RenderWellOutside_OutsideSlot_TwoDice_SpaceSeparator()
    {
        var state   = FakeStateWithOutsideSlotTwoDice();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.WellOutside, state.Players[0].Id);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("[3] [5]", allLines);
    }

    // ── Fake-state personal domain tests ─────────────────────────────────────

    [Fact]
    public void RenderPersonalDomain_UncoveredSpots_CommaFormat()
    {
        var state   = FakeStateWithPdRowTwoUncoveredSpots();
        var pid     = state.Players[state.ActivePlayerIndex].Id;
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, pid);
        var allLines = string.Join("\n", console.Written);
        Assert.Contains("+1 Food, +2 Iron", allLines);
    }

    // ── Logical mutation: inlandStr != "" || outsideStr != "" ─────────────────

    [Fact]
    public void RenderBridges_FarmLineOutputForEachColor()
    {
        // Kills && vs || logical mutation by verifying output per color
        var state   = StartedGame();
        var console = new FakeConsole();
        GameScreenRenderer.RenderArea(console, state, GameAreaView.BridgesFarmlands, state.Players[0].Id);
        foreach (var bridge in state.Board.Bridges)
        {
            var fields = state.Board.FarmingLands.Fields
                .Where(f => f.BridgeColor == bridge.Color).ToList();
            if (fields.Count > 0)
                Assert.Contains(console.Written, l => l.Contains(bridge.Color.ToString())
                    && (l.Contains("Inland(") || l.Contains("Outside(")));
        }
    }

    // ── Personal domain row: exact format ────────────────────────────────────

    [Fact]
    public void RenderPersonalDomain_RowFormatHasDashes()
    {
        // Kills string mutations that remove "— ... —" separators
        var state   = StartedGame();
        var console = new FakeConsole();
        var player  = state.Players[state.ActivePlayerIndex];
        GameScreenRenderer.RenderArea(console, state, GameAreaView.PersonalDomain, player.Id);
        // Each row line should have "— N spots uncovered —" format
        for (int i = 0; i < player.PersonalDomainRows.Count; i++)
        {
            var row = player.PersonalDomainRows[i];
            var uncovered = row.Spots.Count(s => s.IsUncovered);
            Assert.Contains(console.Written,
                l => l.Contains($"Row {i}") && l.Contains($"— {uncovered} spots uncovered"));
        }
    }
}
