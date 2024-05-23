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
        
        BuildRootBVH();
        Subdivide(rootNodeIdx);
    }
    
    public void BuildRootBVH()
    {
        BVHNode rootNode = bnodes[rootNodeIdx];
        rootNode.aabbMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        rootNode.aabbMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        rootNode.firstTriangleIdx = 0;
        rootNode.numTriangles = triangles.Length;
        rootNode.leftNodeIdx = 0;
        
        UpdateBounds(0);
    }
    
    public void UpdateBounds(int nodeIdx)
    {
        BVHNode node = bnodes[nodeIdx];
        node.aabbMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        node.aabbMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
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
        if (node.numTriangles <= MAX_TRIANGLES_PER_NODE)
        {
            return;
        }
        Vector3 extents = node.aabbMax - node.aabbMin;
        int axis = 0;
        if (extents.y > extents.x)
        {
            axis = 1;
        }
        if (extents.z > extents.y)
        {
            axis = 2;
        }
        float splitPos = 0.5f * (node.aabbMin[axis] + node.aabbMax[axis]);

        int i = node.firstTriangleIdx;
        int j = node.numTriangles - 1;
        while (i <= j)
        {
            if(triangles[triIndices[i]].centroid[axis] < splitPos)
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
}
