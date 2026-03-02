# The White Castle — Game Rules (Implemented Subset)

> This document captures the rules as described and implemented so far.
> Sections marked **[DEFERRED]** are known but not yet coded.

## Overview
2–4 players compete to earn the most victory points over 3 rounds by placing dice on the
board and earning coins, lanterns, resources, and monarchial seals.

---

## Components

### Bridges (3)
Colors: **Red**, **Black**, **White**.
Each bridge holds dice in three positions: **High** (top), **Middle** (queue), **Low** (bottom).
At the start of each round, 3 dice are placed per bridge (9 total across all bridges).
Middle dice slide down as High/Low dice are taken.

### Main Castle (3 floors)
- **Ground floor** — 3 rooms, compare value = **3**
- **Mid floor** — 2 rooms, compare value = **4**
- **Top floor** — 1 room — **[DEFERRED — die cannot be placed here; purpose TBD]**

**Die-color restriction**: A die may only be placed in a castle room if the room contains
at least one token whose die-color side matches the die's color.
E.g. a room with tokens [R:Fd][R:Fe][W:VI] accepts Red and White dice but rejects Black dice.

### The Well
- 1 slot, compare value always = **1**
- **Unlimited capacity** (any number of dice may be placed here)

### Outside (2 slots)
- Compare value = **5**

### Tokens (15)
Double-sided cardboard tokens placed at game start in castle rooms and the well.

**Two sides:**
- **Die-color side** (Red / Black / White) — indicates which die color interacts with this token.
- **Resource side** — one of: Food, Iron, Value Item, Any Resource, Coin.

**Composition:** 3 die colors × 5 resource types = 15 tokens (one of each combination).

**Placement at game start (constrained random):**

| Destination | Count | Constraint |
|-------------|-------|------------|
| Ground floor rooms — seed | 1 per room (3 total) | One token of each die color, one per room |
| Mid floor rooms | 2 per room (4 total) | Each room's 2 tokens must have different die colors |
| Ground floor rooms — fill | 2 more per room (6 total) | Final 3 tokens per room must contain ≥2 different die colors |
| Well | 2 remaining | Placed **resource side up** |

Tokens are permanent — they are NOT cleared at round end.

**Effect when die placed in the well:**
The player immediately receives:
- **+1 Monarchial Seal** (capped at 5)
- For each well token (resource side up):
  - Food token → +1 Food (capped at 7)
  - Iron token → +1 Iron (capped at 7)
  - Value Item token → +1 Value Item (capped at 7)
  - Coin token → +1 Coin
  - Any Resource token → player chooses Food, Iron, or Value Item (capped at 7); prompted separately for each

When AnyResource tokens are present, the player must resolve each choice before their turn advances.
In the console, use: `choose food`, `choose iron`, or `choose valueitem`.

**Effect when die placed in a castle room:** **[DEFERRED]**

### Outside Slot Activation

When a die is placed in an **outside slot**, the player must choose which action to activate:
- **Slot 0** (left): Farm or Castle
- **Slot 1** (right): Training Grounds or Castle

