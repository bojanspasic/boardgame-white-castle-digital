using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;

namespace BoardWC.Console.Input;

internal sealed record ParseResult(bool Success, IGameAction? Action, string? ErrorMessage)
{
    public static ParseResult Ok(IGameAction action)  => new(true, action, null);
    public static ParseResult Err(string msg)         => new(false, null, msg);
}

/// <summary>
/// Maps free-text console commands to typed IGameAction objects.
///
/// Commands:
///   bridge red|black|white  high|low          → TakeDieFromBridgeAction
///   place  castle <floor> <room>              → PlaceDieAction(CastleRoomTarget)
///   place  well                               → PlaceDieAction(WellTarget)
///   place  outside <0|1>                      → PlaceDieAction(OutsideSlotTarget)
///   pass                                      → PassAction
///   start                                     → StartGameAction
///   help                                      → shows help text
/// </summary>
internal sealed class ConsoleInputParser
{
    public ParseResult Parse(string raw, GameStateSnapshot state)
    {
        var parts = raw.Trim().ToLowerInvariant()
                       .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ParseResult.Err("Empty input. Type 'help'.");

        var playerId = state.Players[state.ActivePlayerIndex].Id;

        return parts[0] switch
        {
            "bridge" => ParseBridge(parts, playerId),
            "place"  => ParsePlace(parts, playerId),
            "choose" => ParseChoose(parts, playerId),
            "pass"   => ParseResult.Ok(new PassAction(playerId)),
            "start"  => ParseResult.Ok(new StartGameAction()),
            "help"   => ParseResult.Err(HelpText()),
            _        => ParseResult.Err($"Unknown command '{parts[0]}'. Type 'help'."),
        };
    }

    private static ParseResult ParseBridge(string[] parts, Guid playerId)
    {
        if (parts.Length < 3)
            return ParseResult.Err("Usage: bridge <red|black|white> <high|low>");

        if (!TryParseBridgeColor(parts[1], out var color))
            return ParseResult.Err($"Unknown bridge '{parts[1]}'. Use: red, black, white.");

        if (!TryParseDiePosition(parts[2], out var position))
            return ParseResult.Err($"Unknown position '{parts[2]}'. Use: high, low.");

        return ParseResult.Ok(new TakeDieFromBridgeAction(playerId, color, position));
    }

    private static ParseResult ParsePlace(string[] parts, Guid playerId)
    {
        if (parts.Length < 2)
            return ParseResult.Err("Usage: place <castle <floor> <room> | well | outside <0|1>>");

        return parts[1] switch
        {
            "castle"  => ParsePlaceCastle(parts, playerId),
            "well"    => ParseResult.Ok(new PlaceDieAction(playerId, new WellTarget())),
            "outside" => ParsePlaceOutside(parts, playerId),
            _ => ParseResult.Err($"Unknown placement area '{parts[1]}'. Use: castle, well, outside."),
        };
    }

    private static ParseResult ParsePlaceCastle(string[] parts, Guid playerId)
    {
        if (parts.Length < 4)
            return ParseResult.Err("Usage: place castle <floor> <room>  (floor: 0=ground/1=mid)");

        if (!int.TryParse(parts[2], out var floor) || floor < 0 || floor > 1)
            return ParseResult.Err("Floor must be 0 (ground) or 1 (mid).");

        int maxRoom = floor == 0 ? 2 : 1;
        if (!int.TryParse(parts[3], out var room) || room < 0 || room > maxRoom)
            return ParseResult.Err($"Room must be 0–{maxRoom} for floor {floor}.");

        return ParseResult.Ok(new PlaceDieAction(playerId, new CastleRoomTarget(floor, room)));
    }

    private static ParseResult ParseChoose(string[] parts, Guid playerId)
    {
        if (parts.Length < 2)
            return ParseResult.Err("Usage: choose <food|iron|valueitem>");

        return parts[1] switch
        {
            "food"      => ParseResult.Ok(new ChooseResourceAction(playerId, ResourceType.Food)),
            "iron"      => ParseResult.Ok(new ChooseResourceAction(playerId, ResourceType.Iron)),
            "valueitem" => ParseResult.Ok(new ChooseResourceAction(playerId, ResourceType.ValueItem)),
            _ => ParseResult.Err($"Unknown resource '{parts[1]}'. Use: food, iron, valueitem."),
        };
    }

    private static ParseResult ParsePlaceOutside(string[] parts, Guid playerId)
    {
        if (parts.Length < 3)
            return ParseResult.Err("Usage: place outside <0|1>");

        if (!int.TryParse(parts[2], out var slot) || slot < 0 || slot > 1)
            return ParseResult.Err("Outside slot must be 0 or 1.");

        return ParseResult.Ok(new PlaceDieAction(playerId, new OutsideSlotTarget(slot)));
    }

    private static bool TryParseBridgeColor(string s, out BridgeColor result) =>
        Enum.TryParse(Capitalize(s), out result);

    private static bool TryParseDiePosition(string s, out DiePosition result) =>
        Enum.TryParse(Capitalize(s), out result);

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string HelpText() =>
        """

        Commands:
          bridge <red|black|white> <high|low>        — take a die from a bridge
          place castle <floor(0-1)> <room(0-2)>      — place die in castle room
          place well                                 — place die at the well
          place outside <0|1>                        — place die at an outside slot
          choose <food|iron|valueitem>               — choose resource from AnyResource token (well)
          pass                                       — pass your turn
          start                                      — start the game (from Setup phase)
          help                                       — show this message

        Castle: floor 0 = ground (3 rooms, value 3), floor 1 = mid (2 rooms, value 4)
        Well:   value 1, unlimited capacity
        Outside: value 5, 2 slots
        Earn coins when die > slot value; spend coins when die < slot value.
        """;
}
