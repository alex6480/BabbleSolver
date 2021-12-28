Hey Guys

Author of "the Box" here. I promise I won't use it any more. It was only created for the technical challenge in creating a fast algorithm for finding words according to the rules of the games. We achieved that goal, so you won't see the bot again. Unless of course you implement bot-only lobbies. Then I'd be happy to compete.

This repo is the search algorithm used for finding moves. The code for reading the board state and playing moves won't be shared to prevent copycats.

This program can find all possible moves for a given board state in Babble.
It can find roughly 30000 moves in a second (on my laptop) depending on the complexity of the search. It is capable of chaining moves and it can even evaluate when bombs are played, but this greatly increases the search complexity and slows down the search. It also evaluates for each move in the tree whether it is a kill.

The algorithm in this repo only finds possible moves. The algorithm for actually chosing moves has worked a few different ways the last few days. From simply prioritizing emptying the hand so it can play as many moves as quickly as possible, to deliberately killing people.

If you figure out how teleportation works, I'd like to know too.

Sorry for the inconvenience. You won't meet The Box any more.
Good job, to those who managed to kill it!

Best regards
The Box