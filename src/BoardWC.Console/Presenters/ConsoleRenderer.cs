using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Console.Presenters;

internal sealed class ConsoleRenderer
{
    public void Render(GameStateSnapshot state)
    {
        System.Console.Clear();
        Header(state);
        RenderBridges(state);
        RenderPlacementAreas(state);
        RenderPlayers(state);
        RenderClanCards(state);
        System.Console.WriteLine();
    }

    public void RenderEvents(IReadOnlyList<IDomainEvent> events)
    {
        foreach (var e in events)
            System.Console.WriteLine($"  >> {FormatEvent(e)}");
    }

    public void RenderPrompt(GameStateSnapshot state)
    {
        var p = state.Players[state.ActivePlayerIndex];
        System.Console.WriteLine();
        // Show hint if player has a die to place
        if (p.DiceInHand.Count > 0)
        {
            var die = p.DiceInHand[0];
            System.Console.WriteLine($"  [Die in hand: {die.Color} {die.Value} — type 'place ...' to place it]");
        }
        // Show hint if player has pending AnyResource choices
        if (p.PendingAnyResourceChoices > 0)
            System.Console.WriteLine(
                $"  [Pending resource choice ({p.PendingAnyResourceChoices} remaining) — type 'choose food|iron|valueitem']");
        System.Console.Write($"[{p.Name}] > ");
    }

    public void RenderLegalActions(IReadOnlyList<object> actions) { }  // used optionally

    public void RenderFinalScores(IReadOnlyList<PlayerScore> scores)
    {
        System.Console.WriteLine();
        System.Console.WriteLine("═══════════════════════════════════");
        System.Console.WriteLine("          GAME OVER — SCORES       ");
        System.Console.WriteLine("═══════════════════════════════════");
        foreach (var s in scores)
            System.Console.WriteLine(
                $"  {s.PlayerName,-16} {s.Total,4} pts  " +
                $"(Lanterns:{s.LanternPoints} Cards:{s.ClanCardPoints})");
    }

    public void Error(string msg) =>
        System.Console.WriteLine($"  [!] {msg}");

    // ── private helpers ──────────────────────────────────────────────────────

    private static void Header(GameStateSnapshot s) =>
        System.Console.WriteLine(
            $"══ WHITE CASTLE ══  Round {s.CurrentRound}/{s.MaxRounds}  " +
            $"Phase: {s.CurrentPhase}  Active: {s.Players[s.ActivePlayerIndex].Name}");

    private static void RenderBridges(GameStateSnapshot state)
    {
        System.Console.WriteLine("\nBRIDGES:");
        foreach (var bridge in state.Board.Bridges)
        {
            var high   = bridge.High  is null ? "  _" : $"[{bridge.High.Value}]";
            var low    = bridge.Low   is null ? "  _" : $"[{bridge.Low.Value}]";
            var middle = bridge.Middle.Count == 0
                ? ""
                : string.Join(" ", bridge.Middle.Select(d => $"[{d.Value}]"));
            var dice = string.Join(" ", new[] { high, middle, low }
                .Where(s => s.Length > 0));
            System.Console.WriteLine($"  {bridge.Color,-6}  High→  {dice}  ←Low");
        }
    }

    private static void RenderPlacementAreas(GameStateSnapshot state)
    {
        System.Console.WriteLine("\nPLACEMENT AREAS:");

        // Castle
        System.Console.WriteLine("  Castle:");
        var floorNames = new[] { "  Ground(0) val=3", "  Mid(1)    val=4" };
        for (int f = 0; f < state.Board.Castle.Floors.Count; f++)
        {
            var rooms = state.Board.Castle.Floors[f];
            System.Console.Write($"    {floorNames[f]}  ");
            for (int r = 0; r < rooms.Count; r++)
            {
                var ph      = rooms[r];
                var top     = ph.PlacedDice.Count > 0 ? $"[{ph.PlacedDice[^1].Value}]" : "[ ]";
                var tokens  = ph.Tokens.Count > 0
                    ? " tokens:" + string.Join("", ph.Tokens.Select(FormatToken))
                    : "";
                System.Console.Write($"  room{r}:{top}{tokens}");
            }
            System.Console.WriteLine();
        }

        // Well
        var well = state.Board.Well.Placeholder;
        var wellDice = well.PlacedDice.Count == 0
            ? "(empty)"
            : string.Join(" ", well.PlacedDice.Select(d => $"[{d.Value}]"));
        var wellTokens = well.Tokens.Count > 0
            ? " tokens(res-side):" + string.Join("", well.Tokens.Select(FormatToken))
            : "";
        System.Console.WriteLine($"  Well    val=1  {wellDice}{wellTokens}");

        // Outside
        System.Console.Write("  Outside val=5  ");
        for (int s = 0; s < state.Board.Outside.Slots.Count; s++)
        {
            var ph  = state.Board.Outside.Slots[s];
            var top = ph.PlacedDice.Count > 0 ? $"[{ph.PlacedDice[^1].Value}]" : "[ ]";
            System.Console.Write($"  slot{s}:{top}");
        }
        System.Console.WriteLine();
    }

