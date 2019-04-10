﻿using GoRogue.MapViews;
using System;
using System.Collections.Generic;

namespace GoRogue.Pathing
{
	/// <summary>
	/// Implements an optimized AStar pathfinding algorithm. Optionally supports custom heuristics, and custom weights for each tile.  If the map view
	/// you give to the algorithm must change size frequently, consider <see cref="AStarDynamic"/> or <see cref="FastAStarDynamic"/>.
	/// </summary>
	/// <remarks>
	/// Like most GoRogue algorithms, AStar takes as a construction parameter an IMapView representing the map. 
	/// Specifically, it takes an <see cref="IMapView{Boolean}"/>, where true indicates that a tile should be
	/// considered walkable, and false indicates that a tile should be considered impassable.
	/// 
	/// For details on the map view system in general, see <see cref="IMapView{T}"/>.  As well, there is an article
	/// explaining the map view system at the GoRogue documentation page
	/// <a href="https://chris3606.github.io/GoRogue/articles">here</a>
	/// 
	/// This algorithm performs the best when the map view it is given does not change size frequently, and generally, in cases where maximum performance
	/// is needed, it is recommended that the map view _not_ change size frequently (regardless of whether the underlying map is actually changing size).
	/// However, in cases where the map view size must change size frequently, you may get better performance out of <see cref="AStarDynamic"/> or
	/// its fast variant, <see cref="FastAStarDynamic"/>.
	/// </remarks>
	public class AStar
	{
		private Func<int, int, IEnumerable<Direction>> neighborFunc;
		private double[] costSoFar;
		private Coord[] cameFrom;
		private bool[] opened;
		private bool[] closed;
		private int _cachedWidth;
		private int _cachedHeight;

		/// <summary>
		/// The map view being used to determine whether or not each tile is walkable.
		/// </summary>
		public IMapView<bool> WalkabilityMap { get; }

		private Distance _distanceMeasurement;

		/// <summary>
		/// The distance calculation being used to determine distance between points. <see cref="Distance.MANHATTAN"/>
		/// implies 4-way connectivity, while <see cref="Distance.CHEBYSHEV"/> or <see cref="Distance.EUCLIDEAN"/> imply
		/// 8-way connectivity for the purpose of determining adjacent coordinates.
		/// </summary>
		public Distance DistanceMeasurement
		{
			get => _distanceMeasurement;
			set
			{
				_distanceMeasurement = value;
				if (_distanceMeasurement == Distance.MANHATTAN)
					neighborFunc = cardinalNeighbors;
				else
					neighborFunc = neighbors;
			}
		}

		/// <summary>
		/// The heuristic used to estimate distance from nodes to the end point.  If unspecified, defaults to using the distance
		/// calculation specified by <see cref="DistanceMeasurement"/>.
		/// </summary>
		public Func<Coord, Coord, double> Heuristic { get; }

		/// <summary>
		/// Weights given to each tile.  The weight is multiplied by the cost of a tile, so a tile with weight  is twice as hard to enter
		/// as a tile with weight 1.  If unspecified, all tiles have weight 1.
		/// </summary>
		public IMapView<double> Weights { get; }

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="walkabilityMap">Map view used to deterine whether or not each location can be traversed -- true indicates a tile can be traversed,
		/// and false indicates it cannot.</param>
		/// <param name="distanceMeasurement">Distance calculation used to determine whether 4-way or 8-way connectivity is used, and to determine
		/// how to calculate the distance between points.  If <paramref name="heuristic"/> is unspecified, also determines the estimation heuristic used.</param>
		/// <param name="heuristic">Function used to estimate the distance between two given points.  If unspecified, a distance calculation corresponding
		/// to <paramref name="distanceMeasurement"/> is used, which will produce guranteed shortest paths.</param>
		/// <param name="weights">A map view indicating the weights of each location (see <see cref="Weights"/>.  If unspecified, each location will default to having a weight of 1.</param>
		public AStar(IMapView<bool> walkabilityMap, Distance distanceMeasurement, Func<Coord, Coord, double> heuristic = null,
						 IMapView<double> weights = null)
		{
			Heuristic = heuristic ?? distanceMeasurement.Calculate;
			if (weights == null)
			{
				var weightsMap = new ArrayMap<double>(walkabilityMap.Width, walkabilityMap.Height);
				foreach (var pos in weightsMap.Positions())
					weightsMap[pos] = 1.0;
				Weights = weightsMap;
			}
			else
				Weights = weights;

			WalkabilityMap = walkabilityMap;
			DistanceMeasurement = distanceMeasurement;

			InitializeArrays(out costSoFar, out cameFrom, out opened, out closed, WalkabilityMap.Width * WalkabilityMap.Height);
		}

