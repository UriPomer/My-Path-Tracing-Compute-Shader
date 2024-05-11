using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class Tracing : MonoBehaviour
{
    public ComputeShader tracingShader;

    private Camera cam;
    private RenderTexture target;
    
    [SerializeField]
    private Texture skyboxTexture;
    
    [SerializeField]
    private Light directionalLight;
    
    private List<Sphere> spheres = new List<Sphere>();
    
    private bool isInited = false;

    private void Start()
    {
        cam = GetComponent<Camera>();
        spheres = ObjectManager.GetSpheres();
        // Debug.Log(spheres);
        isInited = true;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(isInited == false)
            return;
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
        
        ComputeBuffer sphereBuffer = new ComputeBuffer(spheres.Count, Marshal.SizeOf(typeof(Sphere)));
        sphereBuffer.SetData(spheres);
        tracingShader.SetBuffer(kernelID, "_Spheres", sphereBuffer);
        
        tracingShader.SetVector("_DirectionalLight", directionalLight.transform.forward);
        tracingShader.SetVector("_DirectionalLightColor", directionalLight.color * directionalLight.intensity);
        tracingShader.SetFloat("_Seed", UnityEngine.Random.value);
        tracingShader.SetTexture(kernelID, "_Result", target);
        tracingShader.SetVector("_Resolution", new Vector2(Screen.width, Screen.height));
        tracingShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        tracingShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        tracingShader.SetTexture(kernelID, "_SkyboxTexture", skyboxTexture);

        var threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        var threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        tracingShader.Dispatch(kernelID, threadGroupsX, threadGroupsY, 1);
        
        if(sphereBuffer != null)
            sphereBuffer.Release();
        
        Graphics.Blit(target, destination);
    }
    
    private void OnDisable()
    {
        if (target != null)
        {
            target.Release();
        }
    }
}