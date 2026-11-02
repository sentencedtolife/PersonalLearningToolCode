---
id: c-core-loops-01
title: Core Loops
track: C
unit: C.loops
tier: 1
energy: 2
minutes: 25
paper_type: concept
done_means: "You can write the core loop of any game as 3–5 arrows and say why it repeats."
summary: "What a core loop is, how to find one in a game you know, and how to check that a loop of your own really repeats."
prerequisites: [C.verbs]
tags: [core-loop]
glossary:
  - term: core loop
    definition: "The cycle of actions a player repeats for most of the game."
  - term: verb
    definition: "An action the player can take in a game, such as jump, shoot or plant."
  - term: session
    definition: "One sitting of play, from when the player starts the game to when they stop."
version: 1
---

# Core Loops

## A game you can describe in one breath

Think of Stardew Valley. You plant seeds. You water them every morning. You harvest the crops. You sell them. With the money, you buy more seeds, and you plant again. Hundreds of hours of play are built on that one cycle.

That cycle is the game's **core loop**: the cycle of actions a player repeats for most of the game. Everything else in Stardew Valley (fishing, festivals, friendships) is added around it.

## What a core loop is made of

A core loop is built from **verbs**. A verb is an action the player can take in a game, such as jump, shoot or plant. A core loop strings 3 to 5 verbs together with arrows:

> plant → water → harvest → sell → buy seeds → plant

Two rules make it a loop and not a list:

1. **Each step leads into the next.** You can't harvest before you water.
2. **The last step leads back to the first.** Selling gives money, money buys seeds, and seeds go back into the ground.

If the last step does not lead back to the first, you have a sequence. A sequence ends. A loop repeats.

~~~question
id: q1
type: mcq
prompt: "Which of these is a core loop?"
choices:
  - "Title screen → options → quit"
  - "Search a dungeon → fight → collect loot → upgrade gear → search a dungeon"
  - "Tutorial → credits"
answer: B
feedback:
  A: "These are menu screens. They happen outside of play and don't repeat during it."
  C: "This is a sequence: it happens once and then ends."
explanation: "B repeats for the whole game, and each step leads into the next: better gear lets you search deeper dungeons."
~~~

## Three examples

| Game | Core loop |
|---|---|
| Tetris | a piece appears → move and rotate it → drop it → full lines clear → a piece appears |
| Hades | fight through rooms → die or win → spend what you collected on upgrades → fight through rooms |
| Pokémon Red | walk through tall grass → battle a wild Pokémon → catch it or gain experience → walk on |

Notice that each loop has a **payoff** that feeds the next round. In Tetris, clearing lines makes space, so the next piece has room. In Hades, dying is part of the loop: the upgrades you buy make the next attempt go further.

The loop is usually short. One turn of the Tetris loop takes a few seconds. One turn of the Hades loop (one escape attempt) often takes 30 minutes or more. Some games have a small loop inside a bigger one: in Stardew Valley, each in-game day is a loop, and each season is a bigger loop around it.

~~~question
id: q2
type: multi
prompt: "Which of these are loops, where the last step leads back to the first? Select all that are."
choices:
  - "Cook a meal → eat it → gain energy → gather ingredients → cook a meal"
  - "Watch the intro → name your character → start the game"
  - "Race → win money → upgrade the car → race"
  - "Beat the final boss → watch the ending"
answer: [A, C]
hints:
  - "Read the last step of each option. Does it lead back to the first step?"
explanation: "A and C close back on themselves: the last step gives you what you need for the first step. B and D happen once and end, so they are sequences."
~~~

## How to find the core loop of any game

1. Play (or remember) about 10 minutes of the game.
2. Write down every verb you used.
3. Circle the verbs you used again and again.
4. Put the circled verbs in order with arrows.
5. Check: does the last arrow point back to the first verb? If yes, you found the loop.

## Designing a loop of your own

Start from one verb the player will enjoy doing over and over. Then ask two questions: "What does doing it give the player?" and "How does that make the next round better or different?"

Example: your verb is *dig*, in a small mining game.

