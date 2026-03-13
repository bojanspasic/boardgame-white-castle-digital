using System.Diagnostics.CodeAnalysis;
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed class CastleAdvanceCourtierHandler : IActionHandler
{
    public bool CanHandle(IGameAction action) => action is CastleAdvanceCourtierAction;

    public ValidationResult Validate(IGameAction action, GameState state)
    {
        var a      = (CastleAdvanceCourtierAction)action;
        var player = state.Players.FirstOrDefault(p => p.Id == a.PlayerId);
        if (player is null)
            return ValidationResult.Fail("Unknown player.");
        if (state.ActivePlayer.Id != a.PlayerId)
            return ValidationResult.Fail("It is not this player's turn.");

        bool anyPending = player.Pending.CastlePlaceRemaining > 0 || player.Pending.CastleAdvanceRemaining > 0;
        if (!anyPending)
            return ValidationResult.Fail("No pending castle action to resolve.");

        return ValidateAdvance(player, a);
    }

    public void Apply(IGameAction action, GameState state, List<IDomainEvent> events)
    {
        var a      = (CastleAdvanceCourtierAction)action;
        var player = state.Players.First(p => p.Id == a.PlayerId);

        player.Pending.CastleAdvanceRemaining--;
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
                player.Pending.NewCardActivation = takenCard;
                events.Add(new RoomCardAcquiredEvent(
                    state.GameId, player.Id, takenCard.Id, floorIdx));

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
                    case "Food":           resources = resources.Add(ResourceType.Food,           item.Amount); break;
                    case "Iron":           resources = resources.Add(ResourceType.Iron,           item.Amount); break;
                    case "MotherOfPearls": resources = resources.Add(ResourceType.MotherOfPearls, item.Amount); break;
                    case "Coin":           coins   += item.Amount; break;
                    case "DaimyoSeal":     seals   += item.Amount; break;
                    case "Lantern":        lantern += item.Amount; break;
                }
            }

            player.Resources       = (player.Resources + resources).Clamp(7);
            player.Coins          += coins;
            player.DaimyoSeals = Math.Min(player.DaimyoSeals + seals, 5);
            LanternHelper.Apply(player, lantern, state.GameId, events);

            events.Add(new TopFloorSlotFilledEvent(
                state.GameId, player.Id, slotIndex, resources, coins, seals, lantern));
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ValidationResult ValidateAdvance(Player player, CastleAdvanceCourtierAction a)
    {
        if (player.Pending.CastleAdvanceRemaining <= 0)
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

    /// Unreachable default arm: ValidateAdvance already rejects positions other than Gate/StewardFloor/DiplomatFloor.
    [ExcludeFromCodeCoverage]
    private static int CourtierCountAt(Player player, CourtierPosition pos) => pos switch
    {
        CourtierPosition.Gate          => player.CourtiersAtGate,
        CourtierPosition.StewardFloor  => player.CourtiersOnStewardFloor,
        CourtierPosition.DiplomatFloor => player.CourtiersOnDiplomatFloor,
        _                              => 0,
    };

    private static void ApplyAdvance(Player player, CourtierPosition from, int levels)
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
