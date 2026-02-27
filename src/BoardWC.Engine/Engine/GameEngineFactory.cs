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

        var domainPlayers = players
            .Select(p => new Player
            {
                Id    = Guid.NewGuid(),
                Name  = p.Name,
                Color = p.Color,
                IsAI  = p.IsAI,
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
            new TakeDieFromBridgeHandler(),
            new PlaceDieHandler(),
            new ChooseResourceHandler(),
            new CastlePlayHandler(),
            new TrainingGroundsHandler(),
            new FarmHandler(),
            new PassHandler(),
        });
}
