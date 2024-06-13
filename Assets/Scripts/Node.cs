using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Node
{
    #region ID management
    public int ID
    {
        get
        {
            return nodeID;
        }
    }
    private int nodeID;
    public static int highestID;
    #endregion

    public readonly Vector2 position;
    /// <summary>
    /// The card that this Node comes from.
    /// </summary>
    public readonly Card card;
    /// <summary>
    /// The NodeGraph that this Node comes from.
    /// </summary>
    public readonly NodeGraph graph;
    public readonly bool isPin;

    /// <summary>
    /// If not zero, informs the direction that an extra node should be placed, if this node has a path go through it.
    /// </summary>
    public readonly Vector2 expandOutwardsDir;
    public Node(NodeGraph graph, Card card, Vector2 position, bool isPin, Vector2 expandOutwardsDir)
    {
        nodeID = highestID; highestID++;

        this.graph = graph;
        this.card = card;
        this.position = position;
        this.isPin = isPin;

        linkMap = new();

        this.expandOutwardsDir = expandOutwardsDir;
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

    #region Debug functions that won't be compiled for the release
#if UNITY_EDITOR
    public string debug_customMessage;
    /// <summary>
    /// Should only be called during OnSceneGUI, by an Editor capable of receiving it.
    /// </summary>
    public void Debug_DrawID(Vector3 offset)
    {
        Handles.Label((Vector3)position + offset, "ID: " + ID + "\n" + debug_customMessage);
    }
#endif
    #endregion
}