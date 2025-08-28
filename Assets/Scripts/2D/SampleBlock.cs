using UnityEngine;

public class SampleBlock : MonoBehaviour
{
    public GridBoard m_Grid;
    public bool Active = true;
    
    void Start()
    {
        BlockSample(m_Grid);
    }
    
    void BlockSample(GridBoard grid)
    {
        if (!Active)
            return;
        
        for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
            grid.SetWalkable(r, c, false);
    }
}
