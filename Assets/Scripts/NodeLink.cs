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
        start.RemoveLink(this);
        end.RemoveLink(this);
    }
    //Leaving this public as it will be calculated by our compute shader
    public float distance;
    public NodeLink(Node a, Node b)
    {
        start = a;
        end = b;
        start.AddLink(this);
        end.AddLink(this);
    }
    public bool IsOrigin(Node node)
    {
        return node == start;
    }
}