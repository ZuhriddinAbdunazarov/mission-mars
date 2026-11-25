using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MissionMars
{
    public static class Game
    {
        public static MonoBehaviour Runner;
        public static SkyController Sky;
        public static Camera Cam;
        public static PlayerController Player;
        public static Survival Suit;
        public static Hud Hud;
        public static Robot Bot;
        public static Story Story;
        public static bool InMenu = true;
        public static bool Paused;
#if MARS_BASE_REVISION
        public static readonly bool ExploreBase = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-campaign") < 0;
#else
        public static readonly bool ExploreBase = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-explore-base") >= 0;
#endif
        public static bool PointerOverUI => EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>Global UI modal state: when a modal is open the cursor is free and the player is frozen.</summary>
    public static class UIState
    {
        static int modals;
        public static bool Modal => modals > 0;
        public static bool Cinematic;
        public static void Push() { modals++; Refresh(); }
        public static void Pop() { modals = Mathf.Max(0, modals - 1); Refresh(); }
        public static void Reset() { modals = 0; Refresh(); }
        public static void Refresh()
        {
            bool free = modals > 0 || Game.InMenu;
            Cursor.lockState = free || Cinematic ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
    public static class PostFX
    {
        public static Bloom Bloom; public static ColorAdjustments Color; public static Vignette Vig; public static ChromaticAberration CA; public static FilmGrain Grain; public static LiftGammaGain Lift; public static DepthOfField Dof;

        public static void Setup(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var g = new GameObject("PostFX");
            var v = g.AddComponent<Volume>(); v.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            Bloom = p.Add<Bloom>(true); Bloom.threshold.Override(1.0f); Bloom.intensity.Override(0.6f); Bloom.scatter.Override(0.6f);
            Color = p.Add<ColorAdjustments>(true); Color.postExposure.Override(0.15f); Color.contrast.Override(10f); Color.saturation.Override(8f);
            CA = p.Add<ChromaticAberration>(true); CA.intensity.Override(0.06f);
            Grain = p.Add<FilmGrain>(true); Grain.type.Override(FilmGrainLookup.Thin1); Grain.intensity.Override(0.12f);
            Lift = p.Add<LiftGammaGain>(true);
            Vig = p.Add<Vignette>(true); Vig.intensity.Override(0.24f); Vig.smoothness.Override(0.5f);
            Dof = p.Add<DepthOfField>(true); Dof.mode.Override(DepthOfFieldMode.Gaussian); Dof.gaussianStart.Override(0.5f); Dof.gaussianEnd.Override(3f); Dof.gaussianMaxRadius.Override(1.5f); Dof.active = false;
            v.sharedProfile = p;
        }
    }

    public static class Util
    {
        public static Bounds Bounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(g.transform.position, Vector3.zero); bool init = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer || !r.enabled) continue; if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>Instantiate a model, stand its longest axis up (+Y), scale to the given height and seat it on the ground.</summary>
        public static GameObject PlaceUpright(string model, Vector3 groundPos, float height, Transform parent, bool flip = false)
        {
            var prefab = R.Model(model);
            var root = new GameObject(model); if (parent) root.transform.SetParent(parent, true); root.transform.position = groundPos;
            if (!prefab) return root;
            var inst = Object.Instantiate(prefab, root.transform);
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var b = Bounds(inst);
            Quaternion q = Quaternion.identity;
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) q = Quaternion.Euler(0, 0, flip ? -90 : 90);
            else if (b.size.z >= b.size.y && b.size.z >= b.size.x) q = Quaternion.Euler(flip ? 90 : -90, 0, 0);
            else if (flip) q = Quaternion.Euler(180, 0, 0);
            inst.transform.localRotation = q;
            b = Bounds(inst);
            inst.transform.localScale = Vector3.one * (height / Mathf.Max(0.0001f, b.size.y));
            b = Bounds(inst);
            inst.transform.position += new Vector3(groundPos.x - b.center.x, groundPos.y - b.min.y, groundPos.z - b.center.z);
            b = Bounds(inst);
            var bc = root.AddComponent<BoxCollider>(); bc.center = root.transform.InverseTransformPoint(b.center); bc.size = b.size * 0.9f;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }

        /// <summary>Instantiate a NASA model scaled so its largest dimension (or height) = size, seated on the ground.</summary>
        public static GameObject PlaceModel(string model, Vector3 groundPos, float size, float yaw = 0, Transform parent = null, bool byHeight = false, bool collider = false)
        {
            var prefab = R.Model(model);
            var root = new GameObject(model);
            if (parent) root.transform.SetParent(parent, true);
            root.transform.position = groundPos;
            if (!prefab) return root;
            var inst = Object.Instantiate(prefab, root.transform);
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var b = Bounds(inst);
            float dim = byHeight ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            inst.transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, dim));
            b = Bounds(inst);
            inst.transform.position += new Vector3(groundPos.x - b.center.x, groundPos.y - b.min.y, groundPos.z - b.center.z);
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (collider)
            {
                b = Bounds(inst);
                var bc = root.AddComponent<BoxCollider>();
                bc.center = root.transform.InverseTransformPoint(b.center);
                var ls = root.transform.lossyScale;
                bc.size = new Vector3(b.size.x / ls.x, b.size.y / ls.y, b.size.z / ls.z) * 0.92f;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }
    }
}
