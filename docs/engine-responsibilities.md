# BoardWC.Engine — Class & Interface Responsibilities

Full inventory of every type in `src/BoardWC.Engine/`, organized by directory.

> **See bottom of file** for a SRP analysis — completed refactoring summary.

---

## Actions/

### `IGameAction` (interface)
- Marker interface for all game actions
- Mandates `PlayerId` on every action

### `StartGameAction` (sealed record)
- Signals game start (Setup → SeedCardSelection)
- Uses `PlayerId = Guid.Empty` (no real player initiates this)

### `TakeDieFromBridgeAction` (sealed record)
- Captures which die a player takes (bridge color + High/Low position)

### `PlaceDieAction` (sealed record)
- Captures where a player places their held die (any `PlacementTarget`)

### `PassAction` (sealed record)
- Captures a player's decision to pass their turn

### `ChooseResourceAction` (sealed record)
- Resolves one pending `AnyResource` token choice (well effect)

### `CastlePlaceCourtierAction` (sealed record)
- Captures placing a courtier at the gate (costs 2 coins)

### `CastleAdvanceCourtierAction` (sealed record)
- Captures advancing a courtier up the castle (costs Mother of Pearls; specifies from-position, levels, and optional room index)

### `CastleSkipAction` (sealed record)
- Skips all remaining pending castle play options

### `TrainingGroundsPlaceSoldierAction` (sealed record)
- Places a soldier in one of 3 training grounds areas (costs iron per area config)

### `TrainingGroundsSkipAction` (sealed record)
- Skips a pending training grounds action

### `PlaceFarmerAction` (sealed record)
- Places a farmer on an inland or outside farm field (costs food per card)

### `FarmSkipAction` (sealed record)
- Skips a pending farm action

### `ChooseOutsideActivationAction` (sealed record)
- Resolves which area activates after placing a die at an outside slot (Farm, Castle, or TrainingGrounds)

### `ChooseSeedPairAction` (sealed record)
- Selects a seed card pair during the SeedCardSelection phase

### `ChooseInfluencePayAction` (sealed record)
- Resolves an influence-threshold payment decision (accept seal cost or decline)

### `ChooseCastleCardFieldAction` (sealed record)
- Activates a color-filtered castle room card field (specifies floor, room, field, or all -1 to skip)

### `ChoosePersonalDomainRowAction` (sealed record)
- Activates a personal domain row for free without placing a die

### `ChooseNewCardFieldAction` (sealed record)
- Selects which field to activate on a newly acquired room card (or -1 to skip)

### `CourtierPosition` (enum)
- Defines courtier positions: `Gate`, `StewardFloor`, `DiplomatFloor`

### `OutsideActivation` (enum)
- Defines outside activation choices: `Farm`, `Castle`, `TrainingGrounds`

---

## AI/

### `IAiStrategy` (interface)
- Contract for AI decision-making: exposes `StrategyId` and `SelectAction(legalActions, state)`

### `RandomAiStrategy` (sealed class)
- Implements `IAiStrategy` with uniform random selection from legal actions

### `GreedyResourceAiStrategy` (sealed class)
- Implements `IAiStrategy` preferring the highest-value die take; falls back to Pass

### `AiStrategyRegistry` (static class)
- Factory registry mapping strategy ID strings (`"random"`, `"greedy-resource"`) to `IAiStrategy` instances
- Enforces that only known strategy IDs can be resolved

---

## Domain/

### Enums
| Enum | Values |
|---|---|
| `ResourceType` | Food, Iron, MotherOfPearls |
| `Phase` | Setup, SeedCardSelection, WorkerPlacement, EndOfRound, GameOver |
| `PlayerColor` | White, Black, Red, Blue |
| `BridgeColor` | Red, Black, White |
| `DiePosition` | High, Low |
| `TokenResource` | Food, Iron, MotherOfPearls, AnyResource, Coin |
| `CardGainType` | Food, Iron, MotherOfPearls, Coin, DaimyoSeal, Lantern, AnyResource, Influence, VictoryPoint, Well, CastleGainField |
| `CardCostType` | Coin, DaimyoSeal |
| `SeedActionType` | PlayCastle, PlayFarm, PlayTrainingGrounds |

