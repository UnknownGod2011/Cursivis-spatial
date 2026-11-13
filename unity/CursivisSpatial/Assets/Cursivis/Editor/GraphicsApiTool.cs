using UnityEditor;
using UnityEngine.Rendering;

namespace Cursivis.Spatial.EditorTools
{
    /// <summary>Batch helper: sets Windows graphics APIs without re-running the full project setup.</summary>
    public static class GraphicsApiTool
    {
        public static void WindowsVulkanFirst()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[CursivisBuild] Windows graphics APIs: Vulkan, D3D11, D3D12");
        }
    }
}
