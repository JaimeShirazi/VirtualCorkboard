using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.VisualScripting;
using UnityEngine;

public class Node
{
    public readonly Vector2 position;
    public Node(Vector2 currentPos)
    {
        position = currentPos;
    }
}
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
public class Card
{
    Rect shape;
    enum Edge
    {
        North, East, South, West
    }
    List<Card> links;
    List<Edge> linksTargetEdge;
    public void AddLink(Card to)
    {
        Edge targetEdgeBuffer = Edge.North;
        //TODO: Implement logic for calculating the target edge based on the position of the card relative to this card
        Debug.LogWarning("Logic for calculating target edge for pin has not been implemented!");
        linksTargetEdge.Add(targetEdgeBuffer);

        links.Add(to);

        GenerateCores();
    }
    public void RemoveLink(Card to)
    {
        linksTargetEdge.RemoveAt(links.IndexOf(to));
        links.Remove(to);

        GenerateCores();
    }
    public Card(Rect rect)
    {
        shape = rect;
        links = new();
        linksTargetEdge = new();
        coreLinks = new();
    }
    Node[] coreNodes;
    List<NodeLink> coreLinks;
    /// <param name="side">Which edge to get the ordered cards from</param>
    /// <returns>An ordered list of cards for the given edge (left to right or top to bottom)</returns>
    Card[] GetOrderedLinksFromEdge(Edge side)
    {
        //TODO: Write a function responsible for calculating which order the pins on a given edge belong on
        Debug.LogWarning("Logic for calculating the target order for the pins on a given edge has not been implemented!");

        //temporary solution just doing it based on list order
        List<Card> orderedLinks = new();
        for (int i = 0; i < links.Count; i++)
        {
            if (linksTargetEdge[i] == side)
                orderedLinks.Add(links[i]);
        }
        return orderedLinks.ToArray();
    }
    /// <summary>
    /// Finds the position along a given edge, given a value from 0 to 1 representing how far along
    /// </summary>
    Vector2 PinPositionCalculator(Edge edge, float distribution)
    {
        switch (edge)
        {
            case Edge.North:
            default:
                return new Vector2(
                    Mathf.Lerp(shape.xMin, shape.xMax, distribution),
                    shape.yMax);
            case Edge.East:
                return new Vector2(
                    shape.xMax,
                    Mathf.Lerp(shape.yMax, shape.yMin, distribution));
            case Edge.South:
                return new Vector2(
                    Mathf.Lerp(shape.xMin, shape.xMax, distribution),
                    shape.yMin);
            case Edge.West:
                return new Vector2(
                    shape.xMin,
                    Mathf.Lerp(shape.yMax, shape.yMin, distribution));
        }
    }
    Node GetFirstCornerNode(Edge edge)
    {
        switch (edge)
        {
            default:
            case Edge.North: return coreNodes[0];
            case Edge.East: return coreNodes[1];
            case Edge.South: return coreNodes[2];
            case Edge.West: return coreNodes[0];
        }
    }
    Node GetLastCornerNode(Edge edge)
    {
        switch (edge)
        {
            default:
            case Edge.North: return coreNodes[1];
            case Edge.East: return coreNodes[3];
            case Edge.South: return coreNodes[3];
            case Edge.West: return coreNodes[2];
        }
    }
    void GenerateCores()
    {
        coreNodes = new Node[4 + links.Count];
        // -- GENERATE THE CORNER NODES --
        //Top left
        coreNodes[0] = new Node(new Vector2(shape.xMin, shape.yMax));
        //Top right
        coreNodes[1] = new Node(new Vector2(shape.xMax, shape.yMax));
        //Bottom left
        coreNodes[2] = new Node(new Vector2(shape.xMin, shape.yMin));
        //Bttom right
        coreNodes[3] = new Node(new Vector2(shape.xMax, shape.yMin));

        Edge[] allEdges = System.Enum.GetValues(typeof(Edge)) as Edge[];
        Card[] orderedLinksBuffer;

        foreach (Edge edge in allEdges)
        {
            //Get the pins on this side of the card, in the order they should be
            orderedLinksBuffer = GetOrderedLinksFromEdge(edge);

            if (orderedLinksBuffer.Length > 0) //if there are pins on this side
            {
                for (int i = 0; i < orderedLinksBuffer.Length; i++)
                {
                    //A value between 0 to 1 representing where along the edge this node should be distributed
                    float distribution = (i + 1) / (float)(orderedLinksBuffer.Length + 1);
                    //Create a new node at a position along the edge based on order
                    coreNodes[4 + links.IndexOf(orderedLinksBuffer[i])] = new Node(PinPositionCalculator(edge, distribution));
                }
            }
            else //if there are no pins on this side
            {
                //Create a static link along the edge of this card
                coreLinks.Add(new StaticNodeLink(GetFirstCornerNode(edge), GetLastCornerNode(edge)));
            }
        }
    }
    public Node GetLinkingNode(Card target)
    {
        return coreNodes[4 + links.IndexOf(target)];
    }
    public Node[] AllNodes
    {
        get { return coreNodes; }
    }
    public ReadOnlyCollection<NodeLink> AllNodeLinks
    {
        get { return coreLinks.AsReadOnly(); }
    }
}
public class NodeGraph
{
    public List<NodeLink> links = new();
    public List<Node> nodes = new();

    private List<Card> cards = new();
    public void Clear()
    {
        nodes.Clear();
        links.Clear();
    }
    public void AddCard(Card target)
    {
        cards.Add(target);
    }
    public void AddLink(Card from, Card to)
    {
        from.AddLink(to);
        to.AddLink(from);

        links.Add(new DynamicNodeLink(
            from.GetLinkingNode(to),
            to.GetLinkingNode(from)
            ));
    }
    public void Build()
    {
        foreach (Card card in cards)
        {
            nodes.AddRange(card.AllNodes);
            links.AddRange(card.AllNodeLinks);
        }
    }
    public void Render()
    {
        foreach (NodeLink link in links)
        {
            Debug.DrawLine(link.StartPos, link.EndPos, Color.blue, 1);
        }
    }
}
public class AStarPathfinder : MonoBehaviour
{
    NodeGraph graph = new();
    void Start()
    {
        graph.Clear();
        Card cardA = new Card(new Rect(0, 0, 1, 2));
        Card cardB = new Card(new Rect(2, 3, 0.8f, 3f));
        Card cardC = new Card(new Rect(-4, -1, 2.3f, 1.1f));
        graph.AddCard(cardA);
        graph.AddCard(cardB);
        graph.AddCard(cardC);
        graph.AddLink(cardA, cardB);
        graph.AddLink(cardB, cardC);
        graph.Build();
    }
    void Update()
    {
        graph.Render();
    }
}
