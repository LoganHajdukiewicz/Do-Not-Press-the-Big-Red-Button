# Do Not Press the Big Red Button

## First-person controller

Walking, running, jumping, mouse/gamepad look, and button interactions are implemented using the Unity Input System and a CharacterController. Buttons also press when the player physically touches them, including landing, standing, side contact, and incoming moving buttons. Each continuous contact fires once; E still works as before.

In Unity, choose **Tools > Big Red Button > Create Controller Test Scene**, save the new scene, and press Play. Controls: **WASD** move, **Shift** run, **Space** jump, **E** interact, **Esc** release/capture cursor.

## Day system

Each day is a level in its own scene (`Day 1`, `Day 2`, ...). A day shows **DAY N** in large letters, fades it out, and loads the next day when the green button is pressed. The red button repeats the day.

Select **Tools > Big Red Button > Build Days 1-30** to generate all thirty days from the design below, plus a mechanics sandbox in `TestScene`. Every day is an ordinary scene you can rearrange in the editor. Press **Play** from `Day 1`: the game opens on a black screen with `Opening.mp3`, the warning text appears as it is spoken, then the room fades in.

Add more days with **Tools > Big Red Button > Create Next Day Scene**, then connect the green button's `On Pressed` to `DayLevel.CompleteDay`.

See [controller and day system setup, customization, and tests](Docs/FirstPersonController.md).

## Game design

Each Day is a Level. 
(Robot Voice is RoboSoft 3 from: https://www.tetyys.com/SAPI4/)


OPEN:

Dialogue on a black screen: (OPENING.MP3)(Robotic Voice) HELLO WORKER-FIRSTNAME WORKER-LASTNAME. You are a valued member of the Sisyphusian Company Co. family. As a valued family member, you have one job. DO NOT PRESS THE BIG RED BUTTON. 

TEXT Shows up on screen as the words are spoken: DO NOT PRESS THE BIG RED BUTTON

Fade in looking at the big Red Button. 

Day 1: There is a big Red Button, directly to the left is a Green Button. Player presses the Green Button. A small ding! 

Day 2: The Red Button is directly in front of the player. The Green Button is farther away. 

Day 3: The Green Button is hidden for 5 seconds. 

Day 4: The Red Button turns green after 10 seconds. It switches back after 10 seconds. 

Day 5: The Green Button has a sign that says "DO NOT PRESS" on it. 

Day 6: 30 Red Buttons. 1 Green Button. 

Day 7: The Green Button pushes the mouse away magnetically. 

Day 8: The Green Button talks! It asks to not be pressed anymore! It asks for you to press the Big Red Button. 

Day 9: The Green Button states that pressing the Big Red Button will lead to a secret ending. 

Day 10: The Green Button has painted the words RED on it and painted the Red Button with the word Green

Day 11: The Green Button says that pressing the Red Button gives you a high score. 

Day 12: The Green Button says the trolley problem. It says that 1000 baby puppy kittens are tied to a train track and the only way to save them is to press the Red Button. 

Day 13: The Green Button is disabled, it is Grey, once clicked once it becomes green and allows you to click it. 

Day 14: There is a Maze of Red Buttons. Clicking any of them will fail. At the end there is a single Green Button.

Day 15: The Green Button is directly in front of you. If you click forward a trapdoor will open under you, causing you to fall onto a BIG RED BUTTON. 

Day 16: A Red/Green Colorblind filter is placed in front of a player. Causing Red and Green colors to look the same. 

Day 17: The Green Button has painted itself red and painted the Red Button green. 

Day 18: When you hover on the Green Button it turns red after 0.5 seconds.

Day 19: If you're facing north the button is red, as you turn around the button rotationally, by the time you are facing south, the button is fully green. 

Day 20: Red Buttons will follow you around. They will want to be pressed. 

Day 21: The Green Button spawns behind the player and tries to stay behind the player. As they turn to see it, it turns around them so it is behind them. The player must turn quickly and press the button. 

LAST LEVEL: There are no buttons. There is nothing to decide. For the first time, there is only a door. A door that leads to a beautiful outside. You step outside, for a second you are enveloped in sunshine, then you hear a gunshot, screen goes dark. Employee of the Year {WORKER-FIRSTNAME} {WORKER-LASTNAME}