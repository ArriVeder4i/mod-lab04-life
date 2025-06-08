using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace cli_life
{
    public class Settings
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int CellSize { get; set; }
        public double LiveDensity { get; set; }
        public int SleepInterval { get; set; }
        public string PatternsDirectory { get; set; }
        public int StabilizationWindow { get; set; } = 5;
    }

    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> Neighbors = new List<Cell>();
        private bool nextState;

        public void DetermineNextState()
        {
            int live = Neighbors.Count(n => n.IsAlive);
            nextState = IsAlive ? (live == 2 || live == 3) : (live == 3);
        }
        public void Advance() => IsAlive = nextState;
    }

    public class Board
    {
        public Cell[,] Cells;
        public int Columns => Cells.GetLength(0);
        public int Rows => Cells.GetLength(1);

        public Board(Settings s)
        {
            Cells = new Cell[s.Width / s.CellSize, s.Height / s.CellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();
            ConnectNeighbors();
            Randomize(s.LiveDensity);
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                {
                    int[] dx = { -1, 0, 1 }, dy = { -1, 0, 1 };
                    foreach (var i in dx)
                        foreach (var j in dy)
                        {
                            if (i == 0 && j == 0) continue;
                            int nx = (x + i + Columns) % Columns;
                            int ny = (y + j + Rows) % Rows;
                            Cells[x, y].Neighbors.Add(Cells[nx, ny]);
                        }
                }
        }

        public void Randomize(double density)
        {
            var rnd = new Random();
            foreach (var c in Cells)
                c.IsAlive = rnd.NextDouble() < density;
        }

        public void Advance()
        {
            foreach (var c in Cells) c.DetermineNextState();
            foreach (var c in Cells) c.Advance();
        }

        public void Render()
        {
            Console.Clear();
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                    Console.Write(Cells[x, y].IsAlive ? '*' : ' ');
                Console.WriteLine();
            }
        }

        public void SaveState(string path)
        {
            using var w = new StreamWriter(path);
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                    w.Write(Cells[x, y].IsAlive ? '1' : '0');
                w.WriteLine();
            }
        }
        public void LoadState(string path)
        {
            var lines = File.ReadAllLines(path);
            for (int y = 0; y < Math.Min(Rows, lines.Length); y++)
            {
                var line = lines[y];
                for (int x = 0; x < Math.Min(Columns, line.Length); x++)
                    Cells[x, y].IsAlive = line[x] == '1';
            }
        }

        public (int single, int clusters) CountElements()
        {
            bool[,] visited = new bool[Columns, Rows];
            int singles = 0, clustersCount = 0;
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                {
                    if (!Cells[x, y].IsAlive || visited[x, y]) continue;
                    var size = FloodFill(x, y, visited);
                    if (size == 1) singles++; else clustersCount++;
                }
            return (singles, clustersCount);
        }
        private int FloodFill(int sx, int sy, bool[,] visited)
        {
            var stack = new Stack<(int x, int y)>();
            stack.Push((sx, sy));
            visited[sx, sy] = true;
            int count = 0;
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop(); count++;
                int[] dx = { -1, 0, 1 }, dy = { -1, 0, 1 };
                foreach (var i in dx)
                    foreach (var j in dy)
                    {
                        int nx = (x + i + Columns) % Columns;
                        int ny = (y + j + Rows) % Rows;
                        if ((i != 0 || j != 0) && !visited[nx, ny] && Cells[nx, ny].IsAlive)
                        {
                            visited[nx, ny] = true;
                            stack.Push((nx, ny));
                        }
                    }
            }
            return count;
        }

        public int GenerationsToStabilize(int window)
        {
            var history = new Queue<int>();
            int gens = 0;
            while (true)
            {
                gens++;
                Advance();
                int alive = Cells.Cast<Cell>().Count(c => c.IsAlive);
                history.Enqueue(alive);
                if (history.Count > window) history.Dequeue();
                if (history.Count == window && history.All(v => v == history.Peek())) break;
            }
            return gens;
        }
    }

    class Program
    {
        static Settings s;
        static Board b;

        static void Main(string[] args)
        {
            s = JsonSerializer.Deserialize<Settings>(File.ReadAllText("Settings.json"));
            b = new Board(s);

            if (args.Length > 0)
                b.LoadState(args[0]);

            Console.WriteLine("Modes: R(run), A(analyze), Q(quit)");
            var mode = Console.ReadKey(true).Key;
            if (mode == ConsoleKey.A)
            {
                Analyze();
                return;
            }
            RunLoop();
        }

        static void RunLoop()
        {
            bool paused = false;
            while (true)
            {
                if (!paused) b.Advance();
                b.Render();
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).Key;
                    if (k == ConsoleKey.S) { b.SaveState("saved_state.txt"); Console.WriteLine("Saved"); }
                    if (k == ConsoleKey.L) { b.LoadState("saved_state.txt"); Console.WriteLine("Loaded"); }
                    if (k == ConsoleKey.P) paused = !paused;
                    if (k == ConsoleKey.Q) break;
                }
                Thread.Sleep(s.SleepInterval);
            }
        }

        static void Analyze()
        {
            using var data = new StreamWriter("data.txt");
            for (double d = 0.1; d <= 0.9; d += 0.2)
            {
                b.Randomize(d);
                int gens = b.GenerationsToStabilize(s.StabilizationWindow);
                data.WriteLine($"{d:F2}\t{gens}");
                Console.WriteLine($"Density {d:F2}: {gens} gens");
            }
            Console.WriteLine("Data saved to data.txt");
        }
    }
}
