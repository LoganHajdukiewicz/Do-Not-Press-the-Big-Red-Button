# First-person controller

Targets the project's **Unity 6000.6.2f1** editor and **Input System 1.20.0**. This is a desktop keyboard/mouse and gamepad prototype; touch controls are not included.

## Quick start

1. Open the project in Unity and let scripts/packages finish importing.
2. Select **Tools > Big Red Button > Create Controller Test Scene**. Unity offers to save any open modified scenes first. The command creates a new, unsaved scene; it does not overwrite `SampleScene`.
3. Save the scene under `Assets/Scenes`, then press **Play** and focus the Game view.
4. Walk toward either button, aim at its colored top, and press **E** when the prompt appears. A matching indicator appears above the pedestal and the press is logged in the Console. Use the platforms on the left to test jumping.

The demo command creates materials under `Assets/ControllerDemoMaterials`. Repeated runs create uniquely named materials rather than overwriting existing assets.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Walk | WASD or arrow keys | Left stick |
| Look | Mouse | Right stick |
| Run | Hold Left Shift | Hold left-stick click |
| Jump | Space | South button (A / Cross) |
| Interact | E | North button (Y / Triangle) |
| Release / capture cursor | Escape | Start / Menu |
| Capture released cursor | Left click | Start / Menu |

Interactions happen on **press**, not hold. The existing `Player/Interact` action's `Hold` interaction was removed. Other action bindings are unchanged. Rebinding can be done in `Assets/InputSystem_Actions.inputactions`; the prototype HUD's short button hints are fixed labels and should be updated if bindings change.

Releasing the cursor suppresses look, movement input, jumping and E-key interaction, but gravity and physical button contact still apply. It does **not** pause the world. Losing application focus releases the cursor; click or use Escape/Start to regain control. A resume click never activates a button by itself. Setting `Time.timeScale` to zero also suppresses gameplay updates, including contact presses.

## Add the player to your own scene

Select **GameObject > Big Red Button > First Person Player**. It creates and wires up:

- A `CharacterController`: height 1.8 m, radius 0.3 m, center Y 0.9 m, step offset 0.3 m.
- A child camera at eye height 1.6 m and an AudioListener.
- `FirstPersonController`, `PlayerInteractor`, and the optional `FirstPersonHUD`.
- A reference to the project's existing input asset.

Disable or remove the previous gameplay camera and AudioListener. Position the player's root at floor level, with a little clearance above a floor collider. Keep the root upright and its scale `(1, 1, 1)`. Do **not** add a Rigidbody. Drag the configured player from the Hierarchy into the Project window if you want a reusable prefab.

The player root uses Unity's built-in **Ignore Raycast** layer, so its CharacterController does not block interaction queries. Put any additional player-owned colliders on this layer too. This layer does not disable physical collisions with the floor or walls.

### Inspector settings

On `FirstPersonController`:

- **Walk Speed / Run Speed:** default 4 / 7 metres per second.
- **Jump Height:** default 1.2 metres; jumping is only allowed while grounded.
- **Gravity / Terminal Speed:** default -20 m/s² / 50 m/s.
- **Mouse Sensitivity:** default 0.1 degrees per pixel.
- **Stick Sensitivity:** default 150 degrees per second at full input.
- **Pitch Limit / Invert Y:** default ±85 degrees / off.

Diagonal input is clamped so it is not faster than forward movement. Mouse look is not multiplied by delta time; stick look is. Ceiling collisions cancel upward velocity, while downward ground contact resets falling velocity. Movement follows player yaw rather than camera pitch. Holding Jump does not auto-jump, and there is no midair jump.

On `PlayerInteractor`:

- **Interaction Distance:** default 3 metres from the camera.
- **Raycast Layers:** must include both interactable objects **and walls/obstacles**. The first solid collider blocks the ray, preventing interaction through walls. Trigger colliders are ignored.

The runtime clones the input asset and enables only the clone's Player map. It does not change the enabled state of shared project actions. Do not also attach `PlayerInput` to this player; this controller owns its input lifecycle. The prototype assumes one local player and no separate gameplay input consumer.

## Make a button interactable

1. Add a **non-trigger collider** to the button's top or another intended hit area.
2. Add `ButtonInteractable` to that object or a parent of its colliders.
3. Set **Prompt**, **Interactable**, **One Shot**, **Press On Contact**, **Cooldown**, and **Required Presses** as needed.
4. Connect **On Pressed** in the Inspector to your level logic or animation. It runs after the required active clicks, not after each partial/wake-up click. Use `ButtonSound` for state-aware audio.

Red and green buttons share the same interaction code; their event wiring determines what they do. No win/fail or day progression is hard-coded into the controller. In the generated days, the green button's `On Pressed` calls `DayLevel.CompleteDay` and the red button's calls `DayLevel.FailDay`.

### Click counts and per-click dialogue (Inspector)

Select the button's **cap** in the Hierarchy, not the player controller or pedestal. These settings are per button:

- **Button Interactable → Click Requirement → Required Presses:** minimum active clicks needed for its outcome. Default **1**. A button with dialogue also waits until the entire conversation is acknowledged, even if this minimum has already been reached.
- **Wakeable Button → Presses To Wake:** grey clicks needed before it wakes. These are separate from active clicks. Example: **Presses To Wake = 2**, **Required Presses = 1** gives two grey clicks (the second wakes it), then a third click to ding/advance. A waking click never doubles as an active click.
- **One Shot** locks the button only after the complete sequence, not after a partial/wake-up click. Otherwise the active count restarts for the next sequence. `ResetButton()` clears counts and one-shot/cooldown state; it does not put an already-awake button back to sleep.

