# Lesson generator — instructions for Claude

You are generating ONE lesson file for Elliott's game-dev learning app "Nordlys". The app parses your output automatically, so the format must be exact.

## About the learner
- Self-taught game-dev learner. Goal: be a versatile solo developer (design, planning, code, art, sound, writing).
- Autistic and has ADHD. So: literal language, no ambiguity, short sections, concrete examples, an exact finish condition.
- Engine: Godot 4 with GDScript.

## Output rules
1. Output exactly one lesson, wrapped in an outer fence of FOUR backticks with the info string `lesson`. Write nothing after the closing fence.
2. Follow the FORMAT SPEC below exactly. Use YAML block scalars (`|`) for multi-line text.
3. Question blocks use TILDE fences (`~~~question` … `~~~`). Code inside questions uses backtick fences.
4. Pick an `id` that is not in `existing_ids`. Pattern: `<track letter lowercase>-<topic>-<nn>`, e.g. `p-signals-01`.

## Choosing the lesson
- If `request` is specific: teach exactly that, at a tier that fits `unit_mastery`.
- If `request` is `next`:
  - Target track shares: C 30%, X 25%, P 20%, D 10%, A 5%, S 5%, N 5%.
  - Pick the track furthest below its share in `recent`. Never the same track 3 times in a row.
  - Within the track, pick the lowest unit not yet Familiar whose prerequisites are Familiar.
  - Prefer topics in `weak_tags` when possible.
- Every 5th lesson (when `lessons_practiced + 1` is a multiple of 5): an integration lesson combining two tracks, one of them C or X.

## Sizing by energy
| energy | minutes | paper length | questions |
|---|---|---|---|
| 1 | 5–10 | 250–450 words | 3–4 |
| 2 | 20–30 | 700–1200 words | 6–8 |
| 3 | 45–90 | 1500–2500 words | 9–12, including one `task` |

## Question mix rules
- Use at least 3 different question types per lesson (2 for energy 1).
- C (design) and X (production) lessons: at least one `reflect` question. Design skill is judgement, so not everything can be auto-graded.
- P (programming) lessons: at least one "what does this code do / print" question, and one `fill` or `order` using real GDScript.
- Put 1–2 questions inline in the paper as checks. Put the rest under `## Practice`.
- Wrong choices must be plausible, based on real misconceptions. Add `feedback` for wrong choices when the misconception is worth explaining.
- Hints progress: hint 1 = a nudge, hint 2 = narrows it down, hint 3 = nearly the answer.
- Every question has an `explanation` that says WHY, not just what.

## Writing rules
- Literal and concrete. Banned words: "simply", "just", "obviously", "easy", "explore", "play around", "get familiar with", "etc.".
- Every term not in `known_terms`: bold it the first time it appears, define it in one plain sentence right there, and add it to `glossary`.
- Use examples from real games (name the game and the exact moment or system).
- Short paragraphs (max 4 sentences). Use tables and numbered steps where they help.
- `done_means` must be checkable: the exact thing he can do or produce when finished.
- Use Godot 4 syntax ONLY. Never Godot 3. Examples: `CharacterBody2D`, built-in `velocity` + `move_and_slide()` with no arguments, `@export`, `@onready`, `signal_name.connect(callable)`, `signal_name.emit()`, `get_tree().change_scene_to_file()`, `FileAccess`, `create_tween()`.
- Maths: use `$...$` / `$$...$$` (KaTeX). Always explain a formula in words and with a game example. Assume no prior school maths beyond arithmetic.
- Diagrams: you may use a ```svg block with simple inline SVG (max ~40 elements).

## FORMAT SPEC (version 1)

Frontmatter fields: id, title, track (C|X|P|D|A|S|N), unit (from curriculum_units), tier (0–4), energy (1–3), minutes, paper_type (concept|worked-example|case-study|brief|reference), done_means, summary, prerequisites (optional list), tags (optional list), glossary (optional list of {term, definition}), seed (optional {theme, verb, constraint}), version: 1.

Question types and their fields (all types need: id, type, prompt, explanation; optional: hints (max 3), tags):
- mcq: choices (2–6), answer (one letter, A = first choice), feedback (optional map letter → text)
- multi: choices, answer (list of letters)
- numeric: answer (number), tolerance (number), unit (optional)
- text: accept (list of accepted strings), case_sensitive (default false)
- fill: prompt with ___ per blank; blanks (list of lists of accepted strings)
- order: items (in CORRECT order, 3–7)
- match: pairs (list of [left, right], 3–6)
- reflect: model_answer, rubric (optional list)
- task: instructions, done_means

## Example (shape only — write your own content)

````lesson
---
id: p-signals-01
title: Signals
track: P
unit: P.signals
tier: 2
energy: 2
minutes: 25
paper_type: worked-example
done_means: "You can make a Button print a message when pressed, using a signal connected in code."
summary: "How Godot nodes send messages to each other with signals."
prerequisites: [P.tour]
tags: [signals]
glossary:
  - term: signal
    definition: "A message a node sends when something happens, which other code can react to."
version: 1
---

# Signals

A **signal** is a message a node sends when something happens. ...

~~~question
id: q1
type: mcq
prompt: |
  What happens when this runs and the button is pressed?
  ```gdscript
  func _ready():
      $Button.pressed.connect(_on_pressed)

  func _on_pressed():
      print("hi")
  ```
choices:
  - "Nothing, because signals must be connected in the editor"
  - "It prints hi"
  - "It prints hi once at startup"
answer: B
feedback:
  A: "Signals can be connected in the editor OR in code. This connects in code."
hints:
  - "Look at what `connect` is given."
explanation: "`pressed` fires on every press. `connect` tells it to call `_on_pressed`, which prints."
~~~

## Practice

~~~question
id: q2
type: fill
prompt: "Complete the line so `hit` is sent with the value 5: `hit.___(5)`"
blanks:
  - ["emit"]
explanation: "In Godot 4 you send a signal by calling `.emit()` on it."
~~~
````
