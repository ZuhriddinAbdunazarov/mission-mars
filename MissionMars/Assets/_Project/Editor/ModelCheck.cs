using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MissionMars.EditorTools
{
    /// <summary>Keeps the web download small: a .gltf must use the project's compressed textures,
    /// not hidden uncompressed copies of them. Run: Unity -batchmode -executeMethod MissionMars.EditorTools.ModelCheck.Run</summary>
    public static class ModelCheck
    {
        [MenuItem("Mission Mars/Check model texture copies")]
        public static void Run()
        {
            string dir = "Assets/_Project/Resources/MarsBase9";
            AssetDatabase.Refresh();
            // the textures first (so the models can find them), then the models
            foreach (var p in Directory.GetFiles(dir, "*.png")) AssetDatabase.ImportAsset(p.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            foreach (var p in Directory.GetFiles(dir, "*.gltf")) AssetDatabase.ImportAsset(p.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Paid/Resources/Paid/MarsBaseCrewRover.gltf", ImportAssetOptions.ForceUpdate);
            foreach (var p in Directory.GetFiles(dir, "*.gltf").Append("Assets/Paid/Resources/Paid/MarsBaseCrewRover.gltf"))
            {
                var path = p.Replace('\\', '/');
                var copies = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().ToArray();
                Debug.Log($"[ModelCheck] {Path.GetFileName(path)}: {copies.Length} texture copies inside " +
                          string.Join(", ", copies.Select(t => $"{t.name} {t.width}x{t.height} {t.format}")));
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