After choosing, the chosen action triggers immediately (before the player's next turn).

### Play Castle Action

When a "Play Castle" action is triggered (from an outside slot, or from card field effects), the player receives:
- **1 place-at-gate use** (`CastlePlaceRemaining++`)
- **1 advance-courtier use** (`CastleAdvanceRemaining++`)

The player must then resolve each use before acting further. For each use, they can:

1. **Place a courtier at the gate** — costs 2 coins, moves a courtier from hand to `CourtiersAtGate`
2. **Advance a courtier** — costs Value Items, moves a courtier one or two levels up the castle:
   - **Gate → Ground floor** (1 level, −2 VI): player picks one of 3 rooms
   - **Gate → Mid floor** (2 levels, −5 VI): player picks one of 2 rooms
   - **Ground → Mid floor** (1 level, −2 VI): player picks one of 2 rooms
   - **Ground → Top floor** (2 levels, −5 VI): no room choice
   - **Mid → Top floor** (1 level, −2 VI): no room choice
3. **Skip** — forfeits all remaining castle uses

**Card acquisition (when entering ground or mid floor):**
- The player takes the **current card** from the chosen room
- A replacement is dealt from the floor deck; if no replacement is available, no card is taken
- The taken card's **back** goes into the lantern chain
- The taken card is placed in the player's **personal domain** (see Personal Domain Cards)

### Gate **[DEFERRED]**

### Training Grounds **[DEFERRED]**

### Farming Lands **[DEFERRED]**

---

## Personal Domains

Each player has their own **personal domain** — a dedicated player board that tracks everything
belonging to that player. It is divided into two sections:

### Resource Storage
Three resource types, each stored in their own space on the personal domain:
- **Food** — maximum 7
- **Iron** — maximum 7
- **Value Item** — maximum 7

Additional tracked values (not resources, but stored on personal domain):
- **Coins** — no cap (earned/spent during placement)
- **Monarchial Seals** — maximum 5 (earned by placing dice in the Well)
- **Lantern score** — victory points accumulated over the game

### Personnel Area
The personal domain has a dedicated space for each type of personnel figure.
Each player begins the game with all figures on their personal domain:
- **5 Soldiers** — deployed to Training Grounds
- **5 Courtiers** — deployed to the Castle (Gate → Ground → Mid → Top floor)
- **5 Farmers** — deployed to Farming Lands

When a figure is deployed to the board (via Play Castle, Play Training Grounds, or Play Farm),
it moves from the personal domain to the corresponding board area.
The count of figures remaining on the personal domain determines how many more can be deployed
(`SoldiersAvailable`, `CourtiersAvailable`, `FarmersAvailable`).

Figures are **not returned** to the personal domain when cleared at round end — they stay on the
board area where they were placed for the remainder of the game.

### Lantern Chain

Each player's personal domain has a **Lantern Chain** area — an ordered list of card backs
that fire whenever the Lantern Effect triggers.

**Building the chain:**
- When seeding completes (a player picks their seed pair), the **resource seed card** is flipped
  and its back gain is added as the first entry in the chain.
  - Resource seed card backs: one resource (Food, Iron, or Value Item), quantity 1.
- When a courtier enters a **ground floor** or **mid floor** castle room (see Castle section),
  the room card is taken and its back gain is added to the chain:
  - Ground floor card backs: one Coin
  - Mid floor card backs: one VictoryPoint

**Activation:**
- Every time a Lantern gain fires (Low-die Lantern Effect, card field gains, castle/farm/TG effects),
  the chain activates **once** — every entry fires left-to-right and the player receives all chain gains.
- Resource gains (Food/Iron/ValueItem) are capped at 7; Monarchial Seals capped at 5.
- **VictoryPoint** chain entries increment `LanternScore` directly.
- **Influence** chain entries are a no-op (mechanic deferred).

**Card backs (all cards have a back):**
- Resource seed cards: one resource (Food/Iron/ValueItem)
- Action seed cards: one Influence (**deferred**)
- Ground floor castle cards: one Coin
- Mid floor castle cards: one VictoryPoint (**maps to LanternScore**)

### Die Placement Rows (personal domain board)

The personal domain also contains three **die placement rows**, one per figure type.
Each row is associated with a specific die color and a compare value of **6**.

| Row | Die color | Figure type | Default gain | Spot gain (×5) |
|-----|-----------|-------------|--------------|----------------|
| 0   | Red       | Courtier    | +1 Iron      | Iron           |
| 1   | White     | Farmer      | +1 Value Item | Value Item    |
| 2   | Black     | Soldier     | +1 Food      | Food           |

**Mechanics:**
- Each spot in a row starts **covered** by a figure. Figures are removed **left-to-right** as they are
  deployed to the board (each deployment uncovers one spot, revealing its resource gain).
- A player may place a die of the matching color into a row (spending or earning coins vs. compare value 6).
- **Effect on placement**: default gain + all **uncovered spot** gains (left-to-right) are granted immediately.
  In addition, each **personal domain card** the player has acquired fires the field that maps to this row (see below).
- **One die per row per round** — no stacking. Placed dice are cleared at round end.
- The row configuration (gains, compare value) is loaded from `personal-domain-rows.json` and can be changed there.

### Personal Domain Cards

When a courtier is advanced into a **ground floor** or **mid floor** castle room, the player takes the
room's current card and places a replacement card from the floor deck.
If no replacement is available, the courtier still advances but no card is taken.

- The taken card's **back** is added to the player's lantern chain.
- The taken card is placed in the player's **personal domain** (displayed to the right of the dice rows).

**Field → row mapping** (determines which field fires when a die is placed in each personal domain row):

| Card type | Layout | Row 0 (Red/Courtier) | Row 1 (White/Farmer) | Row 2 (Black/Soldier) |
|-----------|--------|----------------------|----------------------|------------------------|
| Ground floor | 3-field | field[0] | field[1] | field[2] |
| Mid floor | DoubleTop | field[0] | field[0] | field[1] |
| Mid floor | DoubleBottom | field[0] | field[1] | field[1] |

Fields can be **gain fields** (grant resources/coins/seals/lanterns) or **action fields** (trigger Play Castle / Play Farm / Play Training Grounds).

---

## Turn Flow

Each turn a player must do one of:
1. **Take a die** from a bridge (High or Low position), then immediately **place it**.
2. **Pass** (ends your turn for this round).

### Taking and Placing a Die (Two-Step Action)

**Step 1 — Take:** Choose a bridge and position (High or Low).
- The die is held in your hand.
- You cannot do anything else until the die is placed.

**Step 2 — Place:** Choose a placement area (Castle room, Well, or Outside slot).
- The **coin effect** is applied immediately (see below).
- Your turn then ends normally.

**Lantern Effect:** Taking the **Low** die from any bridge triggers the Lantern Effect for you.

---

## Coin Mechanic

When placing a die, compare the die's value against the slot's **compare value**:

```
delta = die value − compare value
```

| delta | Effect |
|-------|--------|
| > 0 | You **earn** `delta` coins |
| = 0 | No effect |
| < 0 | You **pay** `|delta|` coins — **cannot place if you have insufficient coins** |

---

## Placement Capacity

| Area | 2-player | 3-4 player |
|------|----------|------------|
| Castle rooms | max 1 die | max 2 dice |
| Outside slots | max 1 die | max 2 dice |
| Well | unlimited | unlimited |

**In 3-4 player mode**, the 2nd die placed in a slot is compared against the
**1st die's value** (not the base compare value).
The Well always compares against its base value (1), regardless of player count.

---

## Round Mechanics

A round ends when the total dice remaining across all bridges drops to **3 or fewer**.

At round end:
1. All placed dice are cleared from castle rooms, well, and outside slots.
2. New dice are rolled for all bridges.
3. The next round begins.

The game lasts **3 rounds** (configurable in engine).

---

## Victory Points

Calculated at game end:
- **Lantern score** — accumulated lanterns (includes VictoryPoint gains from the lantern chain)

---

## Notes on Implementation
- Coins and Monarchial Seals are tracked per player and persist across rounds.
- Dice colors match their bridge color (cosmetic; does not affect placement rules).
- AI strategy "greedy-resource" scores bridge actions by die face value.
- Tokens are placed once at game start via `Board.PlaceTokens(rng)` and persist across all rounds.
