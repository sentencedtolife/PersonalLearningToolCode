---
id: p-godot-tour-01
title: "Godot Tour: Nodes, Scenes and the Editor"
track: P
unit: P.tour
tier: 0
energy: 2
minutes: 30
paper_type: worked-example
done_means: "You have made a scene with a Node2D root and a Sprite2D child in Godot 4, saved it as main.tscn, and run it so a window shows the Godot icon."
summary: "The four main areas of the Godot 4 editor, what nodes and scenes are, and how to build and run your first scene."
tags: [godot-editor, nodes, scenes]
glossary:
  - term: node
    definition: "The basic building block in Godot. Each node does one job, such as showing an image or playing a sound."
  - term: scene
    definition: "A saved tree of nodes, stored as a .tscn file. A player, a level and a menu can each be a scene."
  - term: scene tree
    definition: "The parent-and-child structure of nodes in a scene, shown in the Scene dock."
  - term: root node
    definition: "The single node at the top of a scene tree. Every other node in the scene is below it."
  - term: property
    definition: "A setting on a node, such as its position or texture, shown and edited in the Inspector."
  - term: script
    definition: "A file of GDScript code (.gd) attached to a node, which gives that node behaviour."
version: 1
---

# Godot Tour: Nodes, Scenes and the Editor

## Goal

By the end of this lesson you will have built and run a tiny scene: a window that shows the Godot icon. On the way you will learn the three ideas everything in Godot is made of: nodes, scenes and the scene tree.

You need Godot 4 installed (any 4.x version). This lesson does not work with Godot 3, because many names changed.

## Step 1: Understand nodes

A **node** is the basic building block in Godot. Each node does one job. Some examples:

| Node | Its one job |
|---|---|
| `Sprite2D` | Shows an image |
| `AudioStreamPlayer` | Plays a sound |
| `Camera2D` | Decides which part of the world is on screen |
| `CollisionShape2D` | Gives something a shape that can bump into things |

Why it works this way: small single-job nodes can be combined like building blocks. A player character is not one big object. It is a few nodes working together: one to move, one to show the image, one for the collision shape.

## Step 2: Understand scenes and the scene tree

Nodes are arranged as parents and children. A child node moves with its parent: if you move the player, its image moves too.

This parent-and-child structure is called the **scene tree**. The single node at the top is the **root node**. Every other node in the scene is below it.

A **scene** is a saved tree of nodes, stored as a `.tscn` file. A player can be a scene. A level can be a scene. A menu can be a scene. Scenes can contain other scenes, so the level scene can contain the player scene.

Here is a scene tree for a small game:

```
Main (Node2D)
├─ Player (CharacterBody2D)
│  ├─ Sprite2D
│  └─ CollisionShape2D
└─ Camera2D
```

~~~question
id: q1
type: numeric
prompt: |
  How many nodes are in this scene tree? Count the root node too.
  ```
  Main (Node2D)
  ├─ Player (CharacterBody2D)
  │  ├─ Sprite2D
  │  └─ CollisionShape2D
  └─ Camera2D
  ```
answer: 5
tolerance: 0
unit: nodes
hints:
  - "Count every line that names a node."
  - "Main is the root node, and it counts."
explanation: "Main, Player, Sprite2D, CollisionShape2D and Camera2D: 5 nodes. The root node is part of the tree, so it counts."
~~~

## Step 3: Learn the four main areas of the editor

When you open a project, the Godot 4 editor shows four areas you will use all the time:

1. **Scene dock** (top left): the scene tree of the scene you have open. You add, rename and reorder nodes here.
2. **FileSystem dock** (bottom left): the files in your project folder. Godot calls this folder `res://`.
3. **Viewport** (centre): shows the scene so you can place things. The buttons above it switch between 2D, 3D and Script views.
4. **Inspector** (right): shows the **properties** of the selected node. A property is a setting on a node, such as its position or texture.

Why it works this way: you pick a node on the left, see it in the middle, and change its settings on the right. That left-to-right order is the same for every kind of node.

## Step 4: Build the scene

Follow these steps exactly.

1. In the Project Manager, click **Create**, give the project a name, and click **Create** again (older 4.x versions say **Create & Edit**).
2. In the Scene dock, click **2D Scene**. This makes a `Node2D` as the root node.
3. With the root node selected, press **Ctrl+A** (Add Child Node). Type `Sprite2D`, select it and click **Create**.
4. With `Sprite2D` selected, find **Texture** in the Inspector. Drag `icon.svg` from the FileSystem dock onto it. The Godot icon appears in the viewport.
5. Drag the icon in the viewport to near the middle of the blue rectangle. The blue rectangle shows what the game window will show.
6. Press **Ctrl+S** and save the scene as `main.tscn`.
7. Press **F6** (Run Current Scene). A game window opens and shows the icon.

