# BoardWC.Engine — Public API Reference

This document describes every type, method, and property that crosses the `BoardWC.Engine`
library boundary. Domain internals (board model, handlers, rules) are `internal` and not
part of this contract.

---

## Quick-start

```csharp
using BoardWC.Engine.Actions;
using BoardWC.Engine.Domain;
using BoardWC.Engine.Engine;
using BoardWC.Engine.Events;

// 1. Create and start the engine
IGameEngine engine = GameEngineFactory.Create(
[
    new PlayerSetup("Alice", PlayerColor.White, IsAI: false),
    new PlayerSetup("Bob",   PlayerColor.Black, IsAI: false),
]);

ActionResult startResult = engine.ProcessAction(new StartGameAction());
// startResult is ActionResult.Success; use startResult.NewState / startResult.Events

// 2. Each round: ask for legal moves, pick one, submit it
GameStateSnapshot state = engine.GetCurrentState();
Guid activePlayerId = state.Players[state.ActivePlayerIndex].Id;

IReadOnlyList<IGameAction> legal = engine.GetLegalActions(activePlayerId);
IGameAction chosen = legal[0];   // pick by game logic, AI, or UI

ActionResult result = engine.ProcessAction(chosen);

if (result is ActionResult.Success ok)
{
    foreach (IDomainEvent evt in ok.Events)
        Console.WriteLine(evt.EventType);
}
else if (result is ActionResult.Failure fail)
{
    Console.WriteLine($"Rejected: {fail.Reason}");
}

// 3. After game over
if (engine.IsGameOver)
{
    foreach (PlayerScore score in engine.GetFinalScores()!)
        Console.WriteLine($"{score.PlayerName}: {score.Total}");
}
```

---

## Entry Point — `GameEngineFactory`

**Namespace:** `BoardWC.Engine.Engine`

### `PlayerSetup`

```csharp
public sealed record PlayerSetup(
    string Name,
    PlayerColor Color,
    bool IsAI,
    string? AiStrategyId = null
);
```

### `GameEngineFactory.Create`

```csharp
public static IGameEngine Create(
    IReadOnlyList<PlayerSetup> players,
    IAiStrategy? aiStrategy = null,
    int maxRounds = 3
)
```

| Parameter | Notes |
|---|---|
| `players` | 2–4 entries required; throws `ArgumentException` otherwise |
| `aiStrategy` | Optional strategy used for AI players (`IsAI = true`) |
| `maxRounds` | Number of worker-placement rounds before scoring (default 3) |

---

## `IGameEngine`

**Namespace:** `BoardWC.Engine.Engine`

```csharp
public interface IGameEngine
{
    Guid GameId { get; }
    bool IsGameOver { get; }
    GameStateSnapshot GetCurrentState();
    ActionResult ProcessAction(IGameAction action);
    IReadOnlyList<IGameAction> GetLegalActions(Guid playerId);
    IReadOnlyList<PlayerScore>? GetFinalScores();
}
```

| Member | Description |
|---|---|
| `GameId` | Stable `Guid` that identifies this session; also present on every event |
| `IsGameOver` | `true` once `CurrentPhase == Phase.GameOver` |
| `GetCurrentState()` | Returns a full immutable snapshot; does not advance state |
| `ProcessAction(action)` | Validates and applies the action. Returns `ActionResult.Success` or `ActionResult.Failure` |
| `GetLegalActions(playerId)` | All moves the player may legally submit right now; empty when it is not their turn |
| `GetFinalScores()` | `null` until the game ends; non-null list of `PlayerScore` afterwards |

---

## `ActionResult`

**Namespace:** `BoardWC.Engine.Engine`

Discriminated union — pattern-match to distinguish success from failure.

```csharp
public abstract record ActionResult
{
    public sealed record Success(
        GameStateSnapshot NewState,
        IReadOnlyList<IDomainEvent> Events
    ) : ActionResult;

    public sealed record Failure(
        GameStateSnapshot CurrentState,
        string Reason
    ) : ActionResult;
}
```

| Subtype | Properties |
|---|---|
| `Success` | `NewState` — snapshot after the action; `Events` — ordered list of domain events fired |
| `Failure` | `CurrentState` — unchanged snapshot; `Reason` — human-readable rejection message |

---

## Actions

**Namespace:** `BoardWC.Engine.Actions`