**Talking Button → Advance On Press** is enabled by default, including on existing saved buttons. Each click advances dialogue **instead of** completing the button. The currently displayed line stays until the next click. After the final line, **one further click acknowledges it and permits the outcome**—the last line is never cut off by loading the next day.

1. Add **Talking Button** to the cap or its assembly root and fill **Lines** in order.
2. Keep your existing **Speaks When** trigger. Approach/gaze/day-start can present line 1; the next click then presents line 2. Choose **Button Press** if line 1 should wait for the first click.
3. Set **Screen Position**, text size and colour; optionally supply matching **Line Clips**. A click replaces the previous line and stops its voice.
4. With three lines and a Button Press trigger: clicks 1–3 show the three lines; click 4 completes the day. Grey/wake-up clicks can advance dialogue, but still cannot complete or ding.

Timed advance, **Loop**, retriggering and **Silence On Press** cannot skip or restart a click-through conversation. Only the final acknowledged outcome dings. Cooldown-rejected clicks do not advance dialogue. E and physical contact share the same sequence; continuous contact still counts once. **Reset Button** resets the conversation too. An empty list or disabled Talking Button does not block completion.

Uncheck **Advance On Press** only for the old non-blocking behaviour: timed dialogue for proximity/gaze/day-start triggers, or expiring per-click popups in Button Press mode. In that optional mode, Seconds Per Line/Loop work as before and do not postpone the button's outcome.

For custom code, `Pressed` is the legacy every-click callback; `PressAccepted(ButtonPress)` carries an immutable pre-click state and click number. `OnPressed` is the completed-sequence event. `AcceptedPressCount`, `PressesRemaining`, and `LastPress` expose progress at runtime.

### Button types

These components cover the tricks in the design. Add them to a button's cap, alongside its `ButtonInteractable`. `TestScene` has one of each, labelled, so every behaviour can be tried in one room.

| Component | What it does | Days |
| --- | --- | --- |
| `ButtonSound` | Clicks on accepted presses; dings on a completed active green press, never on grey/wake-up clicks. | all |
| `ButtonAppearance` | Sets the colour a button *looks* like, separately from what it does: red, green, a blend, or grey. | 4, 10, 13, 16-19 |
| `ButtonColourSchedule` | Switches the look between red and green on a timer. | 4 |
| `ButtonGazeColour` | Colour reacts to the player's view: turns red when watched, or follows their heading. | 18, 19 |
| `ButtonSign` | A word painted above the button, in the company's lettering. | 5, 10, 17 |
| `TalkingButton` | Shows dialogue on approach, gaze, day start, manually, or one line per accepted click. | 8, 9, 11, 12 |
| `WakeableButton` | Starts grey and inert; the first press only wakes it up. | 13 |
| `CursorRepellingButton` | Pushes the player's aim away as they try to point at it. Default strength 210 deg/s. | 7 |
| `ButtonMover` | Chases the player, stays behind their back, or patrols between two points. | 20, 21 |
| `TimedReveal` | Hides a button for a few seconds, then reveals it. | 3 |
| `TrapdoorTrigger` | Opens a floor panel when the player walks over it. | 15 |
| `EndingSequence` | Five seconds outside, gunshot, white flash, black, Employee of the Month. | 31 |
| `BackgroundMusic` | Loops a level's music and carries it across day changes. | all |
| `OfficeCeilingLights` | Fits a grid of recessed LED panels under a ceiling. | all |
| `StatefulDayButton` | Resolves the day by the colour the button is showing when pressed. | 4, 13, 17-19 |

### Judging a button by the colour it is showing

For any button whose colour changes, add `StatefulDayButton` next to its `ButtonInteractable` and `ButtonAppearance`, instead of wiring `CompleteDay` or `FailDay` by hand. What the player sees is then what they get:

- Pressed while it **looks green**, it completes the day.
- Pressed while it **looks red**, it restarts the day.
- Pressed while it shows the **grey** disabled look, nothing happens.

So the shy button advances the day if you can hit it before your stare turns it red, the timed button only counts during its green window, and the compass button depends on which way you are facing. One button, both outcomes.

- **Day:** the `DayLevel` to resolve. Found automatically when left empty.
- **Green Threshold:** how green it must look to count, default halfway.
- **On Pressed While Green / On Pressed While Red:** extra wiring for sounds or dialogue. These fire even with no day attached, so the sandbox can use them.

A sleeping button's wake-up press never resolves the day; it wakes up green, so the next press is the one that counts.

### The sleeping button in order

1. It starts **grey**, whatever the saved scene says, because `WakeableButton` claims the look during setup. A grey button also never dings.
2. The **first press** clicks, wakes it, and turns it **green**. It does not advance the day.
3. The **next press** is a normal green press: it clicks, dings, and completes the day.

Set **Presses To Wake** above one to require several dead presses, or **Wakes Up As** to 0 to have it wake up red, which then restarts the day when pressed.

Notes for building days with these:

- `ButtonAppearance` owns its own material copy, so tinting one button never recolours the others. Drive it with `Greenness` (0 red, 1 green), `SetRed()`, `SetGreen()`, or `IsDisabledLook`.
- A button's colour is only its appearance. What it does still comes from its `On Pressed` wiring, which is what makes the mislabelled and repainted days work.
- `ButtonMover` belongs on the button assembly's root, not the cap, so the whole pedestal moves. Its chase mode presses against the player through the existing contact system.
- `WakeableButton` uses `SuppressEvents`, so the first press still clicks and feels physical but does not reach the level wiring.
- `TalkingButton` and `ButtonSign` draw with Unity's default label text, the same as the day titles and the opening.

