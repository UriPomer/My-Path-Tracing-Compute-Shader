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

struct RayHit
{
    float3 position;
    float distance;
    float3 normal;
    float3 albedo;
    float3 emission;
    float3 specular;
    float smoothness;
};

struct Plane
{
    float3 normal;
    float3 p;
    float3 albedo;
    float3 emission;
    float3 specular;
    float smoothness;
};

RayHit GenRayHit()
{
    RayHit hit;
    hit.position = float3(0.0f, 0.0f, 0.0f);
    hit.distance = 1.#INF;
    hit.normal = float3(0.0f, 0.0f, 0.0f);
    hit.albedo = float3(0.0f, 0.0f, 0.0f);
    hit.specular = float3(0.0f, 0.0f, 0.0f);
    hit.smoothness = 0.0f;
    hit.emission = float3(0.0f, 0.0f, 0.0f);
    return hit;
}