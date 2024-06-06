#include "struct.hlsl"

#define BVHTREE_RECURSE_SIZE 32

/*
 * 传入面的索引idx，交点处通过插值得到的uv坐标data，法线纹理索引normIdx，uv坐标uv
 * 返回局部坐标的法线
 */
float3 GetNormal(int idx, float2 data, int normIdx, float2 uv)
{
    float3 norm0 = _Normals[_Indices[idx]];
    float3 norm1 = _Normals[_Indices[idx + 1]];
    float3 norm2 = _Normals[_Indices[idx + 2]];
    float3 norm = norm1 * data.x + norm2 * data.y + norm0 * (1.0 - data.x - data.y);    //插值得到法线
    float4 tangent0 = _Tangents[_Indices[idx]];
    float4 tangent1 = _Tangents[_Indices[idx + 1]];
    float4 tangent2 = _Tangents[_Indices[idx + 2]];
    float4 tangent = tangent1 * data.x + tangent2 * data.y + tangent0 * (1.0 - data.x - data.y);    //插值得到切线
    //tangent.w = tangent0.w;
    if (normIdx >= 0)
    {
        float3 binorm = normalize(cross(norm, tangent.xyz)) * tangent.w;    //计算副法线，也是一个切线，tangent.w通常是1或-1，为切线的方向，保持正交
        float3x3 TBN = float3x3(
            norm,
            binorm,
            tangent.xyz
        );  // 切线空间矩阵
        float3 normTS = _NormalTextures.SampleLevel(sampler_NormalTextures, float3(uv, normIdx), 0.0).xyz * 2.0 - 1.0;
        return mul(normTS, TBN);   //将法线从切线空间变换到物体的局部坐标系
    }
    else
    {
        return norm;
    }
}

// refer to: https://github.com/HummaWhite/ZillumGL/blob/main/src/shader/material.shader
float DielectricFresnel(float cosTi, float eta)
{
    cosTi = clamp(cosTi, -1.0, 1.0);
    if (cosTi < 0.0)
    {
        eta = 1.0 / eta;
        cosTi = -cosTi;
    }

    float sinTi = sqrt(1.0 - cosTi * cosTi);
    float sinTt = sinTi / eta;
    if (sinTt >= 1.0)
        return 1.0;

    float cosTt = sqrt(1.0 - sinTt * sinTt);

    float rPa = (cosTi - eta * cosTt) / (cosTi + eta * cosTt);
    float rPe = (eta * cosTi - cosTt) / (eta * cosTi + cosTt);
    return (rPa * rPa + rPe * rPe) * 0.5;
}

float sdot(float3 x, float3 y, float f = 1.0f)
{
    return saturate(dot(x, y) * f);
}

float rand()
{
    float result = frac(sin(_Seed / 100.0f * dot(_Pixel, float2(12.9898f, 78.233f))) * 43758.5453f); // Fraction part
    _Seed += 1.0f;
    return result;
}

bool SkipTransparent(Material mat)
{
    float f = DielectricFresnel(0.2, mat.ior);
    float r = mat.roughness * mat.roughness;
    return rand() < (1.0 - f) * (1.0 - mat.metallic) * (1.0 - r);
}

/*
 *把光线的起点和方向变换到局部坐标系，然后返回新的光线
 */
Ray PrepareTreeEnterRay(Ray ray, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    float3 origin = mul(worldToLocal, float4(ray.origin, 1.0)).xyz;     // 把光线的起点变换到局部坐标系
    float3 dir = normalize(mul(worldToLocal, float4(ray.dir, 0.0)).xyz);    // 把光线的方向变换到局部坐标系
    return GenRay(origin, dir);
}

/*
 *变换长度为targetDist的向量到局部坐标系，然后返回新的长度
 */