### Button sound

`ButtonSound` uses the **state captured before click callbacks run**, not the colour after a wake-up callback. A completed click that began active and green dings; grey, suppressed and partial clicks only play the physical click. `StatefulDayButton` uses the same snapshot and green threshold for its outcome.

- **Press Clip / Press Volume:** the click every button makes.
- **Green Clip / Green Volume / Green Delay:** the ding, played after the click so the press lands first.
- **Ding Only When Green:** uncheck to also ding on a completed active red click. This never overrides grey/suppressed/incomplete clicks.
- **Red Clip / Red Volume / Red Delay:** an optional sound for pressing it while red.
- **Spatial Blend / Min Distance / Max Distance:** how the sound sits in the room.

A button with a `ButtonAppearance` is judged by its current colour; one without is treated as green, so plain green buttons ding as before. A button showing the grey disabled look never dings. Give a red button a `ButtonAppearance` set to red if you want it to stay silent while still holding the ding clip.

### Configuring each button type

Every button type exposes its full settings in the inspector, grouped under headings. Select the button's cap object to edit them.

- **`ButtonGazeColour`:** hover delay, blend duration, aim tolerance, max watch distance, the exact watched and unwatched colours, recovery delay, a latch so it never recovers, plus heading reference, sweep angle and inversion for the compass mode. Events fire when it becomes watched or unwatched.
- **`ButtonColourSchedule`:** red and green durations, blend duration, start delay, starting colour, a switch limit, whether it ignores pause, and events for each turn.
- **`ButtonMover`:** speed, start delay, facing, chase stop distance, lose-interest distance, close-in speed, orbit radius and speed, patrol offset, pause and space, height locking, and a reached-player event.
- **`TalkingButton`:** the lines themselves as multi-line text, seconds per line, looping, start delay, what triggers it, trigger distance, aim tolerance, retriggering, a single voice clip or one clip per line, volume, spatial blend, text size, screen position, colour, a hide-text option for voice-only, and started/finished events.
- **`CursorRepellingButton`:** push strength, aim tolerance, max distance, vertical push, whether it ramps up with closeness, an attract-instead option, and a safe distance so it stays pressable up close.
- **`ButtonSign`:** the text, uppercase option, colour, height and offset, base font size, visible distance, constant-screen-size and minimum size options, occlusion hiding, and hiding after the press.
- **`WakeableButton`:** presses needed, asleep prompt, the colour it wakes as, wake delay, an asleep sound, and events for waking and for a press that did nothing.
- **`ButtonAppearance`:** the red, green and disabled colours, starting greenness, a disabled start, additional renderers to tint together, and an emission strength for dim rooms.

### Pressed indicator

Each button has its own indicator settings, so the debug light can be switched off per button:

- **Show Pressed Indicator:** uncheck to keep the indicator hidden. The button still works and still raises `On Pressed`.
- **Pressed Indicator:** the object to show. Leave it empty for no indicator at all.

The indicator starts hidden, appears when the button is pressed, and hides again on `ResetButton()`. Toggling **Show Pressed Indicator** at runtime applies immediately. The generated day scenes ship with indicators off; `TestScene` has them on.

`SetInteractable(bool)` can lock/unlock a button. `ResetButton()` clears the click counters, one-shot state and cooldown without overriding the interactable setting. Disabling the component or its GameObject also prevents interaction.

### Collision-based button presses

**Press On Contact is enabled by default**, including on existing buttons. The first-person controller automatically adds `PlayerButtonContact` at runtime, so the saved floor-button test scene and existing player prefabs do not need to be rebuilt or rewired.

- Falling onto, standing on, walking into, or being touched by a moving button invokes the same `On Pressed` event as E. Looking at the button is not required.
- A button presses **once per continuous contact**. Move fully away and touch again to press it again. Standing still does not repeatedly fire the event when cooldown expires.
- Locked, disabled, cooling-down and one-shot buttons respect their existing rules. An initially unavailable button is retried while touching, until it can accept its first contact press.
- E and physical contact on the same frame do not double-fire, even at zero cooldown. E still works normally while you are touching a button.
- Multiple colliders belonging to one button count as one contact. Only the contacted collider and its parents are searched: touching a pedestal does not activate a separate child button.
- Trigger volumes, ignored collider pairs, and layer pairs disabled in the Physics collision matrix do not press buttons. Other game objects bumping a button do not count as player contact.
- Uncheck **Press On Contact** for a button that should only respond to E.
- `ResetButton()` resets availability, not the player's current contact latch. After a successful contact press, leave and re-touch if you want a reset button to press physically again.

The contact detector combines CharacterController hit reports with a capsule overlap check in `LateUpdate`. The overlap follows the player's body, not the camera, with a 1 cm contact tolerance for floating-point/physics contact gaps. It catches incoming moving buttons even when the controller has not moved, and synchronizes Transform changes before checking. It is not a proximity interaction or a swept detector for objects teleported completely through the player between frames. For moving buttons, prefer a kinematic Rigidbody moved with `MovePosition` during `FixedUpdate`, using speeds/physics steps that preserve contact. Do not add a Rigidbody to the CharacterController player.

Disabling the first-person controller, its CharacterController, or its PlayerInteractor stops contact presses. Setting `CharacterController.detectCollisions` to false also stops them.

For a door or another type of object, derive from `Interactable`:

