using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    // 4-directional movement (no diagonals) to avoid corner-cutting across blocked tiles
    private static readonly Vector2Int[] DIRS4 =
    {
        new Vector2Int(-1, 0),  // up (row-1)
        new Vector2Int(1, 0),   // down (row+1)
        new Vector2Int(0, -1),  // left (col-1)
        new Vector2Int(0, 1),   // right (col+1)
    };

    public static bool FindPath(GridBoard grid, Vector2Int start, Vector2Int goal, List<Vector2Int> pathOut)
    {
        pathOut.Clear();

        // Start/goal must be walkable and inside bounds
        if (!grid.InBounds(start.x, start.y) || !grid.InBounds(goal.x, goal.y)) return false;
        if (!grid.IsWalkable(start.x, start.y) || !grid.IsWalkable(goal.x, goal.y)) return false;

        int rows = grid.rows;
        int cols = grid.cols;

        // A* arrays
        var gScore = new float[rows, cols];
        var fScore = new float[rows, cols];
        var cameFrom = new Vector2Int[rows, cols];
        var inOpen = new bool[rows, cols];
        var inClosed = new bool[rows, cols];

        // initialize
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            gScore[r, c] = float.PositiveInfinity;
            fScore[r, c] = float.PositiveInfinity;
            cameFrom[r, c] = new Vector2Int(-1, -1);
        }

        // simple open set (list) — fine for small boards; easy to read
        var open = new List<Vector2Int>(rows * cols);

        gScore[start.x, start.y] = 0f;
        fScore[start.x, start.y] = Heuristic(start, goal);
        open.Add(start);
        inOpen[start.x, start.y] = true;

        while (open.Count > 0)
        {
            // get node with lowest fScore
            int bestIdx = 0;
            float bestF = fScore[open[0].x, open[0].y];
            for (int i = 1; i < open.Count; i++)
            {
                var rc = open[i];
                float f = fScore[rc.x, rc.y];
                if (f < bestF)
                {
                    bestF = f;
                    bestIdx = i;
                }
            }

            var current = open[bestIdx];
            if (current == goal)
            {
                ReconstructPath(cameFrom, start, goal, pathOut);
                return true;
            }

            // pop current
            open.RemoveAt(bestIdx);
            inOpen[current.x, current.y] = false;
            inClosed[current.x, current.y] = true;

            // explore neighbors
            foreach (var dir in DIRS4)
            {
                var nb = new Vector2Int(current.x + dir.x, current.y + dir.y);
                if (!grid.InBounds(nb.x, nb.y)) continue;
                if (!grid.IsWalkable(nb.x, nb.y)) continue;
                if (inClosed[nb.x, nb.y]) continue;

                float tentativeG = gScore[current.x, current.y] + 1f; // uniform cost (each step = 1)

                if (!inOpen[nb.x, nb.y])
                {
                    open.Add(nb);
                    inOpen[nb.x, nb.y] = true;
                }
                else if (tentativeG >= gScore[nb.x, nb.y])
                {
                    continue; // not a better path
                }

                cameFrom[nb.x, nb.y] = current;
                gScore[nb.x, nb.y] = tentativeG;
                fScore[nb.x, nb.y] = tentativeG + Heuristic(nb, goal);
            }
        }

        // no path
        return false;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Manhattan distance for 4-directional grids
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static void ReconstructPath(Vector2Int[,] cameFrom, Vector2Int start, Vector2Int goal, List<Vector2Int> pathOut)
    {
        pathOut.Clear();
        var cur = goal;
        while (cur != new Vector2Int(-1, -1) && cur != start)
        {
            pathOut.Add(cur);
            cur = cameFrom[cur.x, cur.y];
        }
        pathOut.Add(start);
        pathOut.Reverse();
    }
}
