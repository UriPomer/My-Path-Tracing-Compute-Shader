
struct Material
{
    float3 albedo;
    float3 emission;
    float specular;
    float smoothness;
};


struct Sphere
{
    float3 position;
    float radius;
    Material mat;
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
    Material mat;
};

struct Plane
{
    float3 normal;
    float3 p;
    Material mat;
};

