using UnityEngine;

public class Tracing : MonoBehaviour
{
    public ComputeShader tracingShader;

    private Camera cam;
    private RenderTexture target;

    private void Start()
    {
        cam = GetComponent<Camera>();
        
    }
    
    private void Update()
    {
        // if (Input.GetKeyDown(KeyCode.Space))
        // {
        //     DebugDir();
        // }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
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

        tracingShader.SetTexture(kernelID, "Result", target);
        tracingShader.SetVector("Resolution", new Vector2(Screen.width, Screen.height));
        tracingShader.SetMatrix("CameraToWorld", cam.cameraToWorldMatrix);
        tracingShader.SetMatrix("CameraInverseProjection", cam.projectionMatrix.inverse);

        var threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        var threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        tracingShader.Dispatch(kernelID, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit(target, destination);
    }
    
    private void DebugDir()
    {
        int kernelID = tracingShader.FindKernel("DebugRayDirection");
    
        var threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        var threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        
        target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat,
            RenderTextureReadWrite.Linear);
        ComputeBuffer buffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(float) * 3);
        tracingShader.SetTexture(0, "Result", target);
        tracingShader.SetVector("Resolution", new Vector2(Screen.width, Screen.height));
        tracingShader.SetMatrix("CameraToWorld", cam.cameraToWorldMatrix);
        tracingShader.SetMatrix("CameraInverseProjection", cam.projectionMatrix.inverse);
        tracingShader.SetBuffer(kernelID, "DirOutput", buffer);
        tracingShader.Dispatch(kernelID, threadGroupsX, threadGroupsY, 1);
    
        Vector3[] dirs = new Vector3[Screen.width * Screen.height];
        buffer.GetData(dirs);
        buffer.Release();
    
        Debug.Log(dirs[0]);
    }
}