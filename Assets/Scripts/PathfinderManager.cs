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
    #region Updating the pathfinders
    const int ITERATIONS_PER_FRAME = 4;
    void Update()
    {
        if (activePathfinders.Count > 0)
        {
            for (int a = 0; a < ITERATIONS_PER_FRAME; a++)
            {
                for (int i = activePathfinders.Count - 1; i >= 0; i--)
                {
                    activePathfinders[i].PrepareJobs();
                }
                for (int i = activePathfinders.Count - 1; i >= 0; i--)
                {
                    activePathfinders[i].FinishJobs();
                    if (!activePathfinders[i].keepWaiting)
                    {
                        activePathfinders.RemoveAt(i); continue;
                    }
                }
            }
        }
    }
    #endregion
}
