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
    
    
    private void Start()
    {
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
        tracingShader.SetInt("_PointLightsCount", 0);
        tracingShader.SetBuffer(0,"_PointLights",pointLightsBuffer);

		if (BVHBuilder.VertexBuffer != null) tracingShader.SetBuffer(0, "_Vertices", BVHBuilder.VertexBuffer);
        if (BVHBuilder.IndexBuffer != null) tracingShader.SetBuffer(0, "_Indices", BVHBuilder.IndexBuffer);
        if (BVHBuilder.NormalBuffer != null) tracingShader.SetBuffer(0, "_Normals", BVHBuilder.NormalBuffer);
        if (BVHBuilder.TangentBuffer != null) tracingShader.SetBuffer(0, "_Tangents", BVHBuilder.TangentBuffer);
        if (BVHBuilder.UVBuffer != null) tracingShader.SetBuffer(0, "_UVs", BVHBuilder.UVBuffer);
        if (BVHBuilder.MaterialBuffer != null) tracingShader.SetBuffer(0, "_Materials", BVHBuilder.MaterialBuffer);
        if (BVHBuilder.TLASBuffer != null) tracingShader.SetBuffer(0, "_TNodes", BVHBuilder.TLASBuffer);
        if (BVHBuilder.MeshNodeBuffer != null) tracingShader.SetBuffer(0, "_MeshNodes", BVHBuilder.MeshNodeBuffer);
        if (BVHBuilder.BLASBuffer != null) tracingShader.SetBuffer(0, "_BNodes", BVHBuilder.BLASBuffer);
        if (BVHBuilder.TransformBuffer != null) tracingShader.SetBuffer(0, "_Transforms", BVHBuilder.TransformBuffer);
        if (BVHBuilder.AlbedoTextures != null) tracingShader.SetTexture(0, "_AlbedoTextures", BVHBuilder.AlbedoTextures);
        if (BVHBuilder.EmissionTextures != null) tracingShader.SetTexture(0, "_EmitTextures", BVHBuilder.EmissionTextures);
        if (BVHBuilder.MetallicTextures != null) tracingShader.SetTexture(0, "_MetallicTextures", BVHBuilder.MetallicTextures);
        if (BVHBuilder.NormalTextures != null) tracingShader.SetTexture(0, "_NormalTextures", BVHBuilder.NormalTextures);
        if (BVHBuilder.RoughnessTextures != null) tracingShader.SetTexture(0, "_RoughnessTextures", BVHBuilder.RoughnessTextures);

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
                
                
                // draw bvh bounds
                // find left and right child until leaf node
                for(int j = meshNode.NodeRootIdx; j < meshNode.NodeEndIdx; j++)
                {
                    var bnode = bnodes[j];
                    var bnodeBoundCenter = (bnode.BoundMin + bnode.BoundMax) / 2;
                    var bnodeSize = bnode.BoundMax - bnode.BoundMin;
                    Gizmos.DrawWireCube(localToWorld.MultiplyPoint3x4(bnodeBoundCenter), localToWorld.MultiplyVector(bnodeSize));
                }
            }
        }
    }
}