using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public struct MaterialData
{
    public Vector4 Color;
    public Vector3 Emission;
    public float Metallic;
    public float Smoothness;
    public float IOR;
    public float RenderMode;
    public int AlbedoIdx;
    public int EmitIdx;
    public int MetallicIdx;
    public int NormalIdx;
    public int RoughIdx;
    
    public static int TypeSize = Marshal.SizeOf(typeof(MaterialData));
}

public class BVHBuilder : MonoBehaviour
{
    public static List<MaterialData> materials = new List<MaterialData>();
    
    // Mesh data
    private static List<Vector3> vertices = new List<Vector3>();
    private static List<Vector2> uvs = new List<Vector2>();
    private static List<Vector3> normals = new List<Vector3>();
    private static List<Vector4> tangents = new List<Vector4>();
    
    
    const int N = 1000;
    public static Primitive[] triangles;
    public static int[] triIndices;
    public static BVHNode[] bnodes;
    
    static int rootNodeIdx = 0;
    static int nodeUsed = 0;
    
    public static List<GameObject> GetObjectsCount()
    {
        return ObjectManager.GetObjects();
    }
    
    private static void BuildMaterialData(List<GameObject> objects)
    {
        materials.Clear();
        List<Texture2D> albedoTex = new List<Texture2D>();
        List<Texture2D> emitTex = new List<Texture2D>();
        List<Texture2D> metalTex = new List<Texture2D>();
        List<Texture2D> normTex = new List<Texture2D>();
        List<Texture2D> roughTex = new List<Texture2D>();
        
        materials.Add(new MaterialData()
        {
            Color = new Vector3(1.0f, 1.0f, 1.0f), // white color by default
            Emission = Vector3.zero,
            Metallic = 0.0f,
            Smoothness = 0.0f,
            IOR = 1.0f,
            RenderMode = 0,
            AlbedoIdx = -1,
            EmitIdx = -1,
            MetallicIdx = -1,
            NormalIdx = -1,
            RoughIdx = -1
        });
        
        foreach (var obj in objects)
        {
            MaterialData material = new MaterialData();
            Renderer renderer = obj.GetComponent<Renderer>();
            var mats = renderer.sharedMaterials;
            int matStartIdx = materials.Count;
            int matCount = mats.Length;
            foreach (var mat in mats)
            {
                int albedoIdx = -1, emitIdx = -1, metallicIdx = -1, normalIdx = -1, roughIdx = -1;
                if (mat.HasProperty("_MainTex"))
                {
                    albedoIdx = albedoTex.IndexOf((Texture2D)mat.mainTexture);
                    if (albedoIdx == -1 && mat.mainTexture != null)
                    {
                        albedoTex.Add((Texture2D)mat.mainTexture);
                        albedoIdx = albedoTex.Count - 1;
                    }
                }

                if (mat.HasProperty("_EmissionMap"))
                {
                    var emitMap = mat.GetTexture("_EmissionMap");
                    emitIdx = emitTex.IndexOf(emitMap as Texture2D);
                    if (emitIdx < 0 && emitMap != null)
                    {
                        emitIdx = emitTex.Count;
                        emitTex.Add(emitMap as Texture2D);
                    }
                }

                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    var metalMap = mat.GetTexture("_MetallicGlossMap");
                    metallicIdx = metalTex.IndexOf(metalMap as Texture2D);
                    if (metallicIdx < 0 && metalMap != null)
                    {
                        metallicIdx = metalTex.Count;
                        metalTex.Add(metalMap as Texture2D);
                    }
                }

                if (mat.HasProperty("_BumpMap"))
                {
                    var normMap = mat.GetTexture("_BumpMap");
                    normalIdx = normTex.IndexOf(normMap as Texture2D);
                    if (normalIdx < 0 && normMap != null)
                    {
                        normalIdx = normTex.Count;
                        normTex.Add(normMap as Texture2D);
                    }
                }

                if (mat.HasProperty("_SpecGlossMap"))
                {
                    var roughMap = mat.GetTexture("_SpecGlossMap"); // assume Autodesk interactive shader
                    roughIdx = roughTex.IndexOf(roughMap as Texture2D);
                    if (roughIdx < 0 && roughMap != null)
                    {
                        roughIdx = roughTex.Count;
                        roughTex.Add(roughMap as Texture2D);
                    }
                }
                
                Color emission = Color.black;
                if (mat.IsKeywordEnabled("_EMISSION"))
                {
                    emission = mat.GetColor("_EmissionColor");
                }
                
                materials.Add(new MaterialData()
                {
                    Color = new Vector4(mat.color.r, mat.color.g, mat.color.b, mat.color.a),
                    Emission = new Vector3(emission.r,emission.g,emission.b),
                    Metallic = mat.GetFloat("_Metallic"),
                    Smoothness = mat.GetFloat("_Glossiness"),
                    IOR = mat.HasProperty("_IOR") ? mat.GetFloat("_IOR") : 1.0f,
                    RenderMode = mat.HasProperty("_RenderMode") ? mat.GetFloat("_RenderMode") : 0.0f,
                    AlbedoIdx = albedoIdx,
                    EmitIdx = emitIdx,
                    MetallicIdx = metallicIdx,
                    NormalIdx = normalIdx,
                    RoughIdx = roughIdx
                });
            }
        }
    }

    private static void BuildMeshData(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            vertices.AddRange(mesh.vertices.ToList());
            uvs.AddRange(mesh.uv);
            normals.AddRange(mesh.normals);
            tangents.AddRange(mesh.tangents);
            int vertexStartIdx = vertices.Count;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var subMeshIndices = mesh.GetIndices(i).ToList();
                
            }
        }
    }
}
