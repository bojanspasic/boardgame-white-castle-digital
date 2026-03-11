# Description
Spinner in the [AiThinkingOverlay.cs](/src/BoardWC.Console.UI/AiThinkingOverlay.cs) is not spinning. It always displays the same character and there is no progression in the animation.
Perhaps it is a theading issue?

# Status
COMPLETED

# Acceptance criteria
* In [AiThinkingOverlay.cs](/src/BoardWC.Console.UI/AiThinkingOverlay.cs) the spinner works by alternating characters in the spinner sequence.
* The characters have to advance through the sequence instead of keeping the single character displayed as it is the case now.
