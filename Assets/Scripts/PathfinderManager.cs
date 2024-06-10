using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
public class PathfinderManager : MonoBehaviour
{
    [SerializeField] private NodeGraph graph;
    private List<Pathfinder> activePathfinders = new();
    public Pathfinder CreatePathfinder(Node from, Node to)
    {
        activePathfinders.Add(new Pathfinder(from, to));
        return activePathfinders[activePathfinders.Count - 1];
    }
    void Update()
    {
        for (int i = activePathfinders.Count - 1; i >= 0; i--)
        {
            if (!activePathfinders[i].keepWaiting)
            {
                activePathfinders.RemoveAt(i); continue;
            }
            activePathfinders[i].Update();
        }
    }
    void LateUpdate()
    {
        for (int i = activePathfinders.Count - 1; i >= 0; i--)
        {
            if (!activePathfinders[i].keepWaiting)
            {
                activePathfinders.RemoveAt(i); continue;
            }
            activePathfinders[i].LateUpdate();
        }
    }
}