---

### `Die` (sealed class)
- Represents a single die: owns `Value` (1–6) and `BridgeColor`
- Converts to `DieSnapshot` for public API

### `Token` (sealed record)
- Represents a double-sided token: die-color side / resource side
- Tracks which side is face-up (`IsResourceSideUp`)

### `PlacementTarget` (abstract record) + subtypes
- Discriminated union identifying where a die can be placed:
  - `CastleRoomTarget` — floor (0–1) and room index
  - `WellTarget` — the well (singleton)
  - `OutsideSlotTarget` — slot index (0–1)
  - `PersonalDomainTarget` — row index (0–2)

### `DicePlaceholder` (sealed class)
- Represents a die placement slot (castle room, well, outside, or personal domain row)
- Owns: base value, unlimited-capacity flag, list of placed dice, list of tokens, associated `RoomCard`
- Enforces capacity limits per player count (2-player: 0–1 dice; 3–4 player: 0–2 dice; well: unlimited)

### `Bridge` (sealed class)
- Represents one of 3 bridges with High / Middle / Low die positions
- Owns: `BridgeColor`, current High/Low dice, middle stack
- Enforces die ordering by value; promotes middle dice when High or Low is taken
- Determines dice count per round by player count (2→3, 3→4, 4→5)

### `Board` (sealed class)
- Master aggregator of all board components
- Owns: 3 bridges, castle rooms (2×3 steward + 1×2 diplomat + 1 top floor), well, 2 outside slots, card decks, training grounds, farming lands
- Provides setup methods (`PlaceTokens`, `PlaceCards`, `SetupTrainingGrounds`, `SetupFarmingLands`, `SetupTopFloorCard`) and `RollAllDice` / `ClearPlacementAreas`

### `ResourceBag` (readonly record struct)
- Immutable resource storage: Food, Iron, MotherOfPearls
- Enforces clamping to 7-resource maximum
- Provides `Add`, `Subtract`, `CanAfford`, and operator `+`

### `CardGainItem` (sealed record)
- Stores one card gain entry: `CardGainType` + `Amount`

### `CardCostItem` (sealed record)
- Stores one card cost entry: `CardCostType` + `Amount`

### `CardField` (abstract record) + subtypes
- Discriminated union for card field types:
  - `GainCardField` — list of `CardGainItem`s applied on activation
  - `ActionCardField` — description string + list of `CardCostItem`s

### `RoomCard` (sealed class)
- Represents a castle room card: `Id`, list of `CardField`s, layout hint, optional back gain

### `FloorCardDeck` (sealed class)
- Manages a steward or diplomat floor card draw pile
- Loads from embedded JSON, shuffles, deals one at a time

### `TopFloorSlot` (sealed class)
- Represents one slot on the top floor card: gains list, occupant name
- Enforces single occupation (can only be claimed once)

### `TopFloorCard` (sealed class)
- Represents the top floor card with 3 slots and their associated gains

### `TopFloorRoom` (sealed class)
- Manages the top floor card pool; selects one card per game
- Occupies the first available empty slot on `TryTakeSlot`

### `Player` (sealed class)
- Central player state holder — permanent game state, worker pools, courtier positions, cards, and turn state
- **Resources / scoring**: `ResourceBag`, `LanternScore`, `Influence`, `Coins`, `DaimyoSeals`
- **Worker pools**: `SoldiersAvailable` (5), `CourtiersAvailable` (5), `FarmersAvailable` (5)
- **Courtier positions**: Gate, StewardFloor, DiplomatFloor, TopFloor counts
- **Turn state**: `DiceInHand`, `PersonalDomainRows`, `SeedCard`, `PersonalDomainCards`, `LanternChain`
- `InfluenceGainOrder` — counter for round-start turn-order tiebreaking
- Owns a `PlayerPendingState Pending { get; }` instance for all pending-state flags

