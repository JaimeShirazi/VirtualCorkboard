using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GraphCreator : MonoBehaviour
{
    [SerializeField] private PathfinderManager pathfinder;

    public GameObject spherePrefab;
    private List<Transform> spheres = new();

    NodeGraph graph = new();
    IEnumerator Start()
    {
        #region Create the cards and links
        //Dirty random card generator that avoids intersecting cards, this is just for the sake of testing
        List<Card> randomCards = new List<Card>();
        for (int i = 0; i < 16; i++)
        {
            Rect targetShape;
            while (true)
            {
                targetShape = new Rect(Random.Range(-8, 8), Random.Range(-8, 8), Random.Range(0.2f, 3f), Random.Range(0.2f, 3f));
                bool intersectsWithAnotherCard = false;
                foreach (Card card in randomCards)
                {
                    if (card.shape.Overlaps(targetShape))
                    {
                        intersectsWithAnotherCard = true;
                    }
                }
                if (!intersectsWithAnotherCard)
                    break;
            }
            randomCards.Add(new Card(graph, targetShape));
            graph.AddCard(randomCards[i]);
            if (i > 0)
            {
                graph.AddLink(randomCards[i - 1], randomCards[i]);
            }
        }
        //Small corkboard layout with a few linked cards for when I need a testing environment that doesn't change
        /*Card cardA = new Card(graph, new Rect(0, 0, 1, 2));
        Card cardB = new Card(graph, new Rect(2, 3, 0.8f, 3f));
        Card cardC = new Card(graph, new Rect(-4, -1, 2.3f, 1.1f));
        Card cardD = new Card(graph, new Rect(-5, 2.5f, 1.2f, 0.4f));
        graph.AddCard(cardA);
        graph.AddCard(cardB);
        graph.AddCard(cardC);
        graph.AddCard(cardD);
        graph.AddLink(cardA, cardB);
        graph.AddLink(cardB, cardC);
        graph.AddLink(cardB, cardD);*/
        #endregion

        //Build the graph with the data we just entered
        yield return graph.Build();
    }
    public IEnumerator FindRandomPathAsync(int fromID, int toID)
    {
        //Find random nodes to create a path between, this all code is bad and not performant cos its all temporary
        Node randomStart, randomEnd;
        randomStart = graph.DebugGetNode(fromID, false);
        randomEnd = graph.DebugGetNode(toID, false);
        while (randomStart == randomEnd)
        {
            randomEnd = graph.DebugRandomNode(false);
        }
        graph.DebugClearAllNodeMessage();

        //This is the part that actually would be reused
        Pathfinder activeFinder = pathfinder.CreatePathfinder(randomStart, randomEnd);
        yield return activeFinder;

        debugBestPath = activeFinder.bestPath;
    }
    Path debugBestPath;

    [HideInInspector]
    public bool renderCards, renderPins, renderIntercards, renderPath, renderSpheres = true;
    void Update()
    {
        graph.DebugRender(renderCards, renderPins, renderIntercards, renderPath ? debugBestPath : null);
        Vector3[] debugPos = graph.DebugNodePositions();
        for (int i = 0; i < debugPos.Length; i++)
        {
            if (i >= spheres.Count)
            {
                spheres.Add(Instantiate(spherePrefab).transform);
            }
            spheres[i].position = debugPos[i];
            if (spheres[i].gameObject.activeInHierarchy != renderSpheres)
            {
                spheres[i].gameObject.SetActive(renderSpheres);
            }
        }
    }
#if UNITY_EDITOR
    public void OnSceneGUI()
    {
        graph.DebugRenderLabels();
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(GraphCreator))]
public class GraphCreatorEditor : Editor
{
    GraphCreator component;
    bool renderFoldoutState;
    bool renderNodeLabels = true;
    int fromNodeID, toNodeID;
    private void OnEnable()
    {
        component = (GraphCreator)target;
    }
    private void OnSceneGUI()
    {
        if (renderNodeLabels)
            component.OnSceneGUI();
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        renderFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(renderFoldoutState, "Debug Render Toggles");
        if (renderFoldoutState)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                component.renderCards = EditorGUILayout.Toggle("Card links", component.renderCards);
                component.renderPins = EditorGUILayout.Toggle("Pin links", component.renderPins);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                component.renderIntercards = EditorGUILayout.Toggle("Intercard links", component.renderIntercards);
                component.renderPath = EditorGUILayout.Toggle("Path", component.renderPath);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                component.renderSpheres = EditorGUILayout.Toggle("Nodes", component.renderSpheres);
                renderNodeLabels = EditorGUILayout.Toggle("Node labels", renderNodeLabels);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        using (new EditorGUILayout.HorizontalScope())
        {
            fromNodeID = EditorGUILayout.IntField("From", fromNodeID);
            toNodeID = EditorGUILayout.IntField("To", toNodeID);
        }
        if (GUILayout.Button("Generate a random path"))
        {
            component.StartCoroutine(component.FindRandomPathAsync(fromNodeID, toNodeID));
        }
    }
}
#endif