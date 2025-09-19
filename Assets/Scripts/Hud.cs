using TMPro;
using UnityEngine;

public class Hud : MonoBehaviour
{
    public GameManager m_GameManager;
    
    [Header("Energy")]
    public TextMeshProUGUI m_EnergyText;
    public PlayerAgent m_PlayerAgent; //Only for the initial value

    [Header("Win/Lose")]
    public RectTransform m_WinPanel;
    public RectTransform m_LosePanel;
    
    void Start()
    {
        UpdateEnergyDisplay();

        m_GameManager.onWin.AddListener(OnWin);
        m_GameManager.onLose.AddListener(OnLose);
    }

    void OnWin()
    {
        m_WinPanel.gameObject.SetActive(true);
    }
    
    void OnLose()
    {
        m_LosePanel.gameObject.SetActive(true);
    }
    
    void UpdateEnergyDisplay()
    {
        if (m_PlayerAgent == null || m_EnergyText == null) return;
        m_EnergyText.text = $"Energy: {m_PlayerAgent.CellsPerTurn} cells/turn";
    }
}
