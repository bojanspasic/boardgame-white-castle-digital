# about

I am exploring the capabilities of Claude Code by creating a digital version of The White Castle board game. So I'll start with greenfield project and have Claude Code generate everything.

I suspect that I will not physically be able to review everything it creates, so I'll have to trust it in some aspects.

Prior to any code generation, I am running planning mode. I requested it to plan a C# application that will have all the rules and engine within a library, so it can be reused in a different form of visualization.

# the first run

Plan actually turned out to be pretty decent. However, Claude got the game rules all wrong. Anyhow, I decided to go with it, so we'll update it as we go.

# specifying the rules

By specifying the rules, I led Claude to shape the game properly. First impressions are pretty good. It builds a console interface, which I did not specify, apart for telling it to make a console interface. This is a pretty nice side-effect, so I don't have to waste time on throwaway code.

Claude responds pretty well to hard-set rules. CLAUDE.md requires that gamerules.md are updated as we go, so the knowledge is preserved. So far so good. Will check the coverage and actual tests down the line.

# cleaned up garbage behvavior

In the first pass I decided to let Claude implement the rules as it knows it. It turned to be completely wrong. After several itreations of adding and changing rules, I decided to instruct Claude to remove the remaining wrong rules, which it did in one pass.

# refining the console ui

Since my goal is to dewelop a resuable game engine library, I never exactly specified how the throwable console UI should look like and I let Claude decide. After several iterations it became cluttered, which is expected, since I paid no attention to it. But, since I need to actually be able to test the dehavior myself, I ended up with unusable UI. I instruced Claude to rebuild it and after 3 iterations got what I want.

# tests

As planned I did not push Claude to create tests. Initially I asked it to do so, but afterwards I did not insist. Upon implementing all the rules, I instructed it to review all the rules and create a test suite for the 100% code coverage for the engine (console code coverage is irrelevant atm).
After first run it added 111 tests (in addition to existing 66), and even though I asked for 100% coverage it stopped there. When asked about the coverage it admitted that it was significantly less.
Additionally, one of the tests was failing, even though Claude claimed that all the tests passed. It should not be left unattended. Subagent might resolve this state autonomously though - need to pay attention to this.
...
It proposed to exclude some paths from tests (which is a bit smelly). Let's see where it will end and see what was actually excluded.
...
Claude managed to come up with 333 tests, but coverage is still less than 80% (for some files is at 0%). I need to step in and micromanage it at this point. 
...
Well, this was a baby-sitting activity, but it managed to get code coverage right.

# tests excluded from code coverage

Surprisingly, only three methods are excluded from coverage, with rationale that those are instances of Default _ arn and unreachable anyways due to datasets, which is acceptable at this point.

# rename in documents and in code

In one go, with help with tests, Claude was able to make large-scale ambiguous rename effortlessly.

# go over documents and code and look for tbds and deferreds

Done in one pass, but consumed insanely large amount of tokens. Wanted to execute a Python script to do it, but I wanted it to do it on its own, without Python, so I denied its request to execute it.

# code review

I asked Claude to do a code review, which it did, and had some interesting findings, especially in test code. Again, I am not looking at the code and I am trusting it to do the job.
It uncovered that game AI lies about what it is, so we'll have to deal with that as well.
However it seems that it broke the logic during the process. All the tests (420 in total) are passing, so there is that. Will have to fix the logic to be able to complete the round.

It’s like the fox guarding the henhouse.

# the larger the codebase the bigger the burn

Tokens are burning at incredible rate now. Without any guardrails it keeps burning like crazy

# mutation testing (1st pass)
Overall mutation score: 65.27% (1945 mutants, 736 killed, 307 survived, 119 timeout)

Rating	Files
🟢 100%	ResourceBag, GameState, LanternHelper, IActionHandler
🟢 90%+	ScoreCalculator (93%), InfluenceHelper (94%), PassHandler (91%)
🟡 75-82%	CastlePlayHandler, TrainingGroundsHandler, StartGameHandler, FarmHandler
🟠 55-70%	PlaceDieHandler (55%), CardFieldHelper (69%), PostActionProcessor (61%)
🔴 Critical	ChoosePersonalDomainRowHandler (0% — compile error), LanternChain (0%), PersonalDomain (5%), Player (13%), CompositeActionHandler (29%)

## TODO
Add PlayAnyWhite to the cards
Correct inland farm cards json and training grounds token JSON 
Add CRAP 
Add more cards and logic for 3-4 players
Make better AI
Speed up