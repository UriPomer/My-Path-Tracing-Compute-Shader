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
            newSphere.position = sphere.transform.position;
            newSphere.radius = sphere.radius;
            newSphere.albedo = sphere.GetComponent<Renderer>().material.GetVector("_Color");
            newSphere.specular =
                new Vector3(sphere.GetComponent<Renderer>().material.GetFloat("_Metallic"), 0, 0);
            newSphere.smoothness = sphere.GetComponent<Renderer>().material.GetFloat("_Glossiness");
            newSphere.emission = sphere.GetComponent<Renderer>().material.GetVector("_EmissionColor");
            spheres.Add(newSphere);
        }
    }
}
