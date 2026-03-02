using BoardWC.Engine.Actions;
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
        // Show hint if player has pending castle actions
        if (p.CastlePlaceRemaining > 0 || p.CastleAdvanceRemaining > 0)
        {
            var opts = new List<string> { "skip" };
            if (p.CastlePlaceRemaining > 0)  opts.Add("place (2 coins)");
            if (p.CastleAdvanceRemaining > 0) opts.Add("move <gate|ground|mid> <1|2> (2/5 VI)");
            System.Console.WriteLine(
                $"  [Pending castle play — available: {string.Join(", ", opts)} — type 'castle <option>']");
        }
        // Show hint if player has pending training grounds actions
        if (p.PendingTrainingGroundsActions > 0)
        {
            var tg = state.Board.TrainingGrounds.Areas;
            var available = tg.Select(a => $"{a.AreaIndex}({a.IronCost}Fe)");
            System.Console.WriteLine(
                $"  [Pending training grounds — areas: {string.Join(", ", available)}, skip — type 'train <0|1|2|skip>']");
        }
        // Show hint if player has pending farm actions
        if (p.PendingFarmActions > 0)
        {
            System.Console.WriteLine(
                "  [Pending farm action — type 'farm <red|black|white> <inland|outside>' or 'farm skip']");
        }
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
                $"(Lanterns:{s.LanternPoints})");
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
        var gateInfo = string.Join("  ", state.Players.Select(p => $"{p.Name}:{p.CourtiersAtGate}"));
        System.Console.WriteLine($"    Gate: {gateInfo}");
        // Top floor — courtier card slots
        var topFloor = state.Board.Castle.TopFloor;
        if (topFloor.Slots.Count > 0)
        {
            System.Console.WriteLine($"    Top Floor [{topFloor.CardId}] (courtier slots):");
            foreach (var slot in topFloor.Slots)
            {
                var gains    = string.Join(", ", slot.Gains.Select(g => $"+{g.Amount} {g.GainType}"));
                var occupant = slot.OccupantName is null ? "(empty)" : $"[{slot.OccupantName}]";
                System.Console.WriteLine($"      Slot {slot.SlotIndex}: {gains,-20} {occupant}");
            }
        }

        var floorNames = new[] { "  Ground(0) val=3", "  Mid(1)    val=4" };
        for (int f = 0; f < state.Board.Castle.Floors.Count; f++)
        {
            var rooms = state.Board.Castle.Floors[f];
            System.Console.WriteLine($"    {floorNames[f]}:");
            for (int r = 0; r < rooms.Count; r++)
            {
                var ph     = rooms[r];
                var top    = ph.PlacedDice.Count > 0 ? $"[{ph.PlacedDice[^1].Value}]" : "[ ]";
                var tokens = ph.Tokens.Count > 0
                    ? " tokens:" + string.Join("", ph.Tokens.Select(FormatToken))
                    : "";
                System.Console.WriteLine($"      room{r}: {top}{tokens}");
                if (ph.Card is { } card)
                    System.Console.WriteLine($"             {FormatCardFields(ph.Tokens, card.Fields)}");
            }
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

        // Training grounds
        System.Console.WriteLine("  Training Grounds:");
        foreach (var area in state.Board.TrainingGrounds.Areas)
        {
            var resPart = area.ResourceGain.Count > 0
                ? "[Res] " + string.Join(", ", area.ResourceGain.Select(g => $"+{g.Amount} {g.GainType}"))
                : "";
            var actPart = !string.IsNullOrEmpty(area.ActionDescription)
                ? $"[Action] {area.ActionDescription}"
                : "";
            var effectStr = string.Join("  +  ", new[] { resPart, actPart }.Where(s => s.Length > 0));
            if (string.IsNullOrEmpty(effectStr)) effectStr = "(not set)";
            var soldiers = area.SoldierOwners.Count > 0
                ? "  soldiers: " + string.Join(", ", area.SoldierOwners
                    .GroupBy(n => n)
                    .Select(g => $"{g.Count()} ({g.Key})"))
                : "";
            System.Console.WriteLine(
                $"    Area {area.AreaIndex} ({area.IronCost} iron): {effectStr}{soldiers}");
        }

        // Farming lands
        if (state.Board.FarmingLands.Fields.Count > 0)
        {
            System.Console.WriteLine("  Farming Lands:");
            var byBridge = state.Board.FarmingLands.Fields.GroupBy(f => f.BridgeColor);
            foreach (var group in byBridge)
            {
                System.Console.WriteLine($"    {group.Key}:");
                foreach (var field in group.OrderByDescending(f => f.IsInland))
                {
                    var side    = field.IsInland ? "Inland " : "Outside";
                    var effect  = field.GainItems.Count > 0
                        ? string.Join(", ", field.GainItems.Select(g => $"+{g.Amount} {g.GainType}"))
                        : field.ActionDescription;
                    var farmers = field.FarmerOwners.Count > 0
                        ? "  farmers: " + string.Join(", ", field.FarmerOwners)
                        : "";
                    System.Console.WriteLine(
                        $"      {side} ({field.FoodCost} food): [{effect}]{farmers}");
                }
            }
        }
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

    private static string FormatCardFields(
        IReadOnlyList<TokenSnapshot> tokens,
        IReadOnlyList<CardFieldSnapshot> fields)
    {
        int limit = Math.Min(tokens.Count, fields.Count);
        var parts = new List<string>();
        for (int i = 0; i < limit; i++)
        {
            var colorAbbr = tokens[i].DieColor switch
            {
                BridgeColor.Red   => "R",
                BridgeColor.Black => "B",
                BridgeColor.White => "W",
                _                 => "?",
            };
            var field = fields[i];
            string fieldStr = field.IsGain
                ? string.Join(", ", field.Gains!.Select(g => $"+{g.Amount} {g.GainType}"))
                : $"[Action] {FormatActionCost(field.ActionCost)}→ \"{field.ActionDescription}\"";
            parts.Add($"{colorAbbr}:{fieldStr}");
        }
        return string.Join("  |  ", parts);
    }

    private static string FormatActionCost(IReadOnlyList<CardCostItemSnapshot>? cost)
    {
        if (cost is null || cost.Count == 0) return "";
        return "(" + string.Join(", ", cost.Select(c => $"-{c.Amount} {c.CostType}")) + ") ";
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
                $"S:{p.SoldiersAvailable} F:{p.FarmersAvailable} " +
                $"Courtiers: hand={p.CourtiersAvailable} gate={p.CourtiersAtGate} grnd={p.CourtiersOnGroundFloor} mid={p.CourtiersOnMidFloor} top={p.CourtiersOnTopFloor}  " +
                $"Lanterns:{p.LanternScore}" +
                (p.IsAI ? " [AI]" : ""));
        }
    }

    private static string PlayerName(Guid id, GameStateSnapshot state) =>
        state.Players.FirstOrDefault(p => p.Id == id)?.Name ?? "?";

    internal static string FormatEvent(IDomainEvent e) => e switch
    {
        DieTakenFromBridgeEvent x => $"{PlayerName(x.PlayerId, x)} took [{x.DieValue}] from {x.BridgeColor} bridge ({x.Position})",
        LanternEffectFiredEvent x => $"{PlayerName(x.PlayerId, x)} triggered the Lantern Effect!",
        DiePlacedEvent          x => FormatPlaced(x),
        WellEffectAppliedEvent      x => FormatWellEffect(x),
        CardFieldGainActivatedEvent x => FormatCardFieldGain(x),
        CardActionActivatedEvent    x => $"{PlayerName(x.PlayerId, x)} card action field {x.FieldIndex} on '{x.CardId}': {x.ActionDescription}",
        CastlePlayExecutedEvent         x => FormatCastlePlay(x),
        TrainingGroundsUsedEvent        x => FormatTrainingGrounds(x),
        FarmerPlacedEvent               x => FormatFarmerPlaced(x),
        FarmEffectFiredEvent            x => FormatFarmEffect(x),
        TopFloorSlotFilledEvent         x => FormatTopFloorSlot(x),
        AnyResourceChosenEvent  x => $"{PlayerName(x.PlayerId, x)} chose {x.Choice} from AnyResource token",
        ResourcesCollectedEvent  x => $"{PlayerName(x.PlayerId, x)} collected {x.Gained}",
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

    private static string FormatCardFieldGain(CardFieldGainActivatedEvent x)
    {
        var parts = new List<string>();
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained   > 0) parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.SealsGained   > 0) parts.Add($"+{x.SealsGained} seal(s)");
        if (x.LanternGained > 0) parts.Add($"+{x.LanternGained} lantern(s)");
        var gained = parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        return $"{PlayerName(x.PlayerId, x)} card field {x.FieldIndex} gain: {gained}";
    }

    private static string FormatCastlePlay(CastlePlayExecutedEvent x)
    {
        if (!x.PlacedAtGate && x.AdvancedFrom is null)
            return $"{PlayerName(x.PlayerId, x)} castle play: skipped";

        var parts = new List<string>();
        if (x.PlacedAtGate)
            parts.Add("placed courtier at gate (-2 coins)");
        if (x.AdvancedFrom is { } from)
        {
            var fromStr = from switch
            {
                CourtierPosition.Gate        => "gate",
                CourtierPosition.GroundFloor => "ground",
                CourtierPosition.MidFloor    => "mid",
                _                            => "?",
            };
            int viCost = x.LevelsAdvanced == 1 ? 2 : 5;
            parts.Add($"advanced courtier from {fromStr} {x.LevelsAdvanced} level(s) (-{viCost} VI)");
        }
        return $"{PlayerName(x.PlayerId, x)} castle play: {string.Join(" + ", parts)}";
    }

    private static string FormatTrainingGrounds(TrainingGroundsUsedEvent x)
    {
        if (x.AreaIndex == -1)
            return $"{PlayerName(x.PlayerId, x)} training grounds: skipped";

        var parts = new List<string> { $"area {x.AreaIndex} (-{x.IronSpent} iron)" };
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained   > 0) parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.SealsGained   > 0) parts.Add($"+{x.SealsGained} seal(s)");
        if (x.LanternGained > 0) parts.Add($"+{x.LanternGained} lantern(s)");
        if (x.ActionTriggered is { } act) parts.Add($"action: {act}");
        return $"{PlayerName(x.PlayerId, x)} training grounds: {string.Join(", ", parts)}";
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

    private static string FormatFarmerPlaced(FarmerPlacedEvent x)
    {
        if (x.AreaIndex == -1)
            return $"{PlayerName(x.PlayerId, x)} farm: skipped";

        var field = $"{x.BridgeColor} {(x.IsInland ? "inland" : "outside")}";
        var parts = new List<string> { $"{field} (-{x.FoodSpent} food)" };
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained   > 0) parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.SealsGained   > 0) parts.Add($"+{x.SealsGained} seal(s)");
        if (x.LanternGained > 0) parts.Add($"+{x.LanternGained} lantern(s)");
        if (x.ActionTriggered is { } act) parts.Add($"action: {act}");
        return $"{PlayerName(x.PlayerId, x)} farm: {string.Join(", ", parts)}";
    }

    private static string FormatFarmEffect(FarmEffectFiredEvent x)
    {
        var field = $"{x.BridgeColor} {(x.IsInland ? "inland" : "outside")}";
        var parts = new List<string>();
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained   > 0) parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.SealsGained   > 0) parts.Add($"+{x.SealsGained} seal(s)");
        if (x.LanternGained > 0) parts.Add($"+{x.LanternGained} lantern(s)");
        if (x.ActionTriggered is { } act) parts.Add($"action: {act}");
        var gained = parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        return $"{PlayerName(x.PlayerId, x)} farm re-fire ({field}): {gained}";
    }

    private static string FormatTopFloorSlot(TopFloorSlotFilledEvent x)
    {
        var parts = new List<string> { $"slot {x.SlotIndex}" };
        if (x.ResourcesGained.Total > 0) parts.Add($"resources: {x.ResourcesGained}");
        if (x.CoinsGained   > 0) parts.Add($"+{x.CoinsGained} coin(s)");
        if (x.SealsGained   > 0) parts.Add($"+{x.SealsGained} seal(s)");
        if (x.LanternGained > 0) parts.Add($"+{x.LanternGained} lantern(s)");
        return $"{PlayerName(x.PlayerId, x)} top floor: {string.Join(", ", parts)}";
    }

    // Helpers for FormatEvent — events don't carry the full state, so we use the short ID
    private static string PlayerName(Guid id, IDomainEvent e) => id.ToString()[..8];
}
