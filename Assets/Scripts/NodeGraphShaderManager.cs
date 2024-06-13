using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class NodeGraphShaderManager
{
    private interface IShaderOperation
    {
        public IEnumerator Run(NodeGraphShaderManager manager);
    }
    #region Managing the asynchronous loading of the shaders
    private AsyncGraphShaderLoadOperation shaderLoadOperation;
    protected bool ShadersReady
    {
        get
        {
            return staticDynamicCheckShader != null && dynamicCheckShader != null && lengthShader != null;
        }
    }
    /// <summary>
    /// Returns an object representing a yieldable load operation for the shaders, creating a new one if it doesn't exist yet, or null if all the shaders are already loaded.
    /// </summary>
    public AsyncGraphShaderLoadOperation LoadShaders()
    {
        if (shaderLoadOperation == null)
        {
            if (!ShadersReady)
            {
                shaderLoadOperation = new AsyncGraphShaderLoadOperation(this);
            }
            else
            {
                return null;
            }
        }
        return shaderLoadOperation;
    }
    public class AsyncGraphShaderLoadOperation : CustomYieldInstruction
    {
        const string STATIC_DYNAMIC_INTERSECT_SHADER = "StaticDynamicLinkIntersectCheck";
        const string DYNAMIC_INTERSECT_SHADER = "DynamicLinkIntersectCheck";
        const string LINE_LENGTH_SHADER_PATH = "LineSegmentLengthCalculator";

        ResourceRequest loadStaticDynamicCheckShader = null;
        ResourceRequest loadDynamicCheckShader = null;
        ResourceRequest loadLengthShader = null;
        public override bool keepWaiting
        {
            get
            {
                return CheckLoad();
            }
        }
        readonly NodeGraphShaderManager manager;
        internal AsyncGraphShaderLoadOperation(NodeGraphShaderManager manager)
        {
            this.manager = manager;
            BeginLoad();
        }
        private void BeginLoad()
        {
            if (manager.staticDynamicCheckShader == null)
            {
                loadStaticDynamicCheckShader = Resources.LoadAsync(STATIC_DYNAMIC_INTERSECT_SHADER, typeof(ComputeShader));
            }
            if (manager.dynamicCheckShader == null)
            {
                loadDynamicCheckShader = Resources.LoadAsync(DYNAMIC_INTERSECT_SHADER, typeof(ComputeShader));
            }
            if (manager.lengthShader == null)
            {
                loadLengthShader = Resources.LoadAsync(LINE_LENGTH_SHADER_PATH, typeof(ComputeShader));
            }
        }
        private bool CheckLoad()
        {
            bool keepWaiting = false;
            if (loadStaticDynamicCheckShader != null)
            {
                if (loadStaticDynamicCheckShader.isDone)
                {
                    manager.staticDynamicCheckShader = loadStaticDynamicCheckShader.asset as ComputeShader;
                }
                else { keepWaiting = true; }
            }
            if (loadDynamicCheckShader != null)
            {
                if (loadDynamicCheckShader.isDone)
                {
                    manager.dynamicCheckShader = loadDynamicCheckShader.asset as ComputeShader;
                }
                else { keepWaiting = true; }
            }
            if (loadLengthShader != null)
            {
                if (loadLengthShader.isDone)
                {
                    manager.lengthShader = loadLengthShader.asset as ComputeShader;
                }
                else { keepWaiting = true; }
            }
            return keepWaiting;
        }
    }
    /// <summary>
    /// We load the shaders once and buffer them for use later
    /// </summary>
    private ComputeShader staticDynamicCheckShader, dynamicCheckShader, lengthShader;
    #endregion
    public IEnumerator DoSDCheck(List<NodeLink> staticLinks, List<NodeLink> dynamicLinks)
    {
        yield return RunShader(new SDIntersectCheck(staticLinks, dynamicLinks));
    }
    public IEnumerator DoSDCheckAndAssignLengths(List<NodeLink> staticLinks, List<NodeLink> dynamicLinks)
    {
        yield return RunShader(new SDIntersectAndLengthCheck(staticLinks, dynamicLinks));
    }
    public IEnumerator DoDChecks(List<NodeLink> dynamicLinks)
    {
        yield return RunShader(new DIntersectRemoval(dynamicLinks));
    }
    private IEnumerator RunShader(IShaderOperation operation)
    {
        if (!ShadersReady)
        {
            yield return LoadShaders();
        }
        yield return operation.Run(this);
    }
    private abstract class IntersectCheckBase : IShaderOperation
    {
        public abstract IEnumerator Run(NodeGraphShaderManager manager);

        /// <summary>
        /// An array in GPU memory representing all the links that will have their intersections checked (and later, length calculated)
        /// </summary>
        protected ComputeBuffer linkBuffer;
        /// <summary>
        /// An array in GPU memory that will be written to by the shader with the results of the intersection check
        /// </summary>
        protected ComputeBuffer hitBuffer;
        /// <summary>
        /// The shader used to perform the intersection check
        /// </summary>
        protected ComputeShader intersectShader;

        //These all used to be local variables (this class and child classes used to all be one big IEnumerator before I needed variations of this shader check)
        /// <summary>
        /// Tracks the total number of links in the linkArray that the shaders will use to calculate with
        /// </summary>
        protected int total;
        /// <summary>
        /// Tracks the total amount of threads that will be needed to perform all the checks needed for this operation
        /// </summary>
        protected int totalIterations;
        /// <summary>
        /// Buffers the link array in CPU memory
        /// </summary>
        protected float4[] linkArray;
        /// <summary>
        /// Buffers the hits array in CPU memory
        /// </summary>
        protected int[] hitsArray;
        /// <summary>
        /// Sets the common data that all intersect shaders use.
        /// </summary>
        protected virtual void PrepareRunShader()
        {
            //Set the GPU buffer data
            hitBuffer.SetData(hitsArray);
            //Overwrite the entire link buffer
            linkBuffer.SetData(linkArray);

            //Assign the values to the shader
            intersectShader.SetInt("total", total);
            intersectShader.SetBuffer(0, "links", linkBuffer);
            intersectShader.SetBuffer(0, "hits", hitBuffer);
        }
        /// <summary>
        /// Dispatches and retrieve results from intersect shaders.
        /// </summary>
        protected IEnumerator RunIntersectShader()
        {
            //Run the shader, with as many thread groups as needed to ensure complete coverage of all the line segments.
            intersectShader.Dispatch(0, Mathf.Max(Mathf.CeilToInt(totalIterations / 64f), 1), 1, 1);

            //Prepare an asynchronous data readback, and wait until the shader is done + the data has been read back
            AsyncGPUReadbackRequest hitRequest = AsyncGPUReadback.Request(hitBuffer);
            yield return new WaitUntil(() => hitRequest.done || hitRequest.hasError);
            if (hitRequest.hasError) Debug.LogError("Encountered error attempting to fetch hit results!");

            hitRequest.GetData<int>().CopyTo(hitsArray);
        }
    }
    #region Shader checks that involve static link and dynamic link comparisons
    private abstract class SDCheckBase : IntersectCheckBase
    {
        protected readonly List<NodeLink> staticLinks, dynamicLinks;
        /// <param name="dynamicLinks">Note: The dynamic list will be directly modified by this class. If you don't want this, make a copy that you pass through.</param>
        public SDCheckBase(List<NodeLink> staticLinks, List<NodeLink> dynamicLinks)
        {
            this.staticLinks = staticLinks;
            this.dynamicLinks = dynamicLinks;
        }
        /// <summary>
        /// The point that divides the static and dynamic links in the linkArray.
        /// </summary>
        protected int offset;

        protected void InitialiseValues()
        {
            //Establish some integer values repeatedly used, so we don't have to recalculate every time.
            offset = staticLinks.Count();
            total = offset + dynamicLinks.Count();
            totalIterations = offset * (total - offset);

            //Link array is a giant array of float4s representing the values shaders will use for its math.
            //The array is more like a composite of 2 arrays:
            //1. Static links: all dynamic links are checked against each one of these.
            //2. Dynamic links: these are the links that will be destroyed if they intersect.
            //The float4 is just (A.x, A.y, B.x, B.y) where a given link is represented by line segment AB. NodeLink already defines how it should be typecasted into a float4.
            linkArray = new float4[total];
            //Hits array maps directly to just the dynamic links, and is used to store if any collisions were detected between the any of the static links and a given dynamic link.
            hitsArray = new int[total - offset];

            //Create the buffers that represent the space in GPU memory the shaders will use
            linkBuffer = new ComputeBuffer(staticLinks.Count + dynamicLinks.Count, sizeof(float) * 4);
            hitBuffer = new ComputeBuffer(dynamicLinks.Count, sizeof(int));

            //Populate link array, based on the layout which I explained in Prepare()
            staticLinks.Select(link => (float4)link).ToArray().CopyTo(linkArray, 0);
            dynamicLinks.Select(link => (float4)link).ToArray().CopyTo(linkArray, offset);
        }
        protected override void PrepareRunShader()
        {
            base.PrepareRunShader();
            //Assign offset to the shader
            intersectShader.SetInt("offset", offset);
        }
        protected IEnumerator RunSDIntersect()
        {
            PrepareRunShader();

            yield return RunIntersectShader();

            hitBuffer.Release();
        }
    }
    private class SDIntersectCheck : SDCheckBase
    {
        /// <param name="dynamicLinks">Note: The dynamic list will be directly modified by this class. If you don't want this, make a copy that you pass through.</param>
        public SDIntersectCheck(List<NodeLink> staticLinks, List<NodeLink> dynamicLinks) : base(staticLinks, dynamicLinks) { }
        public override IEnumerator Run(NodeGraphShaderManager manager)
        {
            intersectShader = manager.staticDynamicCheckShader;
            InitialiseValues();
            yield return RunSDIntersect();
            for (int i = hitsArray.Length - 1; i >= 0; i--)
            {
                //If there was any intersections on this link
                if (hitsArray[i] > 0)
                {
                    //Release and remove the link
                    dynamicLinks[i].Release();
                    dynamicLinks.RemoveAt(i);
                }
            }
            linkBuffer.Release();
        }
    }
    private class SDIntersectAndLengthCheck : SDCheckBase
    {
        private ComputeShader lengthShader;
        public SDIntersectAndLengthCheck(List<NodeLink> staticLinks, List<NodeLink> dynamicLinks) : base(staticLinks, dynamicLinks) { }
        public override IEnumerator Run(NodeGraphShaderManager manager)
        {
            intersectShader = manager.staticDynamicCheckShader;
            lengthShader = manager.lengthShader;

            InitialiseValues();

            yield return RunSDIntersect();

            //Creating a temp buffer to store all the remaining links
            float4[] linksToFindLength = new float4[dynamicLinks.Count];

            total = 0; //Redefining the total to be all the remaining links
            //Iterating backwards through every hit
            for (int i = hitsArray.Length - 1; i >= 0; i--)
            {
                //If there was any intersections on this link
                if (hitsArray[i] > 0)
                {
                    //Release and remove the link
                    dynamicLinks[i].Release();
                    dynamicLinks.RemoveAt(i);
                }
                else
                {
                    //Populate the new array with the data from this link
                    linksToFindLength[total] = linkArray[i + offset];
                    total++;
                }
            }
            linkArray = linksToFindLength;

            yield return RunLength();

            //Feed the calculated distance values into their respective links.
            for (int i = 0; i < total; i++)
            {
                //We actually filled the data into the length shader backwards, so here we're inverting the order.
                dynamicLinks[total - i - 1].distance = linkArray[i].x;
            }
        }
        IEnumerator RunLength()
        {
            //Assign the new link array data to the buffer we used for the last shader
            linkBuffer.SetData(linkArray, 0, 0, total);

            //Assign that buffer to the length shader, and dispatch it with as little thread groups needed to ensure complete coverage.
            lengthShader.SetBuffer(0, "links", linkBuffer);
            lengthShader.Dispatch(0, Mathf.Max(Mathf.CeilToInt(total / 64f), 1), 1, 1);

            //Prepare an asynchronous data readback, and wait until the shader is done + the data has been read back
            AsyncGPUReadbackRequest lengthRequest = AsyncGPUReadback.Request(linkBuffer);
            yield return new WaitUntil(() => lengthRequest.done || lengthRequest.hasError);
            if (lengthRequest.hasError) Debug.LogError("Encountered error attempting to fetch link distance results!");
            linkArray = lengthRequest.GetData<float4>().ToArray();
            linkBuffer.Release();
        }
    }
    #endregion
    #region Shader checks that only involve dynamic link comparisons
    private abstract class DCheckBase : IntersectCheckBase
    {
        protected readonly List<NodeLink> dynamicLinks;
        /// <param name="dynamicLinks">Note: The dynamic list will be directly modified by this class. If you don't want this, make a copy that you pass through.</param>
        public DCheckBase(List<NodeLink> dynamicLinks)
        {
            this.dynamicLinks = dynamicLinks;
        }
        protected void InitialiseValues()
        {
            //Establish some integer values repeatedly used, so we don't have to recalculate every time.
            total = dynamicLinks.Count();
            totalIterations = total * (total - 1) / 2;

            //Link array is a giant array of float4s representing the values shaders will use for its math.
            //The float4 is just (A.x, A.y, B.x, B.y) where a given link is represented by line segment AB. NodeLink already defines how it should be typecasted into a float4.
            linkArray = new float4[total];
            //Hits array maps directly to the dynamic links, and is used to store how many collisions between links were detected.
            hitsArray = new int[total];

            //Create the buffers that represent the space in GPU memory the shaders will use
            linkBuffer = new ComputeBuffer(dynamicLinks.Count, sizeof(float) * 4);
            hitBuffer = new ComputeBuffer(dynamicLinks.Count, sizeof(int));

            //Populate link array
            dynamicLinks.Select(link => (float4)link).ToArray().CopyTo(linkArray, 0);
        }
        protected IEnumerator RunDIntersect()
        {
            PrepareRunShader();
            linkBuffer.SetData(linkArray);

            yield return RunIntersectShader();
        }
    }
    /// <summary>
    /// Repeatedly runs Dynamic Intersect checks, removing the line with the most intersections one at a time, until there are no intersections left.
    /// </summary>
    private class DIntersectRemoval : DCheckBase
    {
        /// <param name="dynamicLinks">Note: The dynamic list will be directly modified by this class. If you don't want this, make a copy that you pass through.</param>
        public DIntersectRemoval(List<NodeLink> dynamicLinks) : base(dynamicLinks) { }
        public override IEnumerator Run(NodeGraphShaderManager manager)
        {
            intersectShader = manager.dynamicCheckShader;

            InitialiseValues();
            bool[] removeLink = new bool[total];
            while (true)
            {
                yield return RunDIntersect();
                int mostHits = 0;
                int mostHitsIndex = -1;
                for (int i = total - 1; i >= 0; i--)
                {
                    if (hitsArray[i] > mostHits)
                    {
                        mostHits = hitsArray[i];
                        mostHitsIndex = i;
                    }
                    hitsArray[i] = 0; //Reset the hit count for the next run
                }
                if (mostHitsIndex < 0)
                {
                    break;
                }
                else
                {
                    removeLink[mostHitsIndex] = true;
                    linkArray[mostHitsIndex] = new float4(0, 0, 0, 0); //Setting it to be a none-segment so it doesn't contribute to the next collision checks
                }
            }

            //Remove the members marked for removal
            for (int i = hitsArray.Length - 1; i >= 0; i--)
            {
                if (removeLink[i])
                {
                    dynamicLinks[i].Release();
                    dynamicLinks.RemoveAt(i);
                }
            }

            hitBuffer.Release();
            linkBuffer.Release();
        }
    }
    #endregion
}