### `PlayerPendingState` (sealed class)
- Holds all 9 pending-state flags that block turn advance until resolved:
  - `AnyResourceChoices` — unresolved AnyResource well token choices
  - `InfluenceGain` / `InfluenceSealCost` — influence threshold payment decision
  - `CastlePlaceRemaining` / `CastleAdvanceRemaining` — pending castle uses
  - `TrainingGroundsActions` / `FarmActions` — pending secondary actions
  - `OutsideActivationSlot` — which outside slot's activation choice is pending (-1 = none)
  - `CastleCardFieldFilter` — pending castle card field choice (color or `"GainOnly"`; null = none)
  - `PersonalDomainRowChoice` — pending free personal domain row activation
  - `NewCardActivation` — newly acquired room card awaiting field choice
- `HasAny` — single boolean property; true when any flag is set (used by `TurnAdvancePolicy`)

### `GameState` (sealed class)
- Game-wide state container
- Owns: `GameId`, `CurrentPhase`, `CurrentRound`, `MaxRounds`, `ActivePlayerIndex`, list of `Player`s, `Board`, `Rng`, `SeedCardPairs`, `InfluenceGainCounter`
- Provides `ActivePlayer` property and `AdvanceTurn` method
- Converts to `GameStateSnapshot`

### `PersonalDomainRowConfig` (sealed class)
- Immutable configuration for one personal domain row (shared across all players)
- Owns: die color, compare value, figure type, default gain, 5 spot gains
- Loads from embedded JSON once and caches statically

### `PersonalDomainRow` (sealed class)
- Runtime state for one player's personal domain row
- Owns: reference to shared config + `PlacedDie` this round
- Clears die for each new round

### `LanternChainGain` (sealed record)
- One gain entry in the lantern chain: `CardGainType` + `Amount`

### `LanternChainItem` (sealed class)
- One entry in a player's lantern chain (resource seed, action seed, steward/diplomat floor card, or decree)
- Owns: source card ID, source type, list of `LanternChainGain`s

### `TrainingGroundsToken` (sealed class)
- Represents a training grounds token with a resource side and an action side

### `TrainingGroundsArea` (sealed class)
- Represents one of 3 training grounds areas
- Owns: iron cost, resource gains, action description, list of soldier owners
- Resets per round; effect assigned by token draw

### `TrainingGrounds` (sealed class)
- Training grounds board component; loads token pool from JSON
- Draws 4 random tokens per round and assigns effects to the 3 areas

### `FarmCard` (sealed class)
- Represents a farm card: food cost, gain items, action description, victory point value

### `FarmField` (sealed class)
- Represents one farm field (inland or outside)
- Owns: `FarmCard` + list of farmer owners
- Enforces one farmer per player per field

### `FarmingLands` (sealed class)
- Farming lands board component
- Owns: 3×2 grid of `FarmField`s (3 bridge colors × inland/outside)
- Loads both inland and outside decks from JSON; deals one card per field at game start

### `DecreeCard` (sealed class)
- Represents a decree card that enters the lantern chain: gain type + amount

### `SeedActionCard` (sealed class)
- Represents a seed action card: ID, action type (Castle/Farm/TrainingGrounds), back gain for lantern chain

### `SeedResourceCard` (sealed class)
- Represents a seed resource card: ID, list of resource gains, back gain, optional decree card

### `SeedCardPair` (sealed record)
- Pairs one `SeedActionCard` with one `SeedResourceCard` for seed selection

### `SeedCardDecks` (static class)
- Loads action and resource seed card decks from JSON, shuffles independently, and pairs them for selection

