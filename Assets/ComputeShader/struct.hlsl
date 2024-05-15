

struct Sphere
{
    float3 position;
    float radius;
    float3 albedo;
    float3 emission;
    float3 specular;
    float smoothness;
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

