# BoardWC — Architecture

Digital implementation of *The White Castle* board game in C# / .NET 10.0.
The goal is a rules engine as a reusable library with a thin console UI as the first client.

---

## Solution Structure

```
BoardWC.slnx
│
├── src/
│   ├── BoardWC.Engine/          ← game rules library (no UI references)
│   │   ├── Actions/             ← IGameAction sealed records
│   │   ├── AI/                  ← IAiStrategy public extension point
│   │   ├── Domain/              ← internal model: Board, Player, Die, Token, …
│   │   ├── Engine/              ← IGameEngine, GameEngine, GameEngineFactory, ActionResult
│   │   ├── Events/              ← IDomainEvent sealed records
│   │   └── Rules/               ← handlers, LegalActionGenerator, PostActionProcessor, ScoreCalculator
│   │
│   └── BoardWC.Console/         ← thin interactive client
│       ├── Presenters/          ← ConsoleRenderer, InteractiveConsole
│       └── Program.cs
│
└── tests/
    └── BoardWC.Engine.Tests/    ← xUnit integration-style tests against the public API
```

---

## Layered Boundary

```
┌──────────────────────────────────────────────────────┐
│  External consumers (Console, tests, future clients) │
│  ─ see only public types ─                           │
│  IGameEngine · ActionResult · IGameAction            │
│  IDomainEvent · Snapshot records · ResourceBag       │
└─────────────────────┬────────────────────────────────┘
                      │ public API
┌─────────────────────▼────────────────────────────────┐
│  BoardWC.Engine (library)                            │
│  ┌──────────────────────────────────────────────┐   │
│  │  Engine/  — GameEngine (internal sealed)     │   │
│  │  ├─ GameState (internal)                     │   │
│  │  ├─ CompositeActionHandler (internal)        │   │
│  │  └─ IAiStrategy? (public interface)          │   │
│  ├──────────────────────────────────────────────┤   │
│  │  Rules/ — stateless static helpers           │   │
│  │  ├─ LegalActionGenerator                     │   │
│  │  ├─ PostActionProcessor                      │   │
│  │  └─ ScoreCalculator                          │   │
│  ├──────────────────────────────────────────────┤   │
│  │  Rules/ — handler chain                      │   │
│  │  IActionHandler × 11 concrete handlers       │   │
│  ├──────────────────────────────────────────────┤   │
│  │  Domain/ — internal mutable model            │   │
│  │  Board · Player · Bridge · DicePlaceholder   │   │
│  │  Token · Die · RoomCard · PersonalDomainRow  │   │
│  │  TrainingGrounds · FarmingLands · TopFloorRoom│  │
│  └──────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────┘
```

**Key invariant:** Domain types are `internal sealed class`. The only data that crosses the
library boundary are:
- `sealed record` snapshots (immutable DTOs) — `GetCurrentState()`, `ActionResult.Success.NewState`
- `sealed record` events — `ActionResult.Success.Events`
- `sealed record` actions — submitted via `ProcessAction()`

---

## Core Patterns

### 1 — Command pattern (Actions)

Every player move is a `public sealed record` implementing `IGameAction` in `GameActions.cs`.
Records are immutable value objects; pattern-matching on their type drives all handler routing.

```
IGameAction (marker interface, exposes PlayerId)
  ├── StartGameAction()
  ├── ChooseSeedPairAction(PlayerId, PairIndex)
  ├── TakeDieFromBridgeAction(PlayerId, BridgeColor, DiePosition)
  ├── PlaceDieAction(PlayerId, Target: PlacementTarget)
  ├── ChooseResourceAction(PlayerId, Choice)
  ├── ChooseInfluencePayAction(PlayerId, WillPay)
  ├── ChooseOutsideActivationAction(PlayerId, Choice)
  ├── CastlePlaceCourtierAction(PlayerId)
  ├── CastleAdvanceCourtierAction(PlayerId, From, Levels, RoomIndex)
  ├── CastleSkipAction(PlayerId)
  ├── TrainingGroundsPlaceSoldierAction(PlayerId, AreaIndex)
  ├── TrainingGroundsSkipAction(PlayerId)
  ├── PlaceFarmerAction(PlayerId, BridgeColor, IsInland)
  ├── FarmSkipAction(PlayerId)
  └── PassAction(PlayerId)
```

