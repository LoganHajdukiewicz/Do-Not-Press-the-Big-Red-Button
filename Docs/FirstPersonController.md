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
3. Set **Prompt**, **Interactable**, **One Shot**, **Press On Contact**, and **Cooldown** as needed.
4. Connect **On Pressed** in the Inspector to your level logic, animation, AudioSource.Play, etc.

Red and green buttons share the same interaction code; their event wiring determines what they do. No win/fail or day progression is hard-coded into the controller. In the generated days, the green button's `On Pressed` calls `DayLevel.CompleteDay` and the red button's calls `DayLevel.FailDay`.

### Button types

These components cover the tricks in the design. Add them to a button's cap, alongside its `ButtonInteractable`. `TestScene` has one of each, labelled, so every behaviour can be tried in one room.

| Component | What it does | Days |
| --- | --- | --- |
| `ButtonSound` | Plays the press click on every button, plus an optional extra layer such as the green ding. | all |
| `ButtonAppearance` | Sets the colour a button *looks* like, separately from what it does: red, green, a blend, or grey. | 4, 10, 13, 16-19 |
| `ButtonColourSchedule` | Switches the look between red and green on a timer. | 4 |
| `ButtonGazeColour` | Colour reacts to the player's view: turns red when watched, or follows their heading. | 18, 19 |
| `ButtonSign` | A word painted above the button, in the company's lettering. | 5, 10, 17 |
| `TalkingButton` | Shows lines of dialogue when approached, looked at, or at day start. | 8, 9, 11, 12 |
| `WakeableButton` | Starts grey and inert; the first press only wakes it up. | 13 |
| `CursorRepellingButton` | Pushes the player's aim away as they try to point at it. | 7 |
| `ButtonMover` | Chases the player, stays behind their back, or patrols between two points. | 20, 21 |
| `TimedReveal` | Hides a button for a few seconds, then reveals it. | 3 |

Notes for building days with these:

- `ButtonAppearance` owns its own material copy, so tinting one button never recolours the others. Drive it with `Greenness` (0 red, 1 green), `SetRed()`, `SetGreen()`, or `IsDisabledLook`.
- A button's colour is only its appearance. What it does still comes from its `On Pressed` wiring, which is what makes the mislabelled and repainted days work.
- `ButtonMover` belongs on the button assembly's root, not the cap, so the whole pedestal moves. Its chase mode presses against the player through the existing contact system.
- `WakeableButton` uses `SuppressEvents`, so the first press still clicks and feels physical but does not reach the level wiring.
- `TalkingButton` and `ButtonSign` draw with the shared `CorporateText` look, so signage, dialogue, the day titles and the opening all match.

### Pressed indicator

Each button has its own indicator settings, so the debug light can be switched off per button:

- **Show Pressed Indicator:** uncheck to keep the indicator hidden. The button still works and still raises `On Pressed`.
- **Pressed Indicator:** the object to show. Leave it empty for no indicator at all.

The indicator starts hidden, appears when the button is pressed, and hides again on `ResetButton()`. Toggling **Show Pressed Indicator** at runtime applies immediately. The generated day scenes ship with indicators off; `TestScene` has them on.

`SetInteractable(bool)` can lock/unlock a button. `ResetButton()` clears the one-shot state and cooldown without overriding the interactable setting. Disabling the component or its GameObject also prevents interaction.

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

## Day system (levels)

Each day is its own scene named `Day 1`, `Day 2`, and so on. A day scene shows **DAY N** in large letters, fades it out, and loads the next day when the day is completed.

### Build the first playable days

Select **Tools > Big Red Button > Build Days 1-3**. It generates `Assets/Scenes/Days/Day 1-3.unity` from the README design, registers them in build settings, and rewrites `Assets/Scenes/TestScene.unity` as a mechanics sandbox. Press **Play** from `Day 1`.

- **Opening:** the game starts on a fully black screen and plays `Assets/Audio/Opening.mp3`. `DO NOT PRESS THE BIG RED BUTTON` appears centred at **10.28 seconds**, when the line is spoken, then the black fades away onto the room and `DAY 1` fades in. The player cannot move until the screen clears.
- **Day 1:** a big red button with the green button directly to its left. The green button dings and ends the day.
- **Day 2:** the red button is directly in front of the player; the green button is a walk away behind a divider.
- **Day 3:** the green button is hidden for 5 seconds, then appears.
- **The red button** repeats the current day, so pressing it never advances the game.

The rooms are plain, quiet, and evenly lit, with panel trim and no decorative props: closer to a clean test chamber than a dressed set. The narration, button audio and the title are the only things competing for attention. Re-running the command regenerates these scenes, so keep your own level work in separate scenes or under different day numbers.

`Assets/Scenes/TestScene.unity` is the sandbox: the same wiring as a real day, plus jump platforms, a floor button, and visible pressed indicators. Completing it reloads itself instead of advancing, so it stays available for testing.

### Create further days

1. Open the gameplay scene you want to use as the first day, such as `TestScene`.
2. Select **Tools > Big Red Button > Create Next Day Scene**. This copies the current/latest day into `Assets/Scenes/Days/Day 1.unity`, adds the `Day` object, and registers the scene in build settings. It never overwrites your original scene.
3. In the new scene, select the **Day** object and connect the day's **correct** button: on that button's `On Pressed`, add `DayLevel.CompleteDay`. Optionally connect the wrong button to `DayLevel.FailDay`, which repeats the same day.
4. Run **Create Next Day Scene** again for each following day. Each new day starts as a copy of the previous day, so shared setup and wiring carry over; then change that day's layout.
5. Press **Play** from `Day 1`. Completing a day loads the next one.

