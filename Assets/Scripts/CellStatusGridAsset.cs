using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Generic rectangular grid of cell walkability states.
/// Used by boards, obstacles, patterns, and any feature that needs
/// to describe walkable vs. blocked cells in a reusable format.
/// </summary>
[CreateAssetMenu(fileName = "CellStatusGrid", menuName = "Puzzle/Cell Status Grid")]
public class CellStatusGridAsset : ScriptableObject, ISerializationCallbackReceiver
{
    /// <summary>Walkability state of a cell.</summary>
    public enum CellStatus
    {
        Unspecified = 0,
        Walkable = 1,
        Blocked = 2,
    }

    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    [SerializeField]
    List<CellStatus> cells = new();

    // Legacy bool serialization (BoardAsset used to keep a List&lt;bool&gt; called "walkable").
    [SerializeField, HideInInspector, FormerlySerializedAs("walkable")]
    List<bool> legacyWalkable = new();

    public int CellCount => Mathf.Max(1, width * height);

    public void EnsureSize()
    {
        int target = CellCount;
        if (cells == null) cells = new List<CellStatus>(target);

        if (cells.Count < target)
        {
            int add = target - cells.Count;
            for (int i = 0; i < add; i++)
                cells.Add(CellStatus.Walkable);
        }
        else if (cells.Count > target)
        {
            cells.RemoveRange(target, cells.Count - target);
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public CellStatus GetStatus(int x, int y)
    {
        if (!InBounds(x, y)) return CellStatus.Unspecified;
        EnsureSize();
        return cells[y * width + x];
    }

    public void SetStatus(int x, int y, CellStatus status)
    {
        if (!InBounds(x, y)) return;
        EnsureSize();
        cells[y * width + x] = status;
    }

    public bool GetWalkable(int x, int y)
    {
        var status = GetStatus(x, y);
        return status != CellStatus.Blocked;
    }

    public void SetWalkable(int x, int y, bool walkable)
    {
        SetStatus(x, y, walkable ? CellStatus.Walkable : CellStatus.Blocked);
    }

    public void Fill(CellStatus status)
    {
        EnsureSize();
        for (int i = 0; i < cells.Count; i++)
            cells[i] = status;
    }

    public IReadOnlyList<CellStatus> Data
    {
        get
        {
            EnsureSize();
            return cells;
        }
    }

    public IEnumerable<(Vector2Int cell, CellStatus status)> EnumerateCells()
    {
        EnsureSize();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var status = cells[y * width + x];
            yield return (new Vector2Int(x, y), status);
        }
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (legacyWalkable != null && legacyWalkable.Count > 0)
        {
            if (cells == null) cells = new List<CellStatus>(legacyWalkable.Count);
            else cells.Clear();

            for (int i = 0; i < legacyWalkable.Count; i++)
                cells.Add(legacyWalkable[i] ? CellStatus.Walkable : CellStatus.Blocked);

            legacyWalkable.Clear();
        }

        EnsureSize();
    }
}
