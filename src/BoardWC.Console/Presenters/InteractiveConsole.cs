using BoardWC.Console.UI;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;
using BoardWC.Engine.Events;

namespace BoardWC.Console.Presenters;

internal sealed class InteractiveConsole
{
    // ── Inner types ───────────────────────────────────────────────────────

    private sealed record AreaEntry(
        string Name,
        string Summary,
        bool IsSelectable,
        IGameAction? DirectAction,           // non-null → selecting dispatches immediately
        IReadOnlyList<ActionEntry> Options); // used when DirectAction is null

    private sealed record ActionEntry(
        string Label,
        string? Detail,
        bool IsEnabled,
        IGameAction? Action);               // null = section header (not selectable)

    // ── State ─────────────────────────────────────────────────────────────

    private IReadOnlyList<IDomainEvent> _lastEvents = [];
    private GameAreaView _currentView = GameAreaView.Castle;

    internal void SetLastEvents(IReadOnlyList<IDomainEvent> events) =>
        _lastEvents = events;

    // ── Public entry point ────────────────────────────────────────────────

    internal IGameAction Run(GameStateSnapshot state, IGameEngine engine)
    {
        var player = state.Players[state.ActivePlayerIndex];
        var legal  = engine.GetLegalActions(player.Id);

        // Seed card selection phase — presented before normal game loop
        if (state.CurrentPhase == Phase.SeedCardSelection)
        {
            if (player.PendingAnyResourceChoices > 0)
            {
                var title = $"CHOOSE RESOURCE  ({player.PendingAnyResourceChoices} remaining)";
                return RunDetail(state, title, BuildChooseResourceOptions(legal), allowEsc: false)!;
            }
            return RunDetail(state,
                $"SEED CARD SELECTION  \u2014  {player.Name}'s Turn",
                BuildSeedPairOptions(state, legal), allowEsc: false)!;
        }

        // Die in hand — must place, no Esc
        if (player.DiceInHand.Count > 0)
        {
            var die   = player.DiceInHand[0];
            var title = $"PLACE DIE  ─  {die.Color} [{die.Value}]";
            return RunDetail(state, title, BuildPlaceDieOptions(state, legal), allowEsc: false)!;
        }

        // Influence threshold pending — player must pay seals or refuse — no Esc
        if (player.PendingInfluenceGain > 0)
        {
            var title = $"INFLUENCE GAIN  \u2014  +{player.PendingInfluenceGain} Influence  (costs {player.PendingInfluenceSealCost} seal(s))";
            return RunDetail(state, title, BuildChooseInfluenceOptions(player, legal), allowEsc: false)!;
        }

        // AnyResource choice pending — no Esc
        if (player.PendingAnyResourceChoices > 0)
        {
            var title = $"CHOOSE RESOURCE  ({player.PendingAnyResourceChoices} remaining)";
            return RunDetail(state, title, BuildChooseResourceOptions(legal), allowEsc: false)!;
        }

        // Castle courtier pending — no Esc
        if (player.CastlePlaceRemaining > 0 || player.CastleAdvanceRemaining > 0)
            return RunDetail(state, "CASTLE PLAY", BuildCastleCourtierOptions(state, legal), allowEsc: false)!;

        // Training grounds pending — no Esc
        if (player.PendingTrainingGroundsActions > 0)
            return RunDetail(state, "TRAINING GROUNDS", BuildTrainingOptions(state, legal), allowEsc: false)!;

        // Farm pending — no Esc
        if (player.PendingFarmActions > 0)
            return RunDetail(state, "FARMING LANDS", BuildFarmOptions(state, legal), allowEsc: false)!;

        // Outside activation choice pending — no Esc
        if (player.PendingOutsideActivationSlot >= 0)
            return RunDetail(state, "OUTSIDE \u2014 CHOOSE ACTIVATION",
                BuildOutsideActivationOptions(legal), allowEsc: false)!;

        // Normal turn — full overview
        return RunOverview(state, legal);
    }

    // ── Overview loop ─────────────────────────────────────────────────────

