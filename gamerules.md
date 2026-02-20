# The White Castle — Game Rules (Implemented Subset)

> This document captures the rules as described and implemented so far.
> Sections marked **[DEFERRED]** are known but not yet coded.

## Overview
2–4 players compete to earn the most victory points over 3 rounds by placing dice and
workers on the board, earning resources, coins, lanterns, and clan cards.

---

## Components

### Bridges (3)
Colors: **Red**, **Black**, **White**.
Each bridge holds dice in three positions: **High** (top), **Middle** (queue), **Low** (bottom).
At the start of each round, 3 dice are placed per bridge (9 total across all bridges).
Middle dice slide down as High/Low dice are taken.

### Towers (3)
Zones: **Left**, **Center**, **Right**.
Each tower has 4 levels (0–3). Players place workers on unoccupied levels to perform actions.
Workers return to their owners at the end of each round.

Tower actions (vary by zone and level):
- **Gain Resources** — receive Iron, Rice, or Flower
- **Advance Tower** — move up one level in a tower zone
- **Acquire Clan Card** — take a card from the shared row
- **Gain Lanterns** — add lanterns to your score

### Main Castle (3 floors)
- **Ground floor** — 3 rooms, compare value = **3**
- **Mid floor** — 2 rooms, compare value = **4**
- **Top floor** — 1 room — **[DEFERRED — die cannot be placed here; purpose TBD]**

### The Well
- 1 slot, compare value always = **1**
- **Unlimited capacity** (any number of dice may be placed here)

### Outside (2 slots)
- Compare value = **5**

### Gate **[DEFERRED]**

### Training Grounds **[DEFERRED]**

### Farming Lands **[DEFERRED]**

### Personal Domains **[DEFERRED]**

---

## Turn Flow

Each turn a player must do one of:
1. **Take a die** from a bridge (High or Low position), then immediately **place it**.
2. **Place a worker** on a tower level (pay cost → receive reward).
3. **Pass** (ends your turn for this round).

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
1. All workers return to their owners.
2. All placed dice are cleared from castle rooms, well, and outside slots.
3. New dice are rolled for all bridges.
4. The next round begins.

The game lasts **3 rounds** (configurable in engine).

---

## Resources

Three types: **Iron**, **Rice**, **Flower**.
Resources are gained from tower actions and used to pay tower costs.

---

## Victory Points

Calculated at game end:
- **Lantern score** — accumulated lanterns
- **Clan card VP** — sum of victory points on acquired clan cards
- **Tower advancement** — points per level reached in each tower zone

---

## Notes on Implementation
- Coins are tracked per player and persist across rounds.
- Dice colors match their bridge color (cosmetic; does not affect placement rules).
- AI strategy "greedy-resource" scores bridge actions by die face value and tower actions by
  resource/lantern gain.
