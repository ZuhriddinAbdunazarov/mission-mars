using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MissionMars.EditorTools
{
    /// <summary>Developer tool: prints what is inside a model and renders pictures of it from the front, side and top.
    /// Unity.exe -batchmode -projectPath MissionMars -executeMethod MissionMars.EditorTools.ModelPreview.Run -previewModel Paid/AstronautJJ -previewOut C:/path</summary>
    public static class ModelPreview
    {
        static string Arg(string n) { var a = System.Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == n) return a[i + 1]; return null; }

        public static void Run()
        {
            string model = Arg("-previewModel") ?? "Paid/AstronautJJ", outDir = Arg("-previewOut") ?? "Temp/preview";
            Directory.CreateDirectory(outDir);
            // the astronaut is posed at runtime, so its mesh must be readable
            foreach (var guid in AssetDatabase.FindAssets("AstronautJJ t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi && (!mi.isReadable || mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard))
                { mi.isReadable = true; mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.SaveAndReimport(); }
            }
            var prefab = Resources.Load<GameObject>(model);
            var log = new StringBuilder();
            if (!prefab) { File.WriteAllText(Path.Combine(outDir, "info.txt"), "not found: " + model); EditorApplication.Exit(1); return; }
            var go = Object.Instantiate(prefab);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                int depth = 0; for (var p = t.parent; p; p = p.parent) depth++;
                log.Append(new string(' ', depth * 2)).Append(t.name).Append("  pos ").Append(t.localPosition.ToString("F3")).Append(" rot ").Append(t.localEulerAngles.ToString("F0")).Append(" scl ").Append(t.localScale.ToString("F3"));
                var mf = t.GetComponent<MeshFilter>(); var sm = t.GetComponent<SkinnedMeshRenderer>();
                var mesh = mf ? mf.sharedMesh : sm ? sm.sharedMesh : null;
                if (mesh) log.Append($"  MESH {mesh.name} v{mesh.vertexCount} sub{mesh.subMeshCount} readable={mesh.isReadable} bounds {mesh.bounds.center:F3}/{mesh.bounds.size:F3}");
                var r = t.GetComponent<Renderer>();
                if (r) foreach (var m in r.sharedMaterials) log.Append($"  MAT {(m ? m.name + " " + m.shader.name + " " + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "") + (m.mainTexture ? " tex " + m.mainTexture.name : "") : "null")}");
                log.AppendLine();
            }
            var b = new Bounds(go.transform.position, Vector3.zero); bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            log.AppendLine($"WORLD BOUNDS center {b.center:F3} size {b.size:F3}");
            // vertical slices: how wide is the model at each height (arms out = wide at the shoulders)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue;
                var v = m.vertices; var mtx = mf.transform.localToWorldMatrix;
                for (int s = 0; s < 10; s++)
                {
                    float y0 = b.min.y + b.size.y * s / 10f, y1 = b.min.y + b.size.y * (s + 1) / 10f; float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f; int c = 0;
                    foreach (var vv in v) { var w = mtx.MultiplyPoint3x4(vv); if (w.y < y0 || w.y >= y1) continue; c++; minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z); }
                    log.AppendLine($"  {mf.name} slice {s} y {y0:F2}-{y1:F2}: {c} verts  x {minX:F2}..{maxX:F2}  z {minZ:F2}..{maxZ:F2}");
                }
            }
            File.WriteAllText(Path.Combine(outDir, "info.txt"), log.ToString());

            // pictures
            var light = new GameObject("L").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(40, 150, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("C").AddComponent<Camera>(); cam.backgroundColor = new Color(0.3f, 0.32f, 0.38f); cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 35;
            var rt = new RenderTexture(900, 900, 24); cam.targetTexture = rt;
            float dist = b.size.magnitude * 1.8f;
            foreach (var (name, dir) in new[] { ("front", Vector3.forward), ("back", Vector3.back), ("side", Vector3.right), ("top", new Vector3(0.01f, 1, 0.3f)) })
            {
                cam.transform.position = b.center + dir.normalized * dist; cam.transform.LookAt(b.center);
                cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(900, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            }
            EditorApplication.Exit(0);
        }
    }
}
