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

Red and green buttons share the same interaction code; their event wiring determines what they do. No win/fail or day progression is hard-coded into the controller.

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

## Tests and verification

Open **Window > General > Test Runner**, choose **EditMode**, and run `BigRedButton.Tests.FirstPersonTests`. These tests cover action availability, press interactions, raycast range, wall occlusion, stale targets, disabled/locked objects, child colliders, ignored triggers, cooldowns, and one-shot resets.

Select **PlayMode** in the Test Runner and run `BigRedButton.Tests.ButtonContactTests` for landing, standing, side contact, incoming Transform/kinematic button movement, contact re-arming, shared cooldown/one-shot rules, duplicate prevention, ignored collisions, child colliders, crowded overlap buffers, pause behavior and safe player disabling from a button event.

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
- Alt-tab releases the cursor; returning does not unexpectedly capture it.
- Gamepad sticks, hold-to-run, jump, and interaction work; switching input updates the prompt hint.
- Disabling/re-enabling the controller releases/recaptures the cursor cleanly.

**Validation status:** The implementation was authored in a sandbox without the Unity Editor. Unity compilation, automated Unity tests, and Play Mode checks must still be run in the target editor; static file checks alone do not establish gameplay correctness.
