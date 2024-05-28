using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public struct BIN
{
    public AABB aabb;
    public int triCount;
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

public struct BLASNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int PrimitiveStartIdx;
    public int PrimitiveEndIdx;
    public int MaterialIdx;
    public int ChildIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*4;
}

/// <summary>
/// Raw TLAS node info
/// </summary>
public struct TLASRawNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int TransformIdx;
    public int NodeRootIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*2;
}

/// <summary>
/// TLAS node built with bvh
/// </summary>
public struct TLASNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int RawNodeStartIdx;
    public int RawNodeEndIdx;
    public int ChildIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*3;
}

public class AABBClass
{
    public Vector3 min;
    public Vector3 max;
    public Vector3 extent;

    public AABBClass()
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        extent = max - min;
    }

    public AABBClass(Vector3 min, Vector3 max)
    {
        this.min = Vector3.Min(min, max);
        this.max = Vector3.Max(min, max);
        extent = this.max - this.min;
    }

    public AABBClass(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        min = Vector3.Min(v0, Vector3.Min(v1, v2));
        max = Vector3.Max(v0, Vector3.Max(v1, v2));
        extent = max - min;
    }

    public void Extend(AABBClass volume)
    {
        min = Vector3.Min(volume.min, min);
        max = Vector3.Max(volume.max, max);
        extent = max - min;
    }

    public void Extend(Vector3 p)
    {
        min = Vector3.Min(p, min);
        max = Vector3.Max(p, max);
        extent = max - min;
    }

    public Vector3 Center()
    {
        return (min + max) * 0.5f;
    }

    public int MaxDimension()
    {
        int result = 0; // 0 for x, 1 for y, 2 for z
        if(extent.y > extent[result]) result = 1;
        if(extent.z > extent[result]) result = 2;
        return result;
    }

    public static AABBClass Combine(AABBClass v1, AABBClass v2)
    {
        AABBClass result = v1.Copy();
        result.Extend(v2);
        return result;
    }

    public Vector3 Offset(Vector3 p)
    {
        Vector3 o = p - min;
        if (max.x > min.x) o.x /= extent.x;
        if (max.y > min.y) o.y /= extent.y;
        if (max.z > min.z) o.z /= extent.z;
        return o;
    }

    public float SurfaceArea()
    {
        return 2.0f * (
            extent.x * extent.y +
            extent.x * extent.z +
            extent.y * extent.z
        );
    }

    public AABBClass Copy()
    {
        return new AABBClass(min, max);
    }
}

public class BVH
{
    private readonly int nBuckets = 12;

    public class SAHBucket
    {
        public int Count = 0;
        public AABBClass Bounds = new AABBClass();
    }

    public class BVHNodeClass
    {
        public AABBClass Bounds;
        public BVHNodeClass LeftChild;
        public BVHNodeClass RightChild;
        public int SplitAxis;
        public int PrimitiveStartIdx;
        public int PrimitiveEndIdx;

        public bool IsLeaf()
        {
            return (LeftChild == null) && (RightChild == null);
        }

        public static BVHNodeClass CreateLeaf(int start, int count, AABBClass bounding)
        {
            BVHNodeClass node = new BVHNodeClass
            {
                Bounds = bounding,
                LeftChild = null,
                RightChild = null,
                SplitAxis = -1,
                PrimitiveStartIdx = start,
                PrimitiveEndIdx = start + count
            };
            return node;
        }

        public static BVHNodeClass CreateInterior(int splitAxis, BVHNodeClass nodeLeft, BVHNodeClass nodeRight)
        {
            BVHNodeClass node = new BVHNodeClass
            {
                Bounds = AABBClass.Combine(nodeLeft.Bounds, nodeRight.Bounds),
                LeftChild = nodeLeft,
                RightChild = nodeRight,
                SplitAxis = splitAxis,
                PrimitiveStartIdx = -1,
                PrimitiveEndIdx = -1
            };
            return node;
        }
    }

    public class PrimitiveInfoClass
    {
        public AABBClass Bounds;
        public Vector3 Center;
        public int PrimitiveIdx;
    }


    public List<int> OrderedPrimitiveIndices = new List<int>();
    public BVHNodeClass BVHRoot = null;