All actions implement `IGameAction` (a marker interface exposing only `Guid PlayerId`).
Submit them via `IGameEngine.ProcessAction`. Use `GetLegalActions` to enumerate valid choices
rather than constructing actions directly when the full list is needed.

### Phase: startup

| Action | Parameters | When to send |
|---|---|---|
| `StartGameAction()` | *(none — `PlayerId` is `Guid.Empty`)* | Once after `Create`, while `Phase == Setup` |
| `ChooseSeedPairAction(PlayerId, PairIndex)` | `PairIndex` — 0-based index into `GameStateSnapshot.SeedPairs` | During `Phase.SeedCardSelection`, once per player in turn order |

### Phase: WorkerPlacement — main loop

| Action | Parameters | When to send |
|---|---|---|
| `TakeDieFromBridgeAction(PlayerId, BridgeColor, DiePosition)` | `BridgeColor` — Red/Black/White; `DiePosition` — High/Low | Start of a player's turn; legal while the player has no die in hand |
| `PlaceDieAction(PlayerId, Target)` | `Target` — see PlacementTarget section | After taking a die; the only legal action while a die is in hand |
| `PassAction(PlayerId)` | — | End the current turn; only legal when no pending sub-actions remain |

### Sub-actions (pending state must be resolved before Pass)

| Action | Parameters | Triggered by |
|---|---|---|
| `ChooseResourceAction(PlayerId, Choice)` | `Choice` — `ResourceType` | Placing a die at the Well when a token with `AnyResource` is face-up |
| `ChooseInfluencePayAction(PlayerId, WillPay)` | `WillPay` — `bool` | Influence gain that crosses a Monarchial Seal threshold (5/10/15) |
| `ChooseOutsideActivationAction(PlayerId, Choice)` | `Choice` — `OutsideActivation` | Placing a die in an outside slot |
| `CastlePlaceCourtierAction(PlayerId)` | — | Pending castle-place action (costs 2 coins) |
| `CastleAdvanceCourtierAction(PlayerId, From, Levels, RoomIndex)` | `From` — `CourtierPosition`; `Levels` — 1 or 2; `RoomIndex` — 0-based room to enter (−1 for top floor) | Pending castle-advance action |
| `CastleSkipAction(PlayerId)` | — | Skip remaining castle place/advance options |
| `TrainingGroundsPlaceSoldierAction(PlayerId, AreaIndex)` | `AreaIndex` — 0/1/2 | Pending training-grounds action (costs iron) |
| `TrainingGroundsSkipAction(PlayerId)` | — | Skip training-grounds placement |
| `PlaceFarmerAction(PlayerId, BridgeColor, IsInland)` | `BridgeColor`, `IsInland` | Pending farm action (costs food) |
| `FarmSkipAction(PlayerId)` | — | Skip farm placement |

---

## PlacementTarget

**Namespace:** `BoardWC.Engine.Domain`

Abstract base for the `Target` parameter of `PlaceDieAction`.

| Subtype | Parameters | Description |
|---|---|---|
| `CastleRoomTarget(Floor, RoomIndex)` | `Floor` 0 = Ground (3 rooms), 1 = Mid (2 rooms); `RoomIndex` 0-based | A room in the main castle. The die's color must match a face-up token in that room |
| `WellTarget` | — | The well; unlimited capacity, compare value always 1 |
| `OutsideSlotTarget(SlotIndex)` | `SlotIndex` 0 or 1 | One of the two outside slots; compare value 5 |
| `PersonalDomainTarget(RowIndex)` | `RowIndex` 0–2 | A row on the active player's personal domain board |

---

## Enums

**Namespace:** `BoardWC.Engine.Domain`

| Enum | Values |
|---|---|
| `Phase` | `Setup`, `SeedCardSelection`, `WorkerPlacement`, `EndOfRound`, `GameOver` |
| `PlayerColor` | `White`, `Black`, `Red`, `Blue` |
| `BridgeColor` | `Red`, `Black`, `White` |
| `DiePosition` | `High`, `Low` |
| `ResourceType` | `Food`, `Iron`, `ValueItem` |
| `TokenResource` | `Food`, `Iron`, `ValueItem`, `AnyResource`, `Coin` |

**Namespace:** `BoardWC.Engine.Actions`

