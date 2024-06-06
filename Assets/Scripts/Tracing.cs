using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class Tracing : MonoBehaviour
{
    public ComputeShader tracingShader;

    private Camera cam;
    private RenderTexture target;
    
    [Header("Skybox Settings")]
    [SerializeField]
    private Texture skyboxTexture;
    [SerializeField, Range(0.0f, 10.0f)]
    float SkyboxIntensity = 1.0f;
    
    
    [SerializeField, Range(2, 20)]
    int TraceDepth = 5;
    
    [SerializeField]
    private bool drawGizmos = true;
    [SerializeField, Range(0.0f, 1.0f)]
    private float BVHCostOffset = 1.0f;
    
    
    private void Start()
    {
        BVHBuilder.SetCostOffset(BVHCostOffset);
        cam = GetComponent<Camera>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        BVHBuilder.Validate();

        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        int kernelID = tracingShader.FindKernel("CSMain");
        
        if (target == null || target.width != Screen.width || target.height != Screen.height)
        {
            if (target != null) target.Release();
            target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
        
        LightManager.Instance.UpdateLights();

        var DirectionalLight = LightManager.Instance.DirectionalLight;
        
        Vector3 dir = DirectionalLight.transform.forward;
        Vector3 directionalLightInfo = new Vector3(-dir.x, -dir.y, -dir.z);
        Vector3 directionalLightColorInfo = new Vector4(
            DirectionalLight.color.r,
            DirectionalLight.color.g,
            DirectionalLight.color.b,
            DirectionalLight.intensity
        );

        var pointLightsBuffer = LightManager.Instance.pointLightsBuffer;
        
        tracingShader.SetVector("_DirectionalLight", directionalLightInfo);
        tracingShader.SetVector("_DirectionalLightColor", directionalLightColorInfo);
        tracingShader.SetFloat("_Seed", UnityEngine.Random.value);
        tracingShader.SetTexture(kernelID, "_Result", target);
        tracingShader.SetVector("_Resolution", new Vector2(Screen.width, Screen.height));
        tracingShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        tracingShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        tracingShader.SetTexture(kernelID, "_SkyboxTexture", skyboxTexture);
        tracingShader.SetInt("_PointLightsCount", LightManager.Instance.GetPointLightsCount());
        tracingShader.SetBuffer(kernelID,"_PointLights",pointLightsBuffer);

		if (BVHBuilder.VertexBuffer != null) tracingShader.SetBuffer(kernelID, "_Vertices", BVHBuilder.VertexBuffer);
        if (BVHBuilder.IndexBuffer != null) tracingShader.SetBuffer(kernelID, "_Indices", BVHBuilder.IndexBuffer);
        if (BVHBuilder.NormalBuffer != null) tracingShader.SetBuffer(kernelID, "_Normals", BVHBuilder.NormalBuffer);
        if (BVHBuilder.TangentBuffer != null) tracingShader.SetBuffer(kernelID, "_Tangents", BVHBuilder.TangentBuffer);
        if (BVHBuilder.UVBuffer != null) tracingShader.SetBuffer(kernelID, "_UVs", BVHBuilder.UVBuffer);
        if (BVHBuilder.MaterialBuffer != null) tracingShader.SetBuffer(kernelID, "_Materials", BVHBuilder.MaterialBuffer);
        if (BVHBuilder.TLASBuffer != null) tracingShader.SetBuffer(kernelID, "_TNodes", BVHBuilder.TLASBuffer);
        if (BVHBuilder.MeshNodeBuffer != null) tracingShader.SetBuffer(kernelID, "_MeshNodes", BVHBuilder.MeshNodeBuffer);
        if (BVHBuilder.BLASBuffer != null) tracingShader.SetBuffer(kernelID, "_BNodes", BVHBuilder.BLASBuffer);
        if (BVHBuilder.TransformBuffer != null) tracingShader.SetBuffer(kernelID, "_Transforms", BVHBuilder.TransformBuffer);
        if (BVHBuilder.AlbedoTextures != null) tracingShader.SetTexture(kernelID, "_AlbedoTextures", BVHBuilder.AlbedoTextures);
        if (BVHBuilder.EmissionTextures != null) tracingShader.SetTexture(kernelID, "_EmitTextures", BVHBuilder.EmissionTextures);
        if (BVHBuilder.MetallicTextures != null) tracingShader.SetTexture(kernelID, "_MetallicTextures", BVHBuilder.MetallicTextures);
        if (BVHBuilder.NormalTextures != null) tracingShader.SetTexture(kernelID, "_NormalTextures", BVHBuilder.NormalTextures);
        if (BVHBuilder.RoughnessTextures != null) tracingShader.SetTexture(kernelID, "_RoughnessTextures", BVHBuilder.RoughnessTextures);

        var threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        var threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        tracingShader.Dispatch(kernelID, threadGroupsX, threadGroupsY, 1);

        
        Graphics.Blit(target, destination);
    }
    
    private void OnDisable()
    {
        if (target != null)
        {
            target.Release();
        }
        BVHBuilder.Destroy();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var bnodes = BVHBuilder.GetBLASNodes();
        var meshNodes = BVHBuilder.GetMeshNodes();
        var transforms = BVHBuilder.GetTransforms();
        if (bnodes != null && meshNodes != null && transforms != null)
        {
            for (int i = 0; i < meshNodes.Count; i++)
            {
                var meshNode = meshNodes[i];
                var localToWorld = transforms[meshNode.TransformIdx * 2];
                
                // draw mesh bounds
                Gizmos.color = Color.green;
                var boundCenter = (meshNode.BoundMin + meshNode.BoundMax) / 2;
                var size = meshNode.BoundMax - meshNode.BoundMin;
                Gizmos.DrawWireCube(localToWorld.MultiplyPoint3x4(boundCenter), localToWorld.MultiplyVector(size));
                
                for(int j = meshNode.NodeRootIdx; j < BVHBuilder.nodeStartToEnd[meshNode.NodeRootIdx]; j++)
                {
                    var bnode = bnodes[j];
                    if(bnode.PrimitiveStartIdx < 0) continue;
                    var min = localToWorld.MultiplyPoint3x4(bnode.BoundMin);
                    var max = localToWorld.MultiplyPoint3x4(bnode.BoundMax);
                    var center = (min + max) / 2;
                    var s = max - min;
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(center, s);
                }
            }
        }
    }
}