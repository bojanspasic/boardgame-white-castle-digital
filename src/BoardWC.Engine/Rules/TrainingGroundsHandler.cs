using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class TrainingGroundsHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is TrainingGroundsPlaceSoldierAction or TrainingGroundsSkipAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        Guid playerId = action switch
        {
            TrainingGroundsPlaceSoldierAction a => a.PlayerId,
            TrainingGroundsSkipAction         a => a.PlayerId,
            _                                   => Guid.Empty,
        };

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != playerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingTrainingGroundsActions <= 0)
            return ValidationResult.Fail("No pending training grounds action to resolve.");

        if (action is TrainingGroundsPlaceSoldierAction place)
        {
            if (place.AreaIndex < 0 || place.AreaIndex > 2)
                return ValidationResult.Fail("Area index must be 0, 1, or 2.");
            if (player.SoldiersAvailable <= 0)
                return ValidationResult.Fail("No soldiers available to place.");
            var area = state.Board.TrainingGrounds.Areas[place.AreaIndex];
            if (player.Resources.Iron < area.IronCost)
                return ValidationResult.Fail(
                    $"Need {area.IronCost} iron to place in area {place.AreaIndex}; have {player.Resources.Iron}.");
        }

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var player = state.Players.First(p => p.Id == ((dynamic)action).PlayerId);

        if (action is TrainingGroundsSkipAction)
        {
            player.PendingTrainingGroundsActions = 0;
            events.Add(new TrainingGroundsUsedEvent(
                state.GameId, player.Id,
                -1, 0, new ResourceBag(), 0, 0, 0, null));
            return;
        }

        var place = (TrainingGroundsPlaceSoldierAction)action;
        var area  = state.Board.TrainingGrounds.Areas[place.AreaIndex];

        // Pay iron
        player.Resources = player.Resources.Add(ResourceType.Iron, -area.IronCost);

        // Place soldier
        player.SoldiersAvailable--;
        area.AddSoldier(player.Name);

        // Apply resource gain (areas 0 and 2 have a resource side)
        var resourcesGained = new ResourceBag();
        int coinsGained     = 0;
        int sealsGained     = 0;
        int lanternGained   = 0;

        foreach (var item in area.ResourceGain)
        {
            switch (item.Type)
            {
                case "Food":          resourcesGained = resourcesGained.Add(ResourceType.Food,      item.Amount); break;
                case "Iron":          resourcesGained = resourcesGained.Add(ResourceType.Iron,      item.Amount); break;
                case "MotherOfPearls":     resourcesGained = resourcesGained.Add(ResourceType.MotherOfPearls, item.Amount); break;
                case "Coin":          coinsGained  += item.Amount; break;
                case "DaimyoSeal": sealsGained += item.Amount; break;
            }
        }

        player.Resources      = (player.Resources + resourcesGained).Clamp(7);
        player.Coins          += coinsGained;
        player.DaimyoSeals = Math.Min(player.DaimyoSeals + sealsGained, 5);

        // Apply action side (areas 1 and 2 have an action side)
        string? actionTriggered = null;
        if (!string.IsNullOrEmpty(area.ActionDescription))
        {
            actionTriggered = area.ActionDescription;
            ApplyNamedAction(area.ActionDescription, player, ref lanternGained);
        }

        LanternHelper.Apply(player, lanternGained, state.GameId, events);
        player.PendingTrainingGroundsActions--;

        events.Add(new TrainingGroundsUsedEvent(
            state.GameId, player.Id,
            place.AreaIndex, area.IronCost,
            resourcesGained, coinsGained, sealsGained, lanternGained,
            actionTriggered));
    }

    private static void ApplyNamedAction(string description, Domain.Player player, ref int lanternGained)
    {
        switch (description)
        {
            case "Play castle":
                player.CastlePlaceRemaining++;
                player.CastleAdvanceRemaining++;
                break;

            case "Gain 3 coins":
                player.Coins += 3;
                break;

            case "Gain 1 monarchial seal":
                player.DaimyoSeals = Math.Min(player.DaimyoSeals + 1, 5);
                break;

            case "Gain 1 lantern":
                lanternGained += 1;
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
