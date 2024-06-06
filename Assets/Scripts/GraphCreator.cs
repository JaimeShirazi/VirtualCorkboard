using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphCreator : MonoBehaviour
{
    public ComputeShader shader;

    public GameObject spherePrefab;
    private List<Transform> spheres = new();

    NodeGraph graph = new();
    void Start()
    {
        Card cardA = new Card(new Rect(0, 0, 1, 2));
        Card cardB = new Card(new Rect(2, 3, 0.8f, 3f));
        Card cardC = new Card(new Rect(-4, -1, 2.3f, 1.1f));
        Card cardD = new Card(new Rect(-5, 2.5f, 1.2f, 0.4f));
        graph.AddCard(cardA);
        graph.AddCard(cardB);
        graph.AddCard(cardC);
        graph.AddCard(cardD);
        graph.AddLink(cardA, cardB);
        graph.AddLink(cardB, cardC);
        graph.AddLink(cardB, cardD);
        graph.Build();
        graph.RunComputeShader(shader);
        StartCoroutine(graph.DebugShaderOneAtATime());
    }
    void Update()
    {
        graph.DebugRender();
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
}
