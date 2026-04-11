using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configures the BackgroundCamera as a URP Base camera and adds the Main Camera
/// as an Overlay camera in its stack so the 3D background renders behind the UI.
/// Attach this to the BackgroundCamera GameObject.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BackgroundCameraSetup : MonoBehaviour
{
    [Tooltip("Renderer index in the URP pipeline asset renderer list. " +
             "Index 0 = 2D renderer (default), Index 1 = UniversalRenderer3D.")]
    public int rendererIndex = 0;

    [Tooltip("The Main Camera that renders the UI overlay on top of the 3D background.")]
    public Camera overlayCamera;

    void Awake()
    {
        UniversalAdditionalCameraData cameraData = GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null) return;

        cameraData.SetRenderer(rendererIndex);

        // Add the overlay camera to this camera's stack so it renders on top
        if (overlayCamera != null)
        {
            UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();
            if (overlayCameraData != null && overlayCameraData.renderType == CameraRenderType.Overlay)
            {
                if (!cameraData.cameraStack.Contains(overlayCamera))
                    cameraData.cameraStack.Add(overlayCamera);
            }
        }
    }
}
