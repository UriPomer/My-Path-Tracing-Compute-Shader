using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public struct TLASUpperNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int RawNodeStartIdx;
    public int RawNodeEndIdx;
    public int ChildIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*3;
}

public class AABB
{
    public Vector3 min;
    public Vector3 max;
    public Vector3 extent;

    public AABB()
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        extent = max - min;
    }

    public AABB(Vector3 min, Vector3 max)
    {
        this.min = Vector3.Min(min, max);
        this.max = Vector3.Max(min, max);
        extent = this.max - this.min;
    }

    public AABB(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        min = Vector3.Min(v0, Vector3.Min(v1, v2));
        max = Vector3.Max(v0, Vector3.Max(v1, v2));
        extent = max - min;
    }

    public void Extend(AABB volume)
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

    public static AABB Combine(AABB v1, AABB v2)
    {
        AABB result = v1.Copy();
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

    public AABB Copy()
    {
        return new AABB(min, max);
    }
}

public class BVH
{
    private readonly int nBuckets = 12;

    public class SAHBucket
    {
        public int Count = 0;   //面片数量
        public AABB Bounds = new AABB();  //包围盒
    }

    public class BVHNode
    {
        public AABB Bounds;
        public BVHNode LeftChild;
        public BVHNode RightChild;
        public int SplitAxis;
        public int PrimitiveStartIdx;
        public int PrimitiveEndIdx;

        public bool IsLeaf()
        {
            return (LeftChild == null) && (RightChild == null);
        }

        public static BVHNode CreateLeaf(int start, int count, AABB bounding)
        {
            BVHNode node = new BVHNode
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

        public static BVHNode CreateParent(int splitAxis, BVHNode nodeLeft, BVHNode nodeRight)
        {
            BVHNode node = new BVHNode
            {
                Bounds = AABB.Combine(nodeLeft.Bounds, nodeRight.Bounds),
                LeftChild = nodeLeft,
                RightChild = nodeRight,
                SplitAxis = splitAxis,
                PrimitiveStartIdx = -1,
                PrimitiveEndIdx = -1
            };
            return node;
        }
    }
    
    // 只有AABB和中心点信息，而没有顶点信息
    public class PrimitiveInfo
    {
        public AABB Bounds;
        public Vector3 Center;
        public int PrimitiveIdx;
    }


    public List<int> OrderedPrimitiveIndices = new List<int>();
    public BVHNode BVHRoot = null;

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

