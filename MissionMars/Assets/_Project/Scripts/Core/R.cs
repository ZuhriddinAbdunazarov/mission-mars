using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Central resource access + material factory. Template materials live in Resources/Mats
    /// (created by the editor setup) so every shader variant we need is included in the build.</summary>
    public static class R
    {
        static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();

        static T Load<T>(string path) where T : Object
        {
            if (cache.TryGetValue(path, out var o) && o) return (T)o;
            var r = Resources.Load<T>(path);
            if (r == null) Debug.LogWarning("[R] Missing resource: " + path);
            cache[path] = r;
            return r;
        }

        public static Material Template(string n) => Load<Material>("Mats/" + n);
        public static Texture2D Tex(string n) => Load<Texture2D>("Tex/" + n);
        public static GameObject Model(string n)
        {
            if (n.StartsWith("K_")) return Resources.Load<GameObject>("Kenney/" + n.Substring(2)); // Kenney Space Kit (CC0)
            if (n.StartsWith("P_")) return Resources.Load<GameObject>("Paid/" + n.Substring(2)); // bought props (not in git: may be missing)
            var g = Resources.Load<GameObject>("Sketchfab/" + n); return g ? g : (n.StartsWith("SF_") ? null : Load<GameObject>("NASA/" + n));
        }

        public static TMP_FontAsset Title => Load<TMP_FontAsset>("Fonts/Orbitron SDF");
        public static TMP_FontAsset Body => Load<TMP_FontAsset>("Fonts/Rajdhani-Medium SDF");
        public static TMP_FontAsset Bold => Load<TMP_FontAsset>("Fonts/Rajdhani-Bold SDF");
        public static TMP_FontAsset Mono => Load<TMP_FontAsset>("Fonts/ShareTechMono SDF");
        public static TMP_FontAsset Book => Load<TMP_FontAsset>("Fonts/Lora SDF");
        public static TMP_FontAsset Kid => Load<TMP_FontAsset>("Fonts/Baloo2 SDF");
        public static TMP_FontAsset KidBody => Load<TMP_FontAsset>("Fonts/Nunito SDF");

        public static Material Lit(Color c, float smooth = 0.3f, float metal = 0f)
        {
            var m = new Material(Template("Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        public static Material LitTex(string baseTex, string normalTex, Color tint, float tiling = 1f, float smooth = 0.3f, float metal = 0f)
        {
            var m = new Material(Template(normalTex != null ? "LitNormal" : "Lit"));
            m.SetTexture("_BaseMap", Tex(baseTex));
            if (normalTex != null) m.SetTexture("_BumpMap", Tex(normalTex));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            return m;
        }

        public static Material Emissive(Color baseC, Color emission, float smooth = 0.5f)
        {
            var m = new Material(Template("LitEmissive"));
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        public static Material Holo(Color c, float intensity = 1.5f, float baseA = 0.15f, float vfade = 0f)
        {
            var m = new Material(Template("Holo"));
            m.SetColor("_Color", c);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Base", baseA);
            m.SetFloat("_VFade", vfade);
            return m;
        }

        public static Material Unlit(Color c)
        {
            var m = new Material(Template("Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        // ---- metal / scene palette ----
        static Material _hull, _hullDark, _scorched, _rock, _orange, _cyanGlow, _floor, _panel;
        public static Material Hull => _hull ? _hull : (_hull = LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.86f, 0.86f, 0.88f), 1.5f, 0.55f, 0.75f));
        public static Material HullDark => _hullDark ? _hullDark : (_hullDark = LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.32f, 0.33f, 0.36f), 1.5f, 0.45f, 0.8f));
        public static Material Scorched => _scorched ? _scorched : (_scorched = LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.16f, 0.13f, 0.12f), 1.2f, 0.2f, 0.6f));
        public static Material Rock => _rock ? _rock : (_rock = LitTex("rock_boulder_dry_diff", "rock_boulder_dry_nor", new Color(0.78f, 0.52f, 0.40f), 1f, 0.12f, 0f));
        public static Material Orange => _orange ? _orange : (_orange = Lit(new Color(0.95f, 0.45f, 0.12f), 0.4f));
        public static Material CyanGlow => _cyanGlow ? _cyanGlow : (_cyanGlow = Emissive(new Color(0.1f, 0.2f, 0.25f), new Color(0.3f, 1.6f, 2.2f)));
        public static Material Floor => _floor ? _floor : (_floor = LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.42f, 0.43f, 0.46f), 3f, 0.55f, 0.8f));
        public static Material Panel => _panel ? _panel : (_panel = LitTex("metal_plate_diff", "metal_plate_nor", new Color(0.72f, 0.73f, 0.76f), 2f, 0.4f, 0.4f));
    }
}
