using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class Card
{
    Rect shape;
    public Card(Rect rect)
    {
        shape = rect;

        // -- GENERATE THE CORNER NODES --
        cornerNodes = new Node[4];
        //Top left
        cornerNodes[0] = new Node(new Vector2(shape.xMin, shape.yMax));
        //Top right
        cornerNodes[1] = new Node(new Vector2(shape.xMax, shape.yMax));
        //Bottom left
        cornerNodes[2] = new Node(new Vector2(shape.xMin, shape.yMin));
        //Bttom right
        cornerNodes[3] = new Node(new Vector2(shape.xMax, shape.yMin));

        links = new();
        linksTargetEdge = new();
        cornerLinks = new();
    }
    enum Edge
    {
        North, East, South, West
    }
    List<Card> links;
    List<Edge> linksTargetEdge;
    public void AddLink(Card to)
    {
        if (to == this) return;

        Edge targetEdgeBuffer = Edge.North;
        //TODO: Implement logic for calculating the target edge based on the position of the card relative to this card
        Debug.LogWarning("Logic for calculating target edge for pin has not been implemented!");
        linksTargetEdge.Add(targetEdgeBuffer);

        links.Add(to);

        dataIsStale = true; //Internally mark that this card needs its data regenerated before being read by other objects
    }
    public void RemoveLink(Card to)
    {
        int targetIndex = links.IndexOf(to);
        if (targetIndex < 0) return;

        links.RemoveAt(targetIndex);
        linksTargetEdge.RemoveAt(targetIndex);

        dataIsStale = true; //Internally mark that this card needs its data regenerated before being read by other objects
    }
    Node[] cornerNodes;
    public Node[] CornerNodes
    {
        get
        {
            StaleDataCheck();
            return cornerNodes;
        }
    }
    Node[] pinNodes;
    public Node[] PinNodes
    {
        get
        {
            StaleDataCheck();
            return pinNodes;
        }
    }
    List<NodeLink> cornerLinks;
    public ReadOnlyCollection<NodeLink> AllNodeLinks
    {
        get
        {
            StaleDataCheck();
            return cornerLinks.AsReadOnly();
        }
    }
    public Node GetLinkingNode(Card target)
    {
        StaleDataCheck();
        return pinNodes[links.IndexOf(target)];
    }
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
    /// <summary>
    /// Used to make the code in GeneratePinsAndLinks edge-type-generic.
    /// </summary>
    /// <param name="edge">Which edge we need the first corner of</param>
    /// <returns>The leftmost/topmost corner node, depending on the edge</returns>
    Node GetFirstCornerNode(Edge edge)
    {
        switch (edge)
        {
            default:
            case Edge.North: return cornerNodes[0];
            case Edge.East: return cornerNodes[1];
            case Edge.South: return cornerNodes[2];
            case Edge.West: return cornerNodes[0];
        }
    }
    /// <summary>
    /// Used to make the code in GeneratePinsAndLinks edge-type-generic.
    /// </summary>
    /// <param name="edge">Which edge we need the last corner of</param>
    /// <returns>The rightmost/bottommost corner node, depending on the edge</returns>
    Node GetLastCornerNode(Edge edge)
    {
        switch (edge)
        {
            default:
            case Edge.North: return cornerNodes[1];
            case Edge.East: return cornerNodes[3];
            case Edge.South: return cornerNodes[3];
            case Edge.West: return cornerNodes[2];
        }
    }
    /// <summary>
    /// Generates the pin nodes and links necessary
    /// </summary>
    void GeneratePinsAndLinks()
    {
        pinNodes = new Node[links.Count];

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
                    pinNodes[links.IndexOf(orderedLinksBuffer[i])] = new Node(PinPositionCalculator(edge, distribution));
                }
            }
            else //if there are no pins on this side
            {
                //Create a static link along the edge of this card
                cornerLinks.Add(new StaticNodeLink(GetFirstCornerNode(edge), GetLastCornerNode(edge)));
            }
        }

        dataIsStale = false; //Internally mark this card as having up-to-date nodes and links
    }
    /// <summary>
    /// Has the node layout changed since the card last buffered data? This will prevent the card from processing until it is done recieving links.
    /// </summary>
    private bool dataIsStale = true;
    /// <summary>
    /// Checks if the data generated by the card is out of date, and regenerates it if it is.
    /// </summary>
    void StaleDataCheck()
    {
        if (dataIsStale)
            GeneratePinsAndLinks();
    }
}