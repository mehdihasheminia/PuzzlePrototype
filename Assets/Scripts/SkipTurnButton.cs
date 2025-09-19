using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SkipTurnButton : MonoBehaviour
{
    [Header("Refs")]
    public PlayerAgent player;          // auto-found if null
    public GameManager gameManager;     // auto-found if null
    public Button button;               // assign a UI Button here

    [Header("Input")]
    public bool allowKeyboard = true;
    public KeyCode skipKey = KeyCode.Space;

    void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerAgent>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (button == null) button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    void Update()
    {
        // Keep the button interactable only when skipping is valid
        bool canSkip = CanSkipNow();
        if (button != null) button.interactable = canSkip;

        // Optional keyboard shortcut
        if (allowKeyboard && canSkip && Input.GetKeyDown(skipKey))
            HandleClick();
    }

    bool CanSkipNow()
    {
        if (player == null || gameManager == null) return false;
        if (gameManager.IsGameOver) return false;
        if (!gameManager.IsPlayerTurn) return false;
        if (player.IsMoving) return false; // don't skip while the player is currently animating a move
        return true;
    }

    void HandleClick()
    {
        if (!CanSkipNow()) return;
        player.SkipTurn();
    }
}