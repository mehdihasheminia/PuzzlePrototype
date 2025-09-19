using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoardAsset", menuName = "Puzzle/Board Asset")]
public class BoardAsset : ScriptableObject
{
    [Min(1)] public int width = 8;
    [Min(1)] public int height = 8;

    // Linearized row-major storage: index = y * width + x
    [SerializeField] private List<bool> walkable = new List<bool>();

    // Ensure list has exactly width*height entries
    public void EnsureSize()
    {
        int target = width * height;
        if (target < 1) target = 1;

        if (walkable == null) walkable = new List<bool>(target);

        if (walkable.Count < target)
        {
            int add = target - walkable.Count;
            for (int i = 0; i < add; i++) walkable.Add(true); // default walkable
        }
        else if (walkable.Count > target)
        {
            walkable.RemoveRange(target, walkable.Count - target);
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public bool GetWalkable(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return walkable[y * width + x];
    }

    public void SetWalkable(int x, int y, bool value)
    {
        if (!InBounds(x, y)) return;
        walkable[y * width + x] = value;
    }

    public IReadOnlyList<bool> Data => walkable; // for read-only access
}

//1- more detailed tile/grid that allows us to increase the details like ramps and holes
//2- Switch tiles that bring up a bridge
//3- You can modify terrains so that a path is made for you, block enemies, blocks enemy shots or direct enemies to another path
//4- Also modifying terrains can mean openning/closing doors. Moving cover blocks. 
//5- Tiles that you can stay on them one turn. So this way players cannot skip turn or modify terrain