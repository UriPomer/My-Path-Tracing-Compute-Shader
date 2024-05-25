using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVHBuilder : MonoBehaviour
{
    const int MAX_TRIANGLES_PER_NODE = 4;
    const int N = 1000;
    
    public struct Triangle
    {
        public Vector3 v0;
        public Vector3 v1;
        public Vector3 v2;
        public Vector3 centroid;
    }
    
    
    public struct BVHNode
    {
        public Vector3 aabbMin;
        public Vector3 aabbMax;
        public int leftFirstIdx;    //left child or first triangle index
        public int numTriangles;
    }
    
    public struct AABB
    {
        public Vector3 bmin;
        public Vector3 bmax;
        
        public void grow(Vector3 p)
        {
            bmin = Vector3.Min(bmin, p);
            bmax = Vector3.Max(bmax, p);
        }

        public float area()
        {
            Vector3 d = bmax - bmin;
            return d.x * d.y + d.y * d.z + d.z * d.x;
        }
    }
    
    public Triangle[] triangles;
    public int[] triIndices;
    public BVHNode[] bnodes;
    
    int rootNodeIdx = 0;
    int nodeUsed = 0;
    
    public void BuildBVH()
    {
        bnodes = new BVHNode[N];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i].centroid = (triangles[i].v0 + triangles[i].v1 + triangles[i].v2) / 3.0f;
            triIndices[i] = i;
        }
        
        InitRootBVHNode();
        Subdivide(rootNodeIdx);
    }
    
    public void InitRootBVHNode()
    {
        BVHNode rootNode = bnodes[rootNodeIdx];
        rootNode.aabbMin = new Vector3(1e30f, 1e30f, 1e30f);
        rootNode.aabbMax = new Vector3(-1e30f, -1e30f, -1e30f);
        rootNode.leftFirstIdx = 0;
        rootNode.numTriangles = triangles.Length;
        
        UpdateBounds(0);
    }
    
    public void UpdateBounds(int nodeIdx)
    {
        BVHNode node = bnodes[nodeIdx];
        if (node.numTriangles == 0)
        {
            // BVHNode leftNode = bnodes[node.leftFirstIdx];
            // BVHNode rightNode = bnodes[node.leftFirstIdx + 1];
            // node.aabbMin = Vector3.Min(leftNode.aabbMin, rightNode.aabbMin);
            // node.aabbMax = Vector3.Max(leftNode.aabbMax, rightNode.aabbMax);
            return;
        }
        
        node.aabbMin = new Vector3(1e30f, 1e30f, 1e30f);
        node.aabbMax = new Vector3(-1e30f, -1e30f, -1e30f);
        for (int i = node.leftFirstIdx; i < node.numTriangles; i++)
        {
            Triangle tri = triangles[triIndices[node.leftFirstIdx + i]];
            node.aabbMin = Vector3.Min(node.aabbMin, tri.v0);
            node.aabbMin = Vector3.Min(node.aabbMin, tri.v1);
            node.aabbMin = Vector3.Min(node.aabbMin, tri.v2);
            node.aabbMax = Vector3.Max(node.aabbMax, tri.v0);
            node.aabbMax = Vector3.Max(node.aabbMax, tri.v1);
            node.aabbMax = Vector3.Max(node.aabbMax, tri.v2);
        }
    }

    public void Subdivide(int idx)
    {
        BVHNode node = bnodes[idx];
        
        
        // init params
        int splitAxis = -1;
        float splitPos = 0.0f;
        float splitCost = 1e30f;
        
        
        splitCost = FindBestSplitPlane(ref node, ref splitAxis, ref splitPos);
        
        float noSplitCost = CaculateNodeCost(ref node);
        if (splitCost >= noSplitCost)
        {
            return;
        }

        int i = node.leftFirstIdx;
        int j = node.numTriangles - 1;
        while (i <= j)
        {
            if(triangles[triIndices[i]].centroid[splitAxis] < splitPos)
            {
                i++;
            }
            else
            {
                int tmp = triIndices[i];
                triIndices[i] = triIndices[j];
                triIndices[j] = tmp;
                j--;
            }
        }
        int leftCount = i - node.leftFirstIdx;
        if (leftCount == 0 || leftCount == node.numTriangles)
        {
            return;
        }
        int leftNodeIdx = nodeUsed++;
        int rightNodeIdx = nodeUsed++;
        BVHNode leftNode = bnodes[leftNodeIdx];
        BVHNode rightNode = bnodes[rightNodeIdx];
        leftNode.leftFirstIdx = node.leftFirstIdx;
        leftNode.numTriangles = leftCount;
        rightNode.leftFirstIdx = i;
        rightNode.numTriangles = node.numTriangles - leftCount;
        node.leftFirstIdx = leftNodeIdx;
        node.numTriangles = 0;
        UpdateBounds(leftNodeIdx);
        UpdateBounds(rightNodeIdx);
        //recursively subdivide
        Subdivide(leftNodeIdx);
        Subdivide(rightNodeIdx);
    }

    public float EvaluateSAH(ref BVHNode bnode, int axis, float pos)
    {
        if(bnode.numTriangles == 0)
        {
            return 1e30f;
        }
        
        AABB leftAABB = new AABB();
        AABB rightAABB = new AABB();
        int leftCount = 0;
        int rightCount = 0;
        for (int i = 0; i < bnode.numTriangles; i++)
        {
            Triangle tri = triangles[triIndices[bnode.leftFirstIdx + i]];
            if (tri.centroid[axis] < pos)
            {
                leftAABB.grow(tri.v0);
                leftAABB.grow(tri.v1);
                leftAABB.grow(tri.v2);
                leftCount++;
            }
            else
            {
                rightAABB.grow(tri.v0);
                rightAABB.grow(tri.v1);
                rightAABB.grow(tri.v2);
                rightCount++;
            }
        }
        float cost = leftCount * leftAABB.area() + rightCount * rightAABB.area();
        return cost > 0 ? cost : 1e30f;
    }

    public struct BIN
    {
        public AABB aabb;
        public int triCount;
    }
    
    public float FindBestSplitPlane(ref BVHNode bnode, ref int axis, ref float splitPos)
    {
        float bestCost = 1e30f;
        if(bnode.numTriangles == 0)
        {
            return bestCost;
        }
        int binsCount = 7;
        BIN[] bins = new BIN[binsCount];
        for (int a = 0; a < 3; a++)
        {
            float boundsMin = 1e30f;
            float boundsMax = -1e30f;
            for (int i = 0; i < bnode.numTriangles; i++)
            {
                Triangle tri = triangles[triIndices[bnode.leftFirstIdx + i]];
                boundsMin = Mathf.Min(boundsMin, tri.centroid[a]);
                boundsMax = Mathf.Max(boundsMax, tri.centroid[a]);
            }
            if(boundsMax == boundsMin)
            {
                continue;
            }
            float scale = (binsCount) / (boundsMax - boundsMin);
            for(int i = 0;i < bnode.numTriangles; i++)
            {
                Triangle tri = triangles[triIndices[bnode.leftFirstIdx + i]];
                int binIdx = Mathf.Min(binsCount - 1, (int)((tri.centroid[a] - boundsMin) * scale));
                bins[binIdx].aabb.grow(tri.v0);
                bins[binIdx].aabb.grow(tri.v1);
                bins[binIdx].aabb.grow(tri.v2);
                bins[binIdx].triCount++;
            }
            
            float[] leftArea = new float[binsCount - 1];
            float[] rightArea = new float[binsCount - 1];
            int[] leftCount = new int[binsCount - 1];
            int[] rightCount = new int[binsCount - 1];
            AABB leftAABB = new AABB();
            AABB rightAABB = new AABB();
            int leftSum = 0;
            int rightSum = 0;
            for (int i = 0; i < binsCount - 1; i++)
            {
                leftSum += bins[i].triCount;
                leftCount[i] = leftSum;
                leftAABB.grow(bins[i].aabb.bmin);
                leftArea[i] = leftAABB.area();
                rightSum += bins[binsCount - 1 - i].triCount;
                rightCount[binsCount - 2 - i] = rightSum;
                rightAABB.grow(bins[binsCount - 1 - i].aabb.bmax);
                rightArea[binsCount - 2 - i] = rightAABB.area();
            }
            // calculate cost for each split
            for (int i = 0; i < binsCount - 1; i++)
            {
                float cost = leftArea[i] * leftCount[i] + rightArea[i] * rightCount[i];
                if (cost < bestCost)
                {
                    bestCost = cost;
                    axis = a;
                    splitPos = boundsMin + (i + 1) / scale;
                }
            }
        }
        return bestCost;
    }

    public float CaculateNodeCost(ref BVHNode bnode)
    {
        if (bnode.numTriangles == 0)
        {
            return 1e30f;
        }
        Vector3 e = bnode.aabbMax - bnode.aabbMin;
        float area = e.x * e.y + e.y * e.z + e.z * e.x;
        return area * bnode.numTriangles;
    }
}
