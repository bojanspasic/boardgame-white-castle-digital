namespace BoardWC.Engine.AI;

public static class AiStrategyRegistry
{
    private static readonly Dictionary<string, Func<IAiStrategy>> Factories = new()
    {
        ["random"]          = () => new RandomAiStrategy(),
        ["greedy-resource"] = () => new GreedyResourceAiStrategy(),
    };

    public static IAiStrategy Resolve(string strategyId)
    {
        if (Factories.TryGetValue(strategyId, out var factory))
            return factory();
        throw new ArgumentException(
            $"Unknown AI strategy '{strategyId}'. Available: {string.Join(", ", Factories.Keys)}");
    }

    public static IReadOnlyList<string> AvailableStrategies =>
        Factories.Keys.ToList().AsReadOnly();
}