1. **Dig**: the player digs down through blocks of earth.
2. **Find ore**: some blocks hold ore.
3. **Sell**: back at the surface, ore turns into coins.
4. **Upgrade**: coins buy a stronger drill.
5. Back to **dig**: the stronger drill digs deeper, where better ore is.

As arrows: dig → find ore → sell → upgrade drill → dig. The payoff (a stronger drill) changes the next round, because the player reaches new depths with new ore. Real mining games such as Motherload and SteamWorld Dig are built on this loop.

Test the loop on paper before you write any code. Read it out loud, and at the last arrow ask: "Why would I want to go round again?" If you can't answer in one sentence, change the payoff until you can.

## Common mistakes

- **Writing menus as steps.** "Open the inventory" is rarely a core verb. Ask what the player does with the inventory.
- **Writing a story instead of a loop.** "Meet the king → get the quest → save the princess" happens once. That is plot.
- **Missing the payoff.** If nothing from the last step helps the first step, players have no reason to go round again. Ask: "What does the player get that makes the next round better or different?"
- **Too many steps.** More than 5 steps usually means you have mixed in secondary systems. Keep only the steps the player repeats in almost every **session** (one sitting of play).

## Summary

- A core loop is the cycle of actions a player repeats for most of the game.
- Write it as 3–5 verbs with arrows.
- The last step must lead back to the first, usually through a payoff.
- If it doesn't repeat, it is a sequence, not a loop.

## Practice

~~~question
id: q3
type: order
prompt: "Put Tetris's core loop in order, starting with the first step."
items:
  - "A piece appears"
  - "Move and rotate it"
  - "Drop it"
  - "Full lines clear"
hints:
  - "What has to happen before you can move a piece?"
explanation: "Clearing lines makes space, which lets the next piece appear safely. That is why the loop repeats."
~~~

~~~question
id: q4
type: match
prompt: "Match each game to its core loop."
pairs:
  - ["Stardew Valley", "plant → water → harvest → sell → buy seeds"]
  - ["Tetris", "piece appears → move and rotate → drop → lines clear"]
  - ["Hades", "fight through rooms → die → buy upgrades → fight again"]
  - ["Pokémon Red", "walk through grass → battle → catch or gain experience"]
explanation: "Each loop uses the verbs the player repeats most in that game. The payoff (money, space, upgrades, stronger Pokémon) feeds the next round."
~~~

~~~question
id: q5
type: mcq
prompt: "Playtesters finish one round of your game's loop and then stop playing. They say: 'I didn't see a reason to do it again.' Which part of the loop is most likely missing?"
choices:
  - "A payoff that makes the next round better or different"
  - "More menu screens"
  - "A longer tutorial"
  - "A higher number of verbs"
answer: A
feedback:
  B: "Menus are outside the loop. They don't give a reason to go round again."
  C: "A tutorial explains the loop, but it doesn't make players want to repeat it."
  D: "More verbs make the loop longer, not more rewarding. Most core loops have 3–5 steps."
hints:
  - "Think about Tetris: what does clearing lines give you for the next piece?"
  - "The missing part connects the last step back to the first."
explanation: "A loop repeats when the last step gives the player something that makes the first step worth doing again. Without a payoff, the loop is a sequence that happens to restart."
~~~

~~~question
id: q6
type: text
prompt: "A core loop is built from player actions such as jump, plant and shoot. What one-word name do designers give these actions?"
accept: ["verbs", "verb", "player verbs"]
hints:
  - "It is the same word grammar uses for doing words."
explanation: "Designers call player actions verbs. A core loop is a short chain of verbs where the last one leads back to the first."
~~~

~~~question
id: q7
type: reflect
prompt: "Write the core loop of a game you played this month, as arrows. Then write one sentence saying why the player repeats it."
model_answer: |
  Example (Stardew Valley): plant → water → harvest → sell → buy seeds → plant.
  Why it repeats: selling crops gives money for more and better seeds, so each season's farm can be bigger.
rubric:
  - "3–5 steps"
  - "Each step is a verb the player does"
  - "The last step leads back into the first"
  - "The sentence names the payoff that makes the player go round again"
explanation: "If your loop doesn't close back on itself, it's a sequence, not a loop. If you couldn't name a payoff, look at what the last step gives the player."
~~~
