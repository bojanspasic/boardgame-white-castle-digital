using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChoosePersonalDomainRowHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChoosePersonalDomainRowAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (ChoosePersonalDomainRowAction)action;

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (!player.PendingPersonalDomainRowChoice)
            return ValidationResult.Fail("No pending personal domain row choice to resolve.");
        if (!player.PersonalDomainRows.Any(r => r.Config.DieColor == a.RowColor))
            return ValidationResult.Fail($"No personal domain row matches color {a.RowColor}.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChoosePersonalDomainRowAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        player.PendingPersonalDomainRowChoice = false;

        var row = player.PersonalDomainRows.First(r => r.Config.DieColor == a.RowColor);

        // Default gain always applies
        var gained = new ResourceBag().Add(row.Config.DefaultGainType, row.Config.DefaultGainAmount);

        // Spot gains based on how many figures of this type have been deployed
        int uncovered = UncoveredCount(player, row.Config.FigureType);
        for (int i = 0; i < uncovered; i++)
            gained = gained.Add(row.Config.SpotGains[i].Type, row.Config.SpotGains[i].Amount);

        player.Resources = (player.Resources + gained).Clamp(7);

        events.Add(new PersonalDomainRowChosenEvent(state.GameId, player.Id, a.RowColor, gained));

        // Activate personal domain card fields for this row
        int rowIndex = Array.IndexOf(player.PersonalDomainRows, row);
        foreach (var pdCard in player.PersonalDomainCards)
        {
            int? fi = GetFieldIndexForRow(pdCard, rowIndex);
            if (fi is not { } fieldIdx || fieldIdx >= pdCard.Fields.Count) continue;

            var field = pdCard.Fields[fieldIdx];

            if (field is GainCardField gf)
            {
                var res = new ResourceBag();
                int coins = 0, seals = 0, lantern = 0, vp = 0, influence = 0;

                foreach (var item in gf.Gains)
                {
                    switch (item.Type)
                    {
                        case CardGainType.Food:           res = res.Add(ResourceType.Food,      item.Amount); break;
                        case CardGainType.Iron:           res = res.Add(ResourceType.Iron,      item.Amount); break;
                        case CardGainType.MotherOfPearls:      res = res.Add(ResourceType.MotherOfPearls, item.Amount); break;
                        case CardGainType.Coin:           coins     += item.Amount; break;
                        case CardGainType.DaimyoSeal: seals     += item.Amount; break;
                        case CardGainType.Lantern:        lantern   += item.Amount; break;
                        case CardGainType.VictoryPoint:   vp        += item.Amount; break;
                        case CardGainType.Influence:      influence += item.Amount; break;
                    }
                }

                player.Resources       = (player.Resources + res).Clamp(7);
                player.Coins          += coins;
                player.DaimyoSeals = Math.Min(player.DaimyoSeals + seals, 5);
                LanternHelper.Apply(player, lantern, state.GameId, events);
                player.LanternScore   += vp;
                InfluenceHelper.Apply(player, influence, state, events);

                events.Add(new PersonalDomainCardFieldActivatedEvent(
                    state.GameId, player.Id, pdCard.Id, fieldIdx,
                    res, coins, seals, lantern, vp, influence));
            }
            else if (field is ActionCardField af)
            {
                switch (af.Description)
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
                events.Add(new PersonalDomainCardFieldActivatedEvent(
                    state.GameId, player.Id, pdCard.Id, fieldIdx,
                    new ResourceBag(), 0, 0, 0, 0, 0));
            }
        }
    }

    private static int UncoveredCount(Domain.Player player, string figureType) => figureType switch
    {
        "Courtier" => 5 - player.CourtiersAvailable,
        "Farmer"   => 5 - player.FarmersAvailable,
        "Soldier"  => 5 - player.SoldiersAvailable,
        _          => 0,
    };

    private static int? GetFieldIndexForRow(RoomCard card, int rowIndex) =>
        card.Layout switch
        {
            null           => rowIndex < card.Fields.Count ? rowIndex : (int?)null,
            "DoubleTop"    => rowIndex <= 1 ? 0 : 1,
            "DoubleBottom" => rowIndex == 0 ? 0 : 1,
            _              => null,
        };
}
