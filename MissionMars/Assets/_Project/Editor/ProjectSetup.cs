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

        [MenuItem("Mission Mars/4. Build WebGL (browser)")]
        public static void BuildWeb()
        {
            string outDir = Path.GetFullPath("../Build/Web");
            Directory.CreateDirectory(outDir);
            // Brotli files served with "Content-Encoding: br" (Site/_headers on Netlify): the browser unpacks them natively
            // and streams the wasm, much faster than the JavaScript decompression fallback.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true; // safety net: still loads if a host ignores the headers
            // release-optimized, smaller code (the default "shorter build time" makes a debug-sized wasm)
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = false; // colliders etc. are added from code at runtime; stripping removed them
            PlayerSettings.SplashScreen.show = false; // no Unity logo screen; our own Mars loader shows instead
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { P + "/Scenes/Main.unity" },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[BuildWeb] result: " + report.summary.result + " size: " + report.summary.totalSize + " errors: " + report.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Mission Mars/3. Build Windows (.exe)")]
        public static void Build()
        {
            bool revision = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-revision-build") >= 0;
            string outDir = Path.GetFullPath(revision ? "../Build/MarsBaseRevision" : "../Build/MissionMars");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { P + "/Scenes/Main.unity" },
                locationPathName = Path.Combine(outDir, "MissionMars.exe"),
                extraScriptingDefines = revision ? new[] { "MARS_BASE_REVISION" } : System.Array.Empty<string>(),
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
        public override uint GetVersion() => 5; // bump to reimport textures when these rules change

        static readonly System.Text.RegularExpressions.Regex ModelTex = new System.Text.RegularExpressions.Regex(@"_\d+_(color|normal|mr|occlusion|emissive)\.(jpg|png)$");

        void OnPreprocessTexture()
        {
            var m = ModelTex.Match(assetPath);
            if (m.Success && assetPath.Contains("/Resources/")) { ModelTexture(m.Groups[1].Value); return; }
            if (assetPath.StartsWith("Assets/Paid/")) { PaidTexture(); return; }
            if (!assetPath.Contains("_Project/Resources/Tex")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_nor.jpg");
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            bool big = assetPath.Contains("Hipparcos") || assetPath.Contains("Mars.jpg") || assetPath.Contains("Earth");
            ti.maxTextureSize = big ? 4096 : 2048;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.Contains("Hipparcos")) { ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
            if (assetPath.Contains("QR_")) { ti.textureCompression = TextureImporterCompression.Uncompressed; ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; return; } // must stay sharp to scan
            // web build: smaller and Crunch-compressed (downloads several times smaller; PC keeps full quality)
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL", overridden = true,
                maxTextureSize = big ? 2048 : 1024,
                format = normal || assetPath.EndsWith(".png") ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched,
                crunchedCompression = true, compressionQuality = 50,
            });
        }

        /// <summary>Pictures of 3D models split out of their .glb (Site/tools/externalize.mjs): Unity can compress them,
        /// which it cannot do for pictures kept inside a .glb. Colour = sRGB; normal / metal-rough / occlusion = linear data.</summary>
        void ModelTexture(string role)
        {
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = role == "color" || role == "emissive";
            if (assetPath.Contains("/Quaternius/")) // a tiny 32x32 colour palette: compression or mipmaps would smear the colours
            {
                ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.ClearPlatformTextureSettings("WebGL");
                return;
            }
            ti.mipmapEnabled = true; ti.anisoLevel = 4;
            bool hero = System.Text.RegularExpressions.Regex.IsMatch(assetPath, "MarsBase|Plantation|Habitat|Weather|Comms|Bunker"); // buildings seen up close: keep them sharp
            ti.maxTextureSize = hero ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            bool alpha = ti.DoesSourceTextureHaveAlpha();
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL", overridden = true, maxTextureSize = hero ? 2048 : role == "color" ? 1024 : 512,
                format = alpha ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched,
                crunchedCompression = true, compressionQuality = 60,
            });
        }

        /// <summary>Bought props come with 4K textures: they are small objects, so 512 px on the web (1024 on PC) looks the same.</summary>
        void PaidTexture()
        {
            var ti = (TextureImporter)assetImporter;
            bool tiny = assetPath.Contains("/Reactor/"); // palette textures, already a few pixels: keep them sharp
            if (tiny) { ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; return; }
            bool normal = assetPath.EndsWith("_Normal.png");
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            bool alpha = assetPath.Contains("MetallicSmoothness"); // smoothness lives in alpha
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL", overridden = true, maxTextureSize = 512,
                format = normal || alpha ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched,
                crunchedCompression = true, compressionQuality = 50,
            });
        }
    }
}