| Enum | Values | Used by |
|---|---|---|
| `CourtierPosition` | `Gate`, `GroundFloor`, `MidFloor` | `CastleAdvanceCourtierAction.From` |
| `OutsideActivation` | `Farm`, `Castle`, `TrainingGrounds` | `ChooseOutsideActivationAction.Choice` |

---

## `ResourceBag`

**Namespace:** `BoardWC.Engine.Domain`

Immutable value type representing the three resource currencies.

```csharp
public readonly record struct ResourceBag(int Food = 0, int Iron = 0, int ValueItem = 0)
```

| Member | Description |
|---|---|
| `ResourceBag.Empty` | Static zero bag |
| `Food`, `Iron`, `ValueItem` | Individual resource counts |
| `Total` | `Food + Iron + ValueItem` |
| `Add(ResourceType, int)` | Returns new bag with one resource increased |
| `Add(ResourceBag)` | Returns element-wise sum |
| `Subtract(ResourceBag)` | Returns element-wise difference (no floor — callers must validate first) |
| `CanAfford(ResourceBag cost)` | `true` if all three resources ≥ cost |
| `Clamp(int max)` | Returns bag with each resource capped at `max` |
| `operator +` | Alias for `Add(ResourceBag)` |

---

## State Snapshots

**Namespace:** `BoardWC.Engine.Domain`

All snapshots are `public sealed record` types — immutable, structurally equal, freely
serialisable. They are the **only** data that crosses the engine boundary.

---

### `GameStateSnapshot`

Top-level snapshot returned by `GetCurrentState()` and `ActionResult.Success.NewState`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | Matches `IGameEngine.GameId` |
| `CurrentPhase` | `Phase` | Current phase of the game |
| `CurrentRound` | `int` | 1-based round counter |
| `MaxRounds` | `int` | Total rounds (default 3) |
| `ActivePlayerIndex` | `int` | Index into `Players` whose turn it is |
| `Players` | `IReadOnlyList<PlayerSnapshot>` | One entry per player, same order as setup |
| `Board` | `BoardSnapshot` | Full board state |
| `SeedPairs` | `IReadOnlyList<SeedPairSnapshot>` | Available seed card pairs (populated during `SeedCardSelection`) |

---

### `PlayerSnapshot`

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique player identifier |
| `Name` | `string` | Display name |
| `Color` | `PlayerColor` | Player colour token |
| `IsAI` | `bool` | Whether this player is controlled by the AI strategy |
| `Resources` | `ResourceBag` | Food / Iron / ValueItem (max 7 each) |
| `LanternScore` | `int` | Victory points from lanterns |
| `Influence` | `int` | Current influence level |
| `Coins` | `int` | Coin count |
| `MonarchialSeals` | `int` | Seal count (max 5) |
| `SoldiersAvailable` | `int` | Soldiers not yet placed on the board (0–5) |
| `CourtiersAvailable` | `int` | Courtiers not yet placed in the castle (0–5) |
| `FarmersAvailable` | `int` | Farmers not yet placed on farming lands (0–5) |
| `PendingAnyResourceChoices` | `int` | Number of unresolved `ChooseResourceAction` prompts |
| `PendingTrainingGroundsActions` | `int` | 1 if a training-grounds sub-action must be resolved |
| `PendingFarmActions` | `int` | 1 if a farm sub-action must be resolved |
| `CastlePlaceRemaining` | `int` | 1 if the player may still place a courtier at the gate |
| `CastleAdvanceRemaining` | `int` | 1 if the player may still advance a courtier |
| `PendingOutsideActivationSlot` | `int` | Slot index (0/1) of an unresolved outside-activation choice; −1 otherwise |
| `PendingInfluenceGain` | `int` | Influence amount pending acceptance; 0 if none |
| `PendingInfluenceSealCost` | `int` | Seal cost required to accept the pending influence gain |
| `CourtiersAtGate` | `int` | Courtiers currently at the castle gate |
| `CourtiersOnGroundFloor` | `int` | Courtiers on the ground floor |
| `CourtiersOnMidFloor` | `int` | Courtiers on the mid floor |
| `CourtiersOnTopFloor` | `int` | Courtiers on the top floor |
| `DiceInHand` | `IReadOnlyList<DieSnapshot>` | Die taken from bridge awaiting placement (usually 0 or 1) |
| `PersonalDomainRows` | `IReadOnlyList<PersonalDomainRowSnapshot>` | The 3 rows of the personal domain board |
| `SeedCard` | `SeedActionCardSnapshot?` | The seed action card chosen at game start; `null` before selection |
| `LanternChain` | `IReadOnlyList<LanternChainItemSnapshot>` | Lantern-chain entries accumulated during play |
| `PersonalDomainCards` | `IReadOnlyList<RoomCardSnapshot>` | Room cards acquired by advancing courtiers |