### Snapshot types (`Snapshots.cs`)
All are `public sealed record`s — the only public data surface of the engine:

| Snapshot | Represents |
|---|---|
| `GameStateSnapshot` | Full game state (phase, round, players, board) |
| `PlayerSnapshot` | One player's public-facing state |
| `BoardSnapshot` | All board components |
| `DieSnapshot` | A single die (value, color) |
| `BridgeSnapshot` | One bridge (High, Low, Middle dice) |
| `TokenSnapshot` | One token (die color, resource side, face) |
| `DicePlaceholderSnapshot` | One placement slot (tokens, placed dice, card) |
| `CastleSnapshot` | Full castle grid |
| `WellSnapshot` | Well slot |
| `OutsideSnapshot` | Both outside slots |
| `TopFloorSlotSnapshot` / `TopFloorRoomSnapshot` | Top floor card and slots |
| `RoomCardSnapshot` / `CardFieldSnapshot` / `CardGainItemSnapshot` / `CardCostItemSnapshot` | Room card fields |
| `TgAreaSnapshot` / `TrainingGroundsSnapshot` | Training grounds areas |
| `FarmFieldSnapshot` / `FarmingLandsSnapshot` | Farming lands grid |
| `PersonalDomainRowSnapshot` / `PersonalDomainSpotSnapshot` | Personal domain rows |
| `LanternChainItemSnapshot` / `LanternChainGainSnapshot` | Lantern chain |
| `SeedPairSnapshot` / `SeedActionCardSnapshot` / `SeedResourceCardSnapshot` / `SeedResourceGainSnapshot` | Seed card selection |
| `PlayerScore` | Final scoring result per player |

---

## Engine/

### `IGameEngine` (interface)
- Public engine contract
- Exposes: `GameId`, `IsGameOver`, `GetCurrentState()`, `ProcessAction()`, `GetLegalActions()`, `GetFinalScores()`, `PlayAiTurn()`

### `ActionResult` (abstract record)
- Discriminated union for action outcomes:
  - `Success` — new state snapshot + domain events emitted
  - `Failure` — current state snapshot + reason string

### `GameEngine` (sealed class)
- Core engine implementation
- Owns: `GameState`, `CompositeActionHandler`, optional `IAiStrategy`, cached final scores
- Orchestrates: validate → apply → post-process for every action
- Captures final scores on `GameOver`; delegates AI turn selection to strategy

### `PlayerSetup` (sealed record)
- Configuration data for one player at game creation: name, color, AI flag, strategy ID

### `GameEngineFactory` (static class)
- Game initialization factory
- Enforces 2–4 player requirement
- Wires: `GameState` + full handler chain + AI strategy + `PersonalDomainRowConfig` preload

---

## Events/

### `IDomainEvent` (interface)
- Marker interface for all domain events
- Mandates: `GameId`, `OccurredAt`, `EventType`

All events are `public sealed record` and carry no state-mutation intent — consumed only by UI / tests:

