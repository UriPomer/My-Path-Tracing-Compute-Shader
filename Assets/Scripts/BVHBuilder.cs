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
        public int leftNodeIdx;
        public int firstTriangleIdx;
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
        rootNode.firstTriangleIdx = 0;
        rootNode.numTriangles = triangles.Length;
        rootNode.leftNodeIdx = 0;
        
        UpdateBounds(0);
    }
    
    public void UpdateBounds(int nodeIdx)
    {
        BVHNode node = bnodes[nodeIdx];
        node.aabbMin = new Vector3(1e30f, 1e30f, 1e30f);
        node.aabbMax = new Vector3(-1e30f, -1e30f, -1e30f);
        for (int i = node.firstTriangleIdx; i < node.numTriangles; i++)
        {
            Triangle tri = triangles[triIndices[node.firstTriangleIdx + i]];
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

        int bestAxis = -1;
        float bestPos = 0.0f;
        float bestCost = 1e30f;
        
        Vector3 e = node.aabbMax - node.aabbMin;
        float parentArea = e.x * e.y + e.y * e.z + e.z * e.x;
        float parentCost = parentArea * node.numTriangles;
        
        for (int axis = 0; axis < 3; axis++)
        {
            for (int loopIdx = 0; loopIdx < node.numTriangles; loopIdx++)
            {
                Triangle tri = triangles[triIndices[node.firstTriangleIdx + loopIdx]];
                float candidateSplitPos = tri.centroid[axis];
                float cost = EvaluateSAH(ref node, axis, candidateSplitPos);
                if(cost < bestCost)
                {
                    bestCost = cost;
                    bestAxis = axis;
                    bestPos = candidateSplitPos;
                }
                
                
            }
        }
        
        if (bestCost >= parentCost)
        {
            return;
        }

        int i = node.firstTriangleIdx;
        int j = node.numTriangles - 1;
        while (i <= j)
        {
            if(triangles[triIndices[i]].centroid[bestAxis] < bestPos)
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
        int leftCount = i - node.firstTriangleIdx;
        if (leftCount == 0 || leftCount == node.numTriangles)
        {
            return;
        }
        int leftNodeIdx = nodeUsed++;
        int rightNodeIdx = nodeUsed++;
        BVHNode leftNode = bnodes[leftNodeIdx];
        BVHNode rightNode = bnodes[rightNodeIdx];
        leftNode.firstTriangleIdx = node.firstTriangleIdx;
        leftNode.numTriangles = leftCount;
        rightNode.firstTriangleIdx = i;
        rightNode.numTriangles = node.numTriangles - leftCount;
        node.leftNodeIdx = leftNodeIdx;
        node.numTriangles = 0;
        UpdateBounds(leftNodeIdx);
        UpdateBounds(rightNodeIdx);
        //recursively subdivide
        Subdivide(leftNodeIdx);
        Subdivide(rightNodeIdx);
    }

    public float EvaluateSAH(ref BVHNode bnode, int axis, float pos)
    {
        AABB leftAABB = new AABB();
        AABB rightAABB = new AABB();
        int leftCount = 0;
        int rightCount = 0;
        for (int i = 0; i < bnode.numTriangles; i++)
        {
            Triangle tri = triangles[triIndices[bnode.firstTriangleIdx + i]];
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
}
