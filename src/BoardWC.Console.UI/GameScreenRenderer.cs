using BoardWC.Engine.Domain;

namespace BoardWC.Console.UI;

internal static class GameScreenRenderer
{
    internal static void RenderHeader(
        IConsoleIO console,
        GameStateSnapshot state,
        IReadOnlyList<ConsoleColor> playerColors)
    {
        console.WriteColored($"Round {state.CurrentRound}/{state.MaxRounds}", ConsoleColor.Yellow);

        for (int i = 0; i < state.Players.Count; i++)
        {
            var p     = state.Players[i];
            bool active = i == state.ActivePlayerIndex;
            var color = i < playerColors.Count ? playerColors[i] : ConsoleColor.Gray;
            var prefix = active ? "▲" : " ";
            console.WriteColored(
                $"{prefix} {p.Name,-16}  VP:{p.LanternScore,3}  Inf:{p.Influence,2}",
                color);
        }
    }

    internal static void RenderHotkeyBar(IConsoleIO console, GameAreaView active)
    {
        var labels = new[]
        {
            (GameAreaView.Castle,           "Castle"),
            (GameAreaView.TrainingGrounds,  "Training"),
            (GameAreaView.BridgesFarmlands, "Bridges"),
            (GameAreaView.WellOutside,      "Well/Outside"),
            (GameAreaView.PersonalDomain,   "Personal Domain"),
        };

        var parts = new string[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var (view, label) = labels[i];
            var num = (int)view;
            var mark = view == active ? "*" : " ";
            parts[i] = $"[{num}]{mark}{label}";
        }

        console.WriteLine(string.Join("  ", parts));
    }

    internal static void RenderArea(
        IConsoleIO console,
        GameStateSnapshot state,
        GameAreaView area,
        Guid activePlayerId)
    {
        switch (area)
        {
            case GameAreaView.Castle:
                RenderCastle(console, state);
                break;
            case GameAreaView.TrainingGrounds:
                RenderTrainingGrounds(console, state);
                break;
            case GameAreaView.BridgesFarmlands:
                RenderBridgesFarmlands(console, state);
                break;
            case GameAreaView.WellOutside:
                RenderWellOutside(console, state);
                break;
            case GameAreaView.PersonalDomain:
                RenderPersonalDomain(console, state, activePlayerId);
                break;
        }
    }

    internal static bool TryParseHotkey(char ch, out GameAreaView view)
    {
        switch (ch)
        {
            case '1': view = GameAreaView.Castle;           return true;
            case '2': view = GameAreaView.TrainingGrounds;  return true;
            case '3': view = GameAreaView.BridgesFarmlands; return true;
            case '4': view = GameAreaView.WellOutside;      return true;
            case '5': view = GameAreaView.PersonalDomain;   return true;
            default:  view = GameAreaView.Castle;           return false;
        }
    }

    private static void RenderCastle(IConsoleIO console, GameStateSnapshot state)
    {
        console.WriteLine("  CASTLE");
        var castle = state.Board.Castle;

        var topFloor = castle.TopFloor;
        console.WriteLine($"    Top Floor [{topFloor.CardId}]:");
        for (int i = 0; i < topFloor.Slots.Count; i++)
        {
            var slot = topFloor.Slots[i];
            var gains = slot.Gains.Count > 0
                ? string.Join(", ", slot.Gains.Select(g => $"+{g.Amount} {g.GainType}"))
                : "(none)";
            var occupant = slot.OccupantName ?? "(empty)";
            console.WriteLine($"      Slot {i}: {gains} — {occupant}");
        }

        for (int f = 0; f < castle.Floors.Count; f++)
        {
            var floorName = f == 0 ? "Steward" : "Diplomat";
            console.WriteLine($"    Floor {floorName}:");
            var floor = castle.Floors[f];
            for (int r = 0; r < floor.Count; r++)
            {
                var room = floor[r];
                var dice = room.PlacedDice.Count > 0
                    ? string.Join(" ", room.PlacedDice.Select(d => $"[{d.Value}]"))
                    : "(empty)";
                var tokens = room.Tokens.Count > 0
                    ? string.Join(" ", room.Tokens.Select(FormatToken))
                    : "";
                var cardFields = room.Card != null
                    ? FormatCardFields(room.Card)
                    : "";
                console.WriteLine($"      Room {r}: {dice} {tokens} {cardFields}".TrimEnd());
            }
        }
    }

    private static void RenderTrainingGrounds(IConsoleIO console, GameStateSnapshot state)
    {
        console.WriteLine("  TRAINING GROUNDS");
        foreach (var area in state.Board.TrainingGrounds.Areas)
        {
            var effects = area.ResourceGain.Count > 0
                ? string.Join(", ", area.ResourceGain.Select(g => $"+{g.Amount} {g.GainType}"))
                : area.ActionDescription;
            var soldiers = area.SoldierOwners.Count > 0
                ? $" [{string.Join(", ", area.SoldierOwners)}]"
                : "";
            console.WriteLine($"    Area {area.AreaIndex} ({area.IronCost} iron): {effects}{soldiers}");
        }
    }

