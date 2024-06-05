using UnityEngine;

public abstract class NodeLink
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
    //Leaving this public as it will be calculated by our compute shader
    public float distance;
    public NodeLink(Node a, Node b)
    {
        start = a;
        end = b;
    }
    /// <summary>
    /// Whether this link can be destroyed due to an intersection or not
    /// </summary>
    public abstract bool IsFixed { get; }
}
public class StaticNodeLink : NodeLink
{
    public StaticNodeLink(Node a, Node b) : base(a, b) { }
    public override bool IsFixed { get { return true; } }
}
public class DynamicNodeLink : NodeLink
{
    public DynamicNodeLink(Node a, Node b) : base(a, b) { }
    public override bool IsFixed { get { return false; } }
}