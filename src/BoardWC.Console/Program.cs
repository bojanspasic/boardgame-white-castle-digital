using BoardWC.Console.Input;
using BoardWC.Console.Presenters;
using BoardWC.Engine.Actions;
using BoardWC.Engine.AI;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;

// ── Setup ──────────────────────────────────────────────────────────────────

var aiStrategy = new GreedyResourceAiStrategy();

var players = new[]
{
    new PlayerSetup("Human Player", PlayerColor.White, IsAI: false),
    new PlayerSetup("AI Opponent",  PlayerColor.Black, IsAI: true, AiStrategyId: "greedy-resource"),
};

var engine   = GameEngineFactory.Create(players, aiStrategy, maxRounds: 3);
var renderer = new ConsoleRenderer();
var parser   = new ConsoleInputParser();

// ── Start the game ─────────────────────────────────────────────────────────

var startResult = engine.ProcessAction(new StartGameAction());
if (startResult is ActionResult.Success startOk)
{
    renderer.Render(startOk.NewState);
    renderer.RenderEvents(startOk.Events);
}

// ── Main game loop ─────────────────────────────────────────────────────────

while (!engine.IsGameOver)
{
    var state  = engine.GetCurrentState();
    var active = state.Players[state.ActivePlayerIndex];

    if (active.IsAI)
    {
        System.Console.WriteLine($"\n  [AI — {active.Name}] thinking...");
        System.Threading.Thread.Sleep(600);

        var legal    = engine.GetLegalActions(active.Id);
        var aiAction = aiStrategy.SelectAction(state, legal);
        var aiResult = engine.ProcessAction(aiAction);

        if (aiResult is ActionResult.Success aiOk)
        {
            renderer.RenderEvents(aiOk.Events);
            renderer.Render(aiOk.NewState);
        }
        else if (aiResult is ActionResult.Failure aiErr)
        {
            renderer.Error($"AI error: {aiErr.Reason}");
            break;
        }

        continue;
    }

    // ── Human turn ──────────────────────────────────────────────────────────
    renderer.RenderPrompt(state);

    var raw = System.Console.ReadLine() ?? string.Empty;
    var parsed = parser.Parse(raw, state);

    if (!parsed.Success)
    {
        renderer.Error(parsed.ErrorMessage!);
        continue;
    }

    var result = engine.ProcessAction(parsed.Action!);

    switch (result)
    {
        case ActionResult.Success ok:
            renderer.RenderEvents(ok.Events);
            renderer.Render(ok.NewState);
            break;

        case ActionResult.Failure fail:
            renderer.Error(fail.Reason);
            break;
    }
}

// ── Final scores ────────────────────────────────────────────────────────────

var scores = engine.GetFinalScores();
if (scores is not null)
    renderer.RenderFinalScores(scores);

System.Console.WriteLine("\nPress any key to exit...");
System.Console.ReadKey();
