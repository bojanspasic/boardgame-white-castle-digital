using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PlaceWorkerInTowerHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is PlaceWorkerInTowerAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (PlaceWorkerInTowerAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.WorkersAvailable <= 0)
            return ValidationResult.Fail("No workers available.");

        if (a.Level < 0 || a.Level > 3)
            return ValidationResult.Fail("Level must be 0–3.");

        var tower = state.Board.Towers.FirstOrDefault(t => t.Zone == a.Zone);
        if (tower is null)
            return ValidationResult.Fail($"Unknown tower zone '{a.Zone}'.");

        var level = tower.GetLevel(a.Level);
        if (level.IsOccupied)
            return ValidationResult.Fail("That tower level is already occupied.");

        if (!player.Resources.CanAfford(level.Action.Cost))
            return ValidationResult.Fail(
                $"Insufficient resources. Need {level.Action.Cost}, have {player.Resources}.");

        if (level.Action.ActionType == TowerActionType.AcquireClanCard &&
            state.ClanDeck.VisibleCards.Count == 0)
            return ValidationResult.Fail("No clan cards available to acquire.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (PlaceWorkerInTowerAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var tower  = state.Board.GetTower(a.Zone);
        var level  = tower.GetLevel(a.Level);
        var act    = level.Action;

        // Pay cost
        player.Resources = player.Resources.Subtract(act.Cost);

        // Place worker
        level.OccupiedBy = player.Id;
        player.WorkersAvailable--;
        player.WorkersOnBoard++;

        events.Add(new WorkerPlacedInTowerEvent(
            state.GameId, player.Id, a.Zone, a.Level, act.ToSnapshot()));

        // Resolve action effect
        switch (act.ActionType)
        {
            case TowerActionType.GainResources:
                player.Resources = player.Resources + act.ResourceGain;
                events.Add(new ResourcesCollectedEvent(state.GameId, player.Id, act.ResourceGain));
                break;

            case TowerActionType.AdvanceTower:
                var zone = act.TowerToAdvance ?? a.Zone;
                player.TowerLevels[zone]++;
                events.Add(new TowerAdvancedEvent(state.GameId, player.Id, zone, player.TowerLevels[zone]));
                break;

            case TowerActionType.AcquireClanCard:
                var card = state.ClanDeck.TakeFirstAvailable();
                if (card is not null)
                {
                    player.ClanCards.Add(card);
                    events.Add(new ClanCardAcquiredEvent(state.GameId, player.Id, card.ToSnapshot()));
                }
                break;

            case TowerActionType.GainLanterns:
                player.LanternScore += act.LanternsGained;
                events.Add(new LanternsGainedEvent(state.GameId, player.Id, act.LanternsGained));
                break;
        }

        // Lanterns from action (works for GainResources actions that also grant lanterns)
        if (act.ActionType == TowerActionType.GainResources && act.LanternsGained > 0)
        {
            player.LanternScore += act.LanternsGained;
            events.Add(new LanternsGainedEvent(state.GameId, player.Id, act.LanternsGained));
        }
    }
}