| Event | Signals |
|---|---|
| `GameStartedEvent` | Game transitioned to SeedCardSelection |
| `DieTakenFromBridgeEvent` | Die removed from bridge (includes value) |
| `DiePlacedEvent` | Die placed at a target (includes coin delta) |
| `ResourcesCollectedEvent` | Resources gained |
| `LanternsGainedEvent` | Lantern VP gained |
| `PlayerPassedEvent` | Player passed their turn |
| `RoundEndedEvent` | Round ended (round number) |
| `GameOverEvent` | Game ended (includes final scores) |
| `WellEffectAppliedEvent` | Well token effects applied (seals, resources, coins, pending choices) |
| `AnyResourceChosenEvent` | AnyResource choice resolved |
| `CardFieldGainActivatedEvent` | Card gain field activated |
| `CardActionActivatedEvent` | Card action field activated |
| `TrainingGroundsUsedEvent` | Soldier placed (area, cost, gains, action) |
| `FarmerPlacedEvent` | Farmer placed (cost, gains, action) or skipped |
| `FarmEffectFiredEvent` | Farm end-of-round effects applied |
| `CastlePlayExecutedEvent` | Courtier placed or advanced |
| `TopFloorSlotFilledEvent` | Top floor slot occupied |
| `OutsideActivationChosenEvent` | Outside slot activation choice resolved |
| `PersonalDomainActivatedEvent` | Personal domain row activated with gains |
| `SeedPairChosenEvent` | Seed pair selected with initial resources |
| `SeedCardActivatedEvent` | Seed action card triggered on row placement |
| `LanternChainItemAddedEvent` | Entry added to lantern chain |
| `LanternChainActivatedEvent` | Lantern chain fired with results |
| `LanternEffectFiredEvent` | Single lantern effect triggered |
| `RoomCardAcquiredEvent` | Castle room card acquired |
| `PersonalDomainCardFieldActivatedEvent` | Personal domain card field activated |
| `InfluenceGainPendingEvent` | Influence threshold payment decision created |
| `InfluenceGainResolvedEvent` | Influence gain accepted or declined |
| `CastleCardFieldChosenEvent` | Castle card field chosen with gains/costs |
| `PersonalDomainRowChosenEvent` | Free personal domain row activation chosen |
| `NewCardFieldChosenEvent` | Newly acquired card field activated |

---

## Rules/

### `ValidationResult` (sealed record)
- Carries validation outcome: `IsValid` + `Reason` string
- Factory methods: `Ok()` and `Fail(reason)`

### `IActionHandler` (interface)
- Handler contract: `CanHandle(action)`, `Validate(action, state)`, `Apply(action, state, events)`

### `CompositeActionHandler` (sealed class)
- Chain-of-responsibility dispatcher
- Routes validation and apply to the first handler that claims the action type
- Returns failure if no handler handles the action

### `LegalActionGenerator` (static class)
- Computes all legal moves for the current game state
- Enforces: only generates moves for the active player in the correct phase
- Enforces: placement color matching (castle rooms), capacity limits, resource affordability, courtier/soldier/farmer availability, pending-state priority (only shows actions that resolve pending state while any is present)

### `PostActionProcessor` (static class)
- Orchestrates turn / round / game-end transitions after every action
- Delegates to `TurnAdvancePolicy.HasPendingState` to decide whether to hold the turn
- Delegates to `RoundEndProcessor.Execute` when `TotalDiceRemaining ≤ 3`
- Handles `SeedCardSelection` phase: holds for AnyResource, advances players, transitions to `WorkerPlacement` when all seeds chosen

### `TurnAdvancePolicy` (static class)
- Pure predicate: `HasPendingState(Player)` returns true when `DiceInHand.Count > 0` or `Pending.HasAny`
- No side-effects; fully testable in isolation

### `RoundEndProcessor` (static class)
- Executes all round-end effects: emits `RoundEndedEvent`, fires farm end-of-round effects, clears placement areas, clears personal domain placed dice, advances round counter, selects next first player by influence + `InfluenceGainOrder` tiebreaker, re-rolls dice and refreshes training grounds tokens
- On final round: transitions to `GameOver` and emits `GameOverEvent` with final scores

### `StartGameHandler` (sealed class)
- Handles `StartGameAction`
- Enforces: only valid in Setup phase
- Delegates board initialisation to `BoardSetupService.Setup`
- Applies: draws seed card pairs, advances phase to `SeedCardSelection`

### `BoardSetupService` (static class)
- Encapsulates all board initialisation steps performed at game start: rolls dice, places tokens, deals room cards, sets up training grounds, farming lands, and top floor card
- Called by `StartGameHandler`; also usable directly in tests that need a fully initialised board

