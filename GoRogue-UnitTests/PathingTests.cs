﻿using GoRogue;
using GoRogue.MapViews;
using GoRogue.MapGeneration.Generators;
using GoRogue.Pathing;
using GoRogue.Random;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using CA = EMK.Cartography;

namespace GoRogue_UnitTests
{
    [TestClass]
    public class PathingTests
    {
        static private readonly Coord END = Coord.Get(17, 14);
        static private readonly int ITERATIONS = 100;
        static private readonly int MAP_HEIGHT = 30;
        static private readonly int MAP_WIDTH = 30;
        static private readonly Coord START = Coord.Get(1, 2);

        [TestMethod]
        public void AStarMatchesCorrectChebyshev() => aStarMatches(Distance.CHEBYSHEV);

        [TestMethod]
        public void AStarMatchesCorrectEuclidean() => aStarMatches(Distance.EUCLIDEAN);

        [TestMethod]
        public void AStarMatchesCorrectManhattan() => aStarMatches(Distance.MANHATTAN);

        [TestMethod]
        public void ManualAStarChebyshevTest()
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.CHEBYSHEV);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void ManualAStarEuclidianTest()
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.EUCLIDEAN);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void ManualAStarManhattanTest()
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var pather = new AStar(map, Distance.MANHATTAN);
            var path = pather.ShortestPath(START, END);

            Utility.PrintHightlightedPoints(map, path.StepsWithStart);

            foreach (var point in path.StepsWithStart)
                Console.WriteLine(point);
        }

        [TestMethod]
        public void ManualDijkstraMapTest()
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var dijkstraMap = new DijkstraMap(map);
            dijkstraMap.AddGoal(MAP_WIDTH / 2, MAP_HEIGHT / 2);
            dijkstraMap.AddGoal(MAP_WIDTH / 2 + 5, MAP_HEIGHT / 2 + 5);

            dijkstraMap.Calculate();

            Console.Write(dijkstraMap);
        }

        [TestMethod]
        public void ManualDijkstraMapTimes()
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            RectangleMapGenerator.Generate(map);

            var dijkstraMap = new DijkstraMap(map);
            var c = Coord.Get(2, 2);

            while (c.X < MAP_WIDTH && c.Y < MAP_HEIGHT)
            {
                dijkstraMap.AddGoal(c.X, c.Y);
                c += 2;
            }
            dijkstraMap.Calculate(); // For the moment, displays number of iterations.

            dijkstraMap = new DijkstraMap(map);
            c = Coord.Get(map.Width - 3, 2);

            while(c.X > 0 && c.Y < MAP_HEIGHT)
            {
                dijkstraMap.AddGoal(c.X, c.Y);
                c += Coord.Get(-1, 1);
            }
            dijkstraMap.Calculate();

            dijkstraMap = new DijkstraMap(map);
            c = Coord.Get(map.Width - 3, map.Height - 3);

            while (c.X > 0 && c.Y > 0)
            {
                dijkstraMap.AddGoal(c.X, c.Y);
                c -= 1;
            }
            dijkstraMap.Calculate();

            dijkstraMap = new DijkstraMap(map);
            c = Coord.Get(2, map.Height - 3);

            while (c.X < MAP_WIDTH && c.Y > 0)
            {
                dijkstraMap.AddGoal(c.X, c.Y);
                c  += Coord.Get(1, -1);
            }
            dijkstraMap.Calculate();
        }

        [TestMethod]
        public void DijkstraAreEqual()
        {
            var genMap = new ArrayMap<bool>(100, 100);
            CellularAutomataGenerator.Generate(genMap);

            var goal1 = getWalkableCoord(genMap);

            var goal2 = getWalkableCoord(genMap);
            while (goal1 == goal2)
                goal2 = getWalkableCoord(genMap);

            var map = new ArrayMap<GoalState>(genMap.Width, genMap.Height);

            for (int x = 0; x < genMap.Width; x++)
                for (int y = 0; y < genMap.Height; y++)
                    map[x, y] = genMap[x, y] ? GoalState.Clear : GoalState.Obstacle;

            map[goal1] = GoalState.Goal;
            map[goal2] = GoalState.Goal;

            var dijkstraMap = new DijkstraMap(genMap);
            dijkstraMap.AddGoal(goal1.X, goal1.Y);
            dijkstraMap.AddGoal(goal2.X, goal2.Y);
            dijkstraMap.Calculate();

            var goalMap = new GoalMap<GoalState>(map, (s, c) => s);
            goalMap.Update();

            for (int x = 0; x < genMap.Width; x++)
                for (int y = 0; y < genMap.Height; y++)
                {
                    double translatedValue = !goalMap[x, y].HasValue ? double.MaxValue - 1 : goalMap[x, y].Value;
                    Assert.AreEqual(translatedValue, dijkstraMap[x, y]);
                }

        }

        [TestMethod]
        public void PathInitReversing()
        {
            Coord start = Coord.Get(1, 1);
            Coord end = Coord.Get(6, 6);
            // Because Path constructor is internal to avoid confusion, we use AStar to return a
            // (simple) known path
            var map = new ArrayMap<bool>(10, 10);
            RectangleMapGenerator.Generate(map);
            var pather = new AStar(map, Distance.CHEBYSHEV);

            var actualPath = pather.ShortestPath(start, end);
            var expectedPath = new List<Coord>();

            for (int i = start.X; i <= end.X; i++)
                expectedPath.Add(Coord.Get(i, i));

            Console.WriteLine("Pre-Reverse:");
            printExpectedAndActual(expectedPath, actualPath);

            checkAgainstPath(expectedPath, actualPath, start, end);

            expectedPath.Reverse();
            actualPath.Reverse();

            Console.WriteLine("\nPost-Reverse:");
            printExpectedAndActual(expectedPath, actualPath);

            checkAgainstPath(expectedPath, actualPath, end, start);
        }

        [TestMethod]
        public void OpenMapPathing()
        {
            var map = new ArrayMap<bool>(10, 10);
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    map[x, y] = true;

            Coord start = Coord.Get(1, 6);
            Coord end = Coord.Get(0, 1);
            var pather = new AStar(map, Distance.CHEBYSHEV);

            try
            {
                pather.ShortestPath(start, end);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        private static void checkAdjacency(Path path, Distance distanceCalc)
        {
            if (path.LengthWithStart == 1)
                return;

            for (int i = 0; i < path.LengthWithStart - 2; i++)
            {
                bool isAdjacent = false;
                foreach (var neighbor in ((AdjacencyRule)distanceCalc).Neighbors(path.GetStepWithStart(i)))
                {
                    if (neighbor == path.GetStepWithStart(i + 1))
                    {
                        isAdjacent = true;
                        break;
                    }
                }

                Assert.AreEqual(true, isAdjacent);
            }
        }

        private static void checkWalkable(Path path, IMapView<bool> map)
        {
            foreach (var pos in path.StepsWithStart)
                Assert.AreEqual(true, map[pos]);
        }

        private static CA.Heuristic distanceHeuristic(Distance distanceCalc)
        {
            switch (distanceCalc.Type)
            {
                case Distance.Types.CHEBYSHEV:
                    return CA.AStar.MaxAlongAxisHeuristic;

                case Distance.Types.EUCLIDEAN:
                    return CA.AStar.EuclidianHeuristic;

                case Distance.Types.MANHATTAN:
                    return CA.AStar.ManhattanHeuristic;

                default:
                    throw new Exception("Should not occur");
            }
        }

        public Coord getWalkableCoord(IMapView<bool> mapView)
        {
            var c = Coord.Get(SingletonRandom.DefaultRNG.Next(mapView.Width), SingletonRandom.DefaultRNG.Next(mapView.Height));

            while (!mapView[c])
                c = Coord.Get(SingletonRandom.DefaultRNG.Next(mapView.Width), SingletonRandom.DefaultRNG.Next(mapView.Height));

            return c;
        }

        // Initialize graph for control-case AStar, based on a GoRogue IMapView
        private static GraphReturn initGraph(IMapView<bool> map, Distance connectivity)
        {
            var returnVal = new GraphReturn();
            returnVal.Graph = new CA.Graph();

            returnVal.Nodes = new CA.Node[map.Width, map.Height]; // So we can add arcs easier

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map[x, y])
                    {
                        returnVal.Nodes[x, y] = new CA.Node(x, y, 0);
                        returnVal.Graph.AddNode(returnVal.Nodes[x, y]);
                    }

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    if (map[x, y])
                    {
                        foreach (var neighbor in ((AdjacencyRule)connectivity).Neighbors(x, y))
                        {
                            // Out of bounds of map
                            if (neighbor.X < 0 || neighbor.Y < 0 || neighbor.X >= map.Width || neighbor.Y >= map.Height)
                                continue;

                            if (!map[neighbor]) // Not walkable, so no node exists for it
                                continue;

                            returnVal.Graph.AddArc(new CA.Arc(returnVal.Nodes[x, y], returnVal.Nodes[neighbor.X, neighbor.Y]));
                        }
                    }
                }

            return returnVal;
        }

        private void aStarMatches(Distance distanceCalc)
        {
            var map = new ArrayMap<bool>(MAP_WIDTH, MAP_HEIGHT);
            CellularAutomataGenerator.Generate(map);
            var graphTuple = initGraph(map, distanceCalc);

            var pather = new AStar(map, distanceCalc);
            var controlPather = new CA.AStar(graphTuple.Graph);
            controlPather.ChoosenHeuristic = distanceHeuristic(distanceCalc);

            for (int i = 0; i < ITERATIONS; i++)
            {
                Coord start = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));
                while (!map[start])
                    start = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));

                Coord end = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));
                while (end == start || !map[end])
                    end = Coord.Get(SingletonRandom.DefaultRNG.Next(map.Width - 1), SingletonRandom.DefaultRNG.Next(map.Height - 1));

                var path1 = pather.ShortestPath(start, end);
                controlPather.SearchPath(graphTuple.Nodes[start.X, start.Y], graphTuple.Nodes[end.X, end.Y]);
                var path2 = controlPather.PathByNodes;

                if (path2.Length != path1.LengthWithStart)
                {
                    Console.WriteLine($"Error: Control got {path2.Length}, but custom AStar got {path1.LengthWithStart}");
                    Console.WriteLine("Control: ");
                    Utility.PrintHightlightedPoints(map, Utility.ToCoords(path2));
                    Console.WriteLine("AStar  :");
                    Utility.PrintHightlightedPoints(map, path1.StepsWithStart);
                }

                bool lengthGood = (path1.LengthWithStart <= path2.Length);
                Assert.AreEqual(true, lengthGood);
                Assert.AreEqual(path1.Start, start);
                Assert.AreEqual(path1.End, end);
                checkWalkable(path1, map);
                checkAdjacency(path1, distanceCalc);
            }
        }

        private void checkAgainstPath(IReadOnlyList<Coord> expectedPath, Path actual, Coord start, Coord end)
        {
            var actualList = actual.StepsWithStart.ToList();

            checkListsMatch(expectedPath, actualList);

            for (int i = 0; i < expectedPath.Count; i++)
                Assert.AreEqual(expectedPath[i], actual.GetStepWithStart(i));

            for (int i = 1; i < expectedPath.Count; i++)
                Assert.AreEqual(expectedPath[i], actual.GetStep(i - 1));

            Assert.AreEqual(actual.Start, start);
            Assert.AreEqual(actual.End, end);

            Assert.AreEqual(actual.Length, expectedPath.Count - 1);
            Assert.AreEqual(actual.LengthWithStart, expectedPath.Count);
        }

        private void checkListsMatch(IReadOnlyList<Coord> expected, IReadOnlyList<Coord> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], actual[i]);
        }

        private void printExpectedAndActual(IReadOnlyList<Coord> expected, Path actual)
        {
            Console.WriteLine("Expected:");
            foreach (var i in expected)
                Console.Write(i + ",");
            Console.WriteLine();

            Console.WriteLine("Actual");
            foreach (var i in actual.StepsWithStart)
                Console.Write(i + ",");
            Console.WriteLine();

            Console.WriteLine("Actual by index: ");
            for (int i = 0; i < expected.Count; i++)
                Console.Write(actual.GetStepWithStart(i) + ",");
            Console.WriteLine();
        }
    }

    internal class GraphReturn
    {
        public CA.Graph Graph;
        public CA.Node[,] Nodes;
    }
}