using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

/// <summary>
/// Central helper for applying lantern gains and firing the lantern chain.
/// Call <see cref="Apply"/> when a card/area grants Lantern VP and also triggers the chain.
/// Call <see cref="Trigger"/> when only the chain fires (e.g., taking the Low die) with no VP.
/// </summary>
internal static class LanternHelper
{
    /// <summary>Adds <paramref name="lanternAmount"/> to LanternScore and, if &gt; 0, fires the chain once.</summary>
    public static void Apply(Player player, int lanternAmount, Guid gameId, List<IDomainEvent> events)
    {
        player.LanternScore += lanternAmount;
        if (lanternAmount > 0)
            FireChain(player, gameId, events);
    }

    /// <summary>Fires the chain without adding to LanternScore (used by the Low-die Lantern Effect).</summary>
    public static void Trigger(Player player, Guid gameId, List<IDomainEvent> events)
    {
        FireChain(player, gameId, events);
    }

    private static void FireChain(Player player, Guid gameId, List<IDomainEvent> events)
    {
        if (player.LanternChain.Count == 0) return;

        var resources = ResourceBag.Empty;
        int coins = 0, seals = 0, vp = 0;

        foreach (var item in player.LanternChain)
            foreach (var g in item.Gains)
                switch (g.Type)
                {
                    case CardGainType.Food:           resources = resources.Add(ResourceType.Food,      g.Amount); break;
                    case CardGainType.Iron:           resources = resources.Add(ResourceType.Iron,      g.Amount); break;
                    case CardGainType.MotherOfPearls:      resources = resources.Add(ResourceType.MotherOfPearls, g.Amount); break;
                    case CardGainType.Coin:           coins += g.Amount; break;
                    case CardGainType.DaimyoSeal: seals += g.Amount; break;
                    case CardGainType.VictoryPoint:   vp    += g.Amount; break;
                    // Influence chain gains are intentionally no-op here;
                    // InfluenceHelper is only called by individual handlers, not the chain
                }

        player.Resources       = (player.Resources + resources).Clamp(7);
        player.Coins          += coins;
        player.DaimyoSeals = Math.Min(player.DaimyoSeals + seals, 5);
        player.LanternScore   += vp;

        events.Add(new LanternChainActivatedEvent(gameId, player.Id, resources, coins, seals, vp));
    }
}
