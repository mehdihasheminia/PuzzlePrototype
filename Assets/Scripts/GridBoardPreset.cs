using UnityEngine;

[CreateAssetMenu(fileName = "GridBoardPreset", menuName = "Grid/Board Preset")]
public class GridBoardPreset : ScriptableObject
{
    [TextArea(6, 20)]
    public string json; // serialized board state
}