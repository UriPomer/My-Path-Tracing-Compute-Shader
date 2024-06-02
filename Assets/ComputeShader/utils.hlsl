float3 GetNormal(int idx, float2 data, int normIdx, float2 uv)
{
    float3 norm0 = _Normals[_Indices[idx]];
    float3 norm1 = _Normals[_Indices[idx + 1]];
    float3 norm2 = _Normals[_Indices[idx + 2]];
    float3 norm = norm1 * data.x + norm2 * data.y + norm0 * (1.0 - data.x - data.y);
    float4 tangent0 = _Tangents[_Indices[idx]];
    float4 tangent1 = _Tangents[_Indices[idx + 1]];
    float4 tangent2 = _Tangents[_Indices[idx + 2]];
    float4 tangent = tangent1 * data.x + tangent2 * data.y + tangent0 * (1.0 - data.x - data.y);
    //tangent.w = tangent0.w;
    if (normIdx >= 0)
    {
        float3 binorm = normalize(cross(norm, tangent.xyz)) * tangent.w;
        float3x3 TBN = float3x3(
            norm,
            binorm,
            tangent.xyz
        );
        float3 normTS = _NormalTextures.SampleLevel(sampler_NormalTextures, float3(uv, normIdx), 0.0).xyz * 2.0 - 1.0;
        return mul(normTS, TBN);
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

Ray PrepareTreeEnterRay(Ray ray, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    float3 origin = mul(worldToLocal, float4(ray.origin, 1.0)).xyz;
    float3 dir = normalize(mul(worldToLocal, float4(ray.dir, 0.0)).xyz);
    return GenRay(origin, dir);
}

float PrepareTreeEnterTargetDistance(float targetDist, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (targetDist >= 1.#INF)
    {
        return targetDist;
    }
    else
    {
        // transform a directional vector of length targetDist
        // and return the new length
        float3 dir = mul(worldToLocal, float4(targetDist, 0.0, 0.0, 0.0)).xyz;
        return length(dir);
    }
}

void PrepareTreeEnterHit(Ray rayLocal, inout RayHit hit, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (hit.distance < 1.#INF)
    {
        hit.position = mul(worldToLocal, float4(hit.position, 1.0)).xyz;
        hit.distance = length(hit.position - rayLocal.origin);
    }
}

// update a hit info after exiting a BLAS tree
void PrepareTreeExit(Ray rayWorld, inout RayHit hit, int transformIdx)
{
    float4x4 localToWorld = _Transforms[transformIdx * 2];
    if (hit.distance < 1.#INF)
    {
        hit.position = mul(localToWorld, float4(hit.position, 1.0)).xyz;
        hit.distance = length(hit.position - rayWorld.origin);
    }
}