float PrepareTreeEnterTargetDistance(float targetDist, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (targetDist >= 1.#INF)
    {
        return targetDist;
    }
    else
    {
        // 变换长度为targetDist的向量到局部坐标系，然后返回新的长度
        float3 dir = mul(worldToLocal, float4(targetDist, 0.0, 0.0, 0.0)).xyz;
        return length(dir);
    }
}

/*
 *将RayHit hit的属性变换到局部坐标系
 */
void PrepareTreeEnterHit(Ray rayLocal, inout RayHit hit, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (hit.distance < 1.#INF)
    {
        hit.position = mul(worldToLocal, float4(hit.position, 1.0)).xyz;
        hit.distance = length(hit.position - rayLocal.origin);
    }
}

/*
 *将RayHit hit的属性变换到世界坐标系
 */
void PrepareTreeExit(Ray rayWorld, inout RayHit hit, int transformIdx)
{
    float4x4 localToWorld = _Transforms[transformIdx * 2];
    if (hit.distance < 1.#INF)
    {
        hit.position = mul(localToWorld, float4(hit.position, 1.0)).xyz;
        hit.distance = length(hit.position - rayWorld.origin);
    }
}

// Schlick Fresnel 近似
float3 SchlickFresnel(float cosTheta, float3 F0)
{
    //return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
    return lerp(F0, 1.0, pow(abs(1.0 - cosTheta), 5.0));
}

// Smith GGX shadowing-masking function
float SmithG(float NDotV, float alphaG)
{
    float a = alphaG * alphaG;
    float b = NDotV * NDotV;
    return (2.0 * NDotV) / (NDotV + sqrt(a + b - a * b));
}

/*
 *与地面相交，数据存储在bestHit中
 */
void IntersectGround(Ray ray, inout RayHit bestHit, float yVal = 0.0)
{
    float t = (yVal - ray.origin.y) / ray.dir.y;
    if(t > 0.0 && t < bestHit.distance)
    {
        bestHit.position = ray.origin + t * ray.dir;
        bestHit.distance = t;
        bestHit.normal = float3(0.0, 1.0, 0.0);
        bestHit.material = GenMaterial(float3(1.0, 1.0, 1.0), float3(0.0, 0.0, 0.0), 0.0, 0.0, 0.0, 0.0);
    }
}


/*
 *通过targetDist快速判断光线是否与地面相交
 */
bool IntersectGroundFast(Ray ray, float targetDist, float yVal = 0.0)
{
    float t = -(ray.origin.y - yVal) / ray.dir.y;
    if (t > 0.0 && t < targetDist)
        return true;
    return false;
}


//这里的u和v就是三角形的两个顶点的uv坐标，t是光线与三角形的交点
bool IntersectTriangle(Ray ray, float3 v0, float3 v1, float3 v2,
    inout float t, inout float u, inout float v
)
{
    float3 e1 = v1 - v0;
    float3 e2 = v2 - v0;
    float3 pvec = cross(ray.dir, e2);
    float det = dot(e1, pvec);
    if (det < 1e-8)
        return false;
    float detInv = 1.0 / det;
    float3 tvec = ray.origin - v0;
    u = dot(tvec, pvec) * detInv;
    if(u < 0.0 || u > 1.0)
        return false;
    float3 qvec = cross(tvec, e1);
    v = dot(ray.dir, qvec) * detInv;
    if(v < 0.0 || v + u > 1.0)
        return false;
    t = dot(e2, qvec) * detInv;
    return true;
}

/*
 *判断光线是否与盒子相交
 */
bool IntersectBox1(Ray ray, float3 pMax, float3 pMin)
{
    float t0 = 0.0;
    float t1 = 1.#INF;
    float invRayDir, tNear, tFar;
    for (int i = 0; i < 3; i++)
    {
        invRayDir = 1.0 / ray.dir[i];
        tNear = (pMin[i] - ray.origin[i]) * invRayDir;
        tFar = (pMax[i] - ray.origin[i]) * invRayDir;
        t0 = max(t0, tNear);
        t1 = min(t1, tFar);
        if (t0 > t1)
        {
            return false;
        }
    }
    return true;
}

/*
 *判断光线是否与盒子相交
 */
bool IntersectBox2(Ray ray, float3 pMax, float3 pMin)
{
    // reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/common/intersection.glsl
    // reference: https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525
    float3 invDir = 1.0 / ray.dir;
    float3 f = (pMax - ray.origin) * invDir;
    float3 n = (pMin - ray.origin) * invDir;
    float3 tMax = max(f, n);
    float3 tMin = min(f, n);
    float t0 = max(tMin.x, max(tMin.y, tMin.z));
    float t1 = min(tMax.x, min(tMax.y, tMax.z));
    return t1 >= t0;
}

/*
 *判断光线是否与盒子相交，同时考虑光线的双向
 */
bool IntersectBox3(Ray ray, RayHit bestHit, float3 pMax, float3 pMin)
{
    bool intersectForward = IntersectBox2(ray, pMax, pMin);
    bool intersectBackward = bestHit.distance < 1.#INF ? IntersectBox2(GenRay(bestHit.position, -ray.dir), pMax, pMin) : true;
    return intersectForward && intersectBackward;
}

/*
 *与BLAS树中的三角形面求交
 */
void IntersectBlasTree(Ray ray, inout RayHit bestHit, int startIdx, int transformIdx)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int primitiveIdx;
    stack[stackPtr] = startIdx;
    float4x4 localToWorld = _Transforms[transformIdx * 2];
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];    //模拟栈
        BLASNode node = _BNodes[idx];   //获取当前BLAS节点

        bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);    // 和BLAS的包围盒求交
        bool leaf = node.primitiveStartIdx >= 0;
        if (hit)
        {
            if (leaf)
            {
                // 遍历BLAS中的每一个面
                for (primitiveIdx = node.primitiveStartIdx; primitiveIdx < node.primitiveEndIdx; primitiveIdx++)
                {
                    // 根据之前得出的结论，这里的_Indices对应OrderedPrimitiveIndices，然后每一个BLASNode的primitiveStartIdx和primitiveEndIdx对应的是OrderedPrimitiveIndices的索引
                    // 然后再通过OrderedPrimitiveIndices的索引找到实际的面的索引
                    int i = primitiveIdx * 3;   // i是面的索引
                    float3 v0 = _Vertices[_Indices[i]];
                    float3 v1 = _Vertices[_Indices[i + 1]];
                    float3 v2 = _Vertices[_Indices[i + 2]];
                    float2 uv0 = _UVs[_Indices[i]];
                    float2 uv1 = _UVs[_Indices[i + 1]];
                    float2 uv2 = _UVs[_Indices[i + 2]];
                    float t, u, v;
                    if (IntersectTriangle(ray, v0, v1, v2, t, u, v))    //与面求交
                    {
                        if (t > 0.0 && t < bestHit.distance)    //距离更近且不为负数
                        {
                            MaterialData mat = _Materials[node.materialIdx];
                            float3 hitPos = ray.origin + t * ray.dir;
                            float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);    //插值uv
                            float3 norm = GetNormal(i, float2(u, v), mat.normIdx, uv);
                            Material mats = GenMaterial(
                                mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
                                int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
                            );
                            if (mat.mode == 1.0 && mats.alpha < 1.0)    // 如果这是一个透明材质，那么则忽略它与光线的相交
                                continue;
                            bestHit.distance = t;
                            bestHit.position = hitPos;
                            bestHit.normal = normalize(mul(localToWorld, float4(norm, 0.0)).xyz);
                            bestHit.material = mats;
                        }
                    }
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;  //左子节点
                stack[++stackPtr] = node.childIdx + 1;  //右子节点
            }
        }
    }
}

