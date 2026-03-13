using System.Diagnostics.CodeAnalysis;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class PlaceDieAtPersonalDomainHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is PlaceDieAction a && a.Target is PersonalDomainTarget;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a = (PlaceDieAction)action;

        if (state.CurrentPhase != Phase.WorkerPlacement)
            return ValidationResult.Fail("Not in worker placement phase.");

        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");
        if (player.DiceInHand.Count == 0)
            return ValidationResult.Fail("No die in hand to place.");

        var die = player.DiceInHand[0];
        var pdv = (PersonalDomainTarget)a.Target;

        if (pdv.RowIndex < 0 || pdv.RowIndex >= player.PersonalDomainRows.Length)
            return ValidationResult.Fail("Invalid personal domain row index.");
        var pdRow = player.PersonalDomainRows[pdv.RowIndex];
        if (pdRow.PlacedDie is not null)
            return ValidationResult.Fail("This personal domain row already has a die this round.");
        if (die.Color != pdRow.Config.DieColor)
            return ValidationResult.Fail($"This row requires a {pdRow.Config.DieColor} die.");
        int pdDelta = die.Value - pdRow.Config.CompareValue;
        if (pdDelta < 0 && player.Coins < -pdDelta)
            return ValidationResult.Fail($"Not enough coins. Need {-pdDelta}, have {player.Coins}.");

        return ValidationResult.Ok();
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (PlaceDieAction)action;
        var target = (PersonalDomainTarget)a.Target;
        var player = state.Players.First(p => p.Id == a.PlayerId);
        var die    = player.DiceInHand[0];

        ApplyPersonalDomain(target, player, die, state, events);
    }

    private static void ApplyPersonalDomain(
        PersonalDomainTarget target, Player player, Die die,
        GameState state, List<IDomainEvent> events)
    {
        var row   = player.PersonalDomainRows[target.RowIndex];
        int delta = die.Value - row.Config.CompareValue;

        player.Coins += delta;
        row.PlacedDie = die;
        player.DiceInHand.RemoveAt(0);

        events.Add(new DiePlacedEvent(state.GameId, player.Id, target, die.Value, delta));

        // Default gain always applies
        var gained = new ResourceBag().Add(row.Config.DefaultGainType, row.Config.DefaultGainAmount);

        // Uncovered spots (left-to-right): count = 5 − FigureAvailable
        int uncovered = GetUncoveredCount(player, row.Config.FigureType);
        for (int i = 0; i < uncovered; i++)
            gained = gained.Add(row.Config.SpotGains[i].Type, row.Config.SpotGains[i].Amount);

        player.Resources = (player.Resources + gained).Clamp(7);

        events.Add(new PersonalDomainActivatedEvent(
            state.GameId, player.Id, target.RowIndex, row.Config.DieColor, uncovered, gained));

        // Activate seed action card every time a die is placed in a personal domain row
        if (player.SeedCard is { } seedCard)
        {
            switch (seedCard.ActionType)
            {
                case SeedActionType.PlayCastle:
                    player.Pending.CastlePlaceRemaining++;
                    player.Pending.CastleAdvanceRemaining++;
                    break;
                case SeedActionType.PlayFarm:
                    player.Pending.FarmActions++;
                    break;
                case SeedActionType.PlayTrainingGrounds:
                    player.Pending.TrainingGroundsActions++;
                    break;
            }
            events.Add(new SeedCardActivatedEvent(
                state.GameId, player.Id, seedCard.Id, seedCard.ActionType.ToString(), target.RowIndex));
        }

        // Activate personal domain card fields — each acquired card may grant gains on this row
        foreach (var pdCard in player.PersonalDomainCards)
        {
            int? fi = GetFieldIndexForRow(pdCard, target.RowIndex);
            if (fi is null || fi.Value >= pdCard.Fields.Count) continue;
            int fieldIdx = fi.Value;

            var field = pdCard.Fields[fieldIdx];

            if (field is GainCardField gf)
            {
                var (res, coins, seals, lantern, vp, influence) =
                    CardGainApplier.ApplyGain(player, gf, state, events);

                events.Add(new PersonalDomainCardFieldActivatedEvent(
                    state.GameId, player.Id, pdCard.Id, fieldIdx,
                    res, coins, seals, lantern, vp, influence));
            }
            else if (field is ActionCardField af)
            {
                CardActionApplier.ApplyAction(player, af.Description, state, events);
                events.Add(new PersonalDomainCardFieldActivatedEvent(
                    state.GameId, player.Id, pdCard.Id, fieldIdx,
                    new ResourceBag(), 0, 0, 0, 0, 0));
            }
        }
    }

    /// Default arm unreachable: only Courtier/Farmer/Soldier figure types appear in data.
    [ExcludeFromCodeCoverage]
    private static int GetUncoveredCount(Player player, string figureType) => figureType switch
    {
        "Courtier" => 5 - player.CourtiersAvailable,
        "Farmer"   => 5 - player.FarmersAvailable,
        "Soldier"  => 5 - player.SoldiersAvailable,
        _          => 0
    };

    /// <summary>
    /// Returns which field index of <paramref name="card"/> applies when a die is placed
    /// in <paramref name="rowIndex"/> (0=Red/Courtier, 1=White/Farmer, 2=Black/Soldier).
    /// Steward-floor cards (Layout=null, 3 fields): field[rowIndex].
    /// Diplomat-floor DoubleTop (2 fields): field[0] spans rows 0+1, field[1] is row 2.
    /// Diplomat-floor DoubleBottom (2 fields): field[0] is row 0, field[1] spans rows 1+2.
    /// </summary>
    private static int? GetFieldIndexForRow(RoomCard card, int rowIndex) =>
        card.Layout switch
        {
            null           => rowIndex < card.Fields.Count ? rowIndex : (int?)null,
            "DoubleTop"    => rowIndex <= 1 ? 0 : 1,
            "DoubleBottom" => rowIndex == 0 ? 0 : 1,
            _              => null,
        };
}
