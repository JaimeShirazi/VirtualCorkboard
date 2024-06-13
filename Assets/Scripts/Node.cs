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

    // 0 ------- 1
    // |         |
    // 2 ------- 3
    public readonly int corner;
    public bool ShouldExpand
    {
        get
        {
            return corner >= 0;
        }
    }
    public Vector2[] GetExpansionDirectionVectors()
    {
        //A degrees offset that the corner expansion vectors are from like perfectly diagonal (like parallel and perpendicular to the line given by y=x)
        //The effective range would be 0<offset<=45
        const float OFFSET_DEGREES = 30f;
        float baseAngle = corner switch
        {
            0 => 135f,
            1 => 45f,
            2 => 225f,
            3 => 315f,
            _ => 0
        };
        return new Vector2[]
        {
            new Vector2(Mathf.Cos(Mathf.Deg2Rad * (baseAngle - OFFSET_DEGREES)), Mathf.Sin(Mathf.Deg2Rad * (baseAngle - OFFSET_DEGREES))),
            new Vector2(Mathf.Cos(Mathf.Deg2Rad * (baseAngle + OFFSET_DEGREES)), Mathf.Sin(Mathf.Deg2Rad * (baseAngle + OFFSET_DEGREES)))
        };
    }
    public Node(NodeGraph graph, Card card, Vector2 position, bool isPin, int corner = -1)
    {
        nodeID = highestID; highestID++;

        this.graph = graph;
        this.card = card;
        this.position = position;
        this.isPin = isPin;
        this.corner = corner;

        linkMap = new();
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