    public void AddSubMeshToBLAS(ref List<int> indices, ref List<BLASNode> bnodes,
        ref List<TLASRawNode> tnodesRaw, List<int> subindices,
        int verticesIdxOffset, int materialIdx, int objectTransformIdx)
    {
        int primitiveCount = indices.Count / 3;
        int bnodesCount = bnodes.Count;

        foreach (var primitiveIdx in OrderedPrimitiveIndices)
        {
            indices.Add(subindices[primitiveIdx * 3 + 0] + verticesIdxOffset);
            indices.Add(subindices[primitiveIdx * 3 + 1] + verticesIdxOffset);
            indices.Add(subindices[primitiveIdx * 3 + 2] + verticesIdxOffset);
        }

        Queue<BVHNodeClass> nodes = new Queue<BVHNodeClass>();
        nodes.Enqueue(BVHRoot);

        while (nodes.Count > 0)
        {
            var node = nodes.Dequeue();
            bnodes.Add(new BLASNode
            {
                BoundMax = node.Bounds.max,
                BoundMin = node.Bounds.min,
                // node.PrimitiveStartIdx >= 0 说明是叶子节点
                PrimitiveStartIdx = node.PrimitiveStartIdx >= 0 ? node.PrimitiveStartIdx + primitiveCount : -1,
                PrimitiveEndIdx = node.PrimitiveEndIdx >= 0 ? node.PrimitiveEndIdx + primitiveCount : -1,
                MaterialIdx = node.PrimitiveStartIdx >= 0 ? materialIdx : -1,
                ChildIdx = node.PrimitiveEndIdx >= 0 ? -1 : nodes.Count + bnodesCount + 1
            });
            if (node.LeftChild != null) nodes.Enqueue(node.LeftChild);
            if (node.RightChild != null) nodes.Enqueue(node.RightChild);
        }

        tnodesRaw.Add(new TLASRawNode
        {
            BoundMax = BVHRoot.Bounds.max,
            BoundMin = BVHRoot.Bounds.min,
            TransformIdx = objectTransformIdx,
            NodeRootIdx = bnodesCount
        });
    }

    public void FlattenTLAS(ref List<TLASRawNode> rawNodes, ref List<TLASNode> tnodes)
    {
        List<TLASRawNode> orderedRawNodes = new List<TLASRawNode>();
        foreach (var rawNodeIdx in OrderedPrimitiveIndices)
        {
            orderedRawNodes.Add(rawNodes[rawNodeIdx]);
        }

        rawNodes = orderedRawNodes;
        Queue<BVHNodeClass> nodes = new();
        nodes.Enqueue(BVHRoot);
        while (nodes.Count > 0)
        {
            var node = nodes.Dequeue();
            tnodes.Add(new TLASNode
            {
                BoundMax = node.Bounds.max,
                BoundMin = node.Bounds.min,
                RawNodeStartIdx = node.PrimitiveStartIdx >= 0 ? node.PrimitiveStartIdx : -1,
                RawNodeEndIdx = node.PrimitiveStartIdx >= 0 ? node.PrimitiveEndIdx : -1,
                ChildIdx = node.PrimitiveStartIdx >= 0 ? -1 : nodes.Count + tnodes.Count + 1
            });
            if (node.LeftChild != null) nodes.Enqueue(node.LeftChild);
            if (node.RightChild != null) nodes.Enqueue(node.RightChild);
        }
    }

    private List<PrimitiveInfoClass> createPrimitiveInfo(List<Vector3> vertices, List<int> indices)
    {
        List<PrimitiveInfoClass> infos = new();
        for (int i = 0; i < indices.Count; i += 3)
        {
            infos.Add(new PrimitiveInfoClass
            {
                Bounds = new AABBClass(
                    vertices[indices[i]],
                    vertices[indices[i + 1]],
                    vertices[indices[i + 2]]
                ),
                PrimitiveIdx = i / 3
            });
            infos[i / 3].Center = infos[i / 3].Bounds.Center();
        }

        return infos;
    }

    private List<PrimitiveInfoClass> createPrimitiveInfo(List<TLASRawNode> rawNodes, List<Matrix4x4> transforms)
    {
        List<PrimitiveInfoClass> infos = new();
        for (int i = 0; i < rawNodes.Count; i++)
        {
            var node = rawNodes[i];
            infos.Add(new PrimitiveInfoClass
            {
                Bounds = new AABBClass(transforms[node.TransformIdx * 2].MultiplyPoint3x4(node.BoundMin),
                    transforms[node.TransformIdx * 2].MultiplyPoint3x4(node.BoundMax)),
                PrimitiveIdx = i
            });
            infos[i].Center = infos[i].Bounds.Center();
        }

        return infos;
    }

    public BVH(List<Vector3> vertices, List<int> indices)
    {
        List<PrimitiveInfoClass> primitiveInfos = createPrimitiveInfo(vertices, indices);
        BVHRoot = Build(primitiveInfos, 0, primitiveInfos.Count);
    }

    public BVH(List<TLASRawNode> rawNodes, List<Matrix4x4> transforms)
    {
        List<PrimitiveInfoClass> primitiveInfos = createPrimitiveInfo(rawNodes, transforms);
        BVHRoot = Build(primitiveInfos, 0, primitiveInfos.Count);
    }