---

### `BoardSnapshot`

| Property | Type | Description |
|---|---|---|
| `Bridges` | `IReadOnlyList<BridgeSnapshot>` | The three bridges (Red, Black, White) |
| `Castle` | `CastleSnapshot` | Ground floor, mid floor, and top floor state |
| `Well` | `WellSnapshot` | The well placement area |
| `Outside` | `OutsideSnapshot` | The two outside slots |
| `GroundFloorDeckRemaining` | `int` | Cards still in the ground-floor draw deck |
| `MidFloorDeckRemaining` | `int` | Cards still in the mid-floor draw deck |
| `TrainingGrounds` | `TrainingGroundsSnapshot` | The three training-grounds areas |
| `FarmingLands` | `FarmingLandsSnapshot` | All farming-land fields |
| `TotalDiceRemaining` | `int` | Computed sum of all dice on all bridges (triggers round-end at ≤ 3) |

---

### `DieSnapshot`

| Property | Type | Description |
|---|---|---|
| `Value` | `int` | Face value (1–6) |
| `Color` | `BridgeColor` | The bridge colour the die originally came from |

---

### `BridgeSnapshot`

| Property | Type | Description |
|---|---|---|
| `Color` | `BridgeColor` | Which bridge |
| `High` | `DieSnapshot?` | Die in the High position; `null` if taken |
| `Middle` | `IReadOnlyList<DieSnapshot>` | Dice in middle positions (0–3, varying by player count) |
| `Low` | `DieSnapshot?` | Die in the Low position; `null` if taken |

---

### `DicePlaceholderSnapshot`

Represents any placement slot that can hold one or more dice and carries tokens.

| Property | Type | Description |
|---|---|---|
| `BaseValue` | `int` | The compare value required to place a die here (1 = well, 5 = outside) |
| `UnlimitedCapacity` | `bool` | `true` for the well (accepts any number of dice) |
| `PlacedDice` | `IReadOnlyList<DieSnapshot>` | Dice currently placed here |
| `Tokens` | `IReadOnlyList<TokenSnapshot>` | Resource/action tokens on this slot |
| `Card` | `RoomCardSnapshot?` | Room card attached to this slot; `null` for the well and outside |

---

### `TokenSnapshot`

| Property | Type | Description |
|---|---|---|
| `DieColor` | `BridgeColor` | The die colour that activates this token |
| `ResourceSide` | `TokenResource` | The resource (or special) on the token's resource face |
| `IsResourceSideUp` | `bool` | `true` when the resource face is active; `false` when the die face is active |

---

### `CastleSnapshot`

| Property | Type | Description |
|---|---|---|
| `Floors` | `IReadOnlyList<IReadOnlyList<DicePlaceholderSnapshot>>` | `Floors[0]` = Ground (3 rooms), `Floors[1]` = Mid (2 rooms) |
| `TopFloor` | `TopFloorRoomSnapshot` | The single top-floor courtier room |

---

### `TopFloorRoomSnapshot`

| Property | Type | Description |
|---|---|---|
| `CardId` | `string` | Identifier of the top-floor room card |
| `Slots` | `IReadOnlyList<TopFloorSlotSnapshot>` | Courtier slots (typically 4) |

---

### `TopFloorSlotSnapshot`

| Property | Type | Description |
|---|---|---|
| `SlotIndex` | `int` | 0-based position within the top floor |
| `Gains` | `IReadOnlyList<CardGainItemSnapshot>` | Rewards granted when a courtier reaches this slot |
| `OccupantName` | `string?` | Player name occupying the slot; `null` if empty |

---

### `WellSnapshot`

| Property | Type | Description |
|---|---|---|
| `Placeholder` | `DicePlaceholderSnapshot` | The well's placement state (tokens, placed dice) |

---

### `OutsideSnapshot`

