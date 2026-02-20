using BoardWC.Engine.Actions;
using BoardWC.Engine.AI;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;
using BoardWC.Engine.Rules;

namespace BoardWC.Engine.Engine;

internal sealed class GameEngine : IGameEngine
{
    private readonly GameState _state;
    private readonly CompositeActionHandler _handlers;
    private readonly IAiStrategy? _aiStrategy;
    private IReadOnlyList<PlayerScore>? _finalScores;

    public Guid GameId    => _state.GameId;
    public bool IsGameOver => _state.CurrentPhase == Phase.GameOver;

    internal GameEngine(GameState state, CompositeActionHandler handlers, IAiStrategy? aiStrategy)
    {
        _state      = state;
        _handlers   = handlers;
        _aiStrategy = aiStrategy;
    }

    public GameStateSnapshot GetCurrentState() => _state.ToSnapshot();

    public ActionResult ProcessAction(IGameAction action)
    {
        var validation = _handlers.Validate(action, _state);
        if (!validation.IsValid)
            return new ActionResult.Failure(_state.ToSnapshot(), validation.Reason);

        var events = new List<IDomainEvent>();
        _handlers.Apply(action, _state, events);

        // StartGameAction is a setup transition, not a player turn — don't advance the turn.
        if (action is not StartGameAction)
            PostActionProcessor.Run(_state, events);

        if (IsGameOver)
            _finalScores = ScoreCalculator.Calculate(_state);

        return new ActionResult.Success(_state.ToSnapshot(), events.AsReadOnly());
    }

    public IReadOnlyList<IGameAction> GetLegalActions(Guid playerId) =>
        LegalActionGenerator.Generate(playerId, _state);

    public IReadOnlyList<PlayerScore>? GetFinalScores() => _finalScores;
}
