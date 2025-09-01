using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PuzzleCompleteRule : MonoBehaviour
{
    public List<ThickRaycaster> m_RayCasters;
    public GameObject m_WinUi;
    
    void Update()
    {
        if (m_RayCasters == null || m_RayCasters.Count == 0)
            return;

        bool isComplete = m_RayCasters.All(rayCaster => rayCaster.DidHit);
        
        if (m_WinUi != null)
            m_WinUi.SetActive(isComplete);
        else
            Debug.Log($"Puzzle completed");
    }
}
