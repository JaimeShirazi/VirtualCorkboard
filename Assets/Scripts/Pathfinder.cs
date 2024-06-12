using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// A pathfinder object handles a given pathfind operation. It inherits from CustomYieldInstuction, as it is a task that happens asynchronously and can be used to yield.
/// </summary>
public class Pathfinder : CustomYieldInstruction
{
    /// <summary>
    /// The PriorityQueue that holds all the open paths and their costs.
    /// All split paths get added back here, and the lowest estimated cost path is repeatedly popped from the top of the queue until one of the paths are completed.
    /// </summary>
    private PriorityQueue<NavPath, float> openPaths = new();

    /// <summary>
    /// Once a pathfinder completes, this result will store the best path that the pathfinder found.
    /// </summary>
    public NavPath result
    {
        get
        {
            return bestPath;
        }
    }
    private NavPath bestPath;

    /// <summary>
    /// Called by the PathfinderManager when a new pathfind operation is intended to be started.
    /// </summary>
    /// <param name="from">The node to start at.</param>
    /// <param name="to">The node to reach.</param>
    public Pathfinder(Node from, Node to)
    {
        //Create a brand new path with 0 links and enqueue it
        NavPath initialPath = new NavPath(from, to);
        openPaths.Enqueue(initialPath, initialPath.Cost);

#if UNITY_EDITOR
        //These will help with visualising what's going on
        from.debug_customMessage = "Start";
        to.debug_customMessage = "End";
#endif
    }

    #region CustomYieldInstruction logic
    /// <summary>
    /// Used by CustomYieldInstruction to determine if it should continue to yield.
    /// </summary>
    public override bool keepWaiting
    {
        get
        {
            return isBusy;
        }
    }
    private bool isBusy = true;
    #endregion

    #region Path handling logic
    /// <summary>
    /// A buffer that holds onto the currently executing Path split request.
    /// </summary>
    NavPath.SplitRequest pendingRequest;
    /// <summary>
    /// A buffer that holds onto the currently executing JobHandle.
    /// </summary>
    JobHandle jobHandle;

    /// <summary>
    /// Takes the highest priority path, splits it, and prepares the jobs to be ran across multiple CPU threads.
    /// </summary>
    public void PrepareJobs()
    {
        //Get a SplitRequest struct from the highest priority path, representing the result of splitting that path into a bunch of new paths
        //Grab the highest priority path, split it, and hold onto the struct representing the data we will need from it.
        pendingRequest = openPaths.Dequeue().Split();

        //Schedule the parallel Job
        jobHandle = pendingRequest.job.Schedule(pendingRequest.threads, 1);
    }
    /// <summary>
    /// Ensures all the jobs are completed, and evaluates the results.
    /// </summary>
    public void FinishJobs()
    {
        //Stall the main thread if the jobs aren't all complete
        jobHandle.Complete();
        //Call the struct's onComplete function now that the job is done, and buffer the new paths we get returned
        NavPath[] newPaths = pendingRequest.onComplete();
        float bestCost = float.MaxValue;
        for (int i = 0; i < newPaths.Length; i++)
        {
            float pathCost = newPaths[i].Cost; //Buffering cost since we'll always need it at least once, sometimes more. This will save picoseconds of processing but whatever
            if (newPaths[i].IsComplete)
            {
                isBusy = false;
                //Assign the path if it's the lowest cost
                if (pathCost < bestCost)
                {
                    bestCost = pathCost;
                    bestPath = newPaths[i];
                }
            }
            else
            {
                if (!newPaths[i].IsDead)
                    openPaths.Enqueue(newPaths[i], pathCost);
            }
        }
    }
    #endregion
}