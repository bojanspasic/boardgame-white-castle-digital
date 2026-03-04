using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChooseCastleCardFieldHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseCastleCardFieldAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (ChooseCastleCardFieldAction)action;

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingCastleCardFieldFilter is null)
            return ValidationResult.Fail("No pending castle card field choice to resolve.");

        if (a.Floor == -1)
            return ValidationResult.Ok(); // skip is always valid

        if (a.Floor < 0 || a.Floor > 1)
            return ValidationResult.Fail("Floor must be 0 or 1.");

        var floors = state.Board.CastleFloors;
        if (a.Floor >= floors.Count)
            return ValidationResult.Fail("Invalid floor index.");
        var rooms = floors[a.Floor];
        if (a.RoomIndex < 0 || a.RoomIndex >= rooms.Count)
            return ValidationResult.Fail("Invalid room index.");

        var ph = rooms[a.RoomIndex];
        if (ph.Card is not { } card)
            return ValidationResult.Fail("That castle room has no card.");

        if (a.FieldIndex < 0 || a.FieldIndex >= card.Fields.Count)
            return ValidationResult.Fail("Invalid field index.");

        string filter = player.PendingCastleCardFieldFilter;
        if (filter == "Red"   && !ph.Tokens.Any(t => t.DieColor == BridgeColor.Red))
            return ValidationResult.Fail("The chosen room has no red token (filter: Red).");
        if (filter == "Black" && !ph.Tokens.Any(t => t.DieColor == BridgeColor.Black))
            return ValidationResult.Fail("The chosen room has no black token (filter: Black).");
        if (filter == "White" && !ph.Tokens.Any(t => t.DieColor == BridgeColor.White))
            return ValidationResult.Fail("The chosen room has no white token (filter: White).");
        // "Any" and "GainOnly" have no room-level token restriction

        if (filter == "GainOnly" && card.Fields[a.FieldIndex] is not GainCardField)
            return ValidationResult.Fail("The selected field is not a gain field (filter: GainOnly).");

        // Check that the player can afford the cost if this is an action field
        if (card.Fields[a.FieldIndex] is ActionCardField af)
        {
            foreach (var cost in af.Cost)
            {
                switch (cost.Type)
                {
                    case CardCostType.Coin when player.Coins < cost.Amount:
                        return ValidationResult.Fail(
                            $"Need {cost.Amount} coins to activate this field; have {player.Coins}.");
                    case CardCostType.MonarchialSeal when player.MonarchialSeals < cost.Amount:
                        return ValidationResult.Fail(
                            $"Need {cost.Amount} seals to activate this field; have {player.MonarchialSeals}.");
                }
            }
        }

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseCastleCardFieldAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        player.PendingCastleCardFieldFilter = null;

        if (a.Floor == -1)
        {
            events.Add(new CastleCardFieldChosenEvent(
                state.GameId, player.Id, -1, -1, -1,
                new ResourceBag(), 0, 0, 0, 0, 0, null));
            return;
        }

        var ph   = state.Board.CastleFloors[a.Floor][a.RoomIndex];
        var card = ph.Card!;
        var field = card.Fields[a.FieldIndex];

        var resources = new ResourceBag();
        int coins = 0, seals = 0, lantern = 0, vp = 0, influence = 0;
        string? actionTriggered = null;

        if (field is GainCardField gf)
        {
            foreach (var item in gf.Gains)
            {
                switch (item.Type)
                {
                    case CardGainType.Food:           resources = resources.Add(ResourceType.Food,      item.Amount); break;
                    case CardGainType.Iron:           resources = resources.Add(ResourceType.Iron,      item.Amount); break;
                    case CardGainType.ValueItem:      resources = resources.Add(ResourceType.ValueItem, item.Amount); break;
                    case CardGainType.Coin:           coins     += item.Amount; break;
                    case CardGainType.MonarchialSeal: seals     += item.Amount; break;
                    case CardGainType.Lantern:        lantern   += item.Amount; break;
                    case CardGainType.VictoryPoint:   vp        += item.Amount; break;
                    case CardGainType.Influence:      influence += item.Amount; break;
                }
            }

            player.Resources       = (player.Resources + resources).Clamp(7);
            player.Coins          += coins;
            player.MonarchialSeals = Math.Min(player.MonarchialSeals + seals, 5);
            LanternHelper.Apply(player, lantern, state.GameId, events);
            player.LanternScore   += vp;
            InfluenceHelper.Apply(player, influence, state, events);
        }
        else if (field is ActionCardField af)
        {
            // Pay any costs
            foreach (var cost in af.Cost)
            {
                switch (cost.Type)
                {
                    case CardCostType.Coin:          player.Coins          -= cost.Amount; break;
                    case CardCostType.MonarchialSeal: player.MonarchialSeals -= cost.Amount; break;
                }
            }

            actionTriggered = af.Description;
            ApplyActionDescription(af.Description, player);
        }

        events.Add(new CastleCardFieldChosenEvent(
            state.GameId, player.Id, a.Floor, a.RoomIndex, a.FieldIndex,
            resources, coins, seals, lantern, vp, influence, actionTriggered));
    }

    private static void ApplyActionDescription(string description, Domain.Player player)
    {
        switch (description)
        {
            case "Play castle":
                player.CastlePlaceRemaining++;
                player.CastleAdvanceRemaining++;
                break;
            case "Play training grounds":
                player.PendingTrainingGroundsActions++;
                break;
            case "Play farm":
                player.PendingFarmActions++;
                break;
            case "Play red castle card field":
                player.PendingCastleCardFieldFilter = "Red";
                break;
            case "Play black castle card field":
                player.PendingCastleCardFieldFilter = "Black";
                break;
            case "Play white castle card field":
                player.PendingCastleCardFieldFilter = "White";
                break;
            case "Play any castle card field":
                player.PendingCastleCardFieldFilter = "Any";
                break;
            case "Play castle gain field":
                player.PendingCastleCardFieldFilter = "GainOnly";
                break;
            case "Play personal domain row":
                player.PendingPersonalDomainRowChoice = true;
                break;
        }
    }
}
