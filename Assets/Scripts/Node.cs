using UnityEngine;

public class Node
{
    public readonly Vector2 position;
    public readonly Card owner;
    public readonly bool isPin;
    public Node(Vector2 currentPos, Card card, bool isPin)
    {
        position = currentPos;
        owner = card;
        this.isPin = isPin;
    }
}