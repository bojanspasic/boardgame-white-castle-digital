using BoardWC.Engine.AI;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Engine;

public sealed record PlayerSetup(
    string Name,
    PlayerColor Color,
    bool IsAI,
    string? AiStrategyId = null
);

public static class GameEngineFactory
{
    public static IGameEngine Create(
        IReadOnlyList<PlayerSetup> players,
        IAiStrategy? aiStrategy = null,
        int maxRounds = 3)
    {
        if (players.Count < 2 || players.Count > 4)
            throw new ArgumentException("White Castle requires 2–4 players.", nameof(players));

        var rowConfigs = PersonalDomainRowConfig.Load();

        var domainPlayers = players
            .Select(p => new Player
            {
                Id    = Guid.NewGuid(),
                Name  = p.Name,
                Color = p.Color,
                IsAI  = p.IsAI,
                PersonalDomainRows = rowConfigs.Select(c => new PersonalDomainRow(c)).ToArray(),
            })
            .ToList();

        var state   = new GameState(domainPlayers, maxRounds);
        var handler = BuildHandlerChain();

        return new GameEngine(state, handler, aiStrategy);
    }

    private static CompositeActionHandler BuildHandlerChain() =>
        new(new IActionHandler[]
        {
            new StartGameHandler(),
            new ChooseSeedPairHandler(),
            new TakeDieFromBridgeHandler(),
            new PlaceDieAtCastleHandler(),
            new PlaceDieAtWellHandler(),
            new PlaceDieAtOutsideHandler(),
            new PlaceDieAtPersonalDomainHandler(),
            new ChooseResourceHandler(),
            new ChooseInfluencePayHandler(),
            new OutsideActivationHandler(),
            new ChooseNewCardFieldHandler(),
            new CastlePlaceCourtierHandler(),
            new CastleAdvanceCourtierHandler(),
            new CastleSkipHandler(),
            new TrainingGroundsHandler(),
            new FarmHandler(),
            new ChooseCastleCardFieldHandler(),
            new ChoosePersonalDomainRowHandler(),
            new PassHandler(),
        });
}