### `TakeDieFromBridgeHandler` (sealed class)
- Handles `TakeDieFromBridgeAction`
- Enforces: WorkerPlacement phase, active player, die present at requested position
- Applies: moves die to `DiceInHand`; fires lantern chain if Low position taken

### `PlaceDieAtCastleHandler` (sealed class)
- Handles `PlaceDieAction` where `Target is CastleRoomTarget`
- Enforces: WorkerPlacement phase, active player, die in hand, token color matching, room capacity
- Applies: places die, triggers room card gain/action fields via `CardGainApplier` / `CardActionApplier`

### `PlaceDieAtWellHandler` (sealed class)
- Handles `PlaceDieAction` where `Target is WellTarget`
- Applies: places die, flips tokens, grants seals/resources/coins, sets `PendingAnyResourceChoices`

### `PlaceDieAtOutsideHandler` (sealed class)
- Handles `PlaceDieAction` where `Target is OutsideSlotTarget`
- Enforces: coin affordability
- Applies: deducts coins, places die, sets `PendingOutsideActivationSlot`

### `PlaceDieAtPersonalDomainHandler` (sealed class)
- Handles `PlaceDieAction` where `Target is PersonalDomainTarget`
- Enforces: row not already used, die color matches row, compare value or coin payment
- Applies: row gains, spot gains, seed action trigger, card field choices

### `ChooseResourceHandler` (sealed class)
- Handles `ChooseResourceAction`
- Enforces: active player, at least one pending choice
- Applies: decrements `PendingAnyResourceChoices`, adds chosen resource (clamped to 7)

### `PassHandler` (sealed class)
- Handles `PassAction`
- Enforces: WorkerPlacement phase, active player
- Applies: emits `PlayerPassedEvent` (turn advance handled by `PostActionProcessor`)

### `ChooseInfluencePayHandler` (sealed class)
- Handles `ChooseInfluencePayAction`
- Enforces: active player, pending influence gain exists, seal affordability if paying
- Applies: deducts seals + grants influence + records `InfluenceGainOrder` (if paying), or clears pending without gain (if declining)

### `OutsideActivationHandler` (sealed class)
- Handles `ChooseOutsideActivationAction`
- Enforces: active player, pending slot activation exists, choice valid for that slot (slot 0: Farm/Castle; slot 1: TrainingGrounds/Castle)
- Applies: sets appropriate pending action flags (`PendingFarmActions`, `CastlePlaceRemaining`/`CastleAdvanceRemaining`, `PendingTrainingGroundsActions`)

### `TrainingGroundsHandler` (sealed class)
- Handles `TrainingGroundsPlaceSoldierAction` and `TrainingGroundsSkipAction`
- Enforces: active player, pending action exists, soldier available, iron affordable
- Applies: deducts iron, places soldier in area, applies resource gains and action side effects, decrements pending count

### `FarmHandler` (sealed class)
- Handles `PlaceFarmerAction` and `FarmSkipAction`
- Enforces: active player, pending action exists, farmer available, food affordable, no duplicate farmer per field
- Applies: deducts food, places farmer, applies card gains/action effects, decrements pending count
- Exposes `ApplyCardEffect` publicly for use by `PostActionProcessor` at round end

### `CastlePlaceCourtierHandler` (sealed class)
- Handles `CastlePlaceCourtierAction`
- Enforces: active player, pending place remaining > 0, 2 coin cost, courtier available
- Applies: deducts coins, moves courtier from pool to gate, decrements pending count

### `CastleAdvanceCourtierHandler` (sealed class)
- Handles `CastleAdvanceCourtierAction`
- Enforces: active player, pending advance remaining > 0, MotherOfPearls cost, valid from-position and level, room index for ground/mid floors
- Applies: deducts MoP, advances courtier, acquires room cards and adds them to lantern chain, claims top floor slots