```csharp
public sealed class DoorInteractable : BigRedButton.Interactable
{
    public override string Prompt => "Open door";

    public override void Interact(BigRedButton.PlayerInteractor player)
    {
        gameObject.SetActive(false); // Replace with your door animation.
    }
}
```

## Start menu

`Assets/Scenes/Start Menu.unity` is the front page and **scene 0 in build settings**, so a build and the Editor's Play button both begin there. It shows the game's name, **EMPLOYEE OF THE MONTH**, over a plain dark page with the company's name beneath it.

- **START** loads `Day 1`, which plays the `Opening.mp3` black-screen narration itself, then fades into the room and the `DAY 1` title. Enter, Space or the gamepad's south button also start it.
- **TEAM PLAYER MODE** is the switch under START. With it on, **touching or pressing a red button sends you back to Day 1** instead of repeating the day you were on. Off by default, which keeps the original behaviour of repeating the current day.
- **QUIT** exits the build, or leaves play mode in the Editor.

START always begins a new month at day one, so returning to the menu never resumes a half-finished run. The menu releases the gameplay cursor lock, so the pointer is usable.

### Editing the menu

Select the **Start Menu** object:

- **Game Title / Subtitle** and their height fractions and colours. The title scales with screen height, so it stays prominent on a phone and a monitor. Text is plain and centred, like the day titles.
- **Start Label / Quit Label / Team Player Label / Team Player Note:** the wording, including the one-line explanation under the switch.
- **Start At Day:** which day START loads. Leave it at 1 so the opening plays.
- **Team Player Mode By Default:** the switch's state when the menu opens.
- **Fade In Duration** and **Keyboard And Gamepad Can Start**.
- **On Start Pressed:** an event for a click sound or an animation.

### Menu commands

- **Tools > Big Red Button > Build Start Menu** regenerates the scene and puts it first in build settings.
- **Make Start Menu The First Scene** re-registers it as scene 0 and as the Play Mode start scene, without touching the scene itself.
- **Play From The Open Scene Instead** clears the Play Mode start scene, so pressing Play tests whichever day you have open. Builds still start at the menu. Use this while building a single level, then switch back.

Refreshing the day scene list and rebuilding Days 6-31 both keep the menu at index 0.

### Team Player Mode in code

`GameSettings.TeamPlayerMode` holds the switch for the session, next to `DayFlow`'s day progress; neither is written to disk. `GameSettings.DayAfterFailure(currentDay)` returns the day a failure sends the player to, and `DayLevel.FailDay()` uses it. `TeamPlayerModeChanged` is raised when the value actually changes, for a HUD or a sound.

The mode only changes **failure**. Completing a day still advances by one, and green buttons are unaffected. It applies to any route into `FailDay()`, including red buttons resolved by colour and red buttons pressed by physical contact.

## Day system (levels)

Each day is its own scene named `Day 1`, `Day 2`, and so on. A day scene shows **DAY N** in large letters, fades it out, and loads the next day when the day is completed.

### Build the month (30 button days plus the Day 31 ending)

