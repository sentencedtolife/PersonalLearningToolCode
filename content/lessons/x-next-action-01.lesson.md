---
id: x-next-action-01
title: Next Physical Actions
track: X
unit: X.actions
tier: 0
energy: 1
minutes: 8
paper_type: concept
done_means: "You can rewrite a vague task as one next physical action: it starts with a verb, names the exact thing, and you could start it within one minute."
summary: "How to turn a vague task like 'work on my game' into one concrete step you can start right now."
tags: [next-action, planning]
glossary:
  - term: next physical action
    definition: "The very next visible thing you would do to move a task forward, written so that you could start it within one minute."
version: 1
---

# Next Physical Actions

## The problem with vague tasks

"Work on my game" is not a task. It is a wish. When you sit down with it, your brain has to answer ten questions first: which file, which feature, which tool, what does finished look like? That is tiring, so it is tempting to do something else instead.

Games avoid this problem on purpose. The first quest in Stardew Valley, "Getting Started", does not say "build a great farm". It says: plant the parsnip seeds and harvest a parsnip. One clear action, so the player starts in seconds.

## What a next physical action is

A **next physical action** is the very next visible thing you would do to move a task forward, written so that you could start it within one minute. The idea comes from David Allen's book *Getting Things Done*.

A good next physical action has three parts:

1. **It starts with a verb.** Open, write, draw, record, move, rename.
2. **It names the exact thing.** Not "the player", but `player.tscn`.
3. **You could start it within one minute.** No decisions are left.

| Vague task | Next physical action |
|---|---|
| Work on my game | Open `player.tscn` and add a Sprite2D node |
| Do the art | Draw one 16×16 coin sprite in Pixelorama |
| Learn signals | Read the "Signals" lesson in Nordlys to the first question |

~~~question
id: q1
type: mcq
prompt: "Which of these is a next physical action?"
choices:
  - "Work on my game"
  - "Make the jump feel better"
  - "Open player.gd and change jump_speed from 300 to 400"
  - "Learn GDScript"
answer: C
feedback:
  A: "This names a whole project. Which file would you open first? That is the next physical action."
  B: "This is a goal. It does not say what to open or what to change."
  D: "This is a topic. A next action would name one lesson, page or exercise."
hints:
  - "Look for the option where you know exactly what your hands would do."
explanation: "C starts with a verb (open), names the exact file and value, and you could start it within one minute. The others need more decisions first."
~~~

## How to rewrite a task

Ask yourself: "If I had to start in the next minute, what would my hands do first?" Write that down. If the answer still needs a decision, ask the question again.

## Practice

~~~question
id: q2
type: multi
prompt: "Select every option that is a next physical action."
choices:
  - "Write three player verbs on a sticky note"
  - "Finish the game"
  - "Open level_1.tscn and move the door 2 tiles left"
  - "Get better at art"
  - "Search YouTube for 'Godot 4 CharacterBody2D' and watch the first result"
answer: [A, C, E]
hints:
  - "For each option, ask: could I start this within one minute, with no decisions left?"
  - "Two of the options are big goals with no first step."
explanation: "A, C and E each start with a verb and name the exact thing. 'Finish the game' and 'Get better at art' are goals: they do not say what to do first."
~~~

~~~question
id: q3
type: text
prompt: "What kind of word should a next physical action start with? (one word)"
accept: ["verb", "a verb", "action verb", "an action verb", "doing word", "a doing word"]
hints:
  - "Open, write, draw and move are all this kind of word."
explanation: "A verb (a doing word) tells you what your hands do. Without it, you still have to decide what the action is."
~~~

~~~question
id: q4
type: reflect
prompt: "Pick one vague task from your own game project (or from your life). Write it down, then rewrite it as one next physical action."
model_answer: |
  Vague: "Sort out the menu."
  Next physical action: "Open main_menu.tscn and add a Button node with the text Start."
rubric:
  - "The action starts with a verb"
  - "It names the exact file, tool or object"
  - "You could start it within one minute, with no decisions left"
explanation: "If your rewrite still needs a decision before you can start, ask again: what would my hands do first? Keep asking until the answer is one visible step."
~~~
