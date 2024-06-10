using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System;

public class PathfinderManager : MonoBehaviour
{
    private List<Pathfinder> activePathfinders = new();
    public Pathfinder CreatePathfinder(Node from, Node to)
    {
        //Do some checks to ensure we can create a pathfinder for these nodes
        if (from.graph != to.graph)
            throw new ArgumentException("Cannot create a node for Nodes in different NodeGraphs.");
        if (!from.graph.IsReady)
            throw new ArgumentException("Cannot start pathfinding on a graph that hasn't been built.");

        //Open a new pathfinder and returns the reference to the caller.
        activePathfinders.Add(new Pathfinder(from, to));
        return activePathfinders[^1];
    }
    void Update()
    {
        //We remove finished pathfinders, and call their Updates.
        //This approach is inherently singlethreaded, I would like to figure out if it's possible to multithread this in the future.
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
        //We remove finished pathfinders, and call their LateUpdates.
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