### 2 — Chain-of-responsibility (Handlers)

`CompositeActionHandler` walks a fixed list of `IActionHandler` implementations.
Each handler declares which action type it owns via `CanHandle(action)`.
`Find()` picks the first matching handler; unknown actions return a failure result.

```
internal interface IActionHandler
{
    bool CanHandle(IGameAction action);
    ValidationResult Validate(IGameAction action, GameState state);
    void Apply(IGameAction action, GameState state, List<IDomainEvent> events);
}
```

Handlers registered in `GameEngineFactory.BuildHandlerChain()` (in order):

| Handler | Action |
|---|---|
| `StartGameHandler` | `StartGameAction` |
| `ChooseSeedPairHandler` | `ChooseSeedPairAction` |
| `TakeDieFromBridgeHandler` | `TakeDieFromBridgeAction` |
| `PlaceDieHandler` | `PlaceDieAction` |
| `ChooseResourceHandler` | `ChooseResourceAction` |
| `ChooseInfluencePayHandler` | `ChooseInfluencePayAction` |
| `OutsideActivationHandler` | `ChooseOutsideActivationAction` |
| `CastlePlayHandler` | `CastlePlaceCourtierAction`, `CastleAdvanceCourtierAction`, `CastleSkipAction` |
| `TrainingGroundsHandler` | `TrainingGroundsPlaceSoldierAction`, `TrainingGroundsSkipAction` |
| `FarmHandler` | `PlaceFarmerAction`, `FarmSkipAction` |
| `PassHandler` | `PassAction` |

Each handler's `Validate` checks preconditions (phase, player identity, resource
availability, slot capacity, etc.) and returns `ValidationResult.Ok()` or `.Fail(reason)`.
`Apply` mutates `GameState` unconditionally (validation is always called first by
`GameEngine.ProcessAction`) and appends `IDomainEvent` records to the shared list.

### 3 — Snapshot DTO pattern

Domain objects are never exposed. Each internal type has a `ToSnapshot()` method that
produces an immutable `sealed record`. Consumers read snapshots; they never hold
references into the live domain model.

```
GameState.ToSnapshot()
  └─ Player.ToSnapshot()  ──→  PlayerSnapshot
  └─ Board.ToSnapshot()
       ├─ Bridge.ToSnapshot()       ──→  BridgeSnapshot
       ├─ DicePlaceholder.ToSnapshot() ──→  DicePlaceholderSnapshot
       ├─ TopFloorRoom.ToSnapshot() ──→  TopFloorRoomSnapshot
       ├─ TrainingGrounds.ToSnapshot() ──→ TrainingGroundsSnapshot
       └─ FarmingLands.ToSnapshot() ──→  FarmingLandsSnapshot
```

Snapshot construction happens on every `ProcessAction` call and on every
`GetCurrentState()` call. Snapshot records are value types — equality and
hashing are structural and compiler-generated.

### 4 — Domain events

Handlers emit `IDomainEvent` records during `Apply`. Events flow out through
`ActionResult.Success.Events`. They are **never** read back by any internal engine code —
they are purely for consumers (UI display, logging, AI reward signals, test assertions).

```
IDomainEvent
  ├── GameId        (Guid)
  ├── OccurredAt    (DateTimeOffset, set at construction)
  └── EventType     (string = class name, for serialisation)
```

### 5 — Single mutable state object (`GameState`)

`GameEngine` owns one `GameState` instance for the lifetime of the session.
`GameState` holds references to `Board` and `List<Player>`, plus cross-cutting fields
(`CurrentPhase`, `CurrentRound`, `ActivePlayerIndex`, `Rng`, `InfluenceGainCounter`).

All mutation flows through the handler `Apply` methods; no other code writes to `GameState`.

---

## `GameEngine.ProcessAction` — request lifecycle

