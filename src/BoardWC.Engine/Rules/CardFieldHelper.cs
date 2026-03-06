using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Shared helpers for applying card field effects across all handlers.
/// </summary>
internal static class CardFieldHelper
{
    /// <summary>
    /// Applies all gains from a <see cref="GainCardField"/> to the player,
    /// including lantern chain and influence pending state.
    /// Returns the individual gain amounts for event construction.
    /// </summary>
    internal static (ResourceBag Resources, int Coins, int Seals, int Lantern, int VP, int Influence)
        ApplyGainField(GainCardField gf, Player player, GameState state, List<IDomainEvent> events)
    {
        var resources = new ResourceBag();
        int coins = 0, seals = 0, lantern = 0, vp = 0, influence = 0;

        foreach (var item in gf.Gains)
        {
            switch (item.Type)
            {
                case CardGainType.Food:           resources = resources.Add(ResourceType.Food,           item.Amount); break;
                case CardGainType.Iron:           resources = resources.Add(ResourceType.Iron,           item.Amount); break;
                case CardGainType.MotherOfPearls: resources = resources.Add(ResourceType.MotherOfPearls, item.Amount); break;
                case CardGainType.Coin:           coins     += item.Amount; break;
                case CardGainType.DaimyoSeal:     seals     += item.Amount; break;
                case CardGainType.Lantern:        lantern   += item.Amount; break;
                case CardGainType.VictoryPoint:   vp        += item.Amount; break;
                case CardGainType.Influence:      influence += item.Amount; break;
                case CardGainType.Well:
                    for (int w = 0; w < item.Amount; w++)
                        ApplyWellEffect(player, state, events);
                    break;
            }
        }

        player.Resources    = (player.Resources + resources).Clamp(7);
        player.Coins       += coins;
        player.DaimyoSeals  = Math.Min(player.DaimyoSeals + seals, 5);
        LanternHelper.Apply(player, lantern, state.GameId, events);
        player.LanternScore += vp;
        InfluenceHelper.Apply(player, influence, state, events);

        return (resources, coins, seals, lantern, vp, influence);
    }

    private static void ApplyWellEffect(Player player, GameState state, List<IDomainEvent> events)
    {
        player.DaimyoSeals = Math.Min(player.DaimyoSeals + 1, 5);

        var resourcesGained = new ResourceBag();
        int coinsGained     = 0;
        int pendingChoices  = 0;

        foreach (var token in state.Board.Well.Tokens)
        {
            switch (token.ResourceSide)
            {
                case TokenResource.Food:           resourcesGained = resourcesGained.Add(ResourceType.Food,           1); break;
                case TokenResource.Iron:           resourcesGained = resourcesGained.Add(ResourceType.Iron,           1); break;
                case TokenResource.MotherOfPearls: resourcesGained = resourcesGained.Add(ResourceType.MotherOfPearls, 1); break;
                case TokenResource.Coin:           coinsGained++;    break;
                case TokenResource.AnyResource:    pendingChoices++; break;
            }
        }

        player.Resources = (player.Resources + resourcesGained).Clamp(7);
        player.Coins    += coinsGained;
        player.PendingAnyResourceChoices += pendingChoices;

        events.Add(new WellEffectAppliedEvent(
            state.GameId, player.Id, 1, resourcesGained, coinsGained, pendingChoices));
    }

    /// <summary>
    /// Applies a named action description (from <see cref="ActionCardField.Description"/>) to the
    /// player. Covers all "Play …" action types shared across castle rooms, farm, training grounds,
    /// and personal domain card fields. Returns true if the description was handled.
    /// </summary>
    internal static bool ApplyActionDescription(string description, Player player)
    {
        switch (description)
        {
            case "Play castle":
                player.CastlePlaceRemaining++;
                player.CastleAdvanceRemaining++;
                return true;
            case "Play training grounds":
                player.PendingTrainingGroundsActions++;
                return true;
            case "Play farm":
                player.PendingFarmActions++;
                return true;
            case "Play red castle card field":
                player.PendingCastleCardFieldFilter = "Red";
                return true;
            case "Play black castle card field":
                player.PendingCastleCardFieldFilter = "Black";
                return true;
            case "Play white castle card field":
                player.PendingCastleCardFieldFilter = "White";
                return true;
            case "Play any castle card field":
                player.PendingCastleCardFieldFilter = "Any";
                return true;
            case "Play castle gain field":
                player.PendingCastleCardFieldFilter = "GainOnly";
                return true;
            case "Play personal domain row":
                player.PendingPersonalDomainRowChoice = true;
                return true;
            default:
                return false;
        }
    }
}
