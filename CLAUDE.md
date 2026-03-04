# BoardWC — Claude Code Context

## Documentation Maintenance
**Whenever rules or architecture change, update `CLAUDE.md`, `gamerules.md`, `architecture.md`, and `EngineAPI.md`.**
- `gamerules.md` — source of truth for board game rules as described by the user.
- `CLAUDE.md` — source of truth for codebase architecture, patterns, and conventions.
- `architecture.md` — full description of implemented architecture.
- `EnginaAPI.md` — full description of engine api.

---

## Project Overview
Digital implementation of **The White Castle** board game in C# / .NET 10.0.
Goal: rules engine as a reusable library; console UI as the first client.

## Tech Stack
- .NET 10.0, C# 13
- xUnit for tests
- No external dependencies (pure domain model)

## Solution Structure
```
BoardWC.slnx
src/
  BoardWC.Engine/          ← game library (no UI references)
    Actions/               ← IGameAction sealed records
    AI/                    ← IAiStrategy implementations
    Domain/                ← core model: Board, Player, Die, DicePlaceholder, …
    Engine/                ← IGameEngine, GameEngine, GameEngineFactory
    Events/                ← IDomainEvent sealed records
    Rules/                 ← IActionHandler chain + LegalActionGenerator + PostActionProcessor
  BoardWC.Console/
    Input/                 ← ConsoleInputParser
    Presenters/            ← ConsoleRenderer
    Program.cs
tests/
  BoardWC.Engine.Tests/
    GameEngineTests.cs
```

## Key Architectural Patterns

### Command pattern — actions
All player moves are `sealed record` types implementing `IGameAction` in `GameActions.cs`.
```
StartGameAction()
TakeDieFromBridgeAction(PlayerId, BridgeColor, DiePosition)
PlaceDieAction(PlayerId, PlacementTarget)
ChooseResourceAction(PlayerId, ResourceType)   ← resolves AnyResource token from well
PassAction(PlayerId)
```

### Chain-of-responsibility — handlers
`CompositeActionHandler` walks a list of `IActionHandler` implementations.
Each handler exposes `Validate` and `Apply`; only one handler owns each action type.
Registration order is in `GameEngineFactory.cs`.

```
StartGameHandler
TakeDieFromBridgeHandler
PlaceDieHandler
ChooseResourceHandler
PassHandler
```

### Snapshot DTOs — public surface
Domain types are `internal`. The only public data are immutable `sealed record` snapshots
(`GameStateSnapshot`, `BoardSnapshot`, `PlayerSnapshot`, …) returned by `IGameEngine.GetCurrentState()`.

### Domain events
Every handler emits `IDomainEvent` records on `Apply`. The engine returns them as
`ActionResult.Success(events)`. Consumers (console, tests) read events for side-effects /
display. Events are never used for in-engine state mutation.

### Two-step turn flow (take die → place die → optional resource choices)
`TakeDieFromBridgeHandler` puts the die into `player.DiceInHand`.
`PostActionProcessor` returns early (skips turn advance) while `DiceInHand.Count > 0` or `PendingAnyResourceChoices > 0`.
`LegalActionGenerator` returns ONLY `PlaceDieAction` options while die is in hand; ONLY `ChooseResourceAction` options while choices are pending.
`PlaceDieHandler` removes the die from hand; if placed at the well, applies token effects (seal, resources, coins) and sets `PendingAnyResourceChoices`.
`ChooseResourceHandler` decrements `PendingAnyResourceChoices` and grants the chosen resource.
Castle room placement is restricted to rooms that contain a token matching the die's color
(`PlaceDieHandler.Validate` checks `placeholder.Tokens.Any(t => t.DieColor == die.Color)`;
`LegalActionGenerator` applies the same filter so illegal rooms are never offered).

### Round-end trigger
`PostActionProcessor` checks `board.TotalDiceRemaining ≤ 3` after every action.
When triggered: placement areas clear, dice reroll (or game ends).