```
ProcessAction(action)
│
├─ CompositeActionHandler.Validate(action, state)
│    └─ if invalid → return ActionResult.Failure(state.ToSnapshot(), reason)
│
├─ events = new List<IDomainEvent>()
├─ CompositeActionHandler.Apply(action, state, events)
│    └─ mutates GameState, appends events
│
├─ if action is not StartGameAction:
│    PostActionProcessor.Run(state, events)
│       └─ checks pending sub-states; if all clear → advance turn or end round
│
├─ if IsGameOver:
│    _finalScores = ScoreCalculator.Calculate(state)
│
└─ return ActionResult.Success(state.ToSnapshot(), events.AsReadOnly())
```

---

## `PostActionProcessor` — turn and round state machine

`PostActionProcessor.Run` is called after every action (except `StartGameAction`).
It is a chain of **early-return guards**: if any pending sub-state is detected, it
returns immediately (holding the active player's turn).

```
Guards (in order):
  1. SeedCardSelection phase: hold for AnyResource choices; advance player or enter WorkerPlacement
  2. DiceInHand.Count > 0           → player must place the die first
  3. PendingOutsideActivationSlot ≥ 0 → player must choose Farm/Castle/TG
  4. PendingInfluenceGain > 0       → player must resolve seal payment
  5. PendingAnyResourceChoices > 0  → player must resolve well resource choices
  6. PendingTrainingGroundsActions > 0 → player must resolve TG placement
  7. PendingFarmActions > 0         → player must resolve farm placement
  8. CastlePlaceRemaining > 0 ‖ CastleAdvanceRemaining > 0 → player must resolve castle play

If all guards pass:
  TotalDiceRemaining ≤ 3 → EndRound()
  else                    → AdvanceTurn()
```

`EndRound()` fires farm effects for remaining-dice bridges, clears placement areas, clears
personal-domain placed dice, and either advances the round counter (rolling fresh dice,
re-drawing training-grounds tokens) or transitions to `Phase.GameOver`.

**Round-start player order** is determined by influence: the player with the highest
influence score goes first. Ties are broken by `InfluenceGainOrder` (a monotonically
increasing counter recording the last time each player gained influence — higher = more
recent = goes first).

---

## `LegalActionGenerator` — move enumeration

`LegalActionGenerator.Generate(playerId, state)` returns the exact set of legal moves for
a given player at the current moment. It mirrors the same priority order as
`PostActionProcessor`'s guards, ensuring consistency between "what can I submit?" and
"what will the engine accept?".

**Priority order (first matching group is returned exclusively):**

| Priority | Condition | Offered actions |
|---|---|---|
| 1 | Phase == Setup | `StartGameAction` |
| 2 | Phase == SeedCardSelection, not active player | *(empty)* |
| 2 | Phase == SeedCardSelection, AnyResource pending | `ChooseResourceAction` × 3 |
| 2 | Phase == SeedCardSelection | `ChooseSeedPairAction` × N |
| 3 | Phase ≠ WorkerPlacement | *(empty)* |
| 4 | Not the active player | *(empty)* |
| 5 | PendingInfluenceGain > 0 | `ChooseInfluencePayAction(true)`, `ChooseInfluencePayAction(false)` |
| 6 | PendingAnyResourceChoices > 0 | `ChooseResourceAction` × 3 |
| 7 | PendingOutsideActivationSlot ≥ 0 | Slot 0: Farm+Castle; Slot 1: TG+Castle |
| 8 | PendingFarmActions > 0 | `FarmSkipAction` + valid `PlaceFarmerAction` entries |
| 9 | PendingTrainingGroundsActions > 0 | `TrainingGroundsSkipAction` + valid `TrainingGroundsPlaceSoldierAction` entries |
| 10 | Castle actions pending | `CastleSkipAction` + valid place/advance options |
| 11 | DiceInHand.Count > 0 | Valid `PlaceDieAction` targets |
| 12 | Normal turn | `TakeDieFromBridgeAction` entries + `PassAction` |

For die placement (priority 11), placement is offered for:
- Castle rooms whose token set contains at least one token matching the die's color
- The well (always, subject to coin affordability)
- Outside slots (subject to coin affordability and capacity)
- Personal domain rows whose die-color matches and whose compare-value is affordable

For courtier advances (priority 10), `ValidAdvances` enumerates all (from, levels, roomIndex)
triples based on VI costs: 2 VI for 1 level, 5 VI for 2 levels.

---

## `ScoreCalculator`

Called once when `GameState.CurrentPhase` transitions to `GameOver`.
Returns `IReadOnlyList<PlayerScore>` ordered descending by total score.

| Category | Formula |
|---|---|
| Lantern points | `player.LanternScore` |
| Courtier points | Gate×1 + Ground×3 + Mid×6 + Top×10 |
| Coin points | `Coins / 5` |
| Seal points | `MonarchialSeals / 5` |
| Resource points | ≥4 of one type = 1 VP; 7 = 2 VP (per resource independently) |
| Farm points | Sum of `VictoryPoints` on each field where the player has a farmer |
| Training grounds points | (soldiers in area 0+1) × castle-courtiers + (soldiers in area 2) × 2 × castle-courtiers |
| Influence points | 0–5 → 0; 6–10 → 3; 11–15 → 6; 16 → 7; 17 → 8; 18 → 9; 19 → 10; ≥20 → 11 |

---

## Data Layer

Card data and token configurations are loaded from embedded JSON resources at runtime.
No files are written; all data flows inward (library only reads).

| Resource | Class | Loaded by |
|---|---|---|
| `ground-floor-cards.json` | `FloorCardDeck` | `Board.PlaceCards()` |
| `mid-floor-cards.json` | `FloorCardDeck` | `Board.PlaceCards()` |
| `training-grounds.json` | `TrainingGrounds` | `Board.SetupTrainingGrounds()` |
| `farming-lands.json` | `FarmingLands` | `Board.SetupFarmingLands()` |
| `top-floor-card.json` | `TopFloorRoom` | `Board.SetupTopFloorCard()` |
| `personal-domain-rows.json` | `PersonalDomainRowConfig` | `GameEngineFactory.Create()` |
| `seed-cards.json` | `SeedCardLoader` | `StartGameHandler.Apply()` |

All data is loaded and shuffled during `StartGameAction` handling. The `Random rng` instance
lives on `GameState` so seeding is reproducible when injected.

---

## `IAiStrategy` — extension point

```csharp
public interface IAiStrategy
{
    string StrategyId { get; }
    IGameAction SelectAction(GameStateSnapshot state, IReadOnlyList<IGameAction> legalActions);
}
```

Passed to `GameEngineFactory.Create(aiStrategy: ...)`. When the active player is an AI
(`PlayerSetup.IsAI = true`), the engine may call `SelectAction` — however, the current
`GameEngine` implementation leaves AI turn invocation to the client (the console loop).
The interface is public so external callers can implement and inject their own strategies.

---

## `PlaceDieHandler` — most complex handler

`PlaceDieHandler` covers four distinct placement targets:

| Target | Coin delta | Side effects |
|---|---|---|
| `CastleRoomTarget` | `die.Value − room.CompareValue(playerCount)` | Activates token-matching card fields (GainCardField or ActionCardField); "Play castle/farm/TG" actions set pending flags |
| `WellTarget` | `die.Value − 1` | +1 seal; reads all well tokens: fixed resources, coins, or AnyResource choices |
| `OutsideSlotTarget` | `die.Value − 5` | Sets `PendingOutsideActivationSlot` = slot index |
| `PersonalDomainTarget` | `die.Value − row.CompareValue` | Default resource gain + uncovered-spot gains; activates seed card; activates acquired personal-domain room card fields |

**Castle room token matching**: a die may only be placed in a room whose `Tokens` list
contains at least one token with `DieColor == die.Color`. The same filter is applied in
both `Validate` and `LegalActionGenerator` so the two are always consistent.

**Compare value scaling**: `DicePlaceholder.GetCompareValue(playerCount)` returns the
room's `BaseValue` for 2–3 players and `BaseValue + 1` for 4 players (harder to use in
larger games because dice are more contested).

