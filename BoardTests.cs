using NUnit.Framework;
using cli_life;
using System;
using System.IO;
using System.Linq;

namespace cli_life.Tests
{
    [TestFixture]
    public class BoardTests
    {
        private Settings s;
        private Board board;

        [SetUp]
        public void Setup()
        {
            s = new Settings { Width = 10, Height = 10, CellSize = 1, LiveDensity = 0, StabilizationWindow = 3 };
            board = new Board(s);
        }

        [Test]
        public void ConnectNeighbors_CreatesEightNeighborsForCenterCell()
        {
            Assert.AreEqual(8, board.Cells[5, 5].Neighbors.Count);
        }

        [Test]
        public void Advance_StillLifeBlock_RemainsSame()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Patterns", "block.txt");
            Assert.IsTrue(File.Exists(path), $"File not found: {path}");
            board.LoadState(path);
            board.Advance();
            var (singles, clusters) = board.CountElements();
            Assert.AreEqual(0, singles);
            Assert.AreEqual(1, clusters);
        }

        [Test]
        public void SaveLoad_PreservesState()
        {
            board.Randomize(0.5);
            var tmp = Path.GetTempFileName();
            board.SaveState(tmp);
            var copy = new Board(s);
            copy.LoadState(tmp);
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                    Assert.AreEqual(board.Cells[x, y].IsAlive, copy.Cells[x, y].IsAlive);
            File.Delete(tmp);
        }

        [Test]
        public void SaveState_WritesCorrectNumberOfLines()
        {
            board.Randomize(0);
            var tmp = Path.GetTempFileName();
            board.SaveState(tmp);
            var lines = File.ReadAllLines(tmp);
            Assert.AreEqual(board.Rows, lines.Length);
            File.Delete(tmp);
        }

        [Test]
        public void LoadState_InvalidPath_ThrowsFileNotFoundException()
        {
            var invalid = Path.Combine(TestContext.CurrentContext.TestDirectory, "Patterns", "nofile.txt");
            Assert.Throws<FileNotFoundException>(() => board.LoadState(invalid));
        }

        [Test]
        public void CountElements_EmptyBoard_ReturnsZero()
        {
            Assert.AreEqual((0, 0), board.CountElements());
        }

        [Test]
        public void CountElements_SingleCell_ReturnsOneSingle()
        {
            board.Cells[0, 0].IsAlive = true;
            Assert.AreEqual((1, 0), board.CountElements());
        }

        [Test]
        public void CountElements_MultipleIsolatedCells_ReturnsCorrectSingles()
        {
            board.Cells[0, 0].IsAlive = true;
            board.Cells[2, 2].IsAlive = true;
            Assert.AreEqual((2, 0), board.CountElements());
        }

        [Test]
        public void CountElements_ClusterOfSizeTwo_ReturnsOneCluster()
        {
            board.Cells[0, 0].IsAlive = true;
            board.Cells[0, 1].IsAlive = true;
            Assert.AreEqual((0, 1), board.CountElements());
        }

        [Test]
        public void GenerationsToStabilize_EmptyBoard_StabilizesWithWindowOne()
        {
            int gens = board.GenerationsToStabilize(1);
            Assert.AreEqual(1, gens);
        }

        [Test]
        public void Randomize_DensityZero_NoLiveCells()
        {
            board.Randomize(0);
            Assert.AreEqual(0, board.Cells.Cast<Cell>().Count(c => c.IsAlive));
        }

        [Test]
        public void Randomize_DensityOne_AllCellsAlive()
        {
            board.Randomize(1);
            Assert.AreEqual(board.Columns * board.Rows, board.Cells.Cast<Cell>().Count(c => c.IsAlive));
        }

        [Test]
        public void DetermineNextState_BirthOccursProperly()
        {
            var cell = board.Cells[4, 4];
            cell.IsAlive = false;
            for (int i = 0; i < 3; i++) cell.Neighbors[i].IsAlive = true;
            cell.DetermineNextState(); cell.Advance();
            Assert.IsTrue(cell.IsAlive);
        }

        [Test]
        public void DetermineNextState_SurvivalOccursProperly()
        {
            var cell = board.Cells[3, 3];
            cell.IsAlive = true;
            for (int i = 0; i < 2; i++) cell.Neighbors[i].IsAlive = true;
            cell.DetermineNextState(); cell.Advance();
            Assert.IsTrue(cell.IsAlive);
        }

        [Test]
        public void DetermineNextState_DeathByUnderpopulation()
        {
            var cell = board.Cells[2, 2];
            cell.IsAlive = true;
            cell.DetermineNextState(); cell.Advance();
            Assert.IsFalse(cell.IsAlive);
        }

        [Test]
        public void DetermineNextState_DeathByOverpopulation()
        {
            var cell = board.Cells[1, 1];
            cell.IsAlive = true;
            for (int i = 0; i < 4; i++) cell.Neighbors[i].IsAlive = true;
            cell.DetermineNextState(); cell.Advance();
            Assert.IsFalse(cell.IsAlive);
        }
    }
}