		/// <summary>
		/// Finds the shortest path between the two specified points.
		/// </summary>
		/// <remarks>
		/// Returns <see langword="null"/> if there is no path between the specified points. Will still return an
		/// appropriate path object if the start point is equal to the end point.
		/// </remarks>
		/// <param name="start">The starting point of the path.</param>
		/// <param name="end">The ending point of the path.</param>
		/// <param name="assumeEndpointsWalkable">
		/// Whether or not to assume the start and end points are walkable, regardless of what the
		/// <see cref="WalkabilityMap"/> reports. Defaults to <see langword="true"/>.
		/// </param>
		/// <returns>The shortest path between the two points, or <see langword="null"/> if no valid path exists.</returns>
		public Path ShortestPath(Coord start, Coord end, bool assumeEndpointsWalkable = true)
		{
			// Can't be a path
			if (!assumeEndpointsWalkable && (!WalkabilityMap[start] || !WalkabilityMap[end]))
				return null;

			// Path is 1 long, so don't bother with allocations
			if (start == end)
				return new Path(new List<Coord> { start });

			if (_cachedWidth != WalkabilityMap.Width || _cachedHeight != WalkabilityMap.Height)
			{
				InitializeArrays(out costSoFar, out cameFrom, out opened, out closed, WalkabilityMap.Width * WalkabilityMap.Height);
				_cachedWidth = WalkabilityMap.Width;
				_cachedHeight = WalkabilityMap.Height;
			}
			else // Clear only opened and closed -- cameFrom is never accessed until path found, and opened controls access to costSoFar
			{
				int length = closed.Length;
				Array.Clear(closed, 0, length);
				Array.Clear(opened, 0, length);
			}

			// Calculate path
			var head = new LinkedListPriorityQueueNode<Coord>(start, Heuristic(start, end));
			var open = new LinkedListPriorityQueue<Coord>();
			open.Push(head);
			opened[start.ToIndex(WalkabilityMap.Width)] = true;

			
			while (!open.IsEmpty())
			{
				var current = open.Pop().Value;

				// Path complete -- return
				if (current == end)
				{
					List<Coord> path = new List<Coord>();
					while (current != start)
					{
						path.Add(current);
						current = cameFrom[current.ToIndex(WalkabilityMap.Width)];
					}
					path.Add(start);
					return new Path(path);
				}

				// Step to neighbors
				var currentIndex = current.ToIndex(WalkabilityMap.Width);
				var initialCost = costSoFar[currentIndex];

				closed[currentIndex] = true;

				// Go through neighbors based on distance calculation
				foreach (var neighborDir in neighborFunc(current.X - end.X, current.Y - end.Y))
				{
					var neighbor = current + neighborDir;
					// Ignore if out of bounds or location unwalkable
					if (!WalkabilityMap.Bounds().Contains(neighbor) ||
						(!WalkabilityMap[neighbor] && (!assumeEndpointsWalkable || (!neighbor.Equals(start) && !neighbor.Equals(end)))))
						continue;

					var neighborIndex = neighbor.ToIndex(WalkabilityMap.Width);

					if (closed[neighborIndex])
						continue;

					
					// Real cost of getting to neighbor via path passing through current
					var newCost = initialCost + _distanceMeasurement.Calculate(current, neighbor) * Weights[neighbor];
					
					var oldCost = costSoFar[neighborIndex];
					// Compare to best path to neighbor we have; 0 means no path found yet to neighbor
					if (opened[neighborIndex] && !(newCost < oldCost))
						continue;

					opened[neighborIndex] = true;

					// We've found a better path, so update parent and known cost
					costSoFar[neighborIndex] = newCost;
					cameFrom[neighborIndex] = current;

					// Use new distance + heuristic to compute new expected cost from neighbor to end, and update priority queue
					var expectedCost = newCost + Heuristic(neighbor, end);
					open.Push(new LinkedListPriorityQueueNode<Coord>(neighbor, expectedCost));
				}
			}

			// There is no path
			return null;
		}

		/// <summary>
		/// Finds the shortest path between the two specified points.
		/// </summary>
		/// <remarks>
		/// Returns <see langword="null"/> if there is no path between the specified points. Will still return an
		/// appropriate path object if the start point is equal to the end point.
		/// </remarks>
		/// <param name="startX">The x-coordinate of the starting point of the path.</param>
		/// <param name="startY">The y-coordinate of the starting point of the path.</param>
		/// <param name="endX">The x-coordinate of the ending point of the path.</param>
		/// <param name="endY">The y-coordinate of the ending point of the path.</param>
		/// <param name="assumeEndpointsWalkable">
		/// Whether or not to assume the start and end points are walkable, regardless of what the
		/// <see cref="WalkabilityMap"/> reports. Defaults to <see langword="true"/>.
		/// </param>
		/// <returns>The shortest path between the two points, or <see langword="null"/> if no valid path exists.</returns>
		public Path ShortestPath(int startX, int startY, int endX, int endY, bool assumeEndpointsWalkable = true)
			=> ShortestPath(new Coord(startX, startY), new Coord(endX, endY), assumeEndpointsWalkable);

		// These neighbor functions are special in that they return (approximately) the closest
		// directions to the end goal first. This is intended to "prioritize" more direct-looking
		// paths, in the case that one or more paths are equally short
		internal static IEnumerable<Direction> cardinalNeighbors(int dx, int dy)
		{
			Direction left, right;

			left = right = Direction.GetCardinalDirection(dx, dy);
			yield return right; // Return first direction

			left -= 2;
			right += 2;
			yield return left;
			yield return right;

			// Return last direction
			right += 2;
			yield return right;
		}

