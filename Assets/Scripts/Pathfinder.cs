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
    public NavPath Result
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
    }

    #region CustomYieldInstruction logic
    /// <summary>
    /// Used by CustomYieldInstruction to determine if it should continue to yield.
    /// </summary>
    public override bool keepWaiting
    {
        get
        {
            return bestPath == null;
        }
    }
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
    /// Tracks the best cost-so-far for every node we visit. This allows pathfinders to cull paths that end at nodes we've already visited with a better cost-so-far.
    /// </summary>
    private Dictionary<Node, float> bestPrice = new();
    /// <summary>
    /// Fetches the best cost-so-far for a given node since this pathfinder started.
    /// </summary>
    /// <param name="node">The target node.</param>
    /// <returns>The best cost-so-far last time this pathfinder visited this node, or float.MaxValue if this node hasn't been visited.</returns>
    public float CheckBestNodePrice(Node node)
    {
        if (bestPrice.ContainsKey(node))
        {
            return bestPrice[node];
        }
        else
        {
            return float.MaxValue;
        }
    }
    private void UpdateNodePrice(Node node, float price)
    {
        if (!bestPrice.ContainsKey(node))
        {
            bestPrice.Add(node, price);
        }
        else if (bestPrice[node] > price)
        {
            bestPrice[node] = price;
        }
    }

    /// <summary>
    /// Takes the highest priority path, splits it, and prepares the jobs to be ran across multiple CPU threads.
    /// </summary>
    public void PrepareJobs()
    {
        //Get a SplitRequest struct from the highest priority path, representing the result of splitting that path into a bunch of new paths
        //Grab the highest priority path, split it, and hold onto the struct representing the data we will need from it.
        while (pendingRequest.IsInvalid())
        {
            pendingRequest = openPaths.Dequeue().Split(this);
        }

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
                //Assign the path if it's the lowest cost
                if (pathCost < bestCost)
                {
                    bestCost = pathCost;
                    bestPath = newPaths[i];
                }
            }
            else if (!newPaths[i].IsDead)
            {
                openPaths.Enqueue(newPaths[i], pathCost);
                UpdateNodePrice(newPaths[i].EndNode, newPaths[i].CostSoFar);
            }
        }

        pendingRequest.threads = -1; //Mark the completed request as invalid
    }
    #endregion
}