### `CastleSkipHandler` (sealed class)
- Handles `CastleSkipAction`
- Enforces: active player, at least one pending castle action (place or advance)
- Applies: clears all remaining `CastlePlaceRemaining` and `CastleAdvanceRemaining` pending counts

### `ChooseSeedPairHandler` (sealed class)
- Handles `ChooseSeedPairAction`
- Enforces: SeedCardSelection phase, active player, valid pair index, not already chosen, no pending resource choices
- Applies: assigns seed action card, applies resource gains, sets pending AnyResource choices, adds resource seed card back to lantern chain, optionally adds decree card

### `ChooseNewCardFieldHandler` (sealed class)
- Handles `ChooseNewCardFieldAction`
- Enforces: active player, pending new card exists, valid field index, cost affordable for action fields
- Applies: clears pending, adds card to personal domain, applies chosen field (gain or action), or skips

### `ChooseCastleCardFieldHandler` (sealed class)
- Handles `ChooseCastleCardFieldAction`
- Enforces: active player, pending filter exists, valid target floor/room/field, token color matches filter (or `"GainOnly"` restricts to gain fields only), cost affordable
- Applies: clears pending filter, applies chosen field or skips

### `ChoosePersonalDomainRowHandler` (sealed class)
- Handles `ChoosePersonalDomainRowAction`
- Enforces: active player, pending row choice exists, row color valid
- Applies: clears pending, applies row default gain + spot gains, activates personal domain card fields for that row

### `CardGainApplier` (static class)
- Applies `GainCardField` effects to a player: maps every `CardGainType` to a player-state mutation and domain event
- Covers: resources, coins, seals, lanterns, VP, influence, well token, castle card field filter, personal domain row choice

### `CardActionApplier` (static class)
- Parses action description strings from `ActionCardField` and sets the corresponding pending state on a player
- Covers: Play Castle, Play Farm, Play TrainingGrounds, color-filtered castle, personal domain row

### `LanternHelper` (static class)
- Lantern gain and chain firing logic
- `Apply`: adds VP and optionally fires the full lantern chain
- `Trigger`: fires the chain without adding VP
- `FireChain`: collects all gains from chain items and applies resources / coins / seals / VP (influence is NOT fired from chain)

### `InfluenceHelper` (static class)
- Influence gain and threshold management
- `Apply`: checks if gain crosses thresholds at 5 / 10 / 15; sets pending state if seal cost required, or grants directly if seals not needed
- `SealCost`: calculates total seal cost for crossing one or more thresholds

### `ScoreCalculator` (static class)
- Final game score calculation
- `Calculate`: scores all players, sorts by total descending
- `CalcPlayer`: applies all scoring formulas:
  - Courtiers: 1 / 3 / 6 / 10 points by position (Gate / Steward / Diplomat / Top)
  - Lanterns: direct from `LanternScore`
  - Coins: 1 pt per 5 coins
  - Seals: 1 pt per 5 seals
  - Resources: 1 pt per 4 of same type, 2 pts at 7
  - Farming: victory points from farm cards per farmer owner
  - Training grounds: soldiers × courtiers × area multiplier
  - Influence: lookup table by influence level

---

## SRP Analysis — Post-Refactor Assessment

All planned splits have been implemented. The following records what was done and the current state of every class that was analysed.

---

### ✅ `PlaceDieHandler` → 4 target-specific handlers *(implemented)*

**Was**: one class handling all four `PlaceDieAction` target types (Castle, Well, Outside, PersonalDomain) — ~400 LOC with four independent validate/apply branches.

**Now**: `PlaceDieAtCastleHandler`, `PlaceDieAtWellHandler`, `PlaceDieAtOutsideHandler`, `PlaceDieAtPersonalDomainHandler`. Each has one responsibility. `CanHandle` checks both action type and target type. `CompositeActionHandler` registration order: Castle → Well → Outside → PersonalDomain.

---

### ✅ `CardFieldHelper` → `CardGainApplier` + `CardActionApplier` *(implemented)*