**Room card field activation**: field index `i` is activated when `Tokens[i].DieColor == die.Color`.
For mid-floor cards, `Layout` (`"DoubleTop"` / `"DoubleBottom"`) maps row index to field
index via `GetFieldIndexForRow`.

---

## Personal Domain Card Interaction Chain

When a die is placed on a personal domain row, three activations fire in sequence:

```
PlaceDieHandler.ApplyPersonalDomain()
  1. Default gain (row.Config.DefaultGainType × DefaultGainAmount)
  2. Uncovered spot gains (left-to-right up to UncoveredCount)
  3. Seed card activation (PlayCastle / PlayFarm / PlayTrainingGrounds)
  4. Personal domain card fields (one field per acquired room card)
       └─ Field index mapping: Layout=null → rowIndex; DoubleTop → 0/1 split; DoubleBottom → 0/1 split
```

---

## Board Setup Sequence

`StartGameHandler.Apply` calls these in order:

```
1. Board.RollAllDice(playerCount, rng)    — dice on bridges
2. Board.PlaceTokens(rng)                 — 15 tokens across rooms + well
3. Board.PlaceCards(rng)                  — deal 5 room cards from shuffled decks
4. Board.SetupTrainingGrounds(rng)        — load token pool, draw 4 for round 1
5. Board.SetupFarmingLands(rng)           — load field cards, deal one per field
6. Board.SetupTopFloorCard(rng)           — draw one top-floor room card
7. SeedCardLoader.Load()                  — shuffle pairs, deal N+1 pairs (N = players)
```