    private IGameAction RunOverview(GameStateSnapshot state, IReadOnlyList<IGameAction> legal)
    {
        var areas    = BuildAreas(state, legal);
        int selected = FirstSelectable(areas, a => a.IsSelectable);

        while (true)
        {
            DrawOverview(state, areas, selected);
            var key = System.Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selected = MovePrev(areas, selected, a => a.IsSelectable);
                    break;

                case ConsoleKey.DownArrow:
                    selected = MoveNext(areas, selected, a => a.IsSelectable);
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                {
                    var area = areas[selected];
                    if (!area.IsSelectable) break;
                    if (area.DirectAction != null) return area.DirectAction;
                    var picked = RunDetail(state, area.Name, area.Options, allowEsc: true);
                    if (picked != null) return picked;
                    // Esc — redraw overview
                    break;
                }

                default:
                    if (TrySwitchView(key.KeyChar)) break;
                    break;
            }
        }
    }

    // ── Detail loop ───────────────────────────────────────────────────────

    private IGameAction? RunDetail(
        GameStateSnapshot state,
        string title,
        IReadOnlyList<ActionEntry> options,
        bool allowEsc)
    {
        bool isLeaf(ActionEntry o) => o.IsEnabled && o.Action != null;
        int selected = FirstSelectable(options, isLeaf);

        while (true)
        {
            DrawDetail(state, title, options, selected, allowEsc);
            var key = System.Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selected = MovePrev(options, selected, isLeaf);
                    break;

                case ConsoleKey.DownArrow:
                    selected = MoveNext(options, selected, isLeaf);
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                    var opt = options[selected];
                    if (opt.IsEnabled && opt.Action != null) return opt.Action;
                    break;

                case ConsoleKey.Escape:
                    if (allowEsc) return null;
                    break;

                default:
                    if (TrySwitchView(key.KeyChar)) break;
                    break;
            }
        }
    }

    // ── Draw: Overview ────────────────────────────────────────────────────

    private void DrawOverview(GameStateSnapshot state, List<AreaEntry> areas, int selectedIndex)
    {
        System.Console.Clear();
        var io = new SystemConsoleIO();
        GameScreenRenderer.RenderHeader(io, state, PlayerColors.Colors);
        GameScreenRenderer.RenderHotkeyBar(io, _currentView);
        GameScreenRenderer.RenderArea(io, state, _currentView, state.Players[state.ActivePlayerIndex].Id);
        DrawHeader(state);
        System.Console.WriteLine();

        for (int i = 0; i < areas.Count; i++)
        {
            var area       = areas[i];
            bool isSelected = i == selectedIndex && area.IsSelectable;

            SetAreaColor(isSelected, area.IsSelectable);
            var prefix = isSelected ? "[>]" : "[ ]";
            System.Console.WriteLine($"  {prefix} {area.Name,-20}  {area.Summary}");
            System.Console.ResetColor();
        }

        DrawPlayers(state);
        DrawEvents();
        System.Console.WriteLine();
        Hint("  \u2191\u2193 navigate  Enter/Space select");
    }

    // ── Draw: Detail ──────────────────────────────────────────────────────

    private void DrawDetail(
        GameStateSnapshot state,
        string title,
        IReadOnlyList<ActionEntry> options,
        int selectedIndex,
        bool allowEsc)
    {
        System.Console.Clear();
        var io = new SystemConsoleIO();
        GameScreenRenderer.RenderHeader(io, state, PlayerColors.Colors);
        GameScreenRenderer.RenderHotkeyBar(io, _currentView);
        GameScreenRenderer.RenderArea(io, state, _currentView, state.Players[state.ActivePlayerIndex].Id);
        DrawHeader(state);
        System.Console.WriteLine();

        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine($"  {title}");
        System.Console.WriteLine($"  {new string('\u2500', 50)}");
        System.Console.ResetColor();
        System.Console.WriteLine();

        for (int i = 0; i < options.Count; i++)
        {
            var opt        = options[i];
            bool isSelected = i == selectedIndex && opt.IsEnabled && opt.Action != null;

            // Section header or info row (Action == null)
            if (opt.Action == null)
            {
                if (opt.IsEnabled) // info row
                {
                    System.Console.ForegroundColor = ConsoleColor.Gray;
                    System.Console.WriteLine($"    {opt.Label}");
                }
                else // section header
                {
                    System.Console.ForegroundColor = ConsoleColor.DarkCyan;
                    System.Console.WriteLine($"  {opt.Label}");
                }
                System.Console.ResetColor();
                continue;
            }

            SetItemColor(isSelected, opt.IsEnabled);
            var prefix  = isSelected ? "[>]" : (opt.IsEnabled ? "[ ]" : "[~]");
            var detail  = opt.Detail is null ? "" : $"  {opt.Detail}";
            System.Console.WriteLine($"    {prefix} {opt.Label}{detail}");
            System.Console.ResetColor();
        }

        DrawPlayers(state);
        DrawEvents();
        System.Console.WriteLine();

        bool hasActions = options.Any(o => o.IsEnabled && o.Action != null);
        var enterHint   = hasActions ? "  Enter/Space select" : "";
        var escHint     = allowEsc ? "  Esc back" : "  (cannot go back)";
        Hint($"  \u2191\u2193 navigate{enterHint}{escHint}");
    }

    // ── Draw: shared ──────────────────────────────────────────────────────

    private static void DrawHeader(GameStateSnapshot s)
    {
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine(
            $"\u2550\u2550 WHITE CASTLE \u2550\u2550  Round {s.CurrentRound}/{s.MaxRounds}  " +
            $"Phase: {s.CurrentPhase}  Active: {s.Players[s.ActivePlayerIndex].Name}");
        System.Console.ResetColor();
    }

    private void DrawPlayers(GameStateSnapshot state)
    {
        System.Console.WriteLine();
        System.Console.ForegroundColor = ConsoleColor.DarkYellow;
        System.Console.WriteLine("  PLAYERS:");
        System.Console.ResetColor();

        for (int i = 0; i < state.Players.Count; i++)
        {
            var p        = state.Players[i];
            bool isActive = i == state.ActivePlayerIndex;
            var r        = p.Resources;

            var seedStr  = p.SeedCard is { } sc ? $"  Seed:{sc.ActionType}" : "";
            var allGains = p.LanternChain.SelectMany(i => i.Gains).Select(g => $"+{g.Amount} {g.GainType}");
            var chainStr = p.LanternChain.Count > 0 ? $"  Lantern:[{string.Join(", ", allGains)}]" : "";
            var vpStr    = $"  VP:{p.LanternScore}";
            var infStr   = p.Influence > 0 ? $"  Inf:{p.Influence}" : "";
            System.Console.ForegroundColor = isActive ? ConsoleColor.White : ConsoleColor.Gray;
            System.Console.WriteLine(
                $"    {(isActive ? "*" : " ")}{p.Name,-16} " +
                $"Fd:{r.Food,2} Fe:{r.Iron,2} VI:{r.MotherOfPearls,2} " +
                $"Coins:{p.Coins,3} Seals:{p.DaimyoSeals}" +
                vpStr + infStr + seedStr + chainStr +
                (p.IsAI ? " [AI]" : ""));
            System.Console.ResetColor();
        }
    }

    private void DrawEvents()
    {
        if (_lastEvents.Count == 0) return;
        System.Console.WriteLine();
        System.Console.ForegroundColor = ConsoleColor.DarkYellow;
        System.Console.WriteLine("  RECENT EVENTS:");
        System.Console.ResetColor();
        foreach (var e in _lastEvents.TakeLast(5))
        {
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.WriteLine($"    >> {ConsoleRenderer.FormatEvent(e)}");
            System.Console.ResetColor();
        }
    }

    private bool TrySwitchView(char ch) =>
        GameScreenRenderer.TryParseHotkey(ch, out var v) && (_currentView = v) == v;

    private static void Hint(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.DarkCyan;
        System.Console.WriteLine(text);
        System.Console.ResetColor();
    }

    private static void SetAreaColor(bool isSelected, bool isSelectable)
    {
        if (!isSelectable)
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
        else if (isSelected)
            System.Console.ForegroundColor = ConsoleColor.White;
        else
            System.Console.ForegroundColor = ConsoleColor.Gray;
    }

    private static void SetItemColor(bool isSelected, bool isEnabled)
    {
        if (!isEnabled)
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
        else if (isSelected)
            System.Console.ForegroundColor = ConsoleColor.White;
        else
            System.Console.ForegroundColor = ConsoleColor.Gray;
    }

    // ── Build Areas ───────────────────────────────────────────────────────

    private List<AreaEntry> BuildAreas(GameStateSnapshot state, IReadOnlyList<IGameAction> legal)
    {
        var areas = new List<AreaEntry>();

        // Bridges
        var bridgeActions = legal.OfType<TakeDieFromBridgeAction>().ToList();
        areas.Add(new AreaEntry(
            "Bridges",
            BridgeSummary(state),
            bridgeActions.Count > 0,
            null,
            BuildBridgeOptions(state, bridgeActions)));

        // Castle — always selectable for inspection; courtier options if pending, else info view
        var castleCourtierActions = legal
            .Where(a => a is CastlePlaceCourtierAction or CastleAdvanceCourtierAction or CastleSkipAction)
            .ToList();
        var castleOptions = castleCourtierActions.Count > 0
            ? BuildCastleCourtierOptions(state, legal)
            : BuildCastleInfoOptions(state);
        areas.Add(new AreaEntry("Castle", CastleSummary(state), true, null, castleOptions));

        // Well — always selectable for inspection (when die in hand, Run() bypasses BuildAreas anyway)
        areas.Add(new AreaEntry("Well", WellSummary(state), true, null, BuildWellInfoOptions(state)));

        // Outside
        var outsideActions = legal.OfType<PlaceDieAction>()
            .Where(a => a.Target is OutsideSlotTarget).ToList();
        IGameAction? outsideDirect = outsideActions.Count == 1 ? outsideActions[0] : null;
        var outsideOptions = outsideActions.Count > 1 ? BuildOutsideOptions(state, legal) : [];
        areas.Add(new AreaEntry("Outside", OutsideSummary(state), outsideActions.Count > 0,
            outsideDirect, (IReadOnlyList<ActionEntry>)outsideOptions));

        // Training Grounds — always selectable for inspection
        var trainActions = legal
            .Where(a => a is TrainingGroundsPlaceSoldierAction or TrainingGroundsSkipAction).ToList();
        var trainOptions = trainActions.Count > 0
            ? BuildTrainingOptions(state, legal)
            : BuildTrainingInfoOptions(state);
        areas.Add(new AreaEntry("Training Grounds", TrainingSummary(state), true, null, trainOptions));

        // Farming Lands — always selectable for inspection
        var farmActions = legal.Where(a => a is PlaceFarmerAction or FarmSkipAction).ToList();
        var farmOptions = farmActions.Count > 0
            ? BuildFarmOptions(state, legal)
            : BuildFarmInfoOptions(state);
        areas.Add(new AreaEntry("Farming Lands", FarmSummary(state), true, null, farmOptions));

        // Personal Domain — always selectable for inspection
        areas.Add(new AreaEntry(
            "Personal Domain",
            PersonalDomainSummary(state),
            true,
            null,
            BuildPersonalDomainInfoOptions(state)));

        return areas;
    }

    // ── Build Options: Bridges ────────────────────────────────────────────

    private static List<ActionEntry> BuildBridgeOptions(
        GameStateSnapshot state,
        List<TakeDieFromBridgeAction> legal)
    {
        var options = new List<ActionEntry>();
        foreach (var bridge in state.Board.Bridges)
        {
            if (bridge.High != null)
            {
                var action = legal.FirstOrDefault(
                    a => a.BridgeColor == bridge.Color && a.DiePosition == DiePosition.High);
                options.Add(new ActionEntry(
                    $"{bridge.Color,-6} High  [{bridge.High.Value}]",
                    null, action != null, action));
            }

            foreach (var mid in bridge.Middle)
                options.Add(InfoRow($"{bridge.Color,-6} Mid   [{mid.Value}]"));

            if (bridge.Low != null)
            {
                var action = legal.FirstOrDefault(
                    a => a.BridgeColor == bridge.Color && a.DiePosition == DiePosition.Low);
                options.Add(new ActionEntry(
                    $"{bridge.Color,-6} Low   [{bridge.Low.Value}]  \u2190 Lantern Effect",
                    null, action != null, action));
            }
        }
        return options;
    }

    // ── Build Options: Place Die (die in hand) ────────────────────────────

    private static List<ActionEntry> BuildPlaceDieOptions(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legal)
    {
        var options    = new List<ActionEntry>();
        var player     = state.Players[state.ActivePlayerIndex];
        var die        = player.DiceInHand[0];
        int pCount     = state.Players.Count;

        // Castle rooms
        options.Add(Header("Castle rooms:"));
        for (int f = 0; f < state.Board.Castle.Floors.Count; f++)
        {
            var floorName = f == 0 ? "Steward Floor" : "Diplomat Floor";
            var rooms     = state.Board.Castle.Floors[f];
            for (int r = 0; r < rooms.Count; r++)
            {
                var ph         = rooms[r];
                var action     = legal.OfType<PlaceDieAction>()
                    .FirstOrDefault(a => a.Target is CastleRoomTarget t && t.Floor == f && t.RoomIndex == r);
                var compareVal = ph.PlacedDice.Count > 0 && pCount >= 3
                    ? ph.PlacedDice[^1].Value : ph.BaseValue;
                var delta      = die.Value - compareVal;
                var tokenStr   = string.Join("", ph.Tokens.Select(FormatToken));
                bool hasToken  = ph.Tokens.Any(t => t.DieColor == die.Color);
                bool full      = !ph.UnlimitedCapacity && ph.PlacedDice.Count >= (pCount >= 3 ? 2 : 1);
                string cardEffect = "";
                if (hasToken && ph.Card != null)
                {
                    int limit   = Math.Min(ph.Tokens.Count, ph.Card.Fields.Count);
                    var effects = new List<string>();
                    for (int i = 0; i < limit; i++)
                        if (ph.Tokens[i].DieColor == die.Color)
                            effects.Add(FormatCardField(ph.Card.Fields[i]));
                    if (effects.Count > 0)
                        cardEffect = "  [" + string.Join("; ", effects) + "]";
                }
                string detail  = full        ? "\u2192 full"
                               : !hasToken   ? $"\u2192 no {die.Color} token"
                               :               $"\u2192 {CoinDeltaStr(delta)}{cardEffect}";
                options.Add(new ActionEntry(
                    $"{floorName} Room {r}  {tokenStr}", detail, action != null, action));
            }
        }

        // Well
        options.Add(Header("Well:"));
        {
            var action     = legal.OfType<PlaceDieAction>().FirstOrDefault(a => a.Target is WellTarget);
            var delta      = die.Value - 1;
            var wellTokens = state.Board.Well.Placeholder.Tokens;
            var tokenStr   = wellTokens.Count > 0
                ? "  [" + string.Join(", ", wellTokens.Select(t => FormatTokenResource(t.ResourceSide))) + "]"
                : "";
            options.Add(new ActionEntry(
                "Well  val=1", $"\u2192 {CoinDeltaStr(delta)}  +1 Seal{tokenStr}", action != null, action));
        }

        // Outside
        options.Add(Header("Outside:"));
        for (int s = 0; s < state.Board.Outside.Slots.Count; s++)
        {
            var ph         = state.Board.Outside.Slots[s];
            var action     = legal.OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is OutsideSlotTarget t && t.SlotIndex == s);
            var compareVal = ph.PlacedDice.Count > 0 && pCount >= 3
                ? ph.PlacedDice[^1].Value : ph.BaseValue;
            var delta      = die.Value - compareVal;
            bool full      = !ph.UnlimitedCapacity && ph.PlacedDice.Count >= (pCount >= 3 ? 2 : 1);
            string detail  = full ? "\u2192 full" : $"\u2192 {CoinDeltaStr(delta)}";
            var slotLabel  = s == 0 ? "Outside Slot 0  (Farm or Castle)  val=5"
                                    : "Outside Slot 1  (TG or Castle)    val=5";
            options.Add(new ActionEntry(slotLabel, detail, action != null, action));
        }

        // Personal Domain rows
        options.Add(Header("Personal Domain:"));
        string[] pdCardLines = SeedCardLines(player.SeedCard?.ActionType);
        for (int r = 0; r < player.PersonalDomainRows.Count; r++)
        {
            var row   = player.PersonalDomainRows[r];
            var pdAct = legal.OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is PersonalDomainTarget t && t.RowIndex == r);
            int uncov = row.Spots.Count(s => s.IsUncovered);
            bool full = row.PlacedDie is not null;
            string gain = FormatPdGain(row, uncov);

            string rowLabelBase = full
                ? $"{row.DieColor} ({row.FigureType})  val={row.CompareValue}  [die placed]"
                : $"{row.DieColor} ({row.FigureType})  val={row.CompareValue}  [{gain}]";
            string cardLine = pdCardLines[r];
            string rowLabel = cardLine.Length > 0
                ? $"{rowLabelBase.PadRight(50)}  {cardLine}"
                : rowLabelBase;
            string rowDetail = pdAct is not null
                ? $"\u2192 {CoinDeltaStr(die.Value - row.CompareValue)}"
                : full ? "\u2192 full" : $"\u2192 need {row.DieColor} die";

            options.Add(new ActionEntry(rowLabel, rowDetail, pdAct != null, pdAct));
        }

        return options;
    }

    // ── Build Options: Outside Activation Choice ──────────────────────────

    private static List<ActionEntry> BuildOutsideActivationOptions(
        IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        options.Add(Header("Choose which area to activate:"));
        foreach (var act in legal.OfType<ChooseOutsideActivationAction>())
        {
            var (label, detail) = act.Choice switch
            {
                OutsideActivation.Farm            => ("Play Farm",             "Place a farmer (or skip)"),
                OutsideActivation.Castle          => ("Play Castle",           "Place/advance a courtier (or skip)"),
                OutsideActivation.TrainingGrounds => ("Play Training Grounds", "Place a soldier (or skip)"),
                _                                 => ("?", ""),
            };
            options.Add(new ActionEntry(label, detail, true, act));
        }
        return options;
    }

    // ── Build Options: Castle Courtiers ───────────────────────────────────

    private static List<ActionEntry> BuildCastleCourtierOptions(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        var player  = state.Players[state.ActivePlayerIndex];

        // Place at gate
        var placeAction = legal.OfType<CastlePlaceCourtierAction>().FirstOrDefault();
        options.Add(new ActionEntry(
            $"Place courtier at gate  (\u22122 coins, {player.CourtiersAvailable} available)",
            null, placeAction != null, placeAction));

        // Advance options
        var advances = legal.OfType<CastleAdvanceCourtierAction>().ToList();
        int vi = player.Resources.MotherOfPearls;

        // Room-entering advances: enumerate one entry per room (player picks which room to enter)
        var roomAdvances = new[]
        {
            (From: CourtierPosition.Gate,        Levels: 1, Label: "Gate \u2192 Ground",  ViCost: 2, FloorIdx: 0, RoomCount: 3),
            (From: CourtierPosition.Gate,        Levels: 2, Label: "Gate \u2192 Mid",     ViCost: 5, FloorIdx: 1, RoomCount: 2),
            (From: CourtierPosition.StewardFloor, Levels: 1, Label: "Ground \u2192 Mid",   ViCost: 2, FloorIdx: 1, RoomCount: 2),
        };
        foreach (var adv in roomAdvances)
        {
            int atFrom = adv.From switch
            {
                CourtierPosition.Gate        => player.CourtiersAtGate,
                CourtierPosition.StewardFloor => player.CourtiersOnStewardFloor,
                _                            => 0,
            };
            string? why = atFrom <= 0 ? $"no courtiers at {adv.From}"
                : vi < adv.ViCost ? $"need {adv.ViCost} VI"
                : null;
            string hdr = why is not null
                ? $"Advance {adv.Label}  (\u2212{adv.ViCost} VI)  [{why}]:"
                : $"Advance {adv.Label}  (\u2212{adv.ViCost} VI)  \u2014 pick a room:";
            options.Add(Header(hdr));

            for (int r = 0; r < adv.RoomCount; r++)
            {
                var roomCard = adv.FloorIdx < state.Board.Castle.Floors.Count
                               && r < state.Board.Castle.Floors[adv.FloorIdx].Count
                    ? state.Board.Castle.Floors[adv.FloorIdx][r].Card
                    : null;
                string cardDesc = roomCard is not null ? roomCard.Id : "(no card)";
                var action = advances.FirstOrDefault(
                    a => a.From == adv.From && a.Levels == adv.Levels && a.RoomIndex == r);
                options.Add(new ActionEntry($"  Room {r}: {cardDesc}", null, action != null, action));
            }
        }

        // Top-floor advances: no room choice
        var topAdvances = new[]
        {
            (From: CourtierPosition.StewardFloor, Levels: 2, Label: "Ground \u2192 Top", ViCost: 5),
            (From: CourtierPosition.DiplomatFloor,    Levels: 1, Label: "Mid \u2192 Top",    ViCost: 2),
        };
        foreach (var adv in topAdvances)
        {
            var action = advances.FirstOrDefault(a => a.From == adv.From && a.Levels == adv.Levels);
            int atFrom = adv.From switch
            {
                CourtierPosition.StewardFloor => player.CourtiersOnStewardFloor,
                CourtierPosition.DiplomatFloor    => player.CourtiersOnDiplomatFloor,
                _                            => 0,
            };
            string? why = atFrom <= 0 ? $"no courtiers at {adv.From}"
                : vi < adv.ViCost ? $"need {adv.ViCost} VI"
                : null;
            options.Add(new ActionEntry(
                $"Advance {adv.Label}  (\u2212{adv.ViCost} VI)",
                why is null ? null : $"[{why}]",
                action != null, action));
        }

        // Skip
        var skip = legal.OfType<CastleSkipAction>().FirstOrDefault();
        options.Add(new ActionEntry("Skip", null, skip != null, skip));

        return options;
    }

    // ── Build Options: Training Grounds ──────────────────────────────────

    private static List<ActionEntry> BuildTrainingOptions(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        foreach (var area in state.Board.TrainingGrounds.Areas)
        {
            var action = legal.OfType<TrainingGroundsPlaceSoldierAction>()
                .FirstOrDefault(a => a.AreaIndex == area.AreaIndex);
            var effect = area.ResourceGain.Count > 0
                ? string.Join(", ", area.ResourceGain.Select(g => $"+{g.Amount} {g.GainType}"))
                : area.ActionDescription;
            options.Add(new ActionEntry(
                $"Area {area.AreaIndex}  ({area.IronCost} iron)  [{effect}]",
                null, action != null, action));
        }
        var skip = legal.OfType<TrainingGroundsSkipAction>().FirstOrDefault();
        options.Add(new ActionEntry("Skip", null, skip != null, skip));
        return options;
    }

    // ── Build Options: Farming Lands ──────────────────────────────────────

    private static List<ActionEntry> BuildFarmOptions(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        foreach (var field in state.Board.FarmingLands.Fields)
        {
            var action  = legal.OfType<PlaceFarmerAction>()
                .FirstOrDefault(a => a.BridgeColor == field.BridgeColor && a.IsInland == field.IsInland);
            var side    = field.IsInland ? "Inland " : "Outside";
            var effect  = field.GainItems.Count > 0
                ? string.Join(", ", field.GainItems.Select(g => $"+{g.Amount} {g.GainType}"))
                : field.ActionDescription;
            var farmers = field.FarmerOwners.Count > 0
                ? "  farmers:" + string.Join(",", field.FarmerOwners)
                : "";
            options.Add(new ActionEntry(
                $"{field.BridgeColor} {side}  ({field.FoodCost} food)  [{effect}]{farmers}",
                null, action != null, action));
        }
        var skip = legal.OfType<FarmSkipAction>().FirstOrDefault();
        options.Add(new ActionEntry("Skip", null, skip != null, skip));
        return options;
    }

    // ── Build Options: Outside slots ──────────────────────────────────────

    private static List<ActionEntry> BuildOutsideOptions(
        GameStateSnapshot state,
        IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        for (int s = 0; s < state.Board.Outside.Slots.Count; s++)
        {
            var action    = legal.OfType<PlaceDieAction>()
                .FirstOrDefault(a => a.Target is OutsideSlotTarget t && t.SlotIndex == s);
            var slotLabel = s == 0 ? "Slot 0  (Farm or Castle)" : "Slot 1  (TG or Castle)";
            options.Add(new ActionEntry(slotLabel, null, action != null, action));
        }
        return options;
    }

    // ── Build Options: Choose Resource ────────────────────────────────────

    private static List<ActionEntry> BuildChooseResourceOptions(IReadOnlyList<IGameAction> legal)
    {
        ActionEntry? Choose(ResourceType rt, string label)
        {
            var a = legal.OfType<ChooseResourceAction>().FirstOrDefault(x => x.Choice == rt);
            return new ActionEntry(label, null, a != null, a);
        }
        return
        [
            Choose(ResourceType.Food,      "Food")!,
            Choose(ResourceType.Iron,      "Iron")!,
            Choose(ResourceType.MotherOfPearls, "Mother of Pearls")!,
        ];
    }

    // ── Build Options: Choose Influence Pay ───────────────────────────────

    private static List<ActionEntry> BuildChooseInfluenceOptions(
        PlayerSnapshot player,
        IReadOnlyList<IGameAction> legal)
    {
        var payAction    = legal.OfType<ChooseInfluencePayAction>().FirstOrDefault(x => x.WillPay);
        var refuseAction = legal.OfType<ChooseInfluencePayAction>().FirstOrDefault(x => !x.WillPay);
        bool canAfford   = player.DaimyoSeals >= player.PendingInfluenceSealCost;
        return
        [
            new ActionEntry(
                $"Pay {player.PendingInfluenceSealCost} seal(s) → gain +{player.PendingInfluenceGain} Influence",
                canAfford ? $"(have {player.DaimyoSeals} seal(s))" : $"[insufficient seals: have {player.DaimyoSeals}]",
                payAction != null && canAfford,
                payAction),
            new ActionEntry(
                $"Refuse — lose the +{player.PendingInfluenceGain} Influence gain",
                null,
                refuseAction != null,
                refuseAction),
        ];
    }

    // ── Summary helpers ───────────────────────────────────────────────────

    private static string BridgeSummary(GameStateSnapshot state)
    {
        var parts = state.Board.Bridges.Select(b =>
        {
            var h = b.High is null ? "_" : b.High.Value.ToString();
            var m = b.Middle.Count > 0 ? $"(+{b.Middle.Count})" : "";
            var l = b.Low  is null ? "_" : b.Low.Value.ToString();
            return $"{b.Color.ToString()[..1]}:[{h}]{m}[{l}]";
        });
        return string.Join("  ", parts);
    }

    private static string CastleSummary(GameStateSnapshot state)
    {
        var top    = state.Board.Castle.TopFloor;
        int filled = top.Slots.Count(s => s.OccupantName != null);
        var topStr = top.CardId.Length > 0 ? $"[{top.CardId}] {filled}/{top.Slots.Count} filled" : "—";
        return $"G:{state.Board.Castle.Floors[0].Count}r  M:{state.Board.Castle.Floors[1].Count}r  Top:{topStr}";
    }

    private static string WellSummary(GameStateSnapshot state)
    {
        var w        = state.Board.Well.Placeholder;
        var tokenStr = w.Tokens.Count > 0
            ? "  " + string.Join(" ", w.Tokens.Select(t => FormatTokenResource(t.ResourceSide)))
            : "";
        return $"{w.PlacedDice.Count} dice  val=1  +1 Seal{tokenStr}";
    }

    private static string OutsideSummary(GameStateSnapshot state)
    {
        var slots = state.Board.Outside.Slots;
        string Slot(DicePlaceholderSnapshot s, int i, string tag) =>
            $"slot{i}({tag}):{(s.PlacedDice.Count > 0 ? $"[{s.PlacedDice[^1].Value}]" : "[ ]")}";
        return $"{Slot(slots[0], 0, "Farm/Castle")}  {Slot(slots[1], 1, "TG/Castle")}  val=5";
    }

    private static string TrainingSummary(GameStateSnapshot state) =>
        $"{state.Board.TrainingGrounds.Areas.Count} areas  " +
        $"(iron: {string.Join("/", state.Board.TrainingGrounds.Areas.Select(a => a.IronCost))})";

    private static string FarmSummary(GameStateSnapshot state) =>
        $"{state.Board.FarmingLands.Fields.Count} fields";

    private static string PersonalDomainSummary(GameStateSnapshot state)
    {
        var player = state.Players[state.ActivePlayerIndex];
        var parts  = player.PersonalDomainRows.Select(row =>
        {
            int uncov   = row.Spots.Count(s => s.IsUncovered);
            string die  = row.PlacedDie is not null ? $"[{row.PlacedDie.Value}]" : "[ ]";
            string gain = FormatPdGain(row, uncov);
            return $"{row.DieColor.ToString()[..1]}({row.FigureType[..2]}){die}\u2192{gain}";
        });
        return string.Join("  ", parts);
    }

    private static string FormatPdGain(PersonalDomainRowSnapshot row, int uncoveredCount)
    {
        var parts = new List<string> { $"+{row.DefaultGainAmount} {row.DefaultGainType}" };
        for (int i = 0; i < uncoveredCount; i++)
            parts.Add($"+{row.Spots[i].GainAmount} {row.Spots[i].GainType}");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Returns the formatted field description for a personal domain card at the given row (0–2).
    /// Ground-floor cards (Layout=null): field[rowIndex].
    /// DoubleTop: field[0] spans rows 0+1, field[1] is row 2.
    /// DoubleBottom: field[0] is row 0, field[1] spans rows 1+2.
    /// </summary>
    private static string PdCardFieldForRow(RoomCardSnapshot card, int rowIndex)
    {
        int? fi = card.Layout switch
        {
            null           => rowIndex < card.Fields.Count ? rowIndex : (int?)null,
            "DoubleTop"    => rowIndex <= 1 ? 0 : 1,
            "DoubleBottom" => rowIndex == 0 ? 0 : 1,
            _              => null,
        };
        if (fi is not { } fieldIdx || fieldIdx >= card.Fields.Count) return "";
        var field = card.Fields[fieldIdx];
        if (field.IsGain && field.Gains is { } gains)
            return $"[{string.Join(", ", gains.Select(g => $"+{g.Amount} {g.GainType}"))}]";
        if (!field.IsGain && field.ActionDescription is { } desc)
            return $"[{desc}]";
        return "";
    }

    // ── Navigation helpers ────────────────────────────────────────────────

    private static int FirstSelectable<T>(IReadOnlyList<T> list, Func<T, bool> pred)
    {
        for (int i = 0; i < list.Count; i++)
            if (pred(list[i])) return i;
        return 0;
    }

    private static int MovePrev<T>(IReadOnlyList<T> list, int current, Func<T, bool> pred)
    {
        for (int i = current - 1; i >= 0; i--)
            if (pred(list[i])) return i;
        return current;
    }

    private static int MoveNext<T>(IReadOnlyList<T> list, int current, Func<T, bool> pred)
    {
        for (int i = current + 1; i < list.Count; i++)
            if (pred(list[i])) return i;
        return current;
    }

    // ── Format helpers ────────────────────────────────────────────────────

    private static string[] SeedCardLines(string? actionType)
    {
        if (actionType is null) return ["", "", ""];
        const int w = 20; // inner content width — fits "PlayTrainingGrounds" + 1 space
        string top    = $"\u2554{new string('\u2550', w + 2)}\u2557";
        string middle = $"\u2551 {actionType.PadRight(w)} \u2551";
        string bottom = $"\u255a{new string('\u2550', w + 2)}\u255d";
        return [top, middle, bottom];
    }

    private static string CoinDeltaStr(int delta) => delta switch
    {
        > 0 => $"+{delta} coins",
        0   => "0 coins",
        _   => $"\u2212{-delta} coins (need {-delta})",
    };

    private static string FormatToken(TokenSnapshot t)
    {
        var c = t.DieColor switch
        {
            BridgeColor.Red   => "R",
            BridgeColor.Black => "B",
            BridgeColor.White => "W",
            _                 => "?",
        };
        var r = t.ResourceSide switch
        {
            TokenResource.Food        => "Fd",
            TokenResource.Iron        => "Fe",
            TokenResource.MotherOfPearls   => "VI",
            TokenResource.AnyResource => "Any",
            TokenResource.Coin        => "Coin",
            _                         => "?",
        };
        return $"[{c}:{r}]";
    }

    private static string FormatTokenResource(TokenResource r) => r switch
    {
        TokenResource.Food        => "+1 Food",
        TokenResource.Iron        => "+1 Iron",
        TokenResource.MotherOfPearls   => "+1 VI",
        TokenResource.AnyResource => "+1 Any (choose)",
        TokenResource.Coin        => "+1 Coin",
        _                         => "+1 ?"
    };

    private static string FormatCardField(CardFieldSnapshot field)
    {
        if (field.IsGain && field.Gains is { Count: > 0 })
            return string.Join(", ", field.Gains.Select(g => $"+{g.Amount} {g.GainType}"));
        if (!field.IsGain && field.ActionDescription is { } desc)
        {
            if (field.ActionCost is { Count: > 0 })
            {
                var cost = string.Join(", ", field.ActionCost.Select(c => $"\u2212{c.Amount} {c.CostType}"));
                return $"{desc} ({cost})";
            }
            return desc;
        }
        return "\u2014";
    }

    private static ActionEntry Header(string text) =>
        new(text, null, false, null);

    private static ActionEntry InfoRow(string text) =>
        new(text, null, true, null);

    // ── Info views: Well ──────────────────────────────────────────────────

    private static List<ActionEntry> BuildWellInfoOptions(GameStateSnapshot state)
    {
        var options = new List<ActionEntry>();
        var w       = state.Board.Well.Placeholder;
        options.Add(Header("Well  (compare val=1)"));
        options.Add(InfoRow($"Gains on placement: coins by delta  +1 Daimyo Seal"));
        options.Add(InfoRow($"Dice placed so far: {w.PlacedDice.Count}"));
        if (w.Tokens.Count > 0)
        {
            options.Add(Header("Token effects (once per placement)"));
            foreach (var t in w.Tokens)
                options.Add(InfoRow(FormatTokenResource(t.ResourceSide)));
        }
        return options;
    }

    // ── Info views: Castle ────────────────────────────────────────────────

    private static List<ActionEntry> BuildCastleInfoOptions(GameStateSnapshot state)
    {
        var options = new List<ActionEntry>();
        string[] floorNames = ["Steward Floor", "Diplomat Floor"];

        for (int f = 0; f < state.Board.Castle.Floors.Count; f++)
        {
            var rooms = state.Board.Castle.Floors[f];
            options.Add(Header($"{floorNames[f]}  (base val {rooms[0].BaseValue})"));
            for (int r = 0; r < rooms.Count; r++)
            {
                var ph      = rooms[r];
                var tokens  = string.Join("", ph.Tokens.Select(FormatToken));
                var diceStr = ph.PlacedDice.Count == 0 ? "empty"
                    : string.Join("", ph.PlacedDice.Select(d => $"[{d.Color.ToString()[..1]}:{d.Value}]"));
                options.Add(InfoRow($"Room {r}  {tokens}  →  {diceStr}"));
                if (ph.Card != null)
                {
                    int limit = Math.Min(ph.Tokens.Count, ph.Card.Fields.Count);
                    for (int i = 0; i < limit; i++)
                    {
                        var colorLabel = ph.Tokens[i].DieColor switch
                        {
                            BridgeColor.Red   => "Red  ",
                            BridgeColor.Black => "Black",
                            BridgeColor.White => "White",
                            _                 => "?    "
                        };
                        options.Add(InfoRow($"  {colorLabel}: {FormatCardField(ph.Card.Fields[i])}"));
                    }
                }
            }
        }

        var top = state.Board.Castle.TopFloor;
        options.Add(Header($"Top Floor  [{top.CardId}]"));
        foreach (var slot in top.Slots)
        {
            if (slot.OccupantName != null)
                options.Add(InfoRow($"Slot {slot.SlotIndex}: {slot.OccupantName}"));
            else
            {
                var gains = slot.Gains.Count > 0
                    ? "  gains: " + string.Join(", ", slot.Gains.Select(g => $"+{g.Amount} {g.GainType}"))
                    : "";
                options.Add(InfoRow($"Slot {slot.SlotIndex}: (empty){gains}"));
            }
        }

        options.Add(Header("Courtiers"));
        foreach (var p in state.Players)
            options.Add(InfoRow(
                $"{p.Name}  gate:{p.CourtiersAtGate}  ground:{p.CourtiersOnStewardFloor}  " +
                $"mid:{p.CourtiersOnDiplomatFloor}  top:{p.CourtiersOnTopFloor}"));

        return options;
    }

    // ── Info views: Training Grounds ──────────────────────────────────────

    private static List<ActionEntry> BuildTrainingInfoOptions(GameStateSnapshot state)
    {
        var options = new List<ActionEntry>();
        foreach (var area in state.Board.TrainingGrounds.Areas)
        {
            options.Add(Header($"Area {area.AreaIndex}  ({area.IronCost} iron)"));
            var effect = area.ResourceGain.Count > 0
                ? string.Join(", ", area.ResourceGain.Select(g => $"+{g.Amount} {g.GainType}"))
                : area.ActionDescription;
            options.Add(InfoRow($"Effect: {effect}"));
            var soldiers = area.SoldierOwners.Count > 0
                ? string.Join(", ", area.SoldierOwners) : "none";
            options.Add(InfoRow($"Soldiers: {soldiers}"));
        }
        return options;
    }

    // ── Info views: Farming Lands ─────────────────────────────────────────

    private static List<ActionEntry> BuildFarmInfoOptions(GameStateSnapshot state)
    {
        var options = new List<ActionEntry>();
        foreach (var field in state.Board.FarmingLands.Fields)
        {
            var side = field.IsInland ? "Inland " : "Outside";
            options.Add(Header($"{field.BridgeColor} {side}  ({field.FoodCost} food)"));
            var effect = field.GainItems.Count > 0
                ? string.Join(", ", field.GainItems.Select(g => $"+{g.Amount} {g.GainType}"))
                : field.ActionDescription;
            options.Add(InfoRow($"Effect: {effect}"));
            var farmers = field.FarmerOwners.Count > 0
                ? string.Join(", ", field.FarmerOwners) : "none";
            options.Add(InfoRow($"Farmers: {farmers}"));
        }
        return options;
    }

    // ── Info views: Personal Domain ───────────────────────────────────────

    private static List<ActionEntry> BuildPersonalDomainInfoOptions(GameStateSnapshot state)
    {
        var options = new List<ActionEntry>();
        var player  = state.Players[state.ActivePlayerIndex];

        // Compact side-by-side overview: 3 rows + seed card box + PD card fields
        string[] cardLines = SeedCardLines(player.SeedCard?.ActionType);
        for (int r = 0; r < player.PersonalDomainRows.Count; r++)
        {
            var row   = player.PersonalDomainRows[r];
            int uncov = row.Spots.Count(s => s.IsUncovered);
            string die  = row.PlacedDie is not null ? $"[{row.PlacedDie.Value}]" : "[ ]";
            string gain = FormatPdGain(row, uncov);
            string rowLine  = $"{row.DieColor,-7} ({row.FigureType,-8})  val={row.CompareValue}  {die}  {gain}";
            var sb = new System.Text.StringBuilder(rowLine.PadRight(50));
            string cardLine = cardLines[r];
            if (cardLine.Length > 0)
                sb.Append($"  {cardLine}");
            foreach (var pdCard in player.PersonalDomainCards)
            {
                string pdField = PdCardFieldForRow(pdCard, r);
                if (pdField.Length > 0)
                    sb.Append($"  {pdCard.Id}:{pdField}");
            }
            options.Add(InfoRow(sb.ToString()));
        }

        options.Add(Header(""));

        // Detailed breakdown
        foreach (var row in player.PersonalDomainRows)
        {
            int uncov  = row.Spots.Count(s => s.IsUncovered);
            string die = row.PlacedDie is not null ? $"  die=[{row.PlacedDie.Value}]" : "";
            options.Add(Header($"{row.DieColor} ({row.FigureType})  val={row.CompareValue}{die}"));
            options.Add(InfoRow($"Default gain: +{row.DefaultGainAmount} {row.DefaultGainType}"));
            options.Add(InfoRow($"Spots ({uncov}/{row.Spots.Count} uncovered):"));
            for (int i = 0; i < row.Spots.Count; i++)
            {
                var spot = row.Spots[i];
                var status = spot.IsUncovered ? "uncovered" : "covered";
                options.Add(InfoRow($"  Spot {i}: +{spot.GainAmount} {spot.GainType}  [{status}]"));
            }
        }

        // Personal domain cards
        if (player.PersonalDomainCards.Count > 0)
        {
            string[] rowLabels = ["Red/Courtier", "White/Farmer", "Black/Soldier"];
            options.Add(Header(""));
            options.Add(Header("Personal Domain Cards:"));
            foreach (var pdCard in player.PersonalDomainCards)
            {
                string layoutDesc = pdCard.Layout ?? "3-field";
                options.Add(InfoRow($"  {pdCard.Id}  ({layoutDesc})"));
                for (int r = 0; r < 3; r++)
                {
                    string fieldDesc = PdCardFieldForRow(pdCard, r);
                    if (fieldDesc.Length > 0)
                        options.Add(InfoRow($"    Row {r} ({rowLabels[r]}): {fieldDesc}"));
                }
            }
        }

        // Lantern chain
        options.Add(Header(""));
        options.Add(Header("Lantern Chain:"));
        if (player.LanternChain.Count == 0)
        {
            options.Add(InfoRow("  (empty)"));
        }
        else
        {
            for (int i = 0; i < player.LanternChain.Count; i++)
            {
                var item  = player.LanternChain[i];
                var gains = string.Join(", ", item.Gains.Select(g => $"+{g.Amount} {g.GainType}"));
                options.Add(InfoRow($"  [{i + 1}] {item.SourceCardType,-14} {item.SourceCardId}  →  {gains}"));
            }
        }

        return options;
    }

    // ── Seed card selection ───────────────────────────────────────────────

    private static List<ActionEntry> BuildSeedPairOptions(
        GameStateSnapshot state, IReadOnlyList<IGameAction> legal)
    {
        var options = new List<ActionEntry>();
        var player  = state.Players[state.ActivePlayerIndex];

        options.Add(Header($"Choose one pair (action + starting resources)  —  {state.SeedPairs.Count} remaining"));
        options.Add(Header(""));

        for (int i = 0; i < state.SeedPairs.Count; i++)
        {
            var pair   = state.SeedPairs[i];
            var act    = legal.OfType<ChooseSeedPairAction>().FirstOrDefault(a => a.PairIndex == i);
            var label  = $"{pair.Action.ActionType,-24}  |  {FormatSeedResourceCard(pair.Resource, pair.Action.Back)}";
            options.Add(new ActionEntry(label, null, act != null, act));
        }

        if (player.SeedCard is { } existing)
        {
            options.Add(Header(""));
            options.Add(InfoRow($"Already chosen: {existing.ActionType}"));
        }

        return options;
    }

    private static string FormatSeedResourceCard(SeedResourceCardSnapshot card, LanternChainGainSnapshot actionBack)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(string.Join(", ", card.Gains.Select(g => $"+{g.Amount} {g.GainType}")));
        sb.Append($"  [chain: +{card.Back.Amount} {card.Back.GainType}, +{actionBack.Amount} {actionBack.GainType}]");
        if (card.DecreeGain is { } dg)
            sb.Append($"  [decree: +{dg.Amount} {dg.GainType}]");
        return sb.ToString();
    }
}
