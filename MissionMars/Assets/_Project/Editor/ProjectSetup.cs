using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace MissionMars.EditorTools
{
    /// <summary>One-click (or batch-mode) project setup + Windows build.
    /// Menu: Mission Mars/1. Setup Project, 2. Generate Assets & Scene, 3. Build Windows.</summary>
    public static class ProjectSetup
    {
        const string P = "Assets/_Project";

        [MenuItem("Mission Mars/1. Setup Project (URP, TMP, Player)")]
        public static void Step1()
        {
            // TMP essentials
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
                AssetDatabase.ImportPackage(pkg, false);
            }

            // URP
            Directory.CreateDirectory(P + "/Settings");
            var rdPath = P + "/Settings/MarsRenderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (!rd)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(rd, rdPath);
            }
            var urpPath = P + "/Settings/MarsURP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (!urp)
            {
                urp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(urp, urpPath);
            }
            var so = new SerializedObject(urp);
            void SetF(string n, float v) { var pr = so.FindProperty(n); if (pr != null) { if (pr.propertyType == SerializedPropertyType.Float) pr.floatValue = v; else pr.intValue = (int)v; } else Debug.LogWarning("URP prop missing " + n); }
            void SetB(string n, bool v) { var pr = so.FindProperty(n); if (pr != null) pr.boolValue = v; else Debug.LogWarning("URP prop missing " + n); }
            SetB("m_SupportsHDR", true);
            SetF("m_MSAA", 1);
            SetF("m_RenderScale", 1);
            SetF("m_ShadowDistance", 160);
            SetF("m_ShadowCascadeCount", 4);
            SetF("m_MainLightShadowmapResolution", 4096);
            SetB("m_MainLightShadowsSupported", true);
            SetB("m_SoftShadowsSupported", true);
            SetF("m_AdditionalLightsRenderingMode", 1);
            SetF("m_AdditionalLightsPerObjectLimit", 8);
            SetB("m_AdditionalLightShadowsSupported", false);
            SetB("m_SupportsCameraDepthTexture", false);
            SetB("m_SupportsCameraOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }

            // player
            PlayerSettings.productName = "Mission Mars - The Launch Window";
            PlayerSettings.companyName = "SpaceAppsTeam";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Step1 done");
        }

        [MenuItem("Mission Mars/2. Generate Assets & Scene")]
        public static void Step2()
        {
            Directory.CreateDirectory(P + "/Resources/Mats");
            Directory.CreateDirectory(P + "/Resources/Fonts");
            AssetDatabase.Refresh();

            // ---------- fonts ----------
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            foreach (var f in new[] { "Orbitron", "Rajdhani-Medium", "Rajdhani-Bold", "ShareTechMono", "Lora", "Baloo2", "Nunito" })
            {
                string outPath = $"{P}/Resources/Fonts/{f} SDF.asset";
                if (File.Exists(outPath)) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{P}/Fonts/{f}.ttf");
                if (!font) { Debug.LogError("Font missing " + f); continue; }
                var fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fa.name = f + " SDF";
                AssetDatabase.CreateAsset(fa, outPath);
                foreach (var t in fa.atlasTextures) { t.name = f + " Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
                fa.material.name = f + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
                if (fallback) fa.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
                // warm common glyphs
                fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~°·—–’“”…×−→", out _);
                EditorUtility.SetDirty(fa);
            }
            AssetDatabase.SaveAssets();

            // ---------- material templates ----------
            Mat("Lit", "Universal Render Pipeline/Lit", m => { });
            Mat("LitNormal", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(P + "/Resources/Tex/metal_plate_nor.jpg"));
                m.EnableKeyword("_NORMALMAP");
            });
            Mat("LitEmissive", "Universal Render Pipeline/Lit", m =>
            {
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            Mat("Unlit", "Universal Render Pipeline/Unlit", m => { });
            Mat("Terrain", "Universal Render Pipeline/Terrain/Lit", m => { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_MASKMAP"); });
            Mat("Sky", "MissionMars/MarsSky", m => { m.SetTexture("_StarTex", AssetDatabase.LoadAssetAtPath<Texture2D>(P + "/Resources/Tex/Hipparcos_Star_Map.jpg")); });
            Mat("Planet", "MissionMars/Planet", m => { });
            Mat("Halo", "MissionMars/AtmoHalo", m => { });
            Mat("Holo", "MissionMars/Holo", m => { });
            Mat("ParticleAdd", "MissionMars/ParticleAdd", m => { });
            Mat("ParticleAlpha", "MissionMars/ParticleAlpha", m => { });
            AssetDatabase.SaveAssets();

            // ---------- scene ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.8f, 0.58f, 0.42f);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(P + "/Resources/Mats/Sky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var boot = new GameObject("Bootstrap");
            boot.AddComponent<GameBootstrap>();
            Lightmapping.bakedGI = false; Lightmapping.realtimeGI = false;
            string scenePath = P + "/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Step2 done");
        }

        static void Mat(string name, string shader, System.Action<Material> cfg)
        {
            string path = $"{P}/Resources/Mats/{name}.mat";
            var sh = Shader.Find(shader);
            if (!sh) { Debug.LogError("Shader not found: " + shader); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh; m.enableInstancing = true;
            cfg(m);
            EditorUtility.SetDirty(m);
        }

        [MenuItem("Mission Mars/3. Build Windows (.exe)")]
        public static void Build()
        {
            string outDir = Path.GetFullPath("../Build/MissionMars");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { P + "/Scenes/Main.unity" },
                locationPathName = Path.Combine(outDir, "MissionMars.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[Build] result: " + report.summary.result + " size: " + report.summary.totalSize + " errors: " + report.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }

    public class TexImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("_Project/Resources/Tex")) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_nor.jpg")) ti.textureType = TextureImporterType.NormalMap;
            ti.maxTextureSize = assetPath.Contains("Hipparcos") || assetPath.Contains("Mars.jpg") || assetPath.Contains("Earth") ? 4096 : 2048;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.Contains("Hipparcos")) { ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
        }
    }
}
