﻿using GoRogue;
using GoRogue.Pathing;
using System;
using System.Diagnostics;
using Generators = GoRogue.MapGeneration.Generators;

namespace GoRogue_PerformanceTests
{
    public static class PathingTests
    {
        public static TimeSpan TimeForSingleSourceDijkstra(int mapWidth, int mapHeight, int iterations)
        {
            Stopwatch s = new Stopwatch();

            var map = new ArrayMapOf<bool>(mapWidth, mapHeight);
            Generators.RectangleMapGenerator.Generate(map);

            DijkstraMap dMap = new DijkstraMap(map);

            dMap.AddGoal(5, 5);

            dMap.Calculate(); // warm-up value

            s.Start();
            for (int i = 0; i < iterations; i++)
                dMap.Calculate();

            s.Stop();

            return s.Elapsed;
        }
    }
}