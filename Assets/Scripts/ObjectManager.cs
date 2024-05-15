using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


[StructLayout(LayoutKind.Sequential)]
public struct Sphere
{
    public Vector3 position;
    public float radius;
    public Vector3 albedo;
    public Vector3 specular;
    public float smoothness;
    public Vector3 emission;
}


// 用于统计场景中的物体信息，传递给RayTracingShader
public class ObjectManager : MonoBehaviour
{
    private static List<Sphere> spheres = new List<Sphere>();
    
    public static List<Sphere> GetSpheres()
    {
        return spheres;
    }
    
    //获取场景中的所有球体,通过SphereCollider获取
    private void Awake()
    {
        var sphereArray = FindObjectsOfType<SphereCollider>();

        foreach (var sphere in sphereArray)
        {
            Sphere newSphere = new Sphere();
            Renderer renderer = sphere.GetComponent<Renderer>();
            Material material = renderer.material;
            newSphere.position = sphere.transform.position;
            newSphere.radius = sphere.radius;

            newSphere.albedo = new Vector3(material.color.r, material.color.g, material.color.b);
            newSphere.specular = new Vector3(material.GetFloat("_Metallic"), material.GetFloat("_Metallic"),
                material.GetFloat("_Metallic"));
            newSphere.smoothness = material.GetFloat("_Glossiness");

            Color emission = material.GetColor("_EmissionColor");
            newSphere.emission = new Vector3(emission.r, emission.g, emission.b);
            spheres.Add(newSphere);
        }
    }
}