The game starts at the **start menu**, not at a day. See [Start menu](#start-menu).


Select **Tools > Big Red Button > Rebuild Days 6-31 (Keep Days 1-5)**. It regenerates `Assets/Scenes/Days/Day 6.unity` through `Day 31.unity` and rewrites `Assets/Scenes/TestScene.unity` as a mechanics sandbox. **Days 1–5 are protected:** their scene files are never regenerated. All 31 days remain registered in build settings in numeric order. Press **Play** from `Day 1` and each day leads to the next.

The rebuild also leaves existing shared materials unchanged, including shaders, colours, textures and smoothness. Missing materials are created using defaults. If any protected scene is missing, the rebuild cancels before making changes: restore that scene from Git or a backup. There is no force-overwrite option for Days 1–5.

Every generated day is an ordinary scene. Open any of them and move buttons, resize the room, change the sign text, retime the colour cycles, or replace a whole layout in the inspector. Nothing about a day is locked.

**Re-running the command overwrites only `Day 6-31` and `TestScene`.** Back up hand-edited work in those scenes before rebuilding. Days 1–5 can be edited normally in the Editor without the rebuild replacing them. If Unity prompts you to save an already-open modified scene, that is saving your own edits, not regenerating it.

- **Opening:** the game starts on a fully black screen and plays `Assets/Audio/Opening.mp3`. `DO NOT PRESS THE BIG RED BUTTON` appears centred at **10.28 seconds**, when the line is spoken, then the black fades away onto the room and `DAY 1` fades in. The player cannot move until the screen clears.
- **The red button** repeats the current day, so pressing it never advances the game. Any button showing green completes the day.

| Day | What it is | Built from |
| --- | --- | --- |
| 1 | Red button, green directly to its left | README |
| 2 | Red button in front, green a walk away | README |
| 3 | Green button hidden for 5 seconds | README |
| 4 | Red button turns green for 5 seconds, then back | README |
| 5 | Green button signed `DO NOT PRESS` | README |
| 6 | Thirty red buttons, one green | README |
| 7 | Green button pushes your aim away | README |
| 8 | Green button begs you to stop pressing it | README |
| 9 | Green button promises a secret ending | README |
| 10 | Buttons painted with the other's name | README |
| 11 | Green button offers a high score | README |
| 12 | Green button poses the trolley problem | README |
| 13 | Green button is grey until woken | README |
| 14 | Maze of red buttons, one green at the end | README |
| 15 | W deletes the floor, dropping you onto a big red button | README |
| 16 | Red and green look identical | README |
| 17 | Buttons have repainted each other | README |
| 18 | Green button reddens when you stare at it | README |
| 19 | Colour follows which way you face | README |
| 20 | Red buttons follow you around | README |
| 21 | Green button hides behind your back | README |
| 22-30 | A working day with one red and one green button | placeholder |
| 31 | The ending: no buttons, only a door | README |

Days 22 to 30 are not described in the design yet, so each is generated as a plain, completable day for you to turn into its own idea. A month of work, then it is over.

### Day 7: magnetic aim

`CursorRepellingButton` now feeds yaw and pitch into `FirstPersonController` after input look and before movement/interaction. Pitch persists instead of being overwritten next frame. Default strength remains 210 degrees/second (3× the original). Day 7 has **Safe Distance = 0**, so magnetism does not disappear when you approach. Physical contact still works normally. Escape, disabled player control and pausing prevent aim deflection.

### Day 15: the forward-triggered floor

`ForwardTrapFloor` is attached to the actual **Floor** object in the saved scene and the builder. A fresh press of **W or the Up arrow** disables the floor's renderer/collider immediately, then destroys that object. It is not a second panel sitting on a solid floor. Gamepad forward also triggers it after returning the stick to neutral. Paused/unfocused input does not trigger it.

All three inputs have their own Inspector toggles on **Floor → Forward Trap Floor**: **W Key Triggers**, **Up Arrow Triggers** and **Gamepad Forward Also Triggers**. Turn any of them off if you want a narrower trap.

The pit and its large red contact button cover the chamber underneath. The green button is within E range from spawn, so look at it and press E without moving forward. A successful green press disarms the floor during the day transition. Optional collapse sound, volume and On Collapsed event are editable on Floor. Day 15 no longer uses `TrapdoorTrigger`; that component is retained for other custom scenes.

### Day 21: cornering the behind-you button

`ButtonMover` in **Stay Behind Player** mode now sweeps the whole assembly against solid walls. It slides along a single wall and stops when a corner blocks both directions. It never teleports through the wall to reach its desired orbit position, so steering it into a corner leaves its cap reachable for E or normal contact interaction.

Select **Green Button assembly → Button Mover** to edit **Collide With Walls**, **Wall Layers** and **Wall Skin**. Trigger volumes, the button's own colliders, and the player are excluded from wall blocking; player contact still presses the button normally. The sweep covers the pedestal/housing too and prevents large movement steps from tunnelling through thin walls. Chase and Patrol behaviour is unchanged.

Both saved Day 21 and the sneaking button in TestScene are configured; the builder preserves that setup.

### Day 31, the ending

The last day has **no buttons at all**. Walk through the open doorway onto open grass. The camera renders the Fantasy Skybox, and the ground is a wide **500 × 500 metre** plane textured with `Assets/LevelMaterials/Outside Grass.mat`, which tiles the pack's grass diffuse and normal maps.

Crossing the outside trigger (centre z=9, radius 1.5m) gives **five seconds of free exploration**: a taste of the outside before it is taken away. Then a **gunshot** plays once with a **white flash** (default 0.12 seconds), followed by a fast transition to **black** (0.06 seconds). Control freezes at the shot, not when stepping outside. After a short black hold, the plain centered **Employee of the Month** card fades in with the worker's name.

The exploration timer starts only once, does not restart if you move away or return indoors, and pauses while gameplay is paused or the first-person controller has released input (Escape/focus loss).

#### Step-by-step: adding the skybox and the outside patch

The land patch and the ten-second timer are **already in the saved `Day 31` scene**, so after pulling you only need the skybox. The Fantasy Skybox pack cannot be committed to this repository (Asset Store licence), so it has to be imported on your machine once.

**1. Import the pack**

1. Open <https://assetstore.unity.com/packages/2d/textures-materials/sky/fantasy-skybox-free-18353> while signed in and press **Add to My Assets**.
2. In Unity, open **Window > Package Manager**, switch the dropdown at the top left to **My Assets**, and search for `Fantasy Skybox FREE`.
3. Press **Download**, then **Import**. Leave everything ticked in the import dialog and press **Import** again. It lands in `Assets/Fantasy Skybox FREE/`.

**2. Point Day 31 at a sky**

1. In the Project window, open `Assets/Fantasy Skybox FREE/Materials/Classic` (or `Panoramic`) and click a daytime material, for example `FS000_Day_01`. Make sure you select the **material**, not the texture.
2. Run **Tools > Big Red Button > Day 31 > Apply Selected Skybox Material**.

That copies your choice to `Assets/LevelMaterials/Day 31 Skybox.mat`, assigns it as the scene skybox, and sets the camera to **Skybox** clear flags. It saves `Day 31` and touches nothing else: no day is rebuilt, and no other level changes. Re-run it any time with a different material selected to change the sky. Later full rebuilds reuse the same configured material.

If you skip this, Day 31 still works and is completable; you simply get Unity's default blue-grey sky instead of the pack's.

**3. Try it**

Press **Play** from `Day 31`, walk out of the doorway, and you get ten seconds to wander the patch and look at the sky before the gunshot.

**Adjusting the outside yourself:** select **Outside ground** to resize the land, or the five **Outside bank** boxes to move the invisible edges. Set **Day > Ending Sequence > Exploration Duration** to change the ten seconds. If you enlarge the patch a lot, raise that duration too, or the ending will arrive before the player reaches the far side.

Keep the imported texture dependencies installed locally; do not publish the vendor pack as a standalone download.

- **Doorway Centre / Radius / Height Tolerance:** the outside area that starts the exploration timer. `Begin()` can also be called from a trigger or door animation.
- **Exploration Duration:** clear-view free exploration before the gunshot; default **5 seconds**.
- **Gunshot / Gunshot Volume / Flash Duration:** assigned to `Assets/Audio/gunshot.mp3`; plays once with a full white flash (default **0.12 seconds**). **On Gunshot** also cuts the ending music.
- **Fade To Black Duration / Dark Hold:** flash-to-black transition (default **0.06 seconds**) and pause before the card (default **0.5 seconds**).
- **Card Text / Fade In / Size / Colour:** the closing card. It accepts `{WORKER-FIRSTNAME}` and `{WORKER-LASTNAME}`.
- **Player / Frozen During Ending:** optional references, found automatically when empty. Control is disabled at the gunshot/flash, never when stepping outside.
- **On Ending Started / On Gunshot / On Fade Started / On Card Shown:** separate Inspector events for exploration, the shot/flash, the blackout transition and the card.

### Background music

Every day scene's **Day** object carries a `BackgroundMusic` component. Days 1-30 hold `Assets/Audio/Corporate Background Music.mp3`. Day 31 holds `Assets/Audio/Happy Ending Music.mp3`, which cross-fades in over the corporate track as the player steps outside, then **stops dead on the gunshot**: Day 31's `On Gunshot` event calls `BackgroundMusic.StopImmediately`, so the silence lands with the shot rather than fading afterwards.

The track **loops, and does not restart between days**. The first day creates a separate `Background Music` object, marks that object `DontDestroyOnLoad`, and plays through it. Every later day hands its settings to the running player and then does nothing further.

The component must **never** persist or destroy its own GameObject: it shares the **Day** object with `DayLevel`, `DayTitle`, `TimedReveal` and `OpeningSequence`. An earlier version did exactly that, which carried a stale `DayLevel` into the next day and deleted later days' logic, so buttons stopped resolving and Day 3's green button never appeared. `DayRegressionTests` now guards this.

- A day holding a **different** clip cross-fades to it, so the ending's music will replace the corporate track rather than layering over it.
- A day holding **no** clip fades the music out, for a level that should be silent.
- Starting a new month from the menu calls `BackgroundMusic.ClearPersistent()`, so the track begins from the top rather than resuming mid-loop.

Inspector settings on **Day > Background Music**:

- **Music / Volume / Loop:** the clip, its settled volume (default 0.32, low enough that button clicks stay audible), and whether it repeats.
- **Start Delay:** Day 1 uses **20 seconds** so the music does not talk over the opening narration. Later days use 0.
- **Fade In Duration / Cross Fade Duration:** the initial ramp, and the fade used when swapping or stopping a track.
- **Continue Across Days:** uncheck for music that belongs to a single scene, which then never claims the shared player.
- **Ignore Pause / Play On Start.**

`Stop()` (fade out), `StopImmediately()` (hard cut) and `SetVolume(float)` can be wired to any day's events. They work from a day's own component even after it has handed playback to the persistent carrier, so wiring them in the Inspector behaves as expected.

### LED office lights

Every ceiling in every scene carries `OfficeCeilingLights`, which builds a centred grid of recessed LED panels from the ceiling's own size. The panels glow cool white, and the middle ones also cast real point lights, so a room reads as fitted-out office rather than an evenly-lit void.

Menu commands:

- **Tools > Big Red Button > Add LED Office Lights To Every Level** fits or refits every scene under `Assets/Scenes/`, editing them in place. It does **not** regenerate any day, so hand-made layouts and the finished Days 1-5 survive. Run it again after resizing a room to refit that room.
- **Add LED Office Lights To Current Scene** does only the open scene; save afterwards.
- **Remove LED Office Lights From Every Level** strips them again.

Newly generated rooms are already fitted, so rebuilding Days 6-31 needs no extra step.

Select a **Ceiling** object to edit its fittings:

- **Ceiling / Drop Below Ceiling / Edge Margin:** which slab to line, how far the panels hang, and how far they stay from the walls.
- **Spacing X / Spacing Z / Panel Size / Max Panels:** the grid pitch, the fitting size, and a hard cap so a huge surface cannot spawn thousands of panels.
- **Light Colour / Emission / Show Housing:** the office white, how brightly the panel face glows, and the thin frame around each one.
- **Cast Light / Light Intensity / Light Range / Max Real Lights:** real point lights and their cap, since forward rendering has a per-object light limit. Panels beyond the cap still glow.

The panels and housings have **no colliders**, so they never block movement or an E raycast, and their lights cast no shadows. `Build()` replaces the previous fittings instead of stacking more, so re-running is safe.

The rooms are otherwise plain and quiet, with panel trim and no decorative props: closer to a clean test chamber than a dressed set. Days 6, 14, 20 and 21 use a wider open hall so their crowds and moving buttons have floor space.

`Assets/Scenes/TestScene.unity` is the sandbox: the same wiring as a real day, plus jump platforms, a floor button, and visible pressed indicators. Completing it reloads itself instead of advancing, so it stays available for testing.

### Create further days

1. Open the gameplay scene you want to use as the first day, such as `TestScene`.
2. Select **Tools > Big Red Button > Create Next Day Scene**. This copies the current/latest day into `Assets/Scenes/Days/Day 1.unity`, adds the `Day` object, and registers the scene in build settings. It never overwrites your original scene.
3. In the new scene, select the **Day** object and connect the day's **correct** button: on that button's `On Pressed`, add `DayLevel.CompleteDay`. Optionally connect the wrong button to `DayLevel.FailDay`, which repeats the same day.
4. Run **Create Next Day Scene** again for each following day. Each new day starts as a copy of the previous day, so shared setup and wiring carry over; then change that day's layout.
5. Press **Play** from `Day 1`. Completing a day loads the next one.

To convert a scene you already built by hand, open it, rename it `Day <number>`, and select **Tools > Big Red Button > Set Up Current Scene As A Day**. If you add or rename day scenes outside the menu, run **Tools > Big Red Button > Refresh Day Scene List** so build settings match. The refresh keeps your non-day scenes enabled and logs any gap, such as a missing `Day 4`.

To extend past day 30, use **Create Next Day Scene**, which copies the last day as a starting point.

### DayLevel inspector settings

- **Day Number:** which day the scene is. It sets the on-screen number and which day loads next.
- **Delay Before Next Day:** seconds after the outcome before the next day loads; default 1.
- **On Day Started / On Day Completed / On Day Failed:** hooks for audio, dialogue, or animation.
- **Delay Before Next Day** also applies to a failure, including the trip back to Day 1 in Team Player Mode.
- **On Final Day Completed:** raised instead of loading when no later day scene exists. Use it for the ending.

### Opening sequence

`OpeningSequence` sits on the `Day` object in `Day 1`. It holds the screen fully black, plays the narration, reveals the warning text, then fades out and raises **On Opening Finished**, which plays the day title.

- **Opening Narration:** the clip to play, normally `Assets/Audio/Opening.mp3`. The timing follows the clip's real length.
- **Warning Text / Text Start Delay / Text Reveal Duration:** what appears and when, so the words land with the voice line. Defaults are 10.28 s and 4 s.

- **Hold After Narration / Fade Out Duration:** black-screen hold and fade, default 1.2 s and 3 s.
- **Frozen During Opening:** the player controller, disabled until the screen clears.
- `Finish()` ends the opening early, for a skip button.

To put the opening in another scene, add `OpeningSequence` next to a `DayTitle`, uncheck the title's **Play On Start**, and connect **On Opening Finished** to `DayTitle.PlayCurrentDay`.

### Hiding a button for a few seconds

`TimedReveal` hides a target object and reveals it after a delay; Day 3 uses it with a 5 second delay. Put it on an object that stays active, such as the `Day` object, and set **Target** to the button assembly.

`CompleteDay()` advances, `FailDay()` repeats the day, and `CompleteDayImmediately()` skips the delay. Only the first outcome in a day applies, so extra presses during the transition are ignored.

### DayTitle inspector settings

- **Fade In / Hold / Fade Out Duration:** default 0.4 s, 1.6 s, 1.2 s.
- **Text Height Fraction:** title height relative to screen height, so it stays large on phones and monitors; default 0.16.
- **Text Color** and **Backdrop Opacity:** default white text over a 55% dark backdrop.

The title uses unscaled time, so it still fades if a day sets `Time.timeScale` to zero. It draws with Unity's built-in IMGUI, so no fonts, Canvas, or TextMeshPro assets are required.

### Day order and progression in code

`DayFlow` holds the progression: `CurrentDay`, `HighestDayReached`, `LoadDay(int)`, `LoadNextDay()`, `ReloadCurrentDay()`, and `ResetToFirstDay()`. The `DayStarted` and `SequenceCompleted` events are available for menus or dialogue. Scene names come from `SceneNameFormat` (`"Day {0}"` by default). Progress lives in memory for the session and is not saved to disk. A day scene reports its own number on start, so opening any day scene in the Editor and pressing Play works while building that level.

## Tests and verification

Open **Window > General > Test Runner**, choose **EditMode**, and run `BigRedButton.Tests.FirstPersonTests`. These tests cover action availability, press interactions, raycast range, wall occlusion, stale targets, disabled/locked objects, child colliders, ignored triggers, cooldowns, and one-shot resets.

`BigRedButton.Tests.DayFlowTests` covers day order, missing days, reloads, and rejected day numbers.

`BigRedButton.Tests.RebuildProtectionTests` checks that the rebuild plan contains exactly Days 6–31, excludes each protected day, and reuses an existing material without changing its settings or saved bytes.

`BigRedButton.Tests.DayChainTests` checks the built chain: that days 1 to 31 exist with no gaps, that they are registered in numeric order, that walking from day one reaches the ending, that each scene holds exactly one correctly numbered `DayLevel`, and that every day has some way to complete it. These tests skip themselves if the days have not been built yet.

`BigRedButton.Tests.OpeningAndIndicatorTests` covers the indicator toggle, the black-screen opening, its text reveal, restoring player control, and `TimedReveal`.

`BigRedButton.Tests.ButtonTypeTests` covers the shared press sound, suppressed events, per-button colour instancing, the timed and gaze-driven colour changes, the wake-up button, the chasing and patrolling movers, the talking button, and the magnetic push.

`BigRedButton.Tests.StatefulDayButtonTests` covers colour-based outcomes: green completes, red repeats, grey does nothing, the same button flipping outcome as its colour changes, the halfway threshold, and the sleeping button's wake-up press not ending the day.

`BigRedButton.Tests.DayRegressionTests` guards the day object: the music never persists, destroys or reparents it; a later day keeps its own `DayLevel` and `TimedReveal`; Day 3's green button still appears; green still advances and red still restarts on a later day; only one `DayLevel` exists after a scene change; the music survives a day being unloaded; and Day 1's opening still releases the player.

`BigRedButton.Tests.BackgroundMusicTests` covers the looping track, a new day not restarting or rewinding it, thirty days keeping one audio source, surviving its own day object, an empty slot fading the music out, a different clip cross-fading, the menu restarting it, Day 1's narration delay, and scene-only music never persisting.

`BigRedButton.Tests.StartMenuTests` covers the title text, START loading Day 1 so the opening plays, always restarting the month, starting only once, staying usable when day scenes are missing, the Team Player switch, and the cursor unlock.

`BigRedButton.Tests.TeamPlayerModeTests` covers the mode being off by default, a red press on a deep day returning to Day 1, the same press only repeating the day with the mode off, completion being unaffected, physical red-button contact also restarting the month, and one outcome per day.

`BigRedButton.Tests.CeilingLightTests` covers panels hanging below the ceiling and inside the room, the absence of colliders on every fitting, the real-light cap, safe rebuilding, and the cool-white glow.

`BigRedButton.Tests.EndingSequenceTests` covers the exploration window before the shot, a single gunshot/white flash/black sequence, the outside trigger, no timer restart, pause handling, freezing at the shot, restoring controls, zero-duration settings and the Employee of the Month card.

`DialogueProgressionTests` covers blocking day completion until the last line is acknowledged, parent-mounted dialogue, automatic first-line triggers, cooldown/one-shot/wake behaviour, no timed skipping, reset, and the real DayLevel load hook. `ButtonWallCollisionTests` covers thin walls, sliding, corner pinning with a clickable cap, triggers, floors, wall masks and the collision toggle.

`BigRedButton.Tests.WakeableButtonTests` covers the sleeping button: starting grey rather than red, waking to green without advancing the day, the next press advancing it, multiple wake presses, waking up red, and suppression holding for the press that lifts it.

`BigRedButton.Tests.ButtonClickSequenceTests` covers both sound/wake component orders, disabled/suppressed clicks with the ding override, N-click completion, one-shot sleeping buttons, per-click dialogue (including final/wake clicks), cooldown rejection, contact clicks, popup expiry, and shared sound/outcome snapshots with custom thresholds.

`BigRedButton.Tests.ButtonSoundStateTests` covers the ding following the state: plain green buttons, a button that turns green, grey buttons staying silent, the timed green window, the always-ding option, and the halfway threshold.

Select **PlayMode** in the Test Runner and run `BigRedButton.Tests.DayLevelTests` for the title fade, day advancement from a button press, single-outcome handling, delays, failure repeats and the final-day event. Also run `BigRedButton.Tests.ButtonContactTests` for landing, standing, side contact, incoming Transform/kinematic button movement, contact re-arming, shared cooldown/one-shot rules, duplicate prevention, ignored collisions, child colliders, crowded overlap buffers, pause behavior and safe player disabling from a button event.

Manual Play Mode checklist:

- Walking and running work in every direction; diagonal speed matches forward speed.
- Looking up/down stops at the pitch limit; looking up does not make walking fly.
- Space jumps only when grounded; holding it does not repeatedly jump.
- Walls block movement; jumping into a low ceiling stops the ascent.
- E presses buttons within range, with no interaction through walls.
- Land on a floor button and stand still: exactly one event fires. Step off and return: another event fires (unless one-shot or still cooling down).
- Walk into a button from the side, then move a button horizontally against an idle player: both press it without E or looking at it.
- Touch the pedestal without touching its separate button: no press. Disable Press On Contact: E works but touching does not.
- Escape releases the cursor and stops input; clicking resumes without an E-key interaction. Physical contact still works because releasing the cursor does not pause the world.
- Day 1 starts fully black with narration, shows the warning text, fades in, then shows **DAY 1**.
- Starting a day shows large **DAY N** text that fades out and does not block later gameplay.
- Pressing green ends the day and loads the next one; pressing red repeats the same day.
- Day 3's green button appears after 5 seconds.
- Unchecking **Show Pressed Indicator** hides the light while the button still works.
- Every accepted button press clicks. Completing the required active clicks while green also dings, including a timed-green button. Grey, wake-up and partial clicks never ding.
- The magnetic button shoves the view hard; it is still pressable when you get close to it.
- In `TestScene`, walk the showcase row: the timed button cycles colour, the shy button reddens when stared at, the compass button changes with your heading, the sleeping button needs two presses, the magnetic button shoves your aim, the talking button starts speaking as you approach, and the chasing and sneaking buttons move.
- Press a showcase button while it looks green: the day completes. Press one while it looks red: the day restarts. The Console logs each press.
- The sleeping button starts grey and silent. Press it once: it clicks and turns green, and the day does not change. Press it again: it dings and the day completes.
- The corporate track fades in after the Day 1 narration and keeps playing across every day change without restarting. It goes quiet on Day 31.
- The game opens on the start menu. START plays the Day 1 opening. With Team Player Mode on, a red button on any day returns you to Day 1; with it off, the day repeats.
- On Day 31, explore the outside patch for ten seconds, then hear one gunshot with a white flash, followed by black and Employee of the Month. Controls freeze at the shot.
- Every room has lit LED panels overhead. Walking and pressing E under a panel is unaffected by it.
- On Day 15, both **W** and the **Up arrow** delete the floor and drop you onto the big red button.
- The final day raises **On Final Day Completed** instead of loading a missing scene.
- Alt-tab releases the cursor; returning does not unexpectedly capture it.
- Gamepad sticks, hold-to-run, jump, and interaction work; switching input updates the prompt hint.
- Disabling/re-enabling the controller releases/recaptures the cursor cleanly.

**Validation status:** The implementation was authored in a sandbox without the Unity Editor. Unity compilation, automated Unity tests, and Play Mode checks must still be run in the target editor; static file checks alone do not establish gameplay correctness.
