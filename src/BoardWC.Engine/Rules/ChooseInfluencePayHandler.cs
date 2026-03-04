using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class ChooseInfluencePayHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is ChooseInfluencePayAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (ChooseInfluencePayAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.PendingInfluenceGain <= 0)
            return ValidationResult.Fail("No pending influence gain to resolve.");

        if (a.WillPay && player.DaimyoSeals < player.PendingInfluenceSealCost)
            return ValidationResult.Fail(
                $"Not enough seals. Need {player.PendingInfluenceSealCost}, have {player.DaimyoSeals}.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (ChooseInfluencePayAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        int gain     = player.PendingInfluenceGain;
        int sealCost = player.PendingInfluenceSealCost;

        int sealsPaid = 0;
        if (a.WillPay)
        {
            player.DaimyoSeals  -= sealCost;
            player.Influence        += gain;
            player.InfluenceGainOrder = ++state.InfluenceGainCounter;
            sealsPaid                = sealCost;
        }

        player.PendingInfluenceGain     = 0;
        player.PendingInfluenceSealCost = 0;

        events.Add(new InfluenceGainResolvedEvent(
            state.GameId, player.Id, gain, sealsPaid, a.WillPay));
    }
}
