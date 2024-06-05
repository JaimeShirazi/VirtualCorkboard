using System.Collections;
using UnityEngine;

public class GraphCreator : MonoBehaviour
{
    NodeGraph graph = new();
    IEnumerator Start() //fun trick, you can make start an IEnumerator and it will work properly :)
    {
        Card cardA = new Card(new Rect(0, 0, 1, 2));
        Card cardB = new Card(new Rect(2, 3, 0.8f, 3f));
        Card cardC = new Card(new Rect(-4, -1, 2.3f, 1.1f));
        graph.AddCard(cardA);
        graph.AddCard(cardB);
        graph.AddCard(cardC);
        graph.AddLink(cardA, cardB);
        graph.AddLink(cardB, cardC);
        graph.Build();

        yield return new WaitForSeconds(1);

        graph.RemoveLink(cardB, cardC); //this one should be the link between top right and bottom left
        graph.Build();
    }
    void Update()
    {
        graph.Render();
    }
}
