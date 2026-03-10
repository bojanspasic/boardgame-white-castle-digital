# Feature
When game starts, it enters main menu. Main menu has The White Castel ascii art on screen, and allows selection of players (number of players, human/ai).

# Status
TO DO

# Acceptance criteria
* When game starts, the contents of [title text](title.txt) are displayed.
* Text "SELECT PLAYERS" is displayed below and centered in its row.
* Player selection rows are displayed one below another and the rows are centered: 
    - Text "PLAYER 1 [ ]" is displayed in blue color
    - Text "PLAYER 2 [ ]" is displayed in blue red
    - Text "PLAYER 3 [ ]" is displayed in blue green
    - Text "PLAYER 4 [ ]" is displayed in blue yellow
* by default row with PLAYER 1 is marked with "<" right of the text
* arrows up/down move marker "<" up and dow
* SPACE key results in the following order (subsequent SPACES sircle through them):
    - putting char "H" beween brackets of the marked row (e.g. "PLAYER 1 [X]") - mark of a human player
    - putting char "A" beween brackets of the marked row (e.g. "PLAYER 1 [ ]") - mark of an AI player
    - empties brackets for the marked row (e.g. "PLAYER 1 [ ]) - player not selected.
* Below all that display centered text "Use arrow keys UP/DOWN and SPACE to select. Press ENTER to continue"
* ENTER key accepts the selection and moves forward
* At least two players must be selected to proceed, otherwise display info "At least two players must be selected to proceed.\nPress ENTER to continue" as an overlay framed in ascii-frame. Enter dismisses the overlay and goes back to arrow-selection of players
