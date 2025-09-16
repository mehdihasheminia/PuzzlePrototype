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
        OnEnergyChanged(m_PlayerAgent.maxHealth, m_PlayerAgent.currentHealth);
        
        m_GameManager.onWin.AddListener(OnWin);
        m_GameManager.onLose.AddListener(OnLose);
        m_GameManager.onHealthChanged.AddListener(OnEnergyChanged);
    }

    void OnWin()
    {
        m_WinPanel.gameObject.SetActive(true);
    }
    
    void OnLose()
    {
        m_LosePanel.gameObject.SetActive(true);
    }
    
    void OnEnergyChanged(int current, int max)
    {
        m_EnergyText.text = $"H: {current}";
    }
}
