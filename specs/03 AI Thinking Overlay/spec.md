# Feature
When AI calculates move, leave everything on screen and put thinking message in an overlay window.
Overlay window should be centered vertically as well as horizontally.
It should drop shaddow. Use Box Drawing unicode chars for frame of the message and BLock Elements for shaddow

# Status
COMPLETED

# Acceptance criteria
* When AI plays, the overlay message is shown.
* While AI is thinking a sort of alternating characters should be displayed in the same place before the text, creating looping in progress indicator illusion.
* Overlay should be drawn using U+2554, U+2550, U+2557, U+2551, U+255A, and U+255D
* Shaddow should be drawn using U+2592
* Example overlay box
╔══════════════════════════════════╗
║                                  ║
║   ╭  AI player is thinking       ║▒
║                                  ║▒
╚══════════════════════════════════╝▒
 ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒  