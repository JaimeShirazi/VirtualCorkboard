using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Node
{
#if UNITY_EDITOR
    public int id;
    public static int maxId;
    public string customMessage;
    public void DebugDrawID()
    {
        Handles.Label(position, "ID: " + id + "\n" + customMessage);
    }
#endif

    public readonly Vector2 position;
    public readonly Card owner;
    public readonly bool isPin;
    public Node(Vector2 currentPos, Card card, bool isPin)
    {
        position = currentPos;
        owner = card;
        this.isPin = isPin;
        linkMap = new();
#if UNITY_EDITOR
        id = maxId;
        maxId++;
#endif
    }
    private HashSet<NodeLink> linkMap;
    /// <summary>
    /// Called by NodeLink on construction.
    /// </summary>
    public void AddLink(NodeLink link) => linkMap.Add(link);
    public void RemoveLink(NodeLink link) => linkMap.Remove(link);
    public NodeLink[] Links
    {
        get { return linkMap.ToArray(); }
    }
}