    private BVHNodeClass Build(List<PrimitiveInfoClass> primitiveInfos, int start, int end)
    {
        AABBClass bounding = new();
        for (int i = start; i < end; i++)
        {
            bounding.Extend(primitiveInfos[i].Bounds);
        }

        int primitiveInfoCount = end - start;
        if (primitiveInfoCount == 1)
        {
            int idx = OrderedPrimitiveIndices.Count;
            int primitiveIdx = primitiveInfos[start].PrimitiveIdx;
            OrderedPrimitiveIndices.Add(primitiveIdx);
            return BVHNodeClass.CreateLeaf(idx, 1, bounding);
        }

        AABBClass centerBounding = new();
        for (int i = start; i < end; i++)
        {
            centerBounding.Extend(primitiveInfos[i].Center);
        }

        int dim = centerBounding.MaxDimension();
        int primitiveInfoMid = (start + end) / 2;
        if (centerBounding.max[dim] == centerBounding.min[dim]) //无法在这个维度上划分，则直接创建叶子节点
        {
            int idx = OrderedPrimitiveIndices.Count;
            for (int i = start; i < end; i++)
            {
                int primitiveIdx = primitiveInfos[i].PrimitiveIdx;
                OrderedPrimitiveIndices.Add(primitiveIdx);
            }

            return BVHNodeClass.CreateLeaf(idx, primitiveInfoCount, bounding);
        }

        if (primitiveInfoCount <= 2) // 面片数量太少，直接创建叶子节点
        {
            primitiveInfos.Sort(start, end, Comparer<PrimitiveInfoClass>.Create((x, y) =>
            {
                int dim = x.Bounds.MaxDimension();
                return x.Center[dim].CompareTo(y.Center[dim]); //按照中心点在最大维度上的位置排序
            }));
        }
        else
        {
            List<SAHBucket> buckets = new();
            for (int i = 0; i < nBuckets; i++)
            {
                buckets.Add(new SAHBucket());
            }

            for (int i = start; i < end; i++)
            {
                int b = (int)Mathf.Floor(nBuckets * centerBounding.Offset(primitiveInfos[i].Center)[dim]); //确认该面片属于哪个桶
                b = Mathf.Clamp(b, 0, nBuckets - 1);
                buckets[b].Count++;
                buckets[b].Bounds.Extend(primitiveInfos[i].Bounds);
            }

            //处理桶的cost
            List<int> countLeft = new List<int>() { buckets[0].Count };
            List<int> countRight = new List<int>() { 0 };
            List<AABBClass> boundsLeft = new() { buckets[0].Bounds };
            List<AABBClass> boundsRight = new() { null };

            //以下代码有优化空间
            //先计算左边的
            for (int i = 1; i < nBuckets - 1; i++)
            {
                countLeft.Add(countLeft[i - 1] + buckets[i].Count);
                countRight.Add(0); //初始化为0
                boundsLeft.Add(AABBClass.Combine(boundsLeft[i - 1], buckets[i].Bounds));
                boundsRight.Add(new AABBClass()); //初始化为空
            }

            countRight[nBuckets - 2] = buckets[nBuckets - 1].Count;
            boundsRight[nBuckets - 2] = buckets[nBuckets - 1].Bounds;
            //计算右边的
            for (int i = nBuckets - 3; i >= 0; i--)
            {
                countRight[i] = countRight[i + 1] + buckets[i + 1].Count;
                boundsRight[i] = AABBClass.Combine(boundsRight[i + 1], buckets[i + 1].Bounds);
            }

            //计算cost
            float minCost = float.MaxValue;
            int minCostSplitBucket = -1;
            for (int i = 0; i < nBuckets - 1; i++)
            {
                if (countLeft[i] == 0 || countRight[i] == 0) continue;
                float cost = countLeft[i] * boundsLeft[i].SurfaceArea() + countRight[i] * boundsRight[i].SurfaceArea();
                if (cost < minCost)
                {
                    minCost = cost;
                    minCostSplitBucket = i;
                }
            }

            float leafCost = primitiveInfoCount;
            minCost = 0.5f + minCost / bounding.SurfaceArea();

            if (primitiveInfoCount > 16 || minCost < leafCost)
            {
                List<PrimitiveInfoClass> leftInfos = new();
                List<PrimitiveInfoClass> rightInfos = new();
                for (int i = 0; i < primitiveInfoCount; i++)
                {
                    int b = (int)Mathf.Floor(nBuckets * centerBounding.Offset(primitiveInfos[i].Center)[dim]);
                    b = Mathf.Clamp(b, 0, nBuckets - 1);
                    if (b <= minCostSplitBucket)
                    {
                        leftInfos.Add(primitiveInfos[i]);
                    }
                    else
                    {
                        rightInfos.Add(primitiveInfos[i]);
                    }
                }

                primitiveInfoMid = start + leftInfos.Count; //此处直接用primitiveInfoMid是因为下面要复用

                for (int i = start; i < end; i++)
                {
                    primitiveInfos[i] = i < primitiveInfoMid ? leftInfos[i - start] : rightInfos[i - primitiveInfoMid];
                }
            }
            else
            {
                int idx = OrderedPrimitiveIndices.Count;
                for (int i = start; i < end; i++)
                {
                    int primitiveIdx = primitiveInfos[i].PrimitiveIdx;
                    OrderedPrimitiveIndices.Add(primitiveIdx);
                }

                return BVHNodeClass.CreateLeaf(idx, primitiveInfoCount, bounding);
            }
        }

        if (primitiveInfoMid == start) primitiveInfoMid = (start + end) / 2;

        var leftChild = Build(primitiveInfos, start, primitiveInfoMid);
        var rightChild = Build(primitiveInfos, primitiveInfoMid, end);
        return BVHNodeClass.CreateInterior(dim, leftChild, rightChild);
    }
}
