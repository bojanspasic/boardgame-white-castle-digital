# The White Castle — Game Rules (Implemented Subset)

> This document captures the rules as described and implemented so far.
> Sections marked **[DEFERRED]** are known but not yet coded.

## Overview
2–4 players compete to earn the most victory points over 3 rounds by placing dice on the
board and earning coins, lanterns, clan cards, resources, and monarchial seals.

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

**Effect when die placed in a room:** **[DEFERRED]**

### Gate **[DEFERRED]**

### Training Grounds **[DEFERRED]**

### Farming Lands **[DEFERRED]**

---

## Personal Domains

Each player has a personal domain that stores their resources and personnel.

### Resources
Three types: **Food**, **Iron**, **Value Item**.
- Maximum **7** of each resource per player.
- How resources are earned: **[DEFERRED]**

### Monarchial Seals
- A separate earnable currency distinct from coins.
- Maximum **5** seals per player.
- How seals are earned: **[DEFERRED]**

### Personnel
Each player starts with:
- 5 **Soldiers**
- 5 **Courtiers**
- 5 **Farmers**

Placement rules for personnel: **[DEFERRED]**

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
- **Lantern score** — accumulated lanterns
- **Clan card VP** — sum of victory points on acquired clan cards

---

## Notes on Implementation
- Coins and Monarchial Seals are tracked per player and persist across rounds.
- Dice colors match their bridge color (cosmetic; does not affect placement rules).
- AI strategy "greedy-resource" scores bridge actions by die face value.
- Tokens are placed once at game start via `Board.PlaceTokens(rng)` and persist across all rounds.
