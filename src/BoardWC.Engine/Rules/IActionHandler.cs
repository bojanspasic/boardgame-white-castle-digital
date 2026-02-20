using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Events;

namespace BoardWC.Engine.Rules;

internal sealed record ValidationResult(bool IsValid, string Reason = "")
{
    public static ValidationResult Ok()           => new(true);
    public static ValidationResult Fail(string r) => new(false, r);
}

internal interface IActionHandler
{
    bool CanHandle(IGameAction action);
    ValidationResult Validate(IGameAction action, GameState state);
    void Apply(IGameAction action, GameState state, List<IDomainEvent> events);
}
