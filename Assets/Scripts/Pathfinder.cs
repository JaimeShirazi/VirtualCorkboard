using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

public class Pathfinder : CustomYieldInstruction
{
    public Path bestPath;

    private readonly Node origin;
    private readonly Node target;

    private PriorityQueue<Path, float> openPaths;
    public Pathfinder(Node from, Node to)
    {
        origin = from;
        target = to;
        openPaths = new();
        Path initialPath = new Path(from, to);
        openPaths.Enqueue(initialPath, initialPath.Cost);

#if UNITY_EDITOR
        //These will help with visualising what's going on
        origin.debug_customMessage = "Start";
        target.debug_customMessage = "End";
#endif
    }

    public override bool keepWaiting
    {
        get
        {
            return !isDone;
        }
    }
    private bool isDone;

    Path.SplitRequest pendingRequest;
    JobHandle jobHandle;
    public void Update()
    {
        //Get a PathRequest struct from the highest priority path, representing the result of splitting that path into a bunch of new paths
        Path nextPath = openPaths.Dequeue();
#if UNITY_EDITOR
        Debug.Log("Splitting from " + nextPath.EndNode.ID);
#endif
        pendingRequest = nextPath.Split();

        jobHandle = pendingRequest.job.Schedule(pendingRequest.threads, 1);
    }
    public void LateUpdate()
    {
        //TODO: Some of the logic here is messy, find some more elegant alternatives

        jobHandle.Complete();
        Path[] result = pendingRequest.onComplete();
        foreach (Path path in result)
        {
#if UNITY_EDITOR
            Debug.Log("Recieved path ending at " + path.EndNode.ID + " with cost " + path.Cost);
#endif
            openPaths.Enqueue(path, path.Cost);
            if (path.IsComplete)
            {
                isDone = true;
                if (bestPath != null)
                {
                    if (bestPath.Cost > path.Cost)
                        bestPath = path;
                }
                else
                {
                    bestPath = path;
                }
            }
        }
    }
}