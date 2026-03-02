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

        return action switch
        {
            CastlePlaceCourtierAction => ValidatePlace(player),
            CastleAdvanceCourtierAction a => ValidateAdvance(player, a),
            CastleSkipAction => ValidationResult.Ok(),
            _ => ValidationResult.Fail("Unrecognised castle action."),
        };
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var player = state.Players.First(p => p.Id == ((dynamic)action).PlayerId);

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
                player.Resources = player.Resources.Add(ResourceType.ValueItem, -viCost);
                ApplyAdvance(player, a.From, a.Levels);
                events.Add(new CastlePlayExecutedEvent(state.GameId, player.Id, false, a.From, a.Levels));

                // When entering ground or mid floor, take the room card and add to personal domain
                bool enteringGroundFloor = a.From == CourtierPosition.Gate        && a.Levels == 1;
                bool enteringMidFloor    = (a.From == CourtierPosition.Gate        && a.Levels == 2)
                                        || (a.From == CourtierPosition.GroundFloor && a.Levels == 1);

                if (a.RoomIndex >= 0 && (enteringGroundFloor || enteringMidFloor))
                {
                    int floorIdx    = enteringGroundFloor ? 0 : 1;
                    var room        = state.Board.GetCastleRoom(floorIdx, a.RoomIndex);
                    var replacement = enteringGroundFloor
                        ? state.Board.TryDealGroundReplacement()
                        : state.Board.TryDealMidReplacement();

                    if (replacement is not null && room.Card is { } takenCard)
                    {
                        room.SetCard(replacement);
                        player.PersonalDomainCards.Add(takenCard);
                        events.Add(new RoomCardAcquiredEvent(
                            state.GameId, player.Id, takenCard.Id, takenCard.Name, floorIdx));

                        if (takenCard.Back is { } back)
                        {
                            var chainItem = new LanternChainItem
                            {
                                SourceCardId   = takenCard.Id,
                                SourceCardType = enteringGroundFloor ? "GroundFloor" : "MidFloor",
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
                bool reachedTop = (a.From == CourtierPosition.GroundFloor && a.Levels == 2)
                               || (a.From == CourtierPosition.MidFloor    && a.Levels == 1);
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
                            case "ValueItem":     resources = resources.Add(ResourceType.ValueItem, item.Amount); break;
                            case "Coin":          coins   += item.Amount; break;
                            case "MonarchialSeal": seals  += item.Amount; break;
                            case "Lantern":       lantern += item.Amount; break;
                        }
                    }

                    player.Resources       = (player.Resources + resources).Clamp(7);
                    player.Coins          += coins;
                    player.MonarchialSeals = Math.Min(player.MonarchialSeals + seals, 5);
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
        if (a.From == CourtierPosition.MidFloor && a.Levels == 2)
            return ValidationResult.Fail("Cannot advance 2 levels from the mid floor (would exceed top floor).");

        int count = a.From switch
        {
            CourtierPosition.Gate        => player.CourtiersAtGate,
            CourtierPosition.GroundFloor => player.CourtiersOnGroundFloor,
            CourtierPosition.MidFloor    => player.CourtiersOnMidFloor,
            _                            => 0,
        };
        if (count <= 0)
            return ValidationResult.Fail($"No courtiers at {a.From} to advance.");

        int viCost = a.Levels == 1 ? 2 : 5;
        if (player.Resources.ValueItem < viCost)
            return ValidationResult.Fail(
                $"Need {viCost} value items to advance {a.Levels} level(s); have {player.Resources.ValueItem}.");

        return ValidationResult.Ok();
    }

    private static void ApplyAdvance(Domain.Player player, CourtierPosition from, int levels)
    {
        switch (from)
        {
            case CourtierPosition.Gate:
                player.CourtiersAtGate--;
                if (levels == 1) player.CourtiersOnGroundFloor++;
                else             player.CourtiersOnMidFloor++;
                break;

            case CourtierPosition.GroundFloor:
                player.CourtiersOnGroundFloor--;
                if (levels == 1) player.CourtiersOnMidFloor++;
                else             player.CourtiersOnTopFloor++;
                break;

            case CourtierPosition.MidFloor:
                player.CourtiersOnMidFloor--;
                player.CourtiersOnTopFloor++;
                break;
        }
    }
}