## Player State
Each player tracks:
- **Resources**: Food, Iron, Mother of Pearls (max 7 each)
- **Coins**: earned/spent when placing dice
- **Daimyo Seals**: separate currency (max 5); +1 gained each time a die is placed at the well
- **Lantern score**: victory points from lanterns
- **Dice in hand**: die taken from bridge awaiting placement
- **PendingAnyResourceChoices**: unresolved AnyResource token choices from the well
- **Personnel**: 5 Soldiers, 5 Courtiers, 5 Farmers — placement rules TBD
- **LanternChain**: ordered list of `LanternChainItem` entries; fires left-to-right whenever a Lantern gain triggers (see `LanternHelper`)
- **PersonalDomainCards**: room cards acquired from ground/mid castle floors; each card's fields activate whenever a die is placed in the corresponding personal domain row
- **Influence**: separate currency gained from card fields (`CardGainType.Influence`); tracked as `player.Influence`; displayed in player summary
- **PendingInfluenceGain / PendingInfluenceSealCost**: set when an influence gain crosses a threshold; player must decide to pay seals or forfeit the gain (resolved by `ChooseInfluencePayHandler`)
- **InfluenceGainOrder**: set to `GameState.InfluenceGainCounter++` each time the player actually gains influence; used at round end to break ties in first-player order

## Common Commands
```bash
dotnet build                                      # build all projects
dotnet test                                       # run 66 unit tests
dotnet run --project src/BoardWC.Console          # play in console
```

## Key Enums
- `BridgeColor`: Red, Black, White
- `DiePosition`: High, Low
- `ResourceType`: Food, Iron, MotherOfPearls
- `TokenResource`: Food, Iron, MotherOfPearls, AnyResource, Coin
- `PlayerColor`: White, Black, Red, Blue
- `Phase`: Setup, WorkerPlacement, EndOfRound, GameOver
- `CardGainType`: Food, Iron, MotherOfPearls, Coin, DaimyoSeal, Lantern, AnyResource, **VictoryPoint** (maps to `LanternScore += vp` directly, no chain), **Influence** (routed through `InfluenceHelper.Apply()`; may create pending state)

## Coding Conventions
- Domain classes are `internal sealed class`; records used for value objects.
- Public types are snapshots, actions, events, and the `IGameEngine` interface.
- `static class` for stateless rule logic (`LegalActionGenerator`, `PostActionProcessor`).
- No LINQ in hot validate paths; prefer simple loops and early-return.
- Tests use `GameEngineFactory.Create(players)` for integration-style tests.
- Helper `TakeAndPlaceAtWell(engine)` in tests drains one die (take High + place Well).

## Key Files Quick Reference
| Purpose | File |
|---|---|
| Public game interface | `src/BoardWC.Engine/Engine/IGameEngine.cs` |
| Factory / wiring | `src/BoardWC.Engine/Engine/GameEngineFactory.cs` |
| All actions | `src/BoardWC.Engine/Actions/GameActions.cs` |
| All events | `src/BoardWC.Engine/Events/DomainEvents.cs` |
| All snapshots | `src/BoardWC.Engine/Domain/Snapshots.cs` |
| Board model | `src/BoardWC.Engine/Domain/Board.cs` |
| Player model | `src/BoardWC.Engine/Domain/Player.cs` |
| Token model | `src/BoardWC.Engine/Domain/Token.cs` |
| Placement slot | `src/BoardWC.Engine/Domain/DicePlaceholder.cs` |
| Placement targets | `src/BoardWC.Engine/Domain/PlacementTarget.cs` |
| Legal moves | `src/BoardWC.Engine/Rules/LegalActionGenerator.cs` |
| Turn/round logic | `src/BoardWC.Engine/Rules/PostActionProcessor.cs` |
| Lantern chain helper | `src/BoardWC.Engine/Rules/LanternHelper.cs` |
| Influence threshold helper | `src/BoardWC.Engine/Rules/InfluenceHelper.cs` |
| Console renderer | `src/BoardWC.Console/Presenters/ConsoleRenderer.cs` |
| Console input | `src/BoardWC.Console/Input/ConsoleInputParser.cs` |
| Tests | `tests/BoardWC.Engine.Tests/GameEngineTests.cs` |

### Personal Domain Card Field Mapping
When a room card is acquired and a die later placed in a personal domain row, that card's field fires:
| Card type | Layout | Row 0 (Red/Courtier) | Row 1 (White/Farmer) | Row 2 (Black/Soldier) |
|-----------|--------|----------------------|----------------------|------------------------|
| Ground floor | null (3 fields) | field[0] | field[1] | field[2] |
| Mid floor | DoubleTop | field[0] | field[0] | field[1] |
| Mid floor | DoubleBottom | field[0] | field[1] | field[1] |


### Excplicit Quality Requirements
For each implementation change make sure that appropirate tests are added and that code coverage is at maximum