/*
 *判断是否与BLAS树中的三角形面相交
 */
bool IntersectBlasTreeFast(Ray ray, int startIdx, float targetDist)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int primitiveIdx;
    stack[stackPtr] = startIdx;
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];
        BLASNode node = _BNodes[idx];
        // check if ray intersect with bounding box
        bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);
        bool leaf = node.primitiveStartIdx >= 0;
        if (hit)
        {
            if (leaf)
            {
                for (primitiveIdx = node.primitiveStartIdx; primitiveIdx < node.primitiveEndIdx; primitiveIdx++)
                {
                    int i = primitiveIdx * 3;
                    float3 v0 = _Vertices[_Indices[i]];
                    float3 v1 = _Vertices[_Indices[i + 1]];
                    float3 v2 = _Vertices[_Indices[i + 2]];
                    float2 uv0 = _UVs[_Indices[i]];
                    float2 uv1 = _UVs[_Indices[i + 1]];
                    float2 uv2 = _UVs[_Indices[i + 2]];
                    float t, u, v;
                    if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
                    {
                        if (t > 0.0 && t < targetDist)
                        {
                            MaterialData mat = _Materials[node.materialIdx];
                            float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);
                            Material mats = GenMaterial(
                                mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
                                int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
                            );
                            if (mat.mode == 1.0 && mats.alpha < 1.0 ||
                                    (mat.mode > 1.0 && SkipTransparent(mats)))
                                continue;
                            return true;
                        }
                    }
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;
                stack[++stackPtr] = node.childIdx + 1;
            }
        }
    }
    return false;
}

