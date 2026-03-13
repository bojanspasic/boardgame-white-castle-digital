using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChooseSeedPairHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseSeedPairAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (ChooseSeedPairAction)action;

        if (state.CurrentPhase != Phase.SeedCardSelection)
            return ValidationResult.Fail("Not in seed card selection phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.Pending.AnyResourceChoices > 0)
            return ValidationResult.Fail("Resolve pending resource choices first.");
        if (player.SeedCard is not null)
            return ValidationResult.Fail("Already chose a seed card.");
        if (a.PairIndex < 0 || a.PairIndex >= state.SeedCardPairs.Count)
            return ValidationResult.Fail("Invalid pair index.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseSeedPairAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var pair   = state.SeedCardPairs[a.PairIndex];
        state.SeedCardPairs.RemoveAt(a.PairIndex);

        player.SeedCard = pair.Action;

        var resourcesGained = new ResourceBag();
        int coinsGained = 0, sealsGained = 0, pendingChoices = 0;

        foreach (var gain in pair.Resource.Gains)
        {
            switch (gain.Type)
            {
                case CardGainType.Food:
                    resourcesGained = resourcesGained.Add(ResourceType.Food, gain.Amount); break;
                case CardGainType.Iron:
                    resourcesGained = resourcesGained.Add(ResourceType.Iron, gain.Amount); break;
                case CardGainType.MotherOfPearls:
                    resourcesGained = resourcesGained.Add(ResourceType.MotherOfPearls, gain.Amount); break;
                case CardGainType.Coin:
                    coinsGained   += gain.Amount; break;
                case CardGainType.DaimyoSeal:
                    sealsGained   += gain.Amount; break;
                case CardGainType.AnyResource:
                    pendingChoices += gain.Amount; break;
            }
        }

        player.Resources = (player.Resources + resourcesGained).Clamp(7);
        player.Coins     += coinsGained;
        player.DaimyoSeals = Math.Min(player.DaimyoSeals + sealsGained, 5);
        player.Pending.AnyResourceChoices += pendingChoices;

        events.Add(new SeedPairChosenEvent(
            state.GameId, player.Id,
            pair.Action.Id, pair.Action.ActionType.ToString(),
            resourcesGained, coinsGained, sealsGained, pendingChoices));

        // Flip the resource seed card and add its back to the lantern chain.
        var back = pair.Resource.Back;
        var chainItem = new LanternChainItem
        {
            SourceCardId   = pair.Resource.Id,
            SourceCardType = "ResourceSeed",
            Gains          = new[] { new LanternChainGain(back.Type, back.Amount) },
        };
        player.LanternChain.Add(chainItem);
        events.Add(new LanternChainItemAddedEvent(
            state.GameId, player.Id,
            chainItem.SourceCardId, chainItem.SourceCardType,
            chainItem.Gains.Select(g => (g.Type.ToString(), g.Amount)).ToList().AsReadOnly()));

        // If the resource card carries a decree card, add it to the lantern chain.
        if (pair.Resource.Decree is { } decree)
        {
            var decreeChainItem = new LanternChainItem
            {
                SourceCardId   = decree.Id,
                SourceCardType = "Decree",
                Gains          = new[] { new LanternChainGain(decree.GainType, decree.Amount) },
            };
            player.LanternChain.Add(decreeChainItem);
            events.Add(new LanternChainItemAddedEvent(
                state.GameId, player.Id,
                decreeChainItem.SourceCardId, decreeChainItem.SourceCardType,
                decreeChainItem.Gains.Select(g => (g.Type.ToString(), g.Amount)).ToList().AsReadOnly()));
        }
    }
}