| Property | Type | Description |
|---|---|---|
| `Slots` | `IReadOnlyList<DicePlaceholderSnapshot>` | The two outside placement slots (index 0 and 1) |

---

### `RoomCardSnapshot`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Unique card identifier from the data file |
| `Name` | `string` | Display name |
| `Fields` | `IReadOnlyList<CardFieldSnapshot>` | Ordered fields (corresponds to token positions) |
| `Layout` | `string?` | `null` for ground-floor cards; `"DoubleTop"` or `"DoubleBottom"` for mid-floor |

---

### `CardFieldSnapshot`

Exactly one of `Gains` or `ActionDescription` is non-null depending on `IsGain`.

| Property | Type | Description |
|---|---|---|
| `IsGain` | `bool` | `true` = passive gain field; `false` = active action field |
| `Gains` | `IReadOnlyList<CardGainItemSnapshot>?` | Rewards granted automatically (non-null when `IsGain` is `true`) |
| `ActionDescription` | `string?` | Human-readable action text (non-null when `IsGain` is `false`) |
| `ActionCost` | `IReadOnlyList<CardCostItemSnapshot>?` | Costs to activate the action; may be empty list but non-null when `IsGain` is `false` |

---

### `CardGainItemSnapshot`

| Property | Type | Description |
|---|---|---|
| `GainType` | `string` | Type name: `"Food"`, `"Iron"`, `"ValueItem"`, `"Coin"`, `"MonarchialSeal"`, `"Lantern"`, `"AnyResource"`, `"Influence"`, `"VictoryPoint"` |
| `Amount` | `int` | Quantity of the gain |

---

### `CardCostItemSnapshot`

| Property | Type | Description |
|---|---|---|
| `CostType` | `string` | Type name: `"Coin"` or `"MonarchialSeal"` |
| `Amount` | `int` | Quantity required |

---

### `TrainingGroundsSnapshot`

| Property | Type | Description |
|---|---|---|
| `Areas` | `IReadOnlyList<TgAreaSnapshot>` | The three training-grounds areas (index 0–2) |

---

### `TgAreaSnapshot`

| Property | Type | Description |
|---|---|---|
| `AreaIndex` | `int` | 0, 1, or 2 |
| `IronCost` | `int` | Iron required to place a soldier here |
| `ResourceGain` | `IReadOnlyList<CardGainItemSnapshot>` | Resources granted when a soldier is placed |
| `ActionDescription` | `string` | Text of the action side (may be empty string for gain-only areas) |
| `SoldierOwners` | `IReadOnlyList<string>` | Player names of soldiers currently placed here |

---

### `FarmingLandsSnapshot`

| Property | Type | Description |
|---|---|---|
| `Fields` | `IReadOnlyList<FarmFieldSnapshot>` | All farm fields across all bridges |

---

### `FarmFieldSnapshot`

| Property | Type | Description |
|---|---|---|
| `BridgeColor` | `BridgeColor` | Which bridge this field belongs to |
| `IsInland` | `bool` | `true` = inland field; `false` = outside/coastal field |
| `FoodCost` | `int` | Food required to place a farmer here |
| `GainItems` | `IReadOnlyList<CardGainItemSnapshot>` | Resources granted when a farmer is placed |
| `ActionDescription` | `string` | Text of any action side |
| `VictoryPoints` | `int` | End-game VP contributed by farmers on this field |
| `FarmerOwners` | `IReadOnlyList<string>` | Player names of farmers currently placed here |

---

### `PersonalDomainRowSnapshot`

| Property | Type | Description |
|---|---|---|
| `DieColor` | `BridgeColor` | The die colour that activates this row |
| `CompareValue` | `int` | Die value must equal or exceed this to place in the row |
| `FigureType` | `string` | The personnel figure type for this row (e.g., `"Soldier"`, `"Courtier"`, `"Farmer"`) |
| `DefaultGainType` | `ResourceType` | The resource gained by default when a die is placed |
| `DefaultGainAmount` | `int` | Amount of the default gain |
| `Spots` | `IReadOnlyList<PersonalDomainSpotSnapshot>` | The uncoverable bonus spots |
| `PlacedDie` | `DieSnapshot?` | Die currently placed in this row; `null` if empty |

---

### `PersonalDomainSpotSnapshot`

