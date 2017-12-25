﻿using System.Collections.Generic;
using GoRogue.Random;

namespace GoRogue.MapGeneration.Connectors
{
    /// <summary>
    /// Specifies the method of connecting two areas.
    /// </summary>
    public enum AreaConnectionStrategy
    {
        /// <summary>
        /// Connects two areas by choosing random points from the list of coordinates in each area, and connecting
        /// those two points.
        /// </summary>
        RANDOM_POINT,
        /// <summary>
        /// Connects two areas by connecting the center points of the bounding rectangle for each area.
        /// On concave shapes, the center point of the bounding rectangle is not guaranteed to have a walkable
        /// connection to the rest of the area, so when concave map areas are present this may not connect areas properly.
        /// </summary>
        CENTER_BOUNDS };


    static public class ClosestMapArea
    {
        static public void Connect(ISettableMapOf<bool> map, AreaConnectionStrategy areaConnector) => Connect(map, areaConnector, SingletonRandom.DefaultRNG);

        static public void Connect(ISettableMapOf<bool> map, AreaConnectionStrategy areaConnector, IRandom rng)
        {
            var areas = new List<MapArea>(MapAreaFinder.MapAreas(map, Distance.MANHATTAN));

            var ds = new DisjointSet(areas.Count);
            while (ds.Count > 1) // Haven't unioned all sets into one
            {
                for (int i = 0; i < areas.Count; i++)
                {
                    int iClosest = findNearestMapArea(areas, i, ds);

                    Coord iCoord = (areaConnector == AreaConnectionStrategy.RANDOM_POINT) ? 
                                      areas[i].Positions.RandomItem(rng) : areas[i].Bounds.Center;
                    Coord iClosestCoord = (areaConnector == AreaConnectionStrategy.CENTER_BOUNDS) ?
                                          areas[iClosest].Positions.RandomItem(rng) : areas[iClosest].Bounds.Center;
                    
                    List<Coord> tunnelPositions = Coord.CardinalPositionsOnLine(iCoord, iClosestCoord);

                    Coord previous = null;
                    foreach (var pos in tunnelPositions)
                    {
                        map[pos] = true;
                        // Previous cell, and we're going vertical, go 2 wide so it looks nicer
                        // Make sure not to break rectangles (less than last index)!
                        if (previous != null)
                            if (pos.Y != previous.Y)
                                if (pos.X + 1 < map.Width - 1)
                                    map[pos.X + 1, pos.Y] = true;

                        previous = pos;
                    }
                    ds.MakeUnion(i, iClosest);
                }
            }
        }

        static private int findNearestMapArea(IList<MapArea> mapAreas, int mapAreaIndex, DisjointSet ds)
        {
            int closestIndex = mapAreaIndex;
            double distance = double.MaxValue;

            for (int i = 0; i < mapAreas.Count; i++)
            {
                if (i == mapAreaIndex)
                    continue;

                if (ds.InSameSet(i, mapAreaIndex))
                    continue;

                double distanceBetween = Distance.MANHATTAN.DistanceBetween(mapAreas[mapAreaIndex].Bounds.Center, mapAreas[i].Bounds.Center);
                if (distanceBetween < distance)
                {
                    distance = distanceBetween;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }
    }
}
