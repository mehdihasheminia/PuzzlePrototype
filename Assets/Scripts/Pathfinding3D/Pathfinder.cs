using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Grid2D))]
public class Pathfinder : MonoBehaviour
{
    Grid2D _grid;

    void Awake() => _grid = GetComponent<Grid2D>();

    struct Node
    {
        public Vector2Int pos;
        public int g; // cost from start
        public int f; // g + h
        public Vector2Int parent;
        public Node(Vector2Int p, int g, int f, Vector2Int parent)
        { this.pos = p; this.g = g; this.f = f; this.parent = parent; }
    }

    public bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
    {
        path = null;
        if (!_grid.InBounds(start) || !_grid.InBounds(goal)) return false;
        if (!_grid.IsWalkable(start) || !_grid.IsWalkable(goal)) return false;

        // Open set as a simple list (grids are small; replace with a binary heap if needed)
        var open = new List<Node>(64);
        var openIndex = new Dictionary<Vector2Int, int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

        int H(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan

        open.Add(new Node(start, 0, H(start, goal), new Vector2Int(int.MinValue, int.MinValue)));
        openIndex[start] = 0;

        var closed = new HashSet<Vector2Int>();

        while (open.Count > 0)
        {
            // find node with lowest f
            int best = 0;
            int bestF = open[0].f;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < bestF)
                {
                    best = i;
                    bestF = open[i].f;
                }
            }

            var current = open[best];
            open.RemoveAt(best);
            openIndex.Remove(current.pos);

            if (current.pos == goal)
            {
                // reconstruct
                path = ReconstructPath(cameFrom, current.pos);
                return true;
            }

            closed.Add(current.pos);

            foreach (var n in _grid.GetNeighbors4(current.pos))
            {
                if (closed.Contains(n)) continue;

                int tentativeG = current.g + 1;

                if (!gScore.TryGetValue(n, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[n] = current.pos;
                    gScore[n] = tentativeG;
                    int f = tentativeG + H(n, goal);

                    if (!openIndex.TryGetValue(n, out int idx))
                    {
                        open.Add(new Node(n, tentativeG, f, current.pos));
                        openIndex[n] = open.Count - 1;
                    }
                    else
                    {
                        open[idx] = new Node(n, tentativeG, f, current.pos);
                    }
                }
            }
        }

        return false;
    }

    List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int cur)
    {
        var result = new List<Vector2Int>();
        result.Add(cur);
        while (cameFrom.TryGetValue(cur, out var p))
        {
            cur = p;
            result.Add(cur);
        }
        result.Reverse();
        return result;
    }
}