**Was**: one static class with two unrelated methods — `ApplyGainField` (enum-driven) and `ApplyActionDescription` (string-parsing).

**Now**: `CardGainApplier.ApplyGain` (all `CardGainType` cases) and `CardActionApplier.ApplyAction` (all action description strings). Both remain `internal static`. Callers updated: `ChooseCastleCardFieldHandler`, `ChooseNewCardFieldHandler`, `TrainingGroundsHandler`, `FarmHandler`, `ChoosePersonalDomainRowHandler`.

---

### ✅ `CastlePlayHandler` → 3 action-specific handlers *(implemented)*

**Was**: one class handling three structurally different actions (`CastlePlaceCourtierAction`, `CastleAdvanceCourtierAction`, `CastleSkipAction`).

**Now**: `CastlePlaceCourtierHandler`, `CastleAdvanceCourtierHandler`, `CastleSkipHandler`. Each handles exactly one action type with its own validate/apply logic.

---

### ✅ `PostActionProcessor` — `TurnAdvancePolicy` + `RoundEndProcessor` extracted *(implemented)*

**Was**: one `static Process` method mixing turn-advance logic, round-end effects, and game-end detection.

**Now**: `TurnAdvancePolicy.HasPendingState(Player)` is a pure predicate (no side-effects, fully testable). `RoundEndProcessor.Execute` handles all round-end mutations. `PostActionProcessor` is a thin orchestrator.

---

### ✅ `StartGameHandler` — `BoardSetupService` extracted *(implemented)*

**Was**: handler directly performed all 7 board setup steps (dice, tokens, cards, training grounds, farming, top floor) plus phase advance.

**Now**: `BoardSetupService.Setup(board, players, rng)` owns all board setup. `StartGameHandler` validates, calls `BoardSetupService`, draws seed pairs, and advances phase.

---

### ✅ `Player` — `PlayerPendingState` extracted *(implemented)*

**Was**: 9 pending-state fields mixed into the permanent player state bag.

**Now**: `Player.Pending` holds a `PlayerPendingState` instance with all 9 flags and a `HasAny` boolean. Handlers set `player.Pending.Xxx`; `TurnAdvancePolicy` checks `player.Pending.HasAny`. Adding a new interaction flow requires touching only `PlayerPendingState` + the relevant handler.

---

### 🟢 `LegalActionGenerator` — no split performed

Each pending-state branch is a self-contained `if` block. The class is the natural single place to query "what can the active player do?". Extracting a `PendingActionGenerator` would add an indirection with no testability gain (it would still need all the same state). **Left as-is.**

---

### 🟢 `ScoreCalculator` — no split performed

All scoring categories answer one question ("how many points?") and change together when scoring rules change. Single reason to change. **No split needed.**

---

### Summary

| Class | Action taken | Result |
|---|---|---|
| `PlaceDieHandler` | ✅ Split into 4 handlers | `PlaceDieAtCastleHandler`, `PlaceDieAtWellHandler`, `PlaceDieAtOutsideHandler`, `PlaceDieAtPersonalDomainHandler` |
| `CardFieldHelper` | ✅ Split into 2 helpers | `CardGainApplier`, `CardActionApplier` |
| `CastlePlayHandler` | ✅ Split into 3 handlers | `CastlePlaceCourtierHandler`, `CastleAdvanceCourtierHandler`, `CastleSkipHandler` |
| `PostActionProcessor` | ✅ Extracted 2 classes | `TurnAdvancePolicy`, `RoundEndProcessor` |
| `StartGameHandler` | ✅ Extracted 1 class | `BoardSetupService` |
| `Player` | ✅ Extracted 1 class | `PlayerPendingState` |
| `LegalActionGenerator` | 🟢 No change | Single responsibility: compute legal moves for the current state |
| `ScoreCalculator` | 🟢 No change | Single responsibility: calculate final scores |
