using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class FarmHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is PlaceFarmerAction or FarmSkipAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        Guid playerId = action switch
        {
            PlaceFarmerAction a => a.PlayerId,
            FarmSkipAction    a => a.PlayerId,
            _                   => Guid.Empty,
        };

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != playerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingFarmActions <= 0)
            return ValidationResult.Fail("No pending farm action to resolve.");

        if (action is PlaceFarmerAction place)
        {
            if (player.FarmersAvailable <= 0)
                return ValidationResult.Fail("No farmers available to place.");

            var field = state.Board.FarmingLands.GetField(place.BridgeColor, place.IsInland);
            if (player.Resources.Food < field.Card.FoodCost)
                return ValidationResult.Fail(
                    $"Need {field.Card.FoodCost} food to place on {place.BridgeColor} {(place.IsInland ? "inland" : "outside")} farm; have {player.Resources.Food}.");
            if (field.HasFarmer(player.Name))
                return ValidationResult.Fail("You already have a farmer on this field.");
        }

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var player = state.Players.First(p => p.Id == ((dynamic)action).PlayerId);

        if (action is FarmSkipAction)
        {
            player.PendingFarmActions = 0;
            events.Add(new FarmerPlacedEvent(
                state.GameId, player.Id,
                BridgeColor.Red, false, -1, 0, new ResourceBag(), 0, 0, 0, null));
            return;
        }

        var place = (PlaceFarmerAction)action;
        var field = state.Board.FarmingLands.GetField(place.BridgeColor, place.IsInland);
        var card  = field.Card;

        // Pay food
        player.Resources = player.Resources.Add(ResourceType.Food, -card.FoodCost);

        // Place farmer
        player.FarmersAvailable--;
        field.AddFarmer(player.Name);

        // Apply effect
        var (resourcesGained, coinsGained, sealsGained, lanternGained, actionTriggered) =
            ApplyCardEffect(card, player);

        LanternHelper.Apply(player, lanternGained, state.GameId, events);
        player.PendingFarmActions--;

        events.Add(new FarmerPlacedEvent(
            state.GameId, player.Id,
            place.BridgeColor, place.IsInland, 0,
            card.FoodCost, resourcesGained, coinsGained, sealsGained, lanternGained,
            actionTriggered));
    }

    internal static (ResourceBag Resources, int Coins, int Seals, int Lantern, string? Action)
        ApplyCardEffect(FarmCard card, Domain.Player player)
    {
        var resourcesGained = new ResourceBag();
        int coinsGained     = 0;
        int sealsGained     = 0;
        int lanternGained   = 0;
        string? actionTriggered = null;

        if (!string.IsNullOrEmpty(card.ActionDescription))
        {
            actionTriggered = card.ActionDescription;
            ApplyNamedAction(card.ActionDescription, player, ref coinsGained, ref sealsGained, ref lanternGained);
        }
        else
        {
            foreach (var item in card.GainItems)
            {
                switch (item.Type)
                {
                    case "Food":          resourcesGained = resourcesGained.Add(ResourceType.Food,      item.Amount); break;
                    case "Iron":          resourcesGained = resourcesGained.Add(ResourceType.Iron,      item.Amount); break;
                    case "ValueItem":     resourcesGained = resourcesGained.Add(ResourceType.ValueItem, item.Amount); break;
                    case "Coin":          coinsGained  += item.Amount; break;
                    case "MonarchialSeal": sealsGained += item.Amount; break;
                    case "Lantern":       lanternGained += item.Amount; break;
                }
            }

            player.Resources       = (player.Resources + resourcesGained).Clamp(7);
            player.Coins          += coinsGained;
            player.MonarchialSeals = Math.Min(player.MonarchialSeals + sealsGained, 5);
        }

        return (resourcesGained, coinsGained, sealsGained, lanternGained, actionTriggered);
    }

    private static void ApplyNamedAction(
        string description, Domain.Player player,
        ref int coinsGained, ref int sealsGained, ref int lanternGained)
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

            case "Gain 3 coins":
                coinsGained += 3;
                player.Coins += 3;
                break;

            case "Gain 1 monarchial seal":
                sealsGained++;
                player.MonarchialSeals = Math.Min(player.MonarchialSeals + 1, 5);
                break;

            case "Gain 1 lantern":
                lanternGained++;
                break;
        }
    }
}