void IntersectTlas(Ray ray, inout RayHit bestHit)
{
    uint size, stride;
    _MeshNodes.GetDimensions(size, stride);
    for (uint i = 0; i < size; i++)
    {
        MeshNode node = _MeshNodes[i];
        Ray localRay = PrepareTreeEnterRay(ray, node.transformIdx);
        if (IntersectBox2(localRay, node.boundMax, node.boundMin))
        {
            // intersect with BLAS tree
            PrepareTreeEnterHit(localRay, bestHit, node.transformIdx);
            IntersectBlasTree(localRay, bestHit, node.rootIdx, node.transformIdx);
            PrepareTreeExit(ray, bestHit, node.transformIdx);
        }
    }
}

bool IntersectTlasFast(Ray ray, RayHit bestHit, float targetDist)
{
    uint size, stride;
    _MeshNodes.GetDimensions(size, stride);
    float dist = targetDist;
    for (uint i = 0; i < size; i++)
    {
        MeshNode node = _MeshNodes[i];
        Ray localRay = PrepareTreeEnterRay(ray, node.transformIdx);
        if (IntersectBox2(localRay, node.boundMax, node.boundMin))
        {
            // PrepareTreeEnterHit(localRay, bestHit, node.transformIdx); //注释掉的这两行一点用都没有，一个把bestHit传进去，一个把bestHit传出来，但是在这个函数里面并没有用到bestHit
            dist = PrepareTreeEnterTargetDistance(targetDist, node.transformIdx);   //dist是局部坐标系中的距离
            if (IntersectBlasTreeFast(localRay, node.rootIdx, dist))
                return true;
            // PrepareTreeExit(ray, bestHit, node.transformIdx);
        }
    }
    return false;
}

// void IntersectTlasTree(Ray ray, inout RayHit bestHit)
// {
//     int stack[BVHTREE_RECURSE_SIZE];
//     int stackPtr = 0;
//     int rawNodeIdx;
//     stack[stackPtr] = 0;
//     while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
//     {
//         int idx = stack[stackPtr--];
//         TLASNode node = _TNodes[idx];
//         bool hit = IntersectBox3(ray, bestHit, node.boundMax, node.boundMin);   // 和TLAS的包围盒求交，同时考虑光线的负向
//         bool leaf = node.rawNodeStartIdx >= 0;
//         if(hit)
//         {
//             if(leaf)
//             {
//                 for (rawNodeIdx = node.rawNodeStartIdx; rawNodeIdx < node.rawNodeEndIdx; rawNodeIdx++)
//                 {
//                     MeshNode rawNode = _MeshNodes[rawNodeIdx];
//                     Ray localRay = PrepareTreeEnterRay(ray, rawNode.transformIdx);
//                     PrepareTreeEnterHit(localRay, bestHit, rawNode.transformIdx);
//                     IntersectBlasTree(localRay, bestHit, rawNode.rootIdx, rawNode.transformIdx);
//                     PrepareTreeExit(ray, bestHit, rawNode.transformIdx);
//                 }
//             }
//             else
//             {
//                 stack[++stackPtr] = node.childIdx;
//                 stack[++stackPtr] = node.childIdx + 1;
//             }
//         }
//     }
// }