| Property | Type | Description |
|---|---|---|
| `GainType` | `ResourceType` | Resource granted when this spot is uncovered |
| `GainAmount` | `int` | Amount granted |
| `IsUncovered` | `bool` | `true` after the corresponding personnel figure enters the castle/board |

---

### `LanternChainItemSnapshot`

One entry per room card acquired by advancing courtiers. The full chain fires when a lantern effect triggers.

| Property | Type | Description |
|---|---|---|
| `SourceCardId` | `string` | The room card that contributed this entry |
| `SourceCardType` | `string` | Floor label: `"GroundFloor"` or `"MidFloor"` |
| `Gains` | `IReadOnlyList<LanternChainGainSnapshot>` | Gains yielded each time the chain fires |

---

### `LanternChainGainSnapshot`

| Property | Type | Description |
|---|---|---|
| `GainType` | `string` | Resource or currency type name (same set as `CardGainItemSnapshot.GainType`) |
| `Amount` | `int` | Quantity per activation |

---

### `SeedPairSnapshot`

| Property | Type | Description |
|---|---|---|
| `Action` | `SeedActionCardSnapshot` | The action card in this pair |
| `Resource` | `SeedResourceCardSnapshot` | The resource card in this pair |

---

### `SeedActionCardSnapshot`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Card identifier |
| `ActionType` | `string` | Describes which game action the seed card enables |

---

### `SeedResourceCardSnapshot`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Card identifier |
| `Gains` | `IReadOnlyList<SeedResourceGainSnapshot>` | Starting resources granted when this card is chosen |

(`SeedResourceGainSnapshot` has `GainType: string` and `Amount: int`.)

---

### `PlayerScore`

Returned only after `IsGameOver` is `true`.

| Property | Type | Description |
|---|---|---|
| `PlayerId` | `Guid` | Matches `PlayerSnapshot.Id` |
| `PlayerName` | `string` | Display name |
| `Total` | `int` | Sum of all scoring categories |
| `LanternPoints` | `int` | VP from lanterns |
| `CourtierPoints` | `int` | VP from courtier positions |
| `CoinPoints` | `int` | VP from coins |
| `SealPoints` | `int` | VP from Monarchial Seals |
| `ResourcePoints` | `int` | VP from remaining resources |
| `FarmPoints` | `int` | VP from farmers on farming lands |
| `TrainingGroundsPoints` | `int` | VP from soldiers in training grounds |
| `InfluencePoints` | `int` | VP from influence level |

---

## Domain Events

**Namespace:** `BoardWC.Engine.Events`

### `IDomainEvent`

```csharp
public interface IDomainEvent
{
    Guid GameId { get; }
    DateTimeOffset OccurredAt { get; }
    string EventType { get; }
}
```

`EventType` equals the concrete class name (e.g., `"GameStartedEvent"`), usable for
serialisation or logging without a full type switch.

All events are `public sealed record` types implementing `IDomainEvent`.

---

### Event Reference

#### `GameStartedEvent`
Fired by `StartGameAction`.

| Property | Type |
|---|---|
| `GameId` | `Guid` |

---

#### `DieTakenFromBridgeEvent`
Fired by `TakeDieFromBridgeAction`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `BridgeColor` | `BridgeColor` | Which bridge the die was taken from |
| `Position` | `DiePosition` | High or Low |
| `DieValue` | `int` | Face value of the taken die |

---

#### `DiePlacedEvent`
Fired by `PlaceDieAction`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `Target` | `PlacementTarget` | Where the die was placed |
| `DieValue` | `int` | Face value |
| `CoinDelta` | `int` | Positive = coins earned; negative = coins spent |

---

#### `ResourcesCollectedEvent`
Fired when a placement yields resources.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `Gained` | `ResourceBag` |

---

#### `LanternsGainedEvent`

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `Amount` | `int` |

---

#### `LanternEffectFiredEvent`
Fired when a lantern effect triggers the lantern chain.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |

---

#### `WellEffectAppliedEvent`
Fired when a die placed at the well resolves its tokens.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `SealGained` | `int` | Seals granted by the well |
| `ResourcesGained` | `ResourceBag` | Resources from well tokens |
| `CoinsGained` | `int` | Coins from well tokens |
| `PendingChoices` | `int` | Number of AnyResource tokens requiring `ChooseResourceAction` |

