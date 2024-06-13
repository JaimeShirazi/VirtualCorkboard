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
    //We're stalling the main thread while these computations are being completed, just spreading them out across many threads threads.
    //So while we're still multithreading and saving time, we're still missing out on the benefits of having a completely parallel workflow.
    //If I were to split the PrepareJobs() and FinishJobs() across Update and LateUpdate, we could avoid stalling the main thread, but that would force us to only advance the paths by 1 every frame.
    //In a truly multithreaded workflow, we wouldn't have this problem, but since we're using C# Jobs instead of threading, we still need to sync up with the main thread.
    //This is partially against the spirit of multithreading, but it's better than nothing. I might come back at some point later and try make this even more parallel.
    const int ITERATIONS_PER_FRAME = 4;
    private void Start()
    {
        StartCoroutine(FakeUpdate());
    }
    IEnumerator FakeUpdate()
    {
        while (true)
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
                        yield return activePathfinders[i].FinishJobs();
                        if (!activePathfinders[i].keepWaiting)
                        {
                            activePathfinders.RemoveAt(i); continue;
                        }
                    }
                }
            }
            yield return null;
        }
    }
    #endregion
}