    private static string FormatToken(TokenSnapshot t)
    {
        var color = t.DieColor switch
        {
            BridgeColor.Red   => "R",
            BridgeColor.Black => "B",
            BridgeColor.White => "W",
            _                 => "?",
        };
        var res = t.ResourceSide switch
        {
            TokenResource.Food        => "Fd",
            TokenResource.Iron        => "Fe",
            TokenResource.ValueItem   => "VI",
            TokenResource.AnyResource => "Any",
            TokenResource.Coin        => "Coin",
            _                         => "?",
        };
        return $"[{color}:{res}]";
    }

    private static void RenderPlayers(GameStateSnapshot state)
    {
        System.Console.WriteLine("\nPLAYERS:");
        foreach (var p in state.Players)
        {
            var r = p.Resources;
            System.Console.WriteLine(
                $"  {p.Name,-16} " +
                $"Fd:{r.Food,2} Fe:{r.Iron,2} VI:{r.ValueItem,2} " +
                $"Coins:{p.Coins,3} Seals:{p.MonarchialSeals} " +
                $"S:{p.SoldiersAvailable} C:{p.CourtiersAvailable} F:{p.FarmersAvailable} " +
                $"Lanterns:{p.LanternScore} " +
                $"Cards:{p.ClanCards.Count}" +
                (p.IsAI ? " [AI]" : ""));
        }
    }

    private static void RenderClanCards(GameStateSnapshot state)
    {
        if (state.ClanCardRow.VisibleCards.Count == 0) return;
        System.Console.WriteLine("\nCLAN CARDS AVAILABLE:");
        foreach (var c in state.ClanCardRow.VisibleCards)
            System.Console.WriteLine($"  {c.Name,-16} ({c.VictoryPoints} VP) — {c.Effect}");
    }

    private static string PlayerName(Guid id, GameStateSnapshot state) =>
        state.Players.FirstOrDefault(p => p.Id == id)?.Name ?? "?";

    private static string FormatEvent(IDomainEvent e) => e switch
    {
        DieTakenFromBridgeEvent x => $"{PlayerName(x.PlayerId, x)} took [{x.DieValue}] from {x.BridgeColor} bridge ({x.Position})",
        LanternEffectFiredEvent x => $"{PlayerName(x.PlayerId, x)} triggered the Lantern Effect!",
        DiePlacedEvent          x => FormatPlaced(x),
        WellEffectAppliedEvent  x => FormatWellEffect(x),
        AnyResourceChosenEvent  x => $"{PlayerName(x.PlayerId, x)} chose {x.Choice} from AnyResource token",
        ResourcesCollectedEvent  x => $"{PlayerName(x.PlayerId, x)} collected {x.Gained}",
        ClanCardAcquiredEvent    x => $"{PlayerName(x.PlayerId, x)} acquired clan card: {x.Card.Name}",
        LanternsGainedEvent      x => $"{PlayerName(x.PlayerId, x)} gained {x.Amount} lantern(s)",
        PlayerPassedEvent        x => $"{PlayerName(x.PlayerId, x)} passed",
        RoundEndedEvent          x => $"=== End of Round {x.RoundNumber} ===",
        GameOverEvent            _  => "=== GAME OVER ===",
        GameStartedEvent         _  => "Game started!",
        _                          => e.EventType,
    };

    private static string FormatWellEffect(WellEffectAppliedEvent x)
    {
        var parts = new List<string> { "+1 seal" };
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained > 0)           parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.PendingChoices > 0)        parts.Add($"{x.PendingChoices} choice(s) pending");
        return $"{PlayerName(x.PlayerId, x)} well effect: {string.Join(", ", parts)}";
    }

    private static string FormatPlaced(DiePlacedEvent x)
    {
        var location = x.Target switch
        {
            CastleRoomTarget  c => $"castle floor {c.Floor} room {c.RoomIndex}",
            WellTarget          => "the well",
            OutsideSlotTarget o => $"outside slot {o.SlotIndex}",
            _                   => "unknown",
        };
        var coinEffect = x.CoinDelta switch
        {
            > 0 => $"+{x.CoinDelta} coins",
            < 0 => $"{x.CoinDelta} coins (spent)",
            _   => "no coin effect",
        };
        return $"{PlayerName(x.PlayerId, x)} placed [{x.DieValue}] at {location} ({coinEffect})";
    }

    // Helpers for FormatEvent — events don't carry the full state, so we use the short ID
    private static string PlayerName(Guid id, IDomainEvent e) => id.ToString()[..8];
}
