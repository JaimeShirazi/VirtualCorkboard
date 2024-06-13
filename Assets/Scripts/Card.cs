using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Card
{
    private static int maxID; //Make sure to change this data type if you need more than 2 billion cards at a time lmao
    private int thisID;
    /// <summary>
    /// <para>An order-irrespective value representing two cards.</para>
    /// <para>This code was slightly modified from a stackoverflow answer available here: https://stackoverflow.com/a/4226345</para>
    /// </summary>
    /// <returns>A 64-bit value representing two cards that will always be the same, regardless of the order of the cards</returns>
    public static ulong GetCardPairID(Card a, Card b)
    {
        ulong id = a.thisID > b.thisID ? (uint)b.thisID | ((ulong)a.thisID << 32) :
                                         (uint)a.thisID | ((ulong)b.thisID << 32);
        return id;
    }

    private readonly NodeGraph graph;

    public readonly Rect shape;
    public Card(NodeGraph graph, Rect rect)
    {
        //Assign this card's ID, increment the static counter by 1 for the next card
        thisID = maxID; maxID++;

        this.graph = graph;

        shape = rect;

        // -- GENERATE THE CORNER NODES --
        cornerNodes = new Node[4];
        //Top left
        cornerNodes[0] = new Node(graph, this, new Vector2(shape.xMin, shape.yMax), false);
        //Top right
        cornerNodes[1] = new Node(graph, this, new Vector2(shape.xMax, shape.yMax), false);
        //Bottom left
        cornerNodes[2] = new Node(graph, this, new Vector2(shape.xMin, shape.yMin), false);
        //Bttom right
        cornerNodes[3] = new Node(graph, this, new Vector2(shape.xMax, shape.yMin), false);

        links = new();
        linksTargetEdge = new();
        cornerLinks = new();
        invalidCornerLinks = new();
    }
    enum Edge
    {
        North, East, South, West
    }
    List<Card> links;
    List<Edge> linksTargetEdge;
    /// <summary>
    /// Many lists share the same indexes for cards (e.g the links and pinNodes list line up perfectly), so this dictionary buffers the index for each card's ID
    /// </summary>
    Dictionary<int, int> linkIndexLookup = new();

    /// <summary>
    /// Adds a link between this card and the given card.
    /// </summary>
    /// <param name="to">The target card.</param>
    public void AddLink(Card to)
    {
        if (to == this) return;

        //Calculate the target edge using the shapes of the two cards
        linksTargetEdge.Add(TargetSide(shape, to.shape));

        //Add the card to a buffer tracking all the cards this card has links to.
        links.Add(to);

        //Buffer this card's index
        linkIndexLookup.Add(to.thisID, links.Count - 1);

        dataIsStale = true; //Internally mark that this card needs its data regenerated before being read by other objects
    }
    /// <summary>
    /// Returns the best-fit edge for the first rect to link to the second rect.
    /// </summary>
    /// <param name="from">The card to find the best edge for</param>
    /// <param name="to">The card that this edge will link to</param>
    /// <returns>The best-fit edge</returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    Edge TargetSide(Rect from, Rect to)
    {
        //Distance between the rect edges, ordered NESW
        float[] distance = new float[4];
        distance[0] = to.yMin - from.yMax;
        distance[1] = to.xMin - from.xMax;
        distance[2] = from.yMin - to.yMax;
        distance[3] = from.xMin - to.xMax;

        int bestIndex = -1;
        float bestValue = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            if (distance[i] > 0)
            {
                if (distance[i] > bestValue)
                {
                    bestValue = distance[i];
                    bestIndex = i;
                }
            }
        }

        return bestIndex switch
        {
            0 => Edge.North,
            1 => Edge.East,
            2 => Edge.South,
            3 => Edge.West,
            -1 => Edge.North, //A card is clipping, but I would rather it fails silently than it completely stops execution
            _ => throw new IndexOutOfRangeException("Recieved a greatest distance edge value that shouldn't be possible.")
        };
    }
    public void RemoveLink(Card to)
    {
        if (!linkIndexLookup.TryGetValue(to.thisID, out int targetIndex)) return;

        links.RemoveAt(targetIndex);
        linksTargetEdge.RemoveAt(targetIndex);
        linkIndexLookup.Remove(to.thisID);

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
    /// <summary>
    /// The links along the corners that are valid pathways for pathfinding
    /// </summary>
    public ReadOnlyCollection<NodeLink> ValidCornerLinks
    {
        get
        {
            StaleDataCheck();
            return cornerLinks.AsReadOnly();
        }
    }
    List<NodeLink> invalidCornerLinks;
    /// <summary>
    /// Fake links that are only used for collision testing and not considered valid pathways for pathfinding
    /// </summary>
    public ReadOnlyCollection<NodeLink> InvalidCornerLinks
    {
        get
        {
            StaleDataCheck();
            return invalidCornerLinks.AsReadOnly();
        }
    }
    public Node GetLinkingNode(Card target)
    {
        StaleDataCheck();
        return pinNodes[linkIndexLookup[target.thisID]];
    }
    /// <param name="side">Which edge to get the ordered cards from</param>
    /// <returns>An ordered list of cards for the given edge (left to right or top to bottom)</returns>
    PriorityQueue<Card, float> GetOrderedLinksFromEdge(Edge side)
    {
        float GetCost(Card link)
        {
            switch (side)
            {
                case Edge.North: case Edge.South: default:
                    return shape.center.x - link.shape.center.x;
                case Edge.East: case Edge.West:
                    return shape.center.y - link.shape.center.y;
            }
        }

        PriorityQueue<Card, float> orderedLinks = new();
        for (int i = 0; i < links.Count; i++)
        {
            if (linksTargetEdge[i] == side)
                orderedLinks.Enqueue(links[i], GetCost(links[i]));
        }
        return orderedLinks;
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
    /// Generates the pin nodes and links necessary
    /// </summary>
    void GeneratePinsAndLinks()
    {
        pinNodes = new Node[links.Count];
        cornerLinks.Clear();
        invalidCornerLinks.Clear();

        Edge[] allEdges = System.Enum.GetValues(typeof(Edge)) as Edge[];
        PriorityQueue<Card, float> orderedQueueBuffer;

        foreach (Edge edge in allEdges)
        {
            float GetEdgeLength()
            {
                switch (edge)
                {
                    case Edge.North:
                    case Edge.South:
                    default:
                        return shape.width;
                    case Edge.East:
                    case Edge.West:
                        return shape.height;
                }
            }
            Node GetFirstCornerNode() //Returns the topmost/leftmost node on this edge
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
            Node GetLastCornerNode() //Returns the bottommost/rightmost node on this edge
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

            //Get the pins on this side of the card, in the order they should be
            orderedQueueBuffer = GetOrderedLinksFromEdge(edge);

            if (orderedQueueBuffer.Count > 0) //if there are pins on this side
            {
                int totalPins = orderedQueueBuffer.Count;
                int currentPin = 0;
                List<Card> orderedListBuffer = new(); //Since we need to iterate through the entire list anyways, i figured we might as well just do it here instead of inside GetOrderedLinksFromEdge()
                while (orderedQueueBuffer.Count > 0)
                {
                    orderedListBuffer.Add(orderedQueueBuffer.Dequeue());

                    //A value between 0 to 1 representing where along the edge this node should be distributed
                    float distribution = (currentPin + 1) / (float)(totalPins + 1);
                    //Create a new node at a position along the edge based on order
                    pinNodes[linkIndexLookup[orderedListBuffer[^1].thisID]] = new Node(graph, this, PinPositionCalculator(edge, distribution), true);
                    currentPin++;
                }

                //Create links between the corners of the edge and the leftmost/rightmost pin links
                cornerLinks.Add(new NodeLink(GetFirstCornerNode(), pinNodes[linkIndexLookup[orderedListBuffer[0].thisID]], true));
                cornerLinks[^1].distance = GetEdgeLength() / (totalPins + 1f);
                cornerLinks.Add(new NodeLink(pinNodes[linkIndexLookup[orderedListBuffer[^1].thisID]], GetLastCornerNode(), true));
                cornerLinks[^1].distance = cornerLinks[^2].distance;

                //These are generated for the sake of collision testing, not as valid pathways for strings.
                invalidCornerLinks.Add(new NodeLink(GetFirstCornerNode(), GetLastCornerNode(), false));
                //Generate the distance values for the edge anyways to avoid potential bugs if the code is modified later, i doubt this adds much overhead anyways
                invalidCornerLinks[^1].distance = GetEdgeLength();
                switch (edge)
                {
                    case Edge.North: case Edge.South:
                        invalidCornerLinks[^1].distance = shape.height;
                        break;
                    case Edge.East: case Edge.West:
                        invalidCornerLinks[^1].distance = shape.width;
                        break;
                }
            }
            else //if there are no pins on this side
            {
                //Create a link along the edge of this card
                cornerLinks.Add(new NodeLink(GetFirstCornerNode(), GetLastCornerNode(), true));
                cornerLinks[^1].distance = GetEdgeLength();
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