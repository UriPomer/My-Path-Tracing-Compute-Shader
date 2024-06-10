#ifndef FUNCTION
#define FUNCTION

#include "test_global.hlsl"

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

void SpecReflModel(RayHit hit, float3 V, float3 L, float3 H, inout float3 energy)
{
    float NdotL = abs(dot(hit.normal, L));
    //float NdotV = abs(dot(hit.norm, -V));
    float3 specColor = lerp(0.04, hit.material.albedo, hit.material.metallic);
    float3 F = SchlickFresnel(dot(L, H), specColor);
    //float D = DistributionGGX(hit.norm, H, hit.mat.roughness);
    //float G = GeometrySmith(hit.norm, -V, L, hit.mat.roughness);
    float G = SmithG(NdotL, hit.material.roughness);
    energy *= F * G;
}

void SpecRefrModel(RayHit hit, float3 V, float3 L, float3 H, inout float3 energy)
{
    float NdotL = abs(dot(hit.normal, L));
    //float NdotV = abs(dot(-hit.norm, -V));
    float F = DielectricFresnel(dot(V, H), hit.material.ior);
    //float D = DistributionGGX(hit.norm, H, hit.mat.roughness);
    float G = SmithG(NdotL, hit.material.roughness);
    //float eta2 = hit.mat.ior * hit.mat.ior;
    energy *= pow(hit.material.albedo, 0.5) * (1.0 - hit.material.metallic) *
        (1.0 - F) * G;
}

#endif
