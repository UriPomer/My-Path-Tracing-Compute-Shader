struct Sphere
{
    float3 position;
    float radius;
    float3 albedo;
    float3 specular;
    float smoothness;
    float3 emission;
};

struct Ray
{
    float3 origin;
    float3 dir;
    float3 energy;
};

struct Material
{
    float3 albedo;
    float3 emission;
    float roughness;
    float metallic;
    float alpha;
    float ior;
};

struct RayHit
{
    float3 position;
    float distance;
    float3 normal;
    Material material;
};

RWTexture2D<float4> _Result;
float2 _Resolution;
float4x4 _CameraToWorld;
float4x4 _CameraInverseProjection;
Texture2D<float4> _SkyboxTexture;
SamplerState sampler_SkyboxTexture;
float3 _DirectionalLight;
float4 _DirectionalLightColor;

static const float PI = 3.14159265f;
#define PI_TWO          6.28318530717958623198
#define LUM             0.33333333333333333333


float2 _Pixel;
float _Seed;

RWStructuredBuffer<Sphere> _Spheres;

// object info
struct MeshData
{
    //float4x4 localToWorld;
    int indicesStart;
    int indicesCount;
    int materialIdx;
};
//StructuredBuffer<MeshData> _Meshes;

struct BLASNode
{
    float3 boundMax;
    float3 boundMin;
    int faceStartIdx;
    int faceEndIdx;
    int materialIdx;
    int childIdx;
};
StructuredBuffer<BLASNode> _BNodes;

struct TLASNodeRaw
{
    float3 boundMax;
    float3 boundMin;
    int transformIdx;
    int rootIdx;
};
StructuredBuffer<TLASNodeRaw> _TNodesRaw;

struct TLASNode
{
    float3 boundMax;
    float3 boundMin;
    int rawNodeStartIdx;
    int rawNodeEndIdx;
    int childIdx;
};
StructuredBuffer<TLASNode> _TNodes;

struct MaterialData
{
    float4 color;
    float3 emission;
    float metallic;
    float smoothness;
    float ior;
    float mode;
    int albedoIdx;
    int emitIdx;
    int metalIdx;
    int normIdx;
    int roughIdx;
};
StructuredBuffer<MaterialData> _Materials;

StructuredBuffer<float3> _Vertices;
StructuredBuffer<int> _Indices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<float4> _Tangents;
StructuredBuffer<float2> _UVs;
StructuredBuffer<float4x4> _Transforms;

Texture2DArray<float4> _AlbedoTextures;
SamplerState sampler_AlbedoTextures;
Texture2DArray<float4> _EmitTextures;
SamplerState sampler_EmitTextures;
Texture2DArray<float4> _MetallicTextures;
SamplerState sampler_MetallicTextures;
Texture2DArray<float4> _NormalTextures;
SamplerState sampler_NormalTextures;
Texture2DArray<float4> _RoughnessTextures;
SamplerState sampler_RoughnessTextures;

Material GenMaterial(float3 baseColor, float3 emission,
    float metallic, float smoothness, float alpha, float ior,
    int4 indices = -1, float2 uv = 0.0)
{
    if (indices.x >= 0)
    {
        // fetch albedo color
        // and convert from srgb space
        float4 color = _AlbedoTextures.SampleLevel(sampler_AlbedoTextures, float3(uv, indices.x), 0.0);
        baseColor = baseColor * color.rgb;
        alpha = alpha * color.a;
    }
    if (indices.y >= 0)
    {
        // fetch metallic value
        float4 metallicRoughness = _MetallicTextures.SampleLevel(sampler_MetallicTextures, float3(uv, indices.y), 0.0);
        metallic = metallicRoughness.r;
        smoothness = metallicRoughness.a;

    }
    if(indices.w >= 0)
    {
        smoothness = _RoughnessTextures.SampleLevel(sampler_RoughnessTextures, float3(uv, indices.w), 0.0).x;
        smoothness = 1.0 - smoothness;
    }
    Material mat;
    mat.alpha = alpha;
    mat.albedo = baseColor;
    mat.metallic = metallic;
    if (indices.z >= 0)
    {
        // fetch emission value
        emission = emission * _EmitTextures.SampleLevel(sampler_EmitTextures, float3(uv, indices.z), 0.0).xyz;
    }
    mat.emission = emission;
    mat.roughness = 1.0 - smoothness;
    mat.ior = ior;
    return mat;
}

RayHit GenRayHit()
{
    RayHit hit;
    hit.position = float3(0.0f, 0.0f, 0.0f);
    hit.distance = 1.#INF;
    hit.normal = float3(0.0f, 0.0f, 0.0f);
    hit.material = GenMaterial(float3(0.0f, 0.0f, 0.0f), float3(0.0f, 0.0f, 0.0f), 0.0f, 0.0f, 0.0f, 0.0f);
    return hit;
}

Ray GenRay(float3 origin, float3 dir)
{
    Ray ray;
    ray.origin = origin;
    ray.dir = dir;
    ray.energy = float3(1.0f, 1.0f, 1.0f);
    return ray;
}





