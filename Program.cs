using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BabbleAI;
using Newtonsoft.Json;

namespace BabbleSolver
{
    class Program
    {
        static void Main(string[] args)
        {
            Solver solver;
            using (var reader = new StreamReader("Dictionary.json"))
            {
                var json = reader.ReadToEnd();
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                solver = new Solver(data.Keys.ToArray());
            }

            while (true)
            {
                // The file first contains the board, with _ representing blank spaces
                // Then a representation of the board, where a number represents players.
                // 0 is no player, 1 is the current player and 2+ is other players
                // The player mask is needed in order to evaluate which moves are kills
                var board = Board.Load("TestBoard.txt");
                Solver.PrintBoard(board);

                Console.Write("Enter hand:");
                var hand = Console.ReadLine();
                
                // Adding bombs significantly slows down the solver, due to many new paths having to be evaluated
                Console.Write("Bombs: ");
                var bombs = int.Parse(Console.ReadLine());
                var moves = solver.GetMoves(board, hand, bombs);
                foreach (var move in moves)
                {
                    if (move.Any(m => m.Kill))
                    {
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (move.Any(m => m.HandAfterMoveLength == 0))
                    {
                        Console.BackgroundColor = ConsoleColor.DarkBlue;
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.BackgroundColor = ConsoleColor.Black;
                    }

                    Console.WriteLine(string.Join(" ", move.Select(m => m.ToString())));
                }
            }
        }
    }
}