Why step 5 matters: the position `(0, 0)` is the top-left corner of the window, so a sprite left at `(0, 0)` is only partly visible.

## Step 5: Know that scripts attach to nodes

Nodes do their job without code. To give a node new behaviour, you attach a **script**: a file of GDScript code (`.gd`) attached to a node.

Godot calls some functions in a script by itself. `_ready()` runs once, when the node enters the scene tree. `print()` writes text to the **Output** panel at the bottom of the editor. A script can use `name` to read the node's own name.

```gdscript
extends Node2D

func _ready():
    print("Hello from ", name)
```

~~~question
id: q2
type: mcq
prompt: |
  This script is attached to a node named `Main`. What appears in the Output panel when you run the scene?
  ```gdscript
  extends Node2D

  func _ready():
      print("Hello from ", name)
  ```
choices:
  - "Hello from name"
  - "Hello from Main"
  - "Nothing, because _ready() is never called unless another script calls it"
  - "Hello fromMain"
answer: B
feedback:
  A: "name is not in quotes, so it is a value, not text. Its value is the node's name."
  C: "Godot calls _ready() by itself, once, when the node enters the scene tree."
  D: "The first piece of text ends with a space (\"Hello from \"), so there is a space before Main."
hints:
  - "Which parts are in quotes, and which are not?"
  - "print() joins its pieces together exactly as they are."
explanation: "Godot calls _ready() when the node enters the scene tree. print() joins \"Hello from \" (with its space) and the value of name, which is Main."
~~~

## Summary

- A node does one job. A scene is a saved tree of nodes (`.tscn`).
- The root node is at the top of the scene tree. Children move with their parents.
- Scene dock (tree), FileSystem (files), Viewport (view), Inspector (properties).
- F6 runs the current scene. `_ready()` runs once when a node enters the tree.

## Practice

~~~question
id: q3
type: match
prompt: "Match each editor area to what it does."
pairs:
  - ["Scene dock", "Shows the node tree of the open scene"]
  - ["FileSystem dock", "Shows the files in your project folder (res://)"]
  - ["Viewport", "Shows the scene so you can place things"]
  - ["Inspector", "Shows and edits the properties of the selected node"]
explanation: "You pick a node in the Scene dock, see it in the Viewport, and change its properties in the Inspector. The FileSystem dock is where files such as icon.svg live."
~~~

~~~question
id: q4
type: text
prompt: "What file extension does Godot 4 use for a saved scene?"
accept: [".tscn", "tscn"]
hints:
  - "It starts with a dot and a t."
explanation: "Scenes are saved as .tscn files (text scene). Scripts are .gd files."
~~~

~~~question
id: q5
type: fill
prompt: |
  Fill in the blanks so the code moves the Sprite2D to x = 200, y = 100 when the scene starts.
  ```gdscript
  extends Node2D

  func ___():
      $Sprite2D.___ = Vector2(200, 100)
  ```
blanks:
  - ["_ready", "_ready()"]
  - ["position"]
hints:
  - "The first blank is the function Godot calls once, when the node enters the scene tree."
  - "The second blank is the property that stores where a node is."
explanation: "_ready() runs once at the start. position is the property that stores where a 2D node is, as a Vector2 of x and y."
~~~

~~~question
id: q6
type: order
prompt: "Put the steps for making and running your first scene in order."
items:
  - "Click 2D Scene to make a Node2D root node"
  - "Add a Sprite2D as a child with Ctrl+A"
  - "Drag icon.svg onto the Sprite2D's Texture property"
  - "Save the scene as main.tscn"
  - "Press F6 to run the current scene"
hints:
  - "You need a root node before you can add a child."
  - "Godot needs the scene saved before it can run it."
explanation: "Root first, then the child, then its texture. Save before running, because F6 runs the saved scene."
~~~

~~~question
id: q7
type: task
prompt: "Build and run your first Godot scene."
instructions: |
  1. Open Godot 4 and create a new project.
  2. Click **2D Scene** in the Scene dock.
  3. Add a `Sprite2D` child (Ctrl+A).
  4. Drag `icon.svg` onto the Sprite2D's **Texture** property.
  5. Move the icon inside the blue rectangle.
  6. Save as `main.tscn` (Ctrl+S).
  7. Press **F6**.
done_means: "A game window opens and shows the Godot icon."
explanation: "If the window is empty, check that the Texture property is set and that the icon is inside the blue rectangle."
~~~