To convert a scene you already built by hand, open it, rename it `Day <number>`, and select **Tools > Big Red Button > Set Up Current Scene As A Day**. If you add or rename day scenes outside the menu, run **Tools > Big Red Button > Refresh Day Scene List** so build settings match. The refresh keeps your non-day scenes enabled and logs any gap, such as a missing `Day 4`.

### DayLevel inspector settings

- **Day Number:** which day the scene is. It sets the on-screen number and which day loads next.
- **Delay Before Next Day:** seconds after the outcome before the next day loads; default 1.
- **On Day Started / On Day Completed / On Day Failed:** hooks for audio, dialogue, or animation.
- **On Final Day Completed:** raised instead of loading when no later day scene exists. Use it for the ending.

### Opening sequence

`OpeningSequence` sits on the `Day` object in `Day 1`. It holds the screen fully black, plays the narration, reveals the warning text, then fades out and raises **On Opening Finished**, which plays the day title.

- **Opening Narration:** the clip to play, normally `Assets/Audio/Opening.mp3`. The timing follows the clip's real length.
- **Warning Text / Text Start Delay / Text Reveal Duration:** what appears and when, so the words land with the voice line. Defaults are 10.28 s and 4 s.
- **Letter Spacing:** how far apart the letters sit. The warning and the day titles are uppercase, bold, letter-spaced and shadowed, in off-white on black, so they read as printed company notices rather than game UI.
- **Hold After Narration / Fade Out Duration:** black-screen hold and fade, default 1.2 s and 3 s.
- **Frozen During Opening:** the player controller, disabled until the screen clears.
- `Finish()` ends the opening early, for a skip button.

To put the opening in another scene, add `OpeningSequence` next to a `DayTitle`, uncheck the title's **Play On Start**, and connect **On Opening Finished** to `DayTitle.PlayCurrentDay`.

### Hiding a button for a few seconds

`TimedReveal` hides a target object and reveals it after a delay; Day 3 uses it with a 5 second delay. Put it on an object that stays active, such as the `Day` object, and set **Target** to the button assembly.

`CompleteDay()` advances, `FailDay()` repeats the day, and `CompleteDayImmediately()` skips the delay. Only the first outcome in a day applies, so extra presses during the transition are ignored.

### DayTitle inspector settings

- **Fade In / Hold / Fade Out Duration:** default 0.4 s, 1.6 s, 1.2 s.
- **Text Height Fraction:** title height relative to screen height, so it stays large on phones and monitors; default 0.13.
- **Letter Spacing:** spacing between letters; default 3, which gives the flat corporate look.
- **Text Color** and **Backdrop Opacity:** default off-white ink over a 55% dark backdrop.

The title uses unscaled time, so it still fades if a day sets `Time.timeScale` to zero. It draws with Unity's built-in IMGUI, so no fonts, Canvas, or TextMeshPro assets are required.

### Day order and progression in code

`DayFlow` holds the progression: `CurrentDay`, `HighestDayReached`, `LoadDay(int)`, `LoadNextDay()`, `ReloadCurrentDay()`, and `ResetToFirstDay()`. The `DayStarted` and `SequenceCompleted` events are available for menus or dialogue. Scene names come from `SceneNameFormat` (`"Day {0}"` by default). Progress lives in memory for the session and is not saved to disk. A day scene reports its own number on start, so opening any day scene in the Editor and pressing Play works while building that level.

## Tests and verification

Open **Window > General > Test Runner**, choose **EditMode**, and run `BigRedButton.Tests.FirstPersonTests`. These tests cover action availability, press interactions, raycast range, wall occlusion, stale targets, disabled/locked objects, child colliders, ignored triggers, cooldowns, and one-shot resets.

`BigRedButton.Tests.DayFlowTests` covers day order, missing days, reloads, and rejected day numbers.

`BigRedButton.Tests.OpeningAndIndicatorTests` covers the indicator toggle, the black-screen opening, its text reveal, restoring player control, and `TimedReveal`.

`BigRedButton.Tests.ButtonTypeTests` covers the shared press sound, suppressed events, per-button colour instancing, the timed and gaze-driven colour changes, the wake-up button, the chasing and patrolling movers, the talking button, the magnetic push, and the letter-spacing helper.

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
- Every button clicks when pressed; the green button clicks *and* dings.
- In `TestScene`, walk the showcase row: the timed button cycles colour, the shy button reddens when stared at, the compass button changes with your heading, the sleeping button needs two presses, the magnetic button shoves your aim, the talking button starts speaking as you approach, and the chasing and sneaking buttons move.
- The final day raises **On Final Day Completed** instead of loading a missing scene.
- Alt-tab releases the cursor; returning does not unexpectedly capture it.
- Gamepad sticks, hold-to-run, jump, and interaction work; switching input updates the prompt hint.
- Disabling/re-enabling the controller releases/recaptures the cursor cleanly.

**Validation status:** The implementation was authored in a sandbox without the Unity Editor. Unity compilation, automated Unity tests, and Play Mode checks must still be run in the target editor; static file checks alone do not establish gameplay correctness.
