using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GraphCreator : MonoBehaviour
{
    [SerializeField] private PathfinderManager pathfinder;

    private ComputeShader intersectCheckShader, lengthShader;

    public GameObject spherePrefab;
    private List<Transform> spheres = new();

    NodeGraph graph = new();
    void Awake()
    {
        intersectCheckShader = Resources.Load("LineSegmentIntersectCheck") as ComputeShader;
        lengthShader = Resources.Load("LineSegmentLengthCalculator") as ComputeShader;
    }
    void Start()
    {
        List<Card> randomCards = new List<Card>();
        for (int i = 0; i < 10; i++)
        {
            randomCards.Add(new Card(new Rect(Random.Range(-10, 10), Random.Range(-10, 10), Random.Range(0.1f, 2f), Random.Range(0.1f, 2f))));
            graph.AddCard(randomCards[i]);
            if (i > 0)
            {
                graph.AddLink(randomCards[i - 1], randomCards[i]);
            }
        }
        /*Card cardA = new Card(new Rect(0, 0, 1, 2));
        Card cardB = new Card(new Rect(2, 3, 0.8f, 3f));
        Card cardC = new Card(new Rect(-4, -1, 2.3f, 1.1f));
        Card cardD = new Card(new Rect(-5, 2.5f, 1.2f, 0.4f));
        graph.AddCard(cardA);
        graph.AddCard(cardB);
        graph.AddCard(cardC);
        graph.AddCard(cardD);
        graph.AddLink(cardA, cardB);
        graph.AddLink(cardB, cardC);
        graph.AddLink(cardB, cardD);*/
        graph.Build();
        StartCoroutine(CullAndFindPath());
    }
    IEnumerator CullAndFindPath()
    {
        //We have to wait so the culling is finished before the pathfinding is done
        yield return StartCoroutine(graph.CullIntersectsAndGetLength(intersectCheckShader, lengthShader));

        Node randomStart, randomEnd;
        randomStart = graph.DebugRandomNode(false);
        randomEnd = graph.DebugRandomNode(false);
        while (randomStart == randomEnd)
        {
            randomEnd = graph.DebugRandomNode(false);
        }
        yield return StartCoroutine(FindPathAsync(randomStart, randomEnd));
    }
    Path debugBestPath;
    IEnumerator FindPathAsync(Node from, Node to)
    {
        Pathfinder activeFinder = pathfinder.CreatePathfinder(from, to);
        yield return activeFinder;
        debugBestPath = activeFinder.bestPath;
    }
    void Update()
    {
        graph.DebugRender(debugBestPath);
        Vector3[] debugPos = graph.DebugNodePositions();
        for (int i = 0; i < debugPos.Length; i++)
        {
            if (i >= spheres.Count)
            {
                spheres.Add(Instantiate(spherePrefab).transform);
            }
            spheres[i].position = debugPos[i];
        }
    }
#if UNITY_EDITOR
    public void OnSceneGUI()
    {
        graph.DebugRenderLabels();
    }
#endif
}
[CustomEditor(typeof(GraphCreator))]
public class GraphCreatorEditor : Editor
{
    private void OnSceneGUI()
    {
        GraphCreator graph = target as GraphCreator;
        graph.OnSceneGUI();
    }
}