		internal static IEnumerable<Direction> neighbors(int dx, int dy)
		{
			Direction left, right;

			left = right = Direction.GetDirection(dx, dy);
			yield return right; // Return first direction

			for (int i = 0; i < 3; i++)
			{
				left--;
				right++;

				yield return left;
				yield return right;
			}

			// Return last direction
			right++;
			yield return right;
		}

		private static void InitializeArrays(out double[] costSoFar, out Coord[] cameFrom, out bool[] opened, out bool[] closed, int length)
		{
			costSoFar = new double[length];
			cameFrom = new Coord[length];
			opened = new bool[length];
			closed = new bool[length];
		}
	}

	/// <summary>
	/// Encapsulates a path as returned by pathfinding algorithms like AStar.
	/// </summary>
	/// <remarks>
	/// Provides various functions to iterate through/access steps of the path, as well as
	/// constant-time reversing functionality.
	/// </remarks>
	public class Path
	{
		private IReadOnlyList<Coord> _steps;
		private bool inOriginalOrder;

		/// <summary>
		/// Creates a copy of the path, optionally reversing the path as it does so.
		/// </summary>
		/// <remarks>Reversing is an O(1) operation, since it does not modify the list.</remarks>
		/// <param name="pathToCopy">The path to copy.</param>
		/// <param name="reverse">Whether or not to reverse the path. Defaults to <see langword="false"/>.</param>
		public Path(Path pathToCopy, bool reverse = false)
		{
			_steps = pathToCopy._steps;
			inOriginalOrder = (reverse ? !pathToCopy.inOriginalOrder : pathToCopy.inOriginalOrder);
		}

		// Create based on internal list
		internal Path(IReadOnlyList<Coord> steps)
		{
			_steps = steps;
			inOriginalOrder = true;
		}

		/// <summary>
		/// Ending point of the path.
		/// </summary>
		public Coord End
		{
			get
			{
				if (inOriginalOrder)
					return _steps[0];

				return _steps[_steps.Count - 1];
			}
		}

		/// <summary>
		/// The length of the path, NOT including the starting point.
		/// </summary>
		public int Length { get => _steps.Count - 1; }

		/// <summary>
		/// The length of the path, INCLUDING the starting point.
		/// </summary>
		public int LengthWithStart { get => _steps.Count; }

		/// <summary>
		/// Starting point of the path.
		/// </summary>
		public Coord Start
		{
			get
			{
				if (inOriginalOrder)
					return _steps[_steps.Count - 1];

				return _steps[0];
			}
		}

		/// <summary>
		/// The coordinates that constitute the path (in order), NOT including the starting point.
		/// These are the coordinates something might walk along to follow a path.
		/// </summary>
		public IEnumerable<Coord> Steps
		{
			get
			{
				if (inOriginalOrder)
				{
					for (int i = _steps.Count - 2; i >= 0; i--)
						yield return _steps[i];
				}
				else
				{
					for (int i = 1; i < _steps.Count; i++)
						yield return _steps[i];
				}
			}
		}

		/// <summary>
		/// The coordinates that constitute the path (in order), INCLUDING the starting point.
		/// </summary>
		public IEnumerable<Coord> StepsWithStart
		{
			get
			{
				if (inOriginalOrder)
				{
					for (int i = _steps.Count - 1; i >= 0; i--)
						yield return _steps[i];
				}
				else
				{
					for (int i = 0; i < _steps.Count; i++)
						yield return _steps[i];
				}
			}
		}

		/// <summary>
		/// Gets the nth step along the path, where 0 is the step AFTER the starting point.
		/// </summary>
		/// <param name="stepNum">The (array-like index) of the step to get.</param>
		/// <returns>The coordinate consituting the step specified.</returns>
		public Coord GetStep(int stepNum)
		{
			if (inOriginalOrder)
				return _steps[(_steps.Count - 2) - stepNum];

			return _steps[stepNum + 1];
		}

		/// <summary>
		/// Gets the nth step along the path, where 0 IS the starting point.
		/// </summary>
		/// <param name="stepNum">The (array-like index) of the step to get.</param>
		/// <returns>The coordinate consituting the step specified.</returns>
		public Coord GetStepWithStart(int stepNum)
		{
			if (inOriginalOrder)
				return _steps[(_steps.Count - 1) - stepNum];

			return _steps[stepNum];
		}

		/// <summary>
		/// Reverses the path, in constant time.
		/// </summary>
		public void Reverse() => inOriginalOrder = !inOriginalOrder;

		/// <summary>
		/// Returns a string representation of all the steps in the path, including the start point,
		/// eg. [(1, 2), (3, 4), (5, 6)].
		/// </summary>
		/// <returns>A string representation of all steps in the path, including the start.</returns>
		public override string ToString() => StepsWithStart.ExtendToString();
	}
}