        Queue<BVHNode> nodes = new Queue<BVHNode>();
        nodes.Enqueue(BVHRoot); //BVHRoot是调用这个函数的BVH的根节点

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
                MaterialIdx = node.PrimitiveStartIdx >= 0 ? materialIdx : 0,
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
            NodeRootIdx = bnodesCount   //这个是BLAS的根节点索引，但根本没有用过
        });
    }

    public void FlattenTLAS(ref List<TLASRawNode> rawNodes, ref List<TLASUpperNode> tnodes)
    {
        List<TLASRawNode> orderedRawNodes = new List<TLASRawNode>();
        foreach (var rawNodeIdx in OrderedPrimitiveIndices)
        {
            orderedRawNodes.Add(rawNodes[rawNodeIdx]);
        }

        rawNodes = orderedRawNodes;
        Queue<BVHNode> nodes = new();
        nodes.Enqueue(BVHRoot);
        while (nodes.Count > 0)
        {
            var node = nodes.Dequeue();
            tnodes.Add(new TLASUpperNode
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
    
    // 将顶点和顶点对应的索引转换为PrimitiveInfo，存储AABB和中心点信息
    // 此处生成的PrimitiveInfo的PrimitiveIdx与顶点的索引的对应关系是，PrimitiveIdx = 顶点索引 / 3 取整
    private List<PrimitiveInfo> createPrimitiveInfo(List<Vector3> vertices, List<int> indices)
    {
        List<PrimitiveInfo> infos = new();
        for (int i = 0; i < indices.Count; i += 3)
        {
            infos.Add(new PrimitiveInfo
            {
                Bounds = new AABB(
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
    
    // rawNodes 含有 AABB 和 TransformIdx 信息，transforms 为每个Transform的矩阵
    private List<PrimitiveInfo> createPrimitiveInfo(List<TLASRawNode> rawNodes, List<Matrix4x4> transforms)
    {
        List<PrimitiveInfo> infos = new();
        for (int i = 0; i < rawNodes.Count; i++)
        {
            var node = rawNodes[i];
            infos.Add(new PrimitiveInfo
            {
                // 这里的乘以2是因为每个Transform有两个矩阵，一个是localToWorld，一个是worldToLocal，这里的transform是localToWorld
                Bounds = new AABB(transforms[node.TransformIdx * 2].MultiplyPoint3x4(node.BoundMin),
                    transforms[node.TransformIdx * 2].MultiplyPoint3x4(node.BoundMax)),
                PrimitiveIdx = i
            });
            infos[i].Center = infos[i].Bounds.Center();
        }

        return infos;
    }

    public BVH(List<Vector3> vertices, List<int> indices)
    {
        List<PrimitiveInfo> primitiveInfos = createPrimitiveInfo(vertices, indices);
        BVHRoot = Build(primitiveInfos, 0, primitiveInfos.Count);
    }

    public BVH(List<TLASRawNode> rawNodes, List<Matrix4x4> transforms)
    {
        List<PrimitiveInfo> primitiveInfos = createPrimitiveInfo(rawNodes, transforms);
        BVHRoot = Build(primitiveInfos, 0, primitiveInfos.Count);
    }

    private BVHNode Build(List<PrimitiveInfo> primitiveInfos, int start, int end)
    {
        AABB bounding = new();
        //  计算所有面片的包围盒
        for (int i = start; i < end; i++)
        {
            bounding.Extend(primitiveInfos[i].Bounds);
        }

        int primitiveInfoCount = end - start;
        // 如果只有一个面片，直接创建叶子节点
        if (primitiveInfoCount == 1)
        {
            int idx = OrderedPrimitiveIndices.Count;
            int primitiveIdx = primitiveInfos[start].PrimitiveIdx;
            // 从这里可以看出，OrderedPrimitiveIndices中存储的是面片的索引，排序后的索引对应原面片索引
            //TODO: 那么这个顺序有什么用呢？
            OrderedPrimitiveIndices.Add(primitiveIdx);
            return BVHNode.CreateLeaf(idx, 1, bounding);
        }

        AABB centerBounding = new();   //所有面片的中心点的包围盒
        for (int i = start; i < end; i++)
        {
            centerBounding.Extend(primitiveInfos[i].Center);
        }

        int dim = centerBounding.MaxDimension();
        int primitiveInfoMid = (start + end) / 2;
        if (Mathf.Approximately(centerBounding.max[dim], centerBounding.min[dim])) //无法在最大维度上划分，则直接创建叶子节点
        {
            int idx = OrderedPrimitiveIndices.Count;
            for (int i = start; i < end; i++)
            {
                int primitiveIdx = primitiveInfos[i].PrimitiveIdx;
                OrderedPrimitiveIndices.Add(primitiveIdx);
            }

            return BVHNode.CreateLeaf(idx, primitiveInfoCount, bounding);
        }

        if (primitiveInfoCount <= 2) // 面片数量太少，直接创建叶子节点
        {
            primitiveInfos.Sort(start, end - start, Comparer<PrimitiveInfo>.Create((x, y) =>
                x.Center[dim].CompareTo(y.Center[dim]) //按照中心点在最大维度上的位置排序
            ));
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
            List<AABB> boundsLeft = new() { buckets[0].Bounds };
            List<AABB> boundsRight = new() { null };

            //以下代码有优化空间
            //先计算左边的
            for (int i = 1; i < nBuckets - 1; i++)
            {
                countLeft.Add(countLeft[i - 1] + buckets[i].Count);
                countRight.Add(0); //初始化为0
                boundsLeft.Add(AABB.Combine(boundsLeft[i - 1], buckets[i].Bounds));
                boundsRight.Add(new AABB()); //初始化为空
            }

            countRight[nBuckets - 2] = buckets[nBuckets - 1].Count;
            boundsRight[nBuckets - 2] = buckets[nBuckets - 1].Bounds;
            //计算右边的
            for (int i = nBuckets - 3; i >= 0; i--)
            {
                countRight[i] = countRight[i + 1] + buckets[i + 1].Count;
                boundsRight[i] = AABB.Combine(boundsRight[i + 1], buckets[i + 1].Bounds);
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
            
            // 如果没有任何划分的cost比当前的叶子节点还要大，则直接创建叶子节点，要不然只是徒增cost
            float leafCost = primitiveInfoCount;
            minCost = 0.5f + minCost / bounding.SurfaceArea();  //0.5f是一个修正值

            if (primitiveInfoCount > 16 || minCost < leafCost) //继续划分
            {
                List<PrimitiveInfo> leftInfos = new();
                List<PrimitiveInfo> rightInfos = new();
                for (int i = start; i < end; i++)
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

                primitiveInfoMid = start + leftInfos.Count; //此处直接用primitiveInfoMid是因为下面要复用，此处表示左子树的结束位置

                for (int i = start; i < end; i++)
                {
                    primitiveInfos[i] = i < primitiveInfoMid ? leftInfos[i - start] : rightInfos[i - primitiveInfoMid]; //索引重排
                }
            }
            else  //直接创建叶子节点
            {
                int idx = OrderedPrimitiveIndices.Count;
                for (int i = start; i < end; i++)
                {
                    int primitiveIdx = primitiveInfos[i].PrimitiveIdx;
                    OrderedPrimitiveIndices.Add(primitiveIdx);
                }
                
                // bound是所有面片的包围盒
                // idx是当前叶子节点的索引，primitiveInfoCount是面片数量
                // primitiveInfoCount是怎么和primitiveIdx对应的呢？
                // idx索引对应的叶子节点的第一个面片的索引是idx，最后一个面片的索引是idx+primitiveInfoCount
                // 然后通过这个idx+primitiveInfoCount在OrderedPrimitiveIndices中找到对应的实际面片索引
                return BVHNode.CreateLeaf(idx, primitiveInfoCount, bounding);
            }
        }

        if (primitiveInfoMid == start) primitiveInfoMid = (start + end) / 2;
        
        // 递归细分
        var leftChild = Build(primitiveInfos, start, primitiveInfoMid);
        var rightChild = Build(primitiveInfos, primitiveInfoMid, end);
        return BVHNode.CreateParent(dim, leftChild, rightChild);
    }
}
