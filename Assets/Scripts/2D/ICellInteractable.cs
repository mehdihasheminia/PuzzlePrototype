using UnityEngine;

public interface ICellInteractable
{
    /// Grid cell where this interactable lives (row, col).
    Vector2Int Cell { get; }

    /// Should the actor be allowed to activate (e.g., must be adjacent)?
    bool CanActivate(Vector2Int actorCell);

    /// Perform the interaction.
    void Activate();
}