# Description
The position of player selection menu should be in the middle of the screen, instead of being displayed under the title message.

# Status
COMPLETED

# Acceptance criteria
* In [MainMenu.cs](/src/BoardWC.Console.UI/MainMenu.cs) the menu should be rendered from the position 0,0, e.g. with SetCursorPosition(0,0) instead of being displayed below the title text.
* The menu should be displayed in the screen-centered box, the same way as the Ai thinking message is displayed ([in AiThinkingOverlay.cs](/src/BoardWC.Console.UI/AiThinkingOverlay.cs))
* Make refactorings to ([AiThinkingOverlay.cs](/src/BoardWC.Console.UI/AiThinkingOverlay.cs)) to extract the box presentation logic, to be reusable across the console UI
