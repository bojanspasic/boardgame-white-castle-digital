using BoardWC.Console.Presenters;
using BoardWC.Console.UI;
using BoardWC.Engine.Actions;
using BoardWC.Engine.AI;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;

var io = new SystemConsoleIO();

// ── Splash screen ──────────────────────────────────────────────────────────

SplashScreen.Show(io);

// ── Main menu ──────────────────────────────────────────────────────────────

PlayerColor[] colors = [PlayerColor.Blue, PlayerColor.Red, PlayerColor.White, PlayerColor.Black];
var selected = MainMenu.Show(io);
var players = selected
    .Select((type, i) => (type, i))
    .Where(x => x.type != MainMenu.PlayerType.Empty)
    .Select(x => new PlayerSetup(
        $"Player {x.i + 1}",
        colors[x.i],
        IsAI: x.type == MainMenu.PlayerType.Ai,
        AiStrategyId: x.type == MainMenu.PlayerType.Ai ? "greedy-resource" : null))
    .ToArray();

// ── Setup ──────────────────────────────────────────────────────────────────

var engine   = GameEngineFactory.Create(players, new GreedyResourceAiStrategy(), maxRounds: 3);
var renderer = new ConsoleRenderer();
var ui       = new InteractiveConsole();

// ── Start the game ─────────────────────────────────────────────────────────

var startResult = engine.ProcessAction(new StartGameAction());
if (startResult is ActionResult.Success startOk)
    ui.SetLastEvents(startOk.Events);

// ── Main game loop ─────────────────────────────────────────────────────────

while (!engine.IsGameOver)
{
    var state  = engine.GetCurrentState();
    var active = state.Players[state.ActivePlayerIndex];

    if (active.IsAI)
    {
        ConsoleColor aiColor = active.Color switch
        {
            PlayerColor.Blue  => ConsoleColor.Blue,
            PlayerColor.Red   => ConsoleColor.Red,
            PlayerColor.White => ConsoleColor.White,
            _                 => ConsoleColor.DarkGray,
        };
        var aiResult = AiThinkingOverlay.Show(io, () => engine.PlayAiTurn(active.Id), aiColor);

        if (aiResult is ActionResult.Success aiOk)
        {
            ui.SetLastEvents(aiOk.Events);
            System.Threading.Thread.Sleep(1500);
        }
        else if (aiResult is ActionResult.Failure aiErr)
        {
            renderer.Error($"AI error: {aiErr.Reason}");
            break;
        }
        continue;
    }

    // ── Human turn ──────────────────────────────────────────────────────────

    var action = ui.Run(state, engine);
    var result = engine.ProcessAction(action);

    switch (result)
    {
        case ActionResult.Success ok:
            ui.SetLastEvents(ok.Events);
            System.Threading.Thread.Sleep(1500);
            break;

        case ActionResult.Failure fail:
            // Should rarely happen since we only offer legal actions
            renderer.Error(fail.Reason);
            System.Threading.Thread.Sleep(1500);
            break;
    }
}

// ── Final scores ────────────────────────────────────────────────────────────

var scores = engine.GetFinalScores();
if (scores is not null)
    renderer.RenderFinalScores(scores);

System.Console.WriteLine("\nPress any key to exit...");
System.Console.ReadKey();