Token placement uses a constrained-random algorithm:
- Ground rooms: 3 tokens each; at least 2 different die colors per room
- Mid rooms: 2 tokens each; different die colors per room
- Remaining 2 tokens → well, resource-side up

---

## Console Layer

`BoardWC.Console` is a thin shell that drives `IGameEngine` in a REPL loop.

```
Program.cs
  └─ InteractiveConsole
       ├─ loops: readline → parse → ProcessAction → render
       ├─ ConsoleRenderer — formats GameStateSnapshot as ANSI terminal output
       └─ handles AI turns by calling engine.GetLegalActions + IAiStrategy.SelectAction
```

The console layer only imports `BoardWC.Engine`'s public API. It never touches domain
internals. All game-state reads go through `GetCurrentState()` or the `ActionResult`
returned from `ProcessAction`.

---

## Testing Approach

Tests live in `BoardWC.Engine.Tests` and use `xUnit`.

**Integration style**: tests create a real `IGameEngine` via `GameEngineFactory.Create` and
drive it through `ProcessAction` / `GetLegalActions`. No mocks; the full handler chain runs.

**Coverage targets**: every public action, snapshot property, event, and legal-move path is
exercised. Internal types have no direct tests — coverage is achieved through the public API.

**Key helpers used in tests**:
- `GameEngineFactory.Create([new PlayerSetup(...), ...])` — standard two-player setup
- `engine.ProcessAction(new StartGameAction())` — moves to WorkerPlacement
- `engine.GetLegalActions(playerId)` — verifies which moves are offered
- `state.Board.PlaceCards(state.Rng)` — repopulates card decks in handlers tests
- `DicePlaceholder.SetCard(card)` — injects custom cards for targeted field tests

---

## Design Decisions

| Decision | Rationale |
|---|---|
| All domain types `internal` | Prevents consumers from bypassing the action/validation pipeline; enables free refactoring without breaking the public API |
| Snapshots as `sealed record` | Structural equality is free; safe to cache, diff, or serialize; no aliasing bugs |
| Events not used for internal mutation | Handlers mutate state directly; events are output-only — simpler than event-sourcing while still giving consumers a rich feed of changes |
| `PostActionProcessor` as a static guard chain | Keeps turn-advance logic in one place; each guard mirrors a `LegalActionGenerator` priority, keeping the two in sync |
| `LegalActionGenerator` returns exclusive groups | Prevents the client from submitting actions out of order; only one "mode" of legal actions at a time |
| Coin delta at placement | Spending or earning coins is implicit in die value vs. compare value; no separate "pay coins" action needed |
| `Random rng` on `GameState` | Single source of randomness; injectable for deterministic tests when needed |
| No external dependencies | Pure .NET; embedded JSON via `System.Text.Json`; no NuGet packages in the engine |
