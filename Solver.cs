namespace BabbleAI
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class Solver
    {
        public HashSet<string> Words;
        public Dictionary<string, int> ContainsIndex;

        public Solver(string[] wordList)
        {
            SetDictionary(wordList);
        }

        public IEnumerable<Move[]> GetMoves(Board board, string hand, int bombCount = 0, int maxDepth = int.MaxValue)
        {
            var handBytes = new byte['z' - 'a' + 1];
            for (var i = 0; i < hand.Length; i++)
            {
                handBytes[hand[i] - 'a']++;
            }
            return GetMoves(board, handBytes, bombCount, maxDepth);
        }

        public IEnumerable<Move[]> GetMoves(Board board, byte[] hand, int bombCount = 0, int maxDepth = int.MaxValue)
        {
            // Keep a stack of moves which should be searched for child moves
            var movesToProcess = new Stack<(Board board, byte[] hand, Move[] previousMoves, int bombs, int depth)>();
            movesToProcess.Push((board, hand, new Move[0], bombCount, 1));
            if (bombCount > 0)
            {
                movesToProcess.Push((board.Bomb(), hand, new Move[] { new Move { Bomb = true } }, bombCount - 1, 1));
            }

            var buffer = new char[Math.Max(board.Width, board.Height)];

            while (movesToProcess.Count > 0)
            {
                /*
                    Check for potential moves
                */
                var potentialMoves = new List<Move>();
                int depth;
                int bombs;
                Move[] previousMoves;
                (board, hand, previousMoves, bombs, depth) = movesToProcess.Pop();
                if (board.PlayerPosition.Horizontal)
                {
                    // Check for words along the current word
                    Array.Copy(board.Tiles, board.Width * board.PlayerPosition.Y, buffer, 0, board.Width);
                    var matches = MatchPattern(buffer, board.Width, hand, board.PlayerPosition.X);
                    potentialMoves.AddRange(matches.Select(match => new Move {
                        Horizontal = true,
                        Word = match.Word,
                        HandAfterMoveBytes = match.RemainingHand,
                        X = match.Start,
                        Y = board.PlayerPosition.Y,
                        Length = match.Word.Length,
                        Bomb = false,
                    }));
                    // Check for words perpendicular to the current word
                    for (var i = 0; i < board.PlayerPosition.Length; i++)
                    {
                        for (var j = 0; j < board.Height; j++)
                        {
                            buffer[j] = board.Tiles[board.PlayerPosition.X + i + j * board.Width];
                        }
                        matches = MatchPattern(buffer, board.Height, hand, board.PlayerPosition.Y);
                        var x = board.PlayerPosition.X + i;
                        potentialMoves.AddRange(matches.Select(match => new Move {
                            Horizontal = false,
                            Word = match.Word,
                            HandAfterMoveBytes = match.RemainingHand,
                            X = x,
                            Y = match.Start,
                            Length = match.Word.Length,
                            Bomb = false,
                        }));
                    }
                }
                else
                {
                    // Check for words along the current word
                    for (var i = 0; i < board.Height; i++)
                    {
                        buffer[i] = board.Tiles[board.PlayerPosition.X + i * board.Width];
                    }
                    var matches = MatchPattern(buffer, board.Height, hand, board.PlayerPosition.Y);
                    potentialMoves.AddRange(
                        matches.Select(match => new Move {
                            Horizontal = false,
                            Word = match.Word,
                            HandAfterMoveBytes = match.RemainingHand,
                            X = board.PlayerPosition.X,
                            Y = match.Start,
                            Length = match.Word.Length,
                            Bomb = false,
                        })
                    );
                    // Check for words perpendicular to the current word
                    for (var i = 0; i < board.PlayerPosition.Length; i++)
                    {
                        Array.Copy(board.Tiles, board.Width * (board.PlayerPosition.Y + i), buffer, 0, board.Width);
                        matches = MatchPattern(buffer, board.Width, hand, board.PlayerPosition.X);
                        var y = board.PlayerPosition.Y + i;
                        potentialMoves.AddRange(matches.Select(match => new Move {
                            Horizontal = true,
                            Word = match.Word,
                            HandAfterMoveBytes = match.RemainingHand,
                            X = match.Start,
                            Y = y,
                            Length = match.Word.Length,
                            Bomb = false,
                        }));
                    }
                }

                /*
                 * Verify moves
                 */
                var childMoveCount = 0;
                Move[] childMoves = new Move[potentialMoves.Count];
                for (var i = 0; i < potentialMoves.Count; i++)
                {
                    var potentialMove = potentialMoves[i];
                    (var isValid, var isKill) = PerpendicularMovesValid(board, potentialMove, buffer);
                    if (isValid)
                    {
                        potentialMove.Kill = isKill;
                        childMoves[childMoveCount++] = potentialMove;
                    }
                }

                /*
                 * Process child moves
                 */
                for (var i = 0; i < childMoveCount; i++)
                {
                    var childMove = childMoves[i];

                    // Calculate board after move
                    var newTiles = new char[board.Tiles.Length];
                    Array.Copy(board.Tiles, newTiles, newTiles.Length);
                    var newMask = new byte[board.PlayerMask.Length];
                    Array.Copy(board.PlayerMask, newMask, newMask.Length);
                    var nextBoard = new Board
                    {
                        Height = board.Height,
                        Width = board.Width,
                        Tiles = newTiles,
                        PlayerMask = newMask,
                        PlayerPosition = new WordPosition
                        {
                            Horizontal = childMove.Horizontal,
                            Length = childMove.Word.Length,
                            X = childMove.X,
                            Y = childMove.Y
                        }
                    };

                    
                    // Calculate new hand after move
                    for (var j = 0; j < childMove.Word.Length; j++)
                    {
                        var x = childMove.X + (childMove.Horizontal ? j : 0);
                        var y = childMove.Y + (childMove.Horizontal ? 0 : j);
                        var tile = nextBoard.Tiles[x + y * nextBoard.Width];
                        nextBoard.PlayerMask[x + y * nextBoard.Width] = 1;
                        nextBoard.Tiles[x + y * nextBoard.Width] = childMove.Word[j];
                    }

                    // Reset player mask
                    for (var j = 0; j < board.PlayerPosition.Length; j++)
                    {
                        if (board.PlayerPosition.Horizontal)
                        {
                            nextBoard.PlayerMask[board.PlayerPosition.X + j + board.PlayerPosition.Y * board.Width] = 0;
                        }
                        else
                        {
                            nextBoard.PlayerMask[board.PlayerPosition.X + (board.PlayerPosition.Y + j) * board.Width] = 0;
                        }
                    }

                    // Calculate new move chain that leads to the current position
                    var moveChain = new Move[previousMoves.Length + 1];
                    Array.Copy(previousMoves, 0, moveChain, 0, previousMoves.Length);
                    moveChain[moveChain.Length - 1] = childMove;
                    if (childMove.HandAfterMoveLength == 0 || depth == maxDepth)
                    {
                        // This chain does not have any child moves as the hand i empty (or max depth has been reached)
                        yield return moveChain;
                    }
                    else
                    {
                        // This chain could have child moves. These must be searched.
                        movesToProcess.Push((nextBoard, childMove.HandAfterMoveBytes, moveChain, bombs, depth + 1));
                        
                        if (bombs > 0)
                        {
                            // Queue a move with a bomb explosion as well
                            // Calculate new move chain that leads to the current position
                            var bombMoves = new Move[moveChain.Length + 1];
                            Array.Copy(moveChain, 0, bombMoves, 0, moveChain.Length);
                            bombMoves[bombMoves.Length - 1] = new Move {
                                Bomb = true,
                            };
                            movesToProcess.Push((nextBoard.Bomb(), childMove.HandAfterMoveBytes, bombMoves, bombs - 1, depth + 1));
                        }
                    }
                }
                if (childMoveCount == 0)
                {
                    // No child moves belong to the parent move. Return the parent
                    yield return previousMoves;
                }
            }
        }

        /// <summary>
        /// Returns true if all words perpendicular to the word played are valid
        /// </summary>
        private (bool moveValid, bool kill) PerpendicularMovesValid(Board board, Move move, char[] buffer = null)
        {
            // Create a buffer if one isn't provided
            if (buffer == null) buffer = new char[Math.Max(board.Width, board.Height)];

            // Verify that the move does not create invalid words
            var kill = false;
            var moveValid = true;
            for (var i = 0; i < move.Word.Length; i++)
            {
                var positionInBuffer = 0;
                var start = move.Horizontal ? move.Y : move.X;
                for (var j = -1; start + j >= 0; j--)
                {
                    var c = board.Tiles[move.X + (move.Horizontal ? i : j) + (move.Y + (move.Horizontal ? j : i)) * board.Width];
                    if (c == '_') break;
                    if (j == -1 && board.PlayerMask[move.X + (move.Horizontal ? i : j) + (move.Y + (move.Horizontal ? j : i)) * board.Width] > 1) kill = true;
                    buffer[positionInBuffer++] = char.ToLower(c);
                }
                // Reverse the letters in the beggining of the word as they were found backwards
                for (var j = 0; j < (int)(positionInBuffer * 0.5); j++)
                {
                    var temp = buffer[j];
                    buffer[j] = buffer[positionInBuffer - j - 1];
                    buffer[positionInBuffer - j - 1] = temp;
                }
                buffer[positionInBuffer++] = char.ToLower(move.Word[i]);
                if (board.PlayerMask[move.X + (move.Horizontal ? i : 0) + (move.Y + (move.Horizontal ? 0 : i)) * board.Width] > 1) kill = true;

                for (var j = 1; start + j + 1 < (move.Horizontal ? board.Height : board.Width); j++)
                {
                    var c = board.Tiles[move.X + (move.Horizontal ? i : j) + (move.Y + (move.Horizontal ? j : i)) * board.Width];
                    if (c == '_') break;
                    if (j == 1 && board.PlayerMask[move.X + (move.Horizontal ? i : j) + (move.Y + (move.Horizontal ? j : i)) * board.Width] > 1) kill = true;
                    buffer[positionInBuffer++] = char.ToLower(c);
                }

                if (positionInBuffer > 1)
                {
                    var word = new string(buffer, 0, positionInBuffer);
                    // Make sure that adjacent words can be played
                    if (!Words.Contains(word))
                    {
                        moveValid = false;
                        break;
                    }
                }
            }

            return (moveValid, kill);
        }

        public List<WordMatch> MatchPattern(char[] pattern, int length, byte[] hand, int playerStart)
        {
            var returnedMatches = new List<WordMatch>();

            // Find all gaps that need to be filled in the pattern
            var gaps = new List<int>();
            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '_')
                {
                    gaps.Add(i - playerStart);
                }
            }
            gaps = new List<int>(gaps.OrderBy(g => Math.Abs(g)));

            // Make a copy of the pattern
            var patternCopy = new char[length];
            Array.Copy(pattern, patternCopy, length);

            // Create bitmask from hand
            var handBitmask = 0;
            var letterBit = 1;
            for (var i = 0; i < hand.Length; i++)
            {
                if (hand[i] > 0) handBitmask |= letterBit;
                letterBit *= 2;
            }

            var attempts = new Stack<(char[] pattern, byte[] hand, int handLength, int handBitmask, IEnumerable<int> gaps)>(new [] { (
                patternCopy,
                hand,
                hand.Select(h => (int)h).Sum(),
                handBitmask,
                (IEnumerable<int>)gaps
            ) });
            while (attempts.Count > 0)
            {
                var currentAttempt = attempts.Pop();
                var positiveGap = currentAttempt.gaps.FirstOrDefault(g => g > 0);
                var positiveGap2 = currentAttempt.gaps.FirstOrDefault(g => g > 0 && g > positiveGap);
                var negativeGap = currentAttempt.gaps.FirstOrDefault(g => g < 0);
                var negativeGap2 = currentAttempt.gaps.FirstOrDefault(g => g < 0 && g < negativeGap);

                letterBit = 1;
                for (var i = 0; i <= 'z' - 'a'; i++)
                {
                    // This letter should not be searched any futher as the player either doesn't have it or
                    // no possible words in this branch contain it
                    if ((currentAttempt.handBitmask & letterBit) != letterBit)
                    {
                        letterBit *= 2;
                        continue;
                    }

                    var letter = (char)('a' + i);
                    if (positiveGap != 0)
                    {
                        currentAttempt.pattern[positiveGap + playerStart] = letter;
                        var start = negativeGap != 0 ? (playerStart + negativeGap + 1) : 0;
                        var end = positiveGap2 != 0 ? (playerStart + positiveGap2) : currentAttempt.pattern.Length;
                        var word = new string(currentAttempt.pattern, start, end - start);

                        var nextHand = new byte[currentAttempt.hand.Length];
                        Array.Copy(currentAttempt.hand, nextHand, nextHand.Length);
                        nextHand[letter - 'a'] = (byte)Math.Max(0, nextHand[letter - 'a'] - 1);
                        var nextBitmask = currentAttempt.handBitmask;
                        if (nextHand[letter - 'a'] == 0) nextBitmask &= ~letterBit;
                        if (Words.Contains(word))
                        {
                            var move = new WordMatch {
                                Start = start,
                                Word = word,
                                RemainingHand = nextHand,
                            };
                            returnedMatches.Add(move);
                        }
                        if (ContainsIndex.ContainsKey(word))
                        {
                            nextBitmask &= ContainsIndex[word];
                            if (nextBitmask != 0)
                            {
                                var newPattern = new char[currentAttempt.pattern.Length];
                                Array.Copy(currentAttempt.pattern, newPattern, newPattern.Length);
                                attempts.Push((
                                    newPattern,
                                    nextHand,
                                    currentAttempt.handLength - 1,
                                    nextBitmask,
                                    currentAttempt.gaps.Where(gap => gap != positiveGap).ToList()
                                ));
                            }
                        }
                    }

                    if (negativeGap != 0)
                    {
                        currentAttempt.pattern[negativeGap + playerStart] = letter;
                        var start = negativeGap2 != 0 ? (playerStart + negativeGap2 + 1) : 0;
                        var end = positiveGap != 0 ? (playerStart + positiveGap) : currentAttempt.pattern.Length;
                        var word = new string(currentAttempt.pattern, start, end - start);

                        var nextHand = new byte[currentAttempt.hand.Length];
                        Array.Copy(currentAttempt.hand, nextHand, nextHand.Length);
                        nextHand[letter - 'a'] = (byte)Math.Max(0, nextHand[letter - 'a'] - 1);
                        var nextBitmask = currentAttempt.handBitmask;
                        if (nextHand[letter - 'a'] == 0) nextBitmask &= ~letterBit;
                        if (Words.Contains(word))
                        {
                            var move = new WordMatch {
                                Start = start,
                                Word = word,
                                RemainingHand = nextHand
                            };
                            returnedMatches.Add(move);
                        }
                        if (ContainsIndex.ContainsKey(word))
                        {
                            nextBitmask &= ContainsIndex[word];
                            if (nextBitmask != 0)
                            {
                                var newPattern = new char[end];
                                Array.Copy(currentAttempt.pattern, 0, newPattern, 0, end);
                                attempts.Push((
                                    newPattern,
                                    nextHand,
                                    currentAttempt.handLength - 1,
                                    nextBitmask,
                                    currentAttempt.gaps
                                        .Where(gap => gap != negativeGap && gap < 0)
                                        .ToList()
                                ));
                            }
                        }
                    }
                    
                    letterBit *= 2;
                }
            }

            return returnedMatches;
        }
        public struct WordMatch
        {
            public string Word;
            public int Start;
            public byte[] RemainingHand;
        }

        public void SetDictionary(string[] wordList)
        {
            Words = new HashSet<string>(wordList);
            ContainsIndex = new Dictionary<string, int>();
            int progress = 0;
            foreach (var word in Words)
            {
                for (var i = 1; i <= word.Length; i++)
                {
                    for (var j = 0; j + i <= word.Length; j++)
                    {
                        var key = word.Substring(j, i);
                        if (! ContainsIndex.ContainsKey(key)) ContainsIndex[key] = 0;

                        var charIndex = ContainsIndex[key];
                        for (var k = 0; k < word.Length; k++)
                        {
                            charIndex |= (int)Math.Pow(2, word[k] - 'a');
                        }
                        ContainsIndex[key] = charIndex;
                    }
                }
                if (++progress % (int)(Words.Count / 100) == 0)
                {
                    Console.WriteLine($"Building index {(double)progress / (double)Words.Count() * 100.0:0}%");
                }
            }
        }

        public static void PrintBoard(Board board)
        {
            var colors = new ConsoleColor[] {
                ConsoleColor.Blue,
                ConsoleColor.Red,
                ConsoleColor.Magenta,
                ConsoleColor.Green,
                ConsoleColor.Cyan,
                ConsoleColor.DarkYellow,
                ConsoleColor.Yellow
            };

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var player = board.PlayerMask[x + y * board.Width];
                    if (player == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                    else if (player == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.DarkBlue;
                    }
                    else
                    {
                        Console.ForegroundColor = colors[(player - 2) % colors.Length];
                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                    Console.Write(board.Tiles[x + y * board.Width] + " ");
                }
                Console.WriteLine();
            }
        }
    }

    public struct Board
    {
        public int Width;
        public int Height;
        public char[] Tiles;
        public byte[] PlayerMask;
        public WordPosition PlayerPosition;

        public override string ToString()
        {
            var output = new StringBuilder();
            for (var i = 0; i < Height; i++)
            {
                output.AppendLine(new string(Tiles, i * Width, Width));
            }
            return output.ToString();
        }

        static string FormatHandBitmask(int bitmask)
        {
            var result = new char['z' - 'a'];
            var pow = 1;
            var letterCount = 0 ;
            for (var i = 0; i <= 'z' - 'a'; i++)
            {
                if ((bitmask & pow) == pow)
                {
                    result[letterCount] = (char)('a' + i);
                    letterCount++;
                }
                pow *= 2;
            }
            return new string(result, 0, letterCount);
        }

        /// <summary>Returns a new board where a bomb has been applied</summary>
        public Board Bomb()
        {
            var before = this;
            var bombedTiles = new char[before.Tiles.Length];
            Array.Copy(before.Tiles, bombedTiles, bombedTiles.Length);

            var xFrom = Math.Max(0, before.PlayerPosition.X - 3);
            var xTo = Math.Min(before.Width, before.PlayerPosition.X + 4 + (before.PlayerPosition.Horizontal ? before.PlayerPosition.Length - 1 : 0));
            var yFrom = Math.Max(0, before.PlayerPosition.Y - 2);
            var yTo = Math.Min(before.Height, before.PlayerPosition.Y + 4 + (before.PlayerPosition.Horizontal ? 0 : before.PlayerPosition.Length - 1));
            for (var x = xFrom; x < xTo; x++)
            {
                for (var y = yFrom; y < yTo; y++)
                {
                    var index = x + y * before.Width;
                    if (before.PlayerMask[index] == 0)
                    {
                        bombedTiles[index] = '_';
                    }
                }
            }

            var result = new Board {
                Height = before.Height,
                Width = before.Width,
                Tiles = bombedTiles,
                PlayerMask = before.PlayerMask,
                PlayerPosition = before.PlayerPosition
            };
            return result;
        }

        public static Board Load(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length % 2 != 0) throw new Exception("Uneven number of lines");
            if (lines.Select(line => line.Length).Distinct().Count() > 1) throw new Exception("Line length differs between lines");

            var board = new Board {
                Width = lines[0].Length,
                Height = (int)(lines.Length * 0.5),
                Tiles = new char[lines[0].Length * (int)(lines.Length * 0.5)],
                PlayerMask = new byte[lines[0].Length * (int)(lines.Length * 0.5)],
                PlayerPosition = new WordPosition {
                    Horizontal = false,
                    X = lines[0].Length,
                    Y = (int)(lines.Length * 0.5),
                    Length = 0,
                }
            };
            for (var x = 0; x < board.Width; x++)
            for (var y = 0; y < board.Height; y++)
            {
                board.Tiles[x + y * board.Width] = char.ToLower(lines[y][x]);
                if (! char.IsLetter(board.Tiles[x + y * board.Width]) && board.Tiles[x + y * board.Width] != '_') throw new Exception("Invalid letter");
                board.PlayerMask[x + y * board.Width] = byte.Parse(lines[y  + (int)(lines.Length * 0.5)][x].ToString());

                if (board.PlayerMask[x + y * board.Width] == 1)
                {
                    board.PlayerPosition.X = Math.Min(board.PlayerPosition.X, x);
                    board.PlayerPosition.Y = Math.Min(board.PlayerPosition.Y, y);
                    board.PlayerPosition.Length++;
                    board.PlayerPosition.Horizontal = y == board.PlayerPosition.Y;
                }
            }

            return board;
        }
    }

    public struct WordPosition
    {
        public int X;
        public int Y;
        public int Length;
        public bool Horizontal;
    }

    public struct Move
    {
        public int X;
        public int Y;
        public int Length;
        public bool Horizontal;

        public bool Kill;

        public bool Bomb;

        public byte[] HandAfterMoveBytes;

        public int HandAfterMoveLength => HandAfterMoveBytes?.Where(b => b > 0).Count() ?? -1;

        public string Word;

        public override string ToString()
        {
            if (Bomb) return "BOOM";
            return (Horizontal ? "->" : "\\/") + $"({X}, {Y})" + Word;
        }
    }
}