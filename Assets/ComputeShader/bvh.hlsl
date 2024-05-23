#include "struct.hlsl"


//这占据了36字节，我们希望他是16字节对齐的，所以也许我们可以把leftNodeIdx和firstTriangleIdx合并成一个int
struct BVHNode {
    float3 aabbMin;
    float3 aabbMax;
    int leftNodeIdx;
    int firstTriangleIdx;
    int numTriangles;
};

RWStructuredBuffer<BVHNode> _bvhNodes;

struct Triangle {
    float3 v0;
    float3 v1;
    float3 v2;
    float3 centroid;
};

RWStructuredBuffer<Triangle> _triangles;

float IntersectAABB(inout Ray ray, const float3 aabbMin, const float3 aabbMax, inout RayHit hit)
{
    ray.rDir = float3(1.0f/ ray.dir.x, 1.0f / ray.dir.y, 1.0f / ray.dir.z);
    
    float tx1 = (aabbMin.x - ray.origin.x) * ray.rDir.x;
    float tx2 = (aabbMax.x - ray.origin.x) * ray.rDir.x;
    float tmin = min(tx1, tx2);
    float tmax = max(tx1, tx2);
    float ty1 = (aabbMin.y - ray.origin.y) * ray.rDir.y;
    float ty2 = (aabbMax.y - ray.origin.y) * ray.rDir.y;
    tmin = max(tmin, min(ty1, ty2));
    tmax = min(tmax, max(ty1, ty2));
    float tz1 = (aabbMin.z - ray.origin.z) * ray.rDir.z;
    float tz2 = (aabbMax.z - ray.origin.z) * ray.rDir.z;
    tmin = max(tmin, min(tz1, tz2));
    tmax = min(tmax, max(tz1, tz2));

    if (tmax >= tmin && tmax >= 0.0f && hit.distance > 0)
    {
        return tmin;
    }
    return 1e30f;
}

void IntersectTriangle(inout Ray ray, const Triangle tri, inout RayHit hit)
{
    float3 edge1 = tri.v1 - tri.v0;
    float3 edge2 = tri.v2 - tri.v0;
    float3 h = cross(ray.dir, edge2);
    float a = dot(edge1, h);
    if(a > -1e-6 && a < 1e-6) return;   //ray parallel to triangle
    float f = 1.0f / a;
    float3 s = ray.origin - tri.v0;
    float u = f * dot(s, h);
    if(u < 0.0f || u > 1.0f) return;
    float3 q = cross(s, edge1);
    float v = f * dot(ray.dir, q);
    if(v < 0.0f || u + v > 1.0f) return;
    float t = f * dot(edge2, q);
    if(t < 0.0f || t > hit.distance) return;
    hit.distance = t;
    hit.position = ray.origin + ray.dir * t;
    hit.normal = normalize(cross(edge1, edge2));
}


void IntersectBVH(inout Ray ray, const int nodeIdx)
{
    BVHNode* node = &_bvhNodes[nodeIdx];
    BVHNode* stack[64];
    int stackPtr = 0;
    RayHit hit = GenRayHit();
    while (1)
    {
        if(node->numTriangles > 0)
        {
            for(int i = 0; i < node->numTriangles; i++)
            {
                Triangle tri = _triangles[node->firstTriangleIdx + i];
                IntersectTriangle(ray, tri, hit);
            }
            if(stackPtr == 0) break;
            else
            {
                stackPtr--;
                node = stack[stackPtr];
            }

            continue;
        }
        BVHNode* child1 = &_bvhNodes[node->leftNodeIdx];
        BVHNode* child2 = &_bvhNodes[node->leftNodeIdx + 1];
        float dist1 = IntersectAABB(ray, child1->aabbMin, child1->aabbMax,hit);
        float dist2 = IntersectAABB(ray, child2->aabbMin, child2->aabbMax,hit);

        if(dist1 > dist2)
        {
            // swap dist1 and dist2
            float temp = dist1;
            dist1 = dist2;
            dist2 = temp;

            // swap child1 and child2
            BVHNode* tempNode = child1;
            child1 = child2;
            child2 = tempNode;
        }
        if(dist1 == 1e30f)
        {
            if(stackPtr == 0) break;
            else
            {
                stackPtr--;
                node = stack[stackPtr];
            }
        }
        else
        {
            node = child1;
            if(dist2 != 1e30f)
            {
                stack[stackPtr] = child2;
                stackPtr++;
            }
        }
    }
}
