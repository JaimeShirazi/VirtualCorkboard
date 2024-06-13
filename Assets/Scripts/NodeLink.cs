using Unity.Mathematics;
using UnityEngine;

public class NodeLink
{
    public readonly Node start;
    public Vector3 StartPos
    {
        get { return start.position; }
    }
    public readonly Node end;
    public Vector3 EndPos
    {
        get { return end.position; }
    }
    public static explicit operator float4(NodeLink link)
    {
        return new float4(link.StartPos.x, link.StartPos.y, link.EndPos.x, link.EndPos.y);
    }
    /// <summary>
    /// Must be required for a nodelink to be considered "destroyed" or no longer existing.
    /// </summary>
    public void Release()
    {
        if (validPathway)
        {
            start.RemoveLink(this);
            end.RemoveLink(this);
        }
    }
    //Leaving this public as it will be calculated by our compute shader
    public float distance;
    private readonly bool validPathway;
    /// <summary>
    /// The edge that this link represents, if it does.
    /// </summary>
    public readonly Card.Edge? edge;
    public NodeLink(Node a, Node b, bool validPathway, Card.Edge? edge = null)
    {
        start = a;
        end = b;
        this.validPathway = validPathway;
        if (validPathway)
        {
            start.AddLink(this);
            end.AddLink(this);
        }
        this.edge = edge;
    }
    public bool IsOrigin(Node node)
    {
        return node == start;
    }
}