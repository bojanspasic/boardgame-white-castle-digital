using System.Diagnostics.CodeAnalysis;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class CastlePlayHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) =>
        action is CastlePlaceCourtierAction or CastleAdvanceCourtierAction or CastleSkipAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        Guid playerId = action switch
        {
            CastlePlaceCourtierAction  a => a.PlayerId,
            CastleAdvanceCourtierAction a => a.PlayerId,
            CastleSkipAction           a => a.PlayerId,
            _                            => Guid.Empty,
        };

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != playerId)
            return ValidationResult.Fail("It is not this player's turn.");

        bool anyPending = player.CastlePlaceRemaining > 0 || player.CastleAdvanceRemaining > 0;
        if (!anyPending)
            return ValidationResult.Fail("No pending castle action to resolve.");

        return ValidateKnownCastleAction(action, player);
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        Guid playerId = action switch
        {
            CastlePlaceCourtierAction  a => a.PlayerId,
            CastleAdvanceCourtierAction a => a.PlayerId,
            CastleSkipAction           a => a.PlayerId,
            _                            => Guid.Empty,
        };
        var player = state.Players.First(p => p.Id == playerId);

        switch (action)
        {
            case CastlePlaceCourtierAction:
                player.CastlePlaceRemaining--;
                player.CourtiersAvailable--;
                player.CourtiersAtGate++;
                player.Coins -= 2;
                events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, true, null, 0));
                break;

            case CastleAdvanceCourtierAction a:
                player.CastleAdvanceRemaining--;
                int viCost = a.Levels == 1 ? 2 : 5;
                player.Resources = player.Resources.Add(ResourceType.MotherOfPearls, -viCost);
                ApplyAdvance(player, a.From, a.Levels);
                events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, false, a.From, a.Levels));

                // When entering ground or mid floor, take the room card and add to personal domain
                bool enteringStewardFloor  = a.From == CourtierPosition.Gate           && a.Levels == 1;
                bool enteringDiplomatFloor = (a.From == CourtierPosition.Gate           && a.Levels == 2)
                                          || (a.From == CourtierPosition.StewardFloor   && a.Levels == 1);

                if (a.RoomIndex >= 0 && (enteringStewardFloor || enteringDiplomatFloor))
                {
                    int floorIdx    = enteringStewardFloor ? 0 : 1;
                    var room        = state.Board.GetCastleRoom(floorIdx, a.RoomIndex);
                    var replacement = enteringStewardFloor
                        ? state.Board.TryDealGroundReplacement()
                        : state.Board.TryDealMidReplacement();

                    if (replacement is not null && room.Card is { } takenCard)
                    {
                        room.SetCard(replacement);
                        player.PendingNewCardActivation = takenCard;
                        events.Add(new RoomCardAcquiredEvent(
                            state.GameId, player.Id, takenCard.Id, takenCard.Name, floorIdx));

                        if (takenCard.Back is { } back)
                        {
                            var chainItem = new LanternChainItem
                            {
                                SourceCardId   = takenCard.Id,
                                SourceCardType = enteringStewardFloor ? "StewardFloor" : "DiplomatFloor",
                                Gains          = [new LanternChainGain(back.GainType, back.Amount)],
                            };
                            player.LanternChain.Add(chainItem);
                            events.Add(new LanternChainItemAddedEvent(
                                state.GameId, player.Id,
                                chainItem.SourceCardId, chainItem.SourceCardType,
                                chainItem.Gains.Select(g => (g.Type.ToString(), g.Amount)).ToList().AsReadOnly()));
                        }
                    }
                }

                // When a courtier reaches the top floor, try to claim an empty card slot
                bool reachedTop = (a.From == CourtierPosition.StewardFloor  && a.Levels == 2)
                               || (a.From == CourtierPosition.DiplomatFloor && a.Levels == 1);
                if (reachedTop && state.Board.TopFloorRoom.TryTakeSlot(
                        player.Name, out int slotIndex, out var slotGains))
                {
                    var resources   = new ResourceBag();
                    int coins       = 0;
                    int seals       = 0;
                    int lantern     = 0;

                    foreach (var item in slotGains)
                    {
                        switch (item.Type)
                        {
                            case "Food":          resources = resources.Add(ResourceType.Food,      item.Amount); break;
                            case "Iron":          resources = resources.Add(ResourceType.Iron,      item.Amount); break;
                            case "MotherOfPearls": resources = resources.Add(ResourceType.MotherOfPearls, item.Amount); break;
                            case "Coin":           coins   += item.Amount; break;
                            case "DaimyoSeal":     seals   += item.Amount; break;
                            case "Lantern":       lantern += item.Amount; break;
                        }
                    }

                    player.Resources       = (player.Resources + resources).Clamp(7);
                    player.Coins          += coins;
                    player.DaimyoSeals = Math.Min(player.DaimyoSeals + seals, 5);
                    LanternHelper.Apply(player, lantern, state.GameId, events);

                    events.Add(new TopFloorSlotFilledEvent(
                        state.GameId, player.Id, slotIndex, resources, coins, seals, lantern));
                }
                break;

            case CastleSkipAction:
                player.CastlePlaceRemaining   = 0;
                player.CastleAdvanceRemaining = 0;
                events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, false, null, 0));
                break;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// Unreachable in practice: CanHandle() ensures only castle actions enter Validate.
    [ExcludeFromCodeCoverage]
    private static ValidationResult ValidateKnownCastleAction(IGameAction action, Domain.Player player) =>
        action switch
        {
            CastlePlaceCourtierAction   => ValidatePlace(player),
            CastleAdvanceCourtierAction a => ValidateAdvance(player, a),
            CastleSkipAction            => ValidationResult.Ok(),
            _                           => ValidationResult.Fail("Unrecognised castle action."),
        };

    /// Unreachable default arm: ValidateAdvance already rejects positions other than Gate/StewardFloor/DiplomatFloor.
    [ExcludeFromCodeCoverage]
    private static int CourtierCountAt(Domain.Player player, CourtierPosition pos) => pos switch
    {
        CourtierPosition.Gate        => player.CourtiersAtGate,
        CourtierPosition.StewardFloor => player.CourtiersOnStewardFloor,
        CourtierPosition.DiplomatFloor    => player.CourtiersOnDiplomatFloor,
        _                            => 0,
    };

    private static ValidationResult ValidatePlace(Domain.Player player)
    {
        if (player.CastlePlaceRemaining <= 0)
            return ValidationResult.Fail("No place-at-gate use remaining.");
        if (player.CourtiersAvailable <= 0)
            return ValidationResult.Fail("No courtiers in hand to place at the gate.");
        if (player.Coins < 2)
            return ValidationResult.Fail("Need 2 coins to place a courtier at the gate.");
        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateAdvance(Domain.Player player, CastleAdvanceCourtierAction a)
    {
        if (player.CastleAdvanceRemaining <= 0)
            return ValidationResult.Fail("No advance use remaining.");
        if (a.Levels < 1 || a.Levels > 2)
            return ValidationResult.Fail("Advance levels must be 1 or 2.");
        if (a.From == CourtierPosition.DiplomatFloor && a.Levels == 2)
            return ValidationResult.Fail("Cannot advance 2 levels from the diplomat floor (would exceed top floor).");

        int count = CourtierCountAt(player, a.From);
        if (count <= 0)
            return ValidationResult.Fail($"No courtiers at {a.From} to advance.");

        int viCost = a.Levels == 1 ? 2 : 5;
        if (player.Resources.MotherOfPearls < viCost)
            return ValidationResult.Fail(
                $"Need {viCost} Mother of Pearls to advance {a.Levels} level(s); have {player.Resources.MotherOfPearls}.");

        return ValidationResult.Ok();
    }

    private static void ApplyAdvance(Domain.Player player, CourtierPosition from, int levels)
    {
        switch (from)
        {
            case CourtierPosition.Gate:
                player.CourtiersAtGate--;
                if (levels == 1) player.CourtiersOnStewardFloor++;
                else             player.CourtiersOnDiplomatFloor++;
                break;

            case CourtierPosition.StewardFloor:
                player.CourtiersOnStewardFloor--;
                if (levels == 1) player.CourtiersOnDiplomatFloor++;
                else             player.CourtiersOnTopFloor++;
                break;

            case CourtierPosition.DiplomatFloor:
                player.CourtiersOnDiplomatFloor--;
                player.CourtiersOnTopFloor++;
                break;
        }
    }
}