    private static void RenderBridgesFarmlands(IConsoleIO console, GameStateSnapshot state)
    {
        console.WriteLine("  BRIDGES");
        foreach (var bridge in state.Board.Bridges)
        {
            var high = bridge.High != null ? $"[{bridge.High.Value}]" : "(empty)";
            var mid  = bridge.Middle.Count > 0
                ? string.Join(", ", bridge.Middle.Select(d => $"[{d.Value}]"))
                : "(empty)";
            var low  = bridge.Low != null ? $"[{bridge.Low.Value}]" : "(empty)";
            console.WriteLine($"    {bridge.Color}: High {high}  Mid [{mid}]  Low {low}");
        }

        console.WriteLine("  FARMING LANDS");
        var colors = state.Board.Bridges.Select(b => b.Color).Distinct();
        foreach (var color in colors)
        {
            var fields = state.Board.FarmingLands.Fields.Where(f => f.BridgeColor == color).ToList();
            var inland  = fields.FirstOrDefault(f => f.IsInland);
            var outside = fields.FirstOrDefault(f => !f.IsInland);

            var inlandStr  = inland  != null ? FormatFarmField(inland,  "Inland")  : "";
            var outsideStr = outside != null ? FormatFarmField(outside, "Outside") : "";

            if (inlandStr != "" || outsideStr != "")
                console.WriteLine($"    {color}: {inlandStr}  {outsideStr}".TrimEnd());
        }
    }

    private static string FormatFarmField(FarmFieldSnapshot f, string label)
    {
        var effect = f.GainItems.Count > 0
            ? string.Join(", ", f.GainItems.Select(g => $"+{g.Amount} {g.GainType}"))
            : f.ActionDescription;
        var farmers = f.FarmerOwners.Count > 0
            ? $"[{string.Join(", ", f.FarmerOwners)}]"
            : "";
        return $"{label}({f.FoodCost}):[{effect}]{farmers}";
    }

    private static void RenderWellOutside(IConsoleIO console, GameStateSnapshot state)
    {
        console.WriteLine("  WELL");
        var well = state.Board.Well.Placeholder;
        var wellDice = well.PlacedDice.Count > 0
            ? string.Join(" ", well.PlacedDice.Select(d => $"[{d.Value}]"))
            : "(empty)";
        var wellTokens = well.Tokens.Count > 0
            ? string.Join(" ", well.Tokens.Select(FormatToken))
            : "(none)";
        console.WriteLine($"    Dice: {wellDice}  Tokens: {wellTokens}");

        console.WriteLine("  OUTSIDE SLOTS");
        for (int i = 0; i < state.Board.Outside.Slots.Count; i++)
        {
            var slot = state.Board.Outside.Slots[i];
            var dice = slot.PlacedDice.Count > 0
                ? string.Join(" ", slot.PlacedDice.Select(d => $"[{d.Value}]"))
                : "(empty)";
            console.WriteLine($"    Slot {i}: {dice}");
        }
    }

    private static void RenderPersonalDomain(
        IConsoleIO console,
        GameStateSnapshot state,
        Guid activePlayerId)
    {
        var player = state.Players.FirstOrDefault(p => p.Id == activePlayerId)
                     ?? state.Players[0];

        console.WriteLine($"  PERSONAL DOMAIN — {player.Name}");

        for (int i = 0; i < player.PersonalDomainRows.Count; i++)
        {
            var row = player.PersonalDomainRows[i];
            var placedDie = row.PlacedDie != null ? $"[{row.PlacedDie.Value}]" : "(free)";
            var uncovered = row.Spots.Count(s => s.IsUncovered);
            var gains = row.Spots.Where(s => s.IsUncovered)
                .Select(s => $"+{s.GainAmount} {s.GainType}");
            var gainsStr = string.Join(", ", gains);
            console.WriteLine($"    Row {i} ({row.DieColor}): {placedDie} — {uncovered} spots uncovered — {gainsStr}");
        }
    }

    private static string FormatToken(TokenSnapshot t)
    {
        var side = t.IsResourceSideUp ? t.ResourceSide.ToString() : "Seal";
        var colorStr = t.DieColor.ToString();
        return $"{colorStr[0]}{side[0]}";
    }

    private static string FormatCardFields(RoomCardSnapshot card)
    {
        var fields = card.Fields.Select(f =>
        {
            if (f.IsGain && f.Gains != null)
                return string.Join("+", f.Gains.Select(g => $"{g.Amount}{g.GainType}"));
            if (f.ActionDescription != null)
                return f.ActionDescription;
            return "?";
        });
        return $"[{string.Join("|", fields)}]";
    }
}
