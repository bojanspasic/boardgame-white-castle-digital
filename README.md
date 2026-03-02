# about

I am exploring the capabilities fo Claude Code by creating a digital version of The White Castle board game. So I'll start with greenfield project and have Claude Code generate everything.

I suspect that I will not physically be able to review everything it creates, so I'll have to trust it in some aspects.

Prior to any code generation, I am running planning mode. I requested it to plan a C# application that will have all the rules and engine within a library, so it can be reused in a different form of visualization.

# the first run

Plan actually turned out to be pretty decent. However, Claude got the game rules all wrong. Anyhow, I decided to go with it, so we'll update it as we go.

# specifying the rules

By specifying the rules, I led Claude to shape the game properly. First impressions are pretty good. It builds a console interface, which I did not specify, apart for telling it to make a console interface. This is a pretty nice side-effect, so I don't have to waste time on throwaway code.

Claude responds pretty well to hard-set rules. CLAUDE.md requires that gamerules.md are updated as we go, so the knowledge is preserved. So far so good. Will check the coverage and actual tests down the line.

# refining the console ui

Since my goal is to dewelop a resuable game engine library, I never exactly specified how the throwable console UI should look like and I let Claude decide. After several iterations it became cluttered, which is expected, since I paid no attention to it. But, since I need to actually be able to test the dehavior myself, I ended up with unusable UI. I instruced Claude to rebuild it and after 3 iterations got what I want.