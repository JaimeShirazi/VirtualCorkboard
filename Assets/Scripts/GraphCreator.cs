using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GraphCreator : MonoBehaviour
{
    [SerializeField] private PathfinderManager pathfinder;

    public GameObject spherePrefab;
    private List<Transform> spheres = new();

    NodeGraph graph = new();
    public void BuildGraph()
    {
        StartCoroutine(graph.Build());
    }
    public void RepairPinLinks()
    {
        StartCoroutine(graph.ProcessPinLinks());
    }
    //Dirty random card generator that avoids intersecting cards, this is just for the sake of testing
    public void CreateRandomCardLayout(int amount)
    {
        graph.Clear();

        List<Card> randomCards = new List<Card>();
        for (int i = 0; i < amount; i++)
        {
            Rect targetShape;
            while (true)
            {
                float width = Random.Range(0.2f, 3f);
                float height = Random.Range(0.2f, 3f);
                targetShape = new Rect(Random.Range(-8 + width / 2f, 8 - width / 2f), Random.Range(-8 + height / 2f, 8 - height / 2f), width, height);
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
    }
    public IEnumerator FindManyRandomPathsAsync(int amount)
    {
        List<Pathfinder> pathfinders = new();
        List<int> usedIDs = new();
        for (int i = 0; i < amount; i++)
        {
            Node randomStart = graph.DebugRandomNode(false);
            while (usedIDs.Contains(randomStart.ID))
            {
                randomStart = graph.DebugRandomNode(false);
            }
            usedIDs.Add(randomStart.ID);
            Node randomEnd = graph.DebugRandomNode(false);
            while (usedIDs.Contains(randomEnd.ID))
            {
                randomEnd = graph.DebugRandomNode(false);
            }
            usedIDs.Add(randomEnd.ID);
            pathfinders.Add(pathfinder.CreatePathfinder(randomStart, randomEnd));
        }
        foreach (Pathfinder finder in pathfinders)
        {
            yield return finder;
        }
        debugPaths = pathfinders.Select(finder => finder.result).ToArray();
    }
    public IEnumerator FindPathAsync(int fromID, int toID)
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

        debugPaths = new NavPath[] { activeFinder.result };
    }
    NavPath[] debugPaths;

    [HideInInspector]
    public bool renderCards, renderPins, renderIntercards, renderPath, renderSpheres = true;
    void Update()
    {
        graph.DebugRender(transform.position, renderCards, renderPins, renderIntercards, renderPath ? debugPaths : null);
        Vector3[] debugPos = graph.DebugNodePositions();
        for (int i = 0; i < debugPos.Length; i++)
        {
            if (i >= spheres.Count)
            {
                spheres.Add(Instantiate(spherePrefab, transform).transform);
            }
            spheres[i].localPosition = debugPos[i];
            if (spheres[i].gameObject.activeInHierarchy != renderSpheres)
            {
                spheres[i].gameObject.SetActive(renderSpheres);
            }
        }
    }
#if UNITY_EDITOR
    public void OnSceneGUI()
    {
        graph.DebugRenderLabels(transform.position);
    }
#endif
}

#if UNITY_EDITOR
[CanEditMultipleObjects]
[CustomEditor(typeof(GraphCreator))]
public class GraphCreatorEditor : Editor
{
    List<GraphCreator> components = new();
    bool renderFoldoutState;
    bool renderNodeLabels = false;
    int fromNodeID, toNodeID;
    int pathsToGenerate = 1;
    int cardsToGenerate = 16;
    private void OnEnable()
    {
        components.Add((GraphCreator)target);
        foreach (Object oneTarget in targets)
        {
            if (!components.Contains((GraphCreator)oneTarget))
            {
                components.Add((GraphCreator)oneTarget);
            }
        }
    }
    private void OnSceneGUI()
    {
        if (renderNodeLabels)
        {
            foreach (GraphCreator graphCreator in components)
            {
                graphCreator.OnSceneGUI();
            }
        }
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        renderFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(renderFoldoutState, "Debug Render Toggles");
        if (renderFoldoutState)
        {
            bool newValueBuffer;
            using (new EditorGUILayout.HorizontalScope())
            {
                newValueBuffer = EditorGUILayout.Toggle("Card links", components[0].renderCards);
                foreach (GraphCreator graphCreator in components)
                    graphCreator.renderCards = newValueBuffer;

                newValueBuffer = EditorGUILayout.Toggle("Pin links", components[0].renderPins);
                foreach (GraphCreator graphCreator in components)
                    graphCreator.renderPins = newValueBuffer;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                newValueBuffer = EditorGUILayout.Toggle("Intercard links", components[0].renderIntercards);
                foreach (GraphCreator graphCreator in components)
                    graphCreator.renderIntercards = newValueBuffer;

                newValueBuffer = EditorGUILayout.Toggle("Path", components[0].renderPath);
                foreach (GraphCreator graphCreator in components)
                    graphCreator.renderPath = newValueBuffer;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                newValueBuffer = EditorGUILayout.Toggle("Nodes", components[0].renderSpheres);
                foreach (GraphCreator graphCreator in components)
                    graphCreator.renderSpheres = newValueBuffer;

                renderNodeLabels = EditorGUILayout.Toggle("Node labels", renderNodeLabels);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            cardsToGenerate = Mathf.Max(EditorGUILayout.IntField("Cards", cardsToGenerate), 1);
            if (GUILayout.Button("Create new layout"))
            {
                foreach (GraphCreator graphCreator in components)
                {
                    graphCreator.CreateRandomCardLayout(cardsToGenerate);
                }
            }
        }
        EditorGUILayout.Space(8);
        if (GUILayout.Button("Build graphs"))
        {
            foreach (GraphCreator graphCreator in components)
            {
                graphCreator.BuildGraph();
            }
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Repair pin links"))
        {
            foreach (GraphCreator graphCreator in components)
            {
                graphCreator.RepairPinLinks();
            }
        }

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            pathsToGenerate = Mathf.Max(EditorGUILayout.IntField("Num. o/paths to find", pathsToGenerate), 1);
            if (GUILayout.Button("Go"))
            {
                foreach (GraphCreator graphCreator in components)
                {
                    graphCreator.StartCoroutine(graphCreator.FindManyRandomPathsAsync(pathsToGenerate));
                }
            }
        }

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            fromNodeID = EditorGUILayout.IntField("From", fromNodeID);
            toNodeID = EditorGUILayout.IntField("To", toNodeID);
        }
        if (GUILayout.Button("Generate path with target"))
        {
            foreach (GraphCreator graphCreator in components)
            {
                graphCreator.StartCoroutine(graphCreator.FindPathAsync(fromNodeID, toNodeID));
            }
        }
    }
}
#endif