---

#### `AnyResourceChosenEvent`
Fired by `ChooseResourceAction`.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `Choice` | `ResourceType` |

---

#### `CardFieldGainActivatedEvent`
Fired when a castle-room gain field activates.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `CardId` | `string` | Source card |
| `FieldIndex` | `int` | Field index on the card |
| `ResourcesGained` | `ResourceBag` | |
| `CoinsGained` | `int` | |
| `SealsGained` | `int` | |
| `LanternGained` | `int` | |
| `VpGained` | `int` | |
| `InfluenceGained` | `int` | |

---

#### `CardActionActivatedEvent`
Fired when a castle-room action field is triggered.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `CardId` | `string` | |
| `FieldIndex` | `int` | |
| `ActionDescription` | `string` | Human-readable description of what fired |

---

#### `PersonalDomainActivatedEvent`
Fired when a die is placed on a personal domain row.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `RowIndex` | `int` | Row that was activated (0–2) |
| `DieColor` | `BridgeColor` | |
| `UncoveredSpots` | `int` | Number of bonus spots uncovered by this placement |
| `ResourcesGained` | `ResourceBag` | Default gain + uncovered spot gains |

---

#### `PersonalDomainCardFieldActivatedEvent`
Fired when a field on a personally-owned room card activates.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `CardId` | `string` |
| `FieldIndex` | `int` |
| `ResourcesGained` | `ResourceBag` |
| `CoinsGained` | `int` |
| `SealsGained` | `int` |
| `LanternGained` | `int` |
| `VpGained` | `int` |
| `InfluenceGained` | `int` |

---

#### `RoomCardAcquiredEvent`
Fired when a courtier advances into a ground- or mid-floor room.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `CardId` | `string` | |
| `CardName` | `string` | |
| `Floor` | `int` | 0 = ground floor, 1 = mid floor |

---

#### `LanternChainItemAddedEvent`
Fired when a room card with a lantern-chain back is added to the player's chain.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `SourceCardId` | `string` | |
| `SourceCardType` | `string` | Floor label |
| `Gains` | `IReadOnlyList<(string GainType, int Amount)>` | Chain gains per activation |

---

#### `LanternChainActivatedEvent`
Fired once per lantern-chain activation (after all chain items fire).

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `Resources` | `ResourceBag` |
| `Coins` | `int` |
| `Seals` | `int` |
| `VpGained` | `int` |

---

#### `CastlePlayExecutedEvent`
Fired by any of the castle-play actions.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `PlacedAtGate` | `bool` | `true` if a courtier was placed at the gate this event |
| `AdvancedFrom` | `CourtierPosition?` | Starting position of the advanced courtier; `null` if no advance |
| `LevelsAdvanced` | `int` | 0 if only placement occurred, 1 or 2 for advance |

---

#### `TopFloorSlotFilledEvent`
Fired when a courtier reaches the top floor.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `SlotIndex` | `int` |
| `ResourcesGained` | `ResourceBag` |
| `CoinsGained` | `int` |
| `SealsGained` | `int` |
| `LanternGained` | `int` |

---

#### `OutsideActivationChosenEvent`
Fired by `ChooseOutsideActivationAction`.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `SlotIndex` | `int` |
| `Choice` | `OutsideActivation` |

---

#### `TrainingGroundsUsedEvent`
Fired by `TrainingGroundsPlaceSoldierAction` or `TrainingGroundsSkipAction`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `AreaIndex` | `int` | −1 if skipped |
| `IronSpent` | `int` | |
| `ResourcesGained` | `ResourceBag` | |
| `CoinsGained` | `int` | |
| `SealsGained` | `int` | |
| `LanternGained` | `int` | |
| `ActionTriggered` | `string?` | Description of any action side triggered; `null` if skipped or gain-only |

---

#### `FarmerPlacedEvent`
Fired by `PlaceFarmerAction` or `FarmSkipAction`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `BridgeColor` | `BridgeColor` | Ignored when skipped |
| `IsInland` | `bool` | Ignored when skipped |
| `AreaIndex` | `int` | −1 if skipped |
| `FoodSpent` | `int` | |
| `ResourcesGained` | `ResourceBag` | |
| `CoinsGained` | `int` | |
| `SealsGained` | `int` | |
| `LanternGained` | `int` | |
| `ActionTriggered` | `string?` | `null` if skipped or gain-only |

