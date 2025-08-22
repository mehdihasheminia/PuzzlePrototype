using System;

public static class GridBoardEvents
{
    // Hook you call FROM GridBoard.SetWalkable after it updates data:
    public static Action<int, int, bool> OnWalkableChanged;

    public static void RaiseWalkableChanged(int r, int c, bool isWalkable)
        => OnWalkableChanged?.Invoke(r, c, isWalkable);

    public static void SubscribeToWalkableChanged(GridBoard gb, Action<int,int,bool> cb)
    {
        OnWalkableChanged += cb;
    }
    public static void UnsubscribeFromWalkableChanged(GridBoard gb, Action<int,int,bool> cb)
    {
        OnWalkableChanged -= cb;
    }
}