// bool IntersectTlasTreeFast(Ray ray, RayHit bestHit, float targetDist)
// {
//     int stack[BVHTREE_RECURSE_SIZE];
//     int stackPtr = 0;
//     int rawNodeIdx;
//     stack[stackPtr] = 0;
//     while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
//     {
//         int idx = stack[stackPtr--];
//         TLASNode node = _TNodes[idx];
//         bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);
//         bool leaf = node.rawNodeStartIdx >= 0;
//         if (hit)
//         {
//             if (leaf)
//             {
//                 for (rawNodeIdx = node.rawNodeStartIdx; rawNodeIdx < node.rawNodeEndIdx; rawNodeIdx++)
//                 {
//                     MeshNode rawNode = _MeshNodes[rawNodeIdx];
//                     Ray localRay = PrepareTreeEnterRay(ray, rawNode.transformIdx);
//                     PrepareTreeEnterHit(localRay, bestHit, rawNode.transformIdx);
//                     if (IntersectBlasTreeFast(localRay, rawNode.rootIdx, targetDist))
//                         return true;
//                     PrepareTreeExit(ray, bestHit, rawNode.transformIdx);
//                 }
//             }
//             else
//             {
//                 stack[++stackPtr] = node.childIdx;
//                 stack[++stackPtr] = node.childIdx + 1;
//             }
//         }
//     }
//     return false;
// }


// intersect with mesh object every vertices
// void IntersectMeshObject(Ray ray, inout RayHit bestHit, MeshData mesh)
// {
//     int offset = mesh.indicesStart;
//     int count = mesh.indicesCount;
//     for (int i = offset; i < offset + count; i += 3)
//     {
//         float3 v0 = _Vertices[_Indices[i]];
//         float3 v1 = _Vertices[_Indices[i + 1]];
//         float3 v2 = _Vertices[_Indices[i + 2]];
//         float2 uv0 = _UVs[_Indices[i]];
//         float2 uv1 = _UVs[_Indices[i + 1]];
//         float2 uv2 = _UVs[_Indices[i + 2]];
//         float t, u, v;
//         if(IntersectTriangle(ray, v0, v1, v2, t, u, v))
//         {
//             if(t > 0.0 && t < bestHit.distance)
//             {
//                 MaterialData mat = _Materials[mesh.materialIdx];
//                 float3 hitPos = ray.origin + t * ray.dir;
//                 float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);
//                 float3 norm = GetNormal(i, float2(u, v), mat.normIdx, uv);
//                 Material mats = GenMaterial(
//                     mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
//                     int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
//                 );
//                 if ((mat.mode == 1.0 && mats.alpha < 1.0) || (mat.mode > 1.0 && SkipTransparent(mats)))
//                     continue;
//                 bestHit.distance = t;
//                 bestHit.position = hitPos;
//                 bestHit.normal = normalize(norm);
//                 bestHit.material = mats;
//             }
//         }
//
//     }
// }
//
// bool IntersectMeshObjectFast(Ray ray, MeshData mesh, float targetDist)
// {
//     int offset = mesh.indicesStart;
//     int count = mesh.indicesCount;
//     for (int i = offset; i < offset + count; i += 3)
//     {
//         float3 v0 = _Vertices[_Indices[i]];
//         float3 v1 = _Vertices[_Indices[i + 1]];
//         float3 v2 = _Vertices[_Indices[i + 2]];
//         float t, u, v;
//         if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
//         {
//             if (t > 0.0 && t < targetDist)
//             {
//                 return true; // do not test for back face culling
//             }
//         }
//     }
//     return false;
// }