---

#### `FarmEffectFiredEvent`
Fired for each existing farmer that re-activates when a new farmer is placed on the same bridge.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `BridgeColor` | `BridgeColor` |
| `IsInland` | `bool` |
| `ResourcesGained` | `ResourceBag` |
| `CoinsGained` | `int` |
| `SealsGained` | `int` |
| `LanternGained` | `int` |
| `ActionTriggered` | `string?` |

---

#### `SeedPairChosenEvent`
Fired by `ChooseSeedPairAction`.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `ActionCardId` | `string` |
| `ActionType` | `string` |
| `ResourcesGained` | `ResourceBag` |
| `CoinsGained` | `int` |
| `SealsGained` | `int` |
| `PendingAnyChoices` | `int` |

---

#### `SeedCardActivatedEvent`
Fired when the seed action card triggers a personal-domain row.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `ActionCardId` | `string` |
| `ActionType` | `string` |
| `RowIndex` | `int` |

---

#### `InfluenceGainPendingEvent`
Fired when an influence gain crosses a threshold (5, 10, or 15) and a seal payment decision is required.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |
| `InfluenceGain` | `int` |
| `SealCost` | `int` |

---

#### `InfluenceGainResolvedEvent`
Fired by `ChooseInfluencePayAction`.

| Property | Type | Description |
|---|---|---|
| `GameId` | `Guid` | |
| `PlayerId` | `Guid` | |
| `InfluenceGain` | `int` | The influence accepted (0 if refused) |
| `SealsPaid` | `int` | Seals spent; 0 if refused |
| `Accepted` | `bool` | Whether the player paid the cost |

---

#### `PlayerPassedEvent`

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `PlayerId` | `Guid` |

---

#### `RoundEndedEvent`
Fired at the end of each placement round (triggered when `TotalDiceRemaining ≤ 3`).

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `RoundNumber` | `int` |

---

#### `GameOverEvent`
Fired once when the game transitions to `Phase.GameOver`.

| Property | Type |
|---|---|
| `GameId` | `Guid` |
| `FinalScores` | `IReadOnlyList<PlayerScore>` |

---

## Typical Turn Flow

```
Phase.Setup
  └─ ProcessAction(StartGameAction)
       → Phase.SeedCardSelection

Phase.SeedCardSelection
  └─ each player, in turn order:
       ProcessAction(ChooseSeedPairAction)
       → when all players have chosen: Phase.WorkerPlacement

Phase.WorkerPlacement  [repeats each round]
  └─ active player's turn:
       1. ProcessAction(TakeDieFromBridgeAction)
       2. ProcessAction(PlaceDieAction)
            └─ if placed at Well with AnyResource tokens:
                 repeat: ProcessAction(ChooseResourceAction) until PendingAnyResourceChoices == 0
            └─ if placed at OutsideSlot:
                 ProcessAction(ChooseOutsideActivationAction)
                   → Farm:           ProcessAction(PlaceFarmerAction | FarmSkipAction)
                   → TrainingGrounds: ProcessAction(TrainingGroundsPlaceSoldierAction | TrainingGroundsSkipAction)
                   → Castle:         (falls through to castle play below)
            └─ if placed in CastleRoom (or outside → Castle):
                 optional: ProcessAction(CastlePlaceCourtierAction)
                 optional: ProcessAction(CastleAdvanceCourtierAction)
                 or:       ProcessAction(CastleSkipAction)  [skips both]
            └─ if influence gain crossed threshold:
                 ProcessAction(ChooseInfluencePayAction)
       3. ProcessAction(PassAction)
            → advances to next player (or end of round if all dice taken)

  └─ end of round (TotalDiceRemaining ≤ 3):
       RoundEndedEvent fired; dice rerolled; new round begins
       → after MaxRounds: Phase.GameOver

Phase.GameOver
  └─ IGameEngine.IsGameOver == true
  └─ IGameEngine.GetFinalScores() returns IReadOnlyList<PlayerScore>
  └─ GameOverEvent in last ActionResult.Success.Events
```

> **Tip:** Always use `GetLegalActions(playerId)` to determine which action to send next.
> The engine enforces every constraint; `ProcessAction` returns `ActionResult.Failure` with
> a descriptive `Reason` for any illegal move.
