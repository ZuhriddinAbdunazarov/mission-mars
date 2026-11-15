using UnityEngine;

namespace MissionMars
{
    /// <summary>Particle effect factory.</summary>
    public static class FX
    {
        static Texture2D _puff, _dot;
        static Material _add, _alpha;

        public static Texture2D Puff
        {
            get
            {
                if (_puff) return _puff;
                int s = 128; _puff = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.6f + Mathf.PerlinNoise(x * 0.15f + 5, y * 0.15f) * 0.4f;
                        float a = Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d);
                        a *= Mathf.Lerp(0.55f, 1.15f, n);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                    }
                _puff.SetPixels32(px); _puff.Apply(true);
                return _puff;
            }
        }

        public static Texture2D Dot
        {
            get
            {
                if (_dot) return _dot;
                int s = 64; _dot = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.5f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _dot.SetPixels32(px); _dot.Apply(true);
                return _dot;
            }
        }

        public static Material Add { get { if (!_add) { _add = new Material(R.Template("ParticleAdd")); _add.mainTexture = Dot; } return _add; } }
        public static Material Alpha { get { if (!_alpha) { _alpha = new Material(R.Template("ParticleAlpha")); _alpha.mainTexture = Puff; } return _alpha; } }
        static Material _addPuff;
        public static Material AddPuff { get { if (!_addPuff) { _addPuff = new Material(R.Template("ParticleAdd")); _addPuff.mainTexture = Puff; } return _addPuff; } }

        public static ParticleSystem Make(string name, Vector3 pos, Transform parent, Material mat, System.Action<ParticleSystem> cfg)
        {
            var g = new GameObject(name);
            g.SetActive(false);
            if (parent) g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>();
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var main = ps.main; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            cfg(ps);
            g.SetActive(true);
            return ps;
        }

        static ParticleSystem.MinMaxGradient Grad(params (Color c, float t)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length]; var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) { ck[i] = new GradientColorKey(keys[i].c, keys[i].t); ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t); }
            g.SetKeys(ck, ak); return new ParticleSystem.MinMaxGradient(g);
        }

        public static ParticleSystem Fire(Vector3 pos, float scale, Transform parent = null)
        {
            return Make("Fire", pos, parent, AddPuff, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.8f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.4f * scale); m.maxParticles = 200; m.gravityModifier = -0.05f;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var e = ps.emission; e.rateOverTime = 45;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 12; s.radius = 0.5f * scale;
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1f, 0.85f, 0.5f, 0f), 0), (new Color(1f, 0.55f, 0.15f, 0.9f), 0.15f), (new Color(0.8f, 0.2f, 0.05f, 0.4f), 0.6f), (new Color(0.2f, 0.05f, 0.02f, 0f), 1));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f));
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1, 1);
            });
        }

        public static ParticleSystem Smoke(Vector3 pos, float scale, Transform parent = null, float dark = 0.12f)
        {
            return Make("Smoke", pos, parent, Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(6, 10); m.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 2f * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(1.5f * scale, 3f * scale); m.maxParticles = 160;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                m.startColor = new Color(dark, dark * 0.95f, dark * 0.9f, 0.7f);
                var e = ps.emission; e.rateOverTime = 7;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 10; s.radius = 0.6f * scale;
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); v.y = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 0), 0), (new Color(1, 1, 1, 0.75f), 0.1f), (new Color(1, 0.9f, 0.85f, 0.35f), 0.6f), (new Color(1, 1, 1, 0), 1));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 3.5f));
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            });
        }

        public static ParticleSystem Sparks(Vector3 pos, Transform parent = null, float rate = 0)
        {
            return Make("Sparks", pos, parent, Add, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.0f); m.startSpeed = new ParticleSystem.MinMaxCurve(2, 6);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f); m.gravityModifier = 0.38f; m.maxParticles = 200;
                m.startColor = new Color(1f, 0.7f, 0.3f);
                var e = ps.emission; e.rateOverTime = rate;
                if (rate <= 0) e.SetBursts(new[] { new ParticleSystem.Burst(0, 25, 40, 999, 2.5f) });
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.2f;
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 0.9f, 0.6f, 1), 0), (new Color(1, 0.4f, 0.1f, 1), 0.5f), (new Color(0.6f, 0.1f, 0.05f, 0), 1));
                var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f;
            });
        }

        public static ParticleSystem BurstSparks(Vector3 pos, Color c, int count = 40)
        {
            var ps = Make("Burst", pos, null, Add, p =>
            {
                var m = p.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f); m.gravityModifier = 0.2f; m.loop = false; m.duration = 1;
                m.startColor = c; m.stopAction = ParticleSystemStopAction.Destroy;
                var e = p.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.15f;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 1), 0), (new Color(1, 1, 1, 0), 1));
            });
            return ps;
        }

        /// <summary>Ambient dust motes that follow a target (attach to player).</summary>
        public static ParticleSystem AmbientDust(Transform follow)
        {
            return Make("AmbientDust", follow.position, follow, Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = 9; m.startSpeed = 0; m.maxParticles = 600;
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.18f);
                m.startColor = new Color(0.62f, 0.42f, 0.30f, 0.16f);
                var e = ps.emission; e.rateOverTime = 60;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(40, 10, 40);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(1.5f, 3.5f); v.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f); v.z = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
                var n = ps.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.3f;
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 0), 0), (new Color(1, 1, 1, 1), 0.2f), (new Color(1, 1, 1, 1), 0.8f), (new Color(1, 1, 1, 0), 1));
            });
        }

        /// <summary>Big low dust banks drifting across the plain (atmospheric depth).</summary>
        public static ParticleSystem DustBanks(Vector3 center)
        {
            return Make("DustBanks", center, null, Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = 40; m.startSpeed = 0; m.maxParticles = 90;
                m.startSize = new ParticleSystem.MinMaxCurve(30, 60); m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                m.startColor = new Color(0.86f, 0.58f, 0.4f, 0.16f); m.prewarm = true;
                var e = ps.emission; e.rateOverTime = 2.2f;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(600, 4, 600);
                var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(2, 4); v.y = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 0), 0), (new Color(1, 1, 1, 1), 0.25f), (new Color(1, 1, 1, 1), 0.75f), (new Color(1, 1, 1, 0), 1));
            });
        }

        /// <summary>Big cinematic explosion: flash, fireball, sparks, smoke and a light pulse.</summary>
        public static void Explosion(Vector3 pos, float scale, Transform parent = null)
        {
            var root = new GameObject("Explosion").transform; root.position = pos; if (parent) root.SetParent(parent, true);
            Object.Destroy(root.gameObject, 9f);
            Make("Flash", pos, root, Add, p =>
            {
                var m = p.main; m.loop = false; m.duration = 0.5f; m.startLifetime = 0.35f; m.startSpeed = 0; m.startSize = 26 * scale; m.maxParticles = 4;
                m.startColor = new Color(1, 0.9f, 0.7f, 1);
                var e = p.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 2) });
                var s = p.shape; s.enabled = false;
                var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.4f, 1, 1.4f));
                var col = p.colorOverLifetime; col.enabled = true; col.color = Grad((new Color(1, 1, 1, 1), 0), (new Color(1, 0.6f, 0.2f, 0), 1));
            });
            Make("Fireball", pos, root, AddPuff, p =>
            {
                var m = p.main; m.loop = false; m.duration = 0.6f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4 * scale, 14 * scale); m.startSize = new ParticleSystem.MinMaxCurve(3 * scale, 7 * scale); m.maxParticles = 300;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var e = p.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 90), new ParticleSystem.Burst(0.15f, 50) });
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 1.5f * scale;
                var lim = p.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1f * scale; lim.dampen = 0.12f;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 0.85f, 1), 0), (new Color(1, 0.6f, 0.15f, 0.95f), 0.2f), (new Color(0.9f, 0.25f, 0.05f, 0.6f), 0.55f), (new Color(0.15f, 0.05f, 0.02f, 0), 1));
                var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.5f, 1, 1.8f));
            });
            Make("Debris", pos, root, Add, p =>
            {
                var m = p.main; m.loop = false; m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(15 * scale, 45 * scale); m.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.45f * scale); m.maxParticles = 400;
                m.gravityModifier = 0.4f; m.startColor = new Color(1, 0.7f, 0.3f, 1);
                var e = p.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 260) });
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.5f * scale;
                var col = p.colorOverLifetime; col.enabled = true; col.color = Grad((new Color(1, 0.9f, 0.6f, 1), 0), (new Color(1, 0.4f, 0.1f, 0.8f), 0.6f), (new Color(0.5f, 0.1f, 0, 0), 1));
                var r = p.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1f;
            });
            Make("Smoke", pos, root, Alpha, p =>
            {
                var m = p.main; m.loop = false; m.duration = 1.5f; m.startDelay = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(3, 6);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2 * scale, 7 * scale); m.startSize = new ParticleSystem.MinMaxCurve(5 * scale, 10 * scale); m.maxParticles = 120;
                m.startColor = new Color(0.12f, 0.1f, 0.09f, 0.85f); m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var e = p.emission; e.rateOverTime = 40;
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 2f * scale;
                var lim = p.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.5f * scale; lim.dampen = 0.1f;
                var col = p.colorOverLifetime; col.enabled = true; col.color = Grad((new Color(1, 1, 1, 0), 0), (new Color(1, 1, 1, 0.9f), 0.15f), (new Color(1, 1, 1, 0), 1));
                var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2f));
            });
            // pre-rendered Blender explosion (flipbook 8x8) if available
            var sheet = Resources.Load<Texture2D>("Tex/explosion_sheet");
            if (sheet)
            {
                var fm = new Material(R.Template("ParticleAdd")); fm.mainTexture = sheet;
                Make("Flipbook", pos, root, fm, p =>
                {
                    var m = p.main; m.loop = false; m.duration = 0.4f; m.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 2.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 2f * scale);
                    m.startSize = new ParticleSystem.MinMaxCurve(14 * scale, 22 * scale); m.maxParticles = 8; m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                    var e = p.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 1), new ParticleSystem.Burst(0.12f, 2) });
                    var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 1.5f * scale;
                    var ts = p.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 8; ts.numTilesY = 8; ts.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
                });
            }
            var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(root, false); l.type = LightType.Point; l.range = 60 * scale; l.color = new Color(1, 0.6f, 0.25f);
            Tween.Run(1.6f, t => { if (l) l.intensity = 40 * (1 - t) * (1 - t); });
        }

        public static ParticleSystem DustBlast(Vector3 pos, float scale, bool loop = false)
        {
            return Make("DustBlast", pos, null, Alpha, ps =>
            {
                var m = ps.main; m.loop = loop; m.duration = 2; m.startLifetime = new ParticleSystem.MinMaxCurve(3, 7);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4 * scale, 14 * scale); m.startSize = new ParticleSystem.MinMaxCurve(3 * scale, 8 * scale);
                m.startColor = new Color(0.72f, 0.45f, 0.3f, 0.85f); m.maxParticles = 400; m.gravityModifier = 0.02f;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                if (!loop) m.stopAction = ParticleSystemStopAction.Destroy;
                var e = ps.emission; e.rateOverTime = loop ? 60 : 0;
                if (!loop) e.SetBursts(new[] { new ParticleSystem.Burst(0, 160) });
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = 2 * scale;
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.5f; lim.dampen = 0.08f;
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 0.9f), 0), (new Color(1, 1, 1, 0.6f), 0.5f), (new Color(1, 1, 1, 0), 1));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.7f, 1, 2.2f));
            });
        }

        public static ParticleSystem Exhaust(Transform parent, Vector3 localPos, float scale)
        {
            var ps = Make("Exhaust", parent.TransformPoint(localPos), parent, AddPuff, p =>
            {
                var m = p.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(18 * scale, 30 * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.4f * scale); m.maxParticles = 600;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = p.emission; e.rateOverTime = 260;
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 7; s.radius = 0.35f * scale;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 0.9f, 1), 0), (new Color(1, 0.65f, 0.2f, 0.9f), 0.25f), (new Color(0.9f, 0.25f, 0.05f, 0.3f), 0.7f), (new Color(0.2f, 0.05f, 0, 0), 1));
                var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.5f, 1, 2.2f));
            });
            ps.transform.localPosition = localPos;
            ps.transform.localRotation = Quaternion.Euler(90, 0, 0); // emit downward (-Y of parent)
            return ps;
        }

        /// <summary>Tight, bright rocket-engine flame (for the ship in space).</summary>
        public static ParticleSystem Flame(Transform parent, float scale)
        {
            var ps = Make("Flame", parent.position, parent, AddPuff, p =>
            {
                var m = p.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f); m.startSpeed = new ParticleSystem.MinMaxCurve(40 * scale, 60 * scale);
                m.startSize = new ParticleSystem.MinMaxCurve(1.6f * scale, 2.4f * scale); m.maxParticles = 500; m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = p.emission; e.rateOverTime = 300;
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 3; s.radius = 0.6f * scale;
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 1), 0), (new Color(1, 0.8f, 0.4f, 0.9f), 0.3f), (new Color(1, 0.4f, 0.1f, 0.4f), 0.7f), (new Color(0.6f, 0.1f, 0, 0), 1));
                var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1f, 1, 0.25f));
                var r = p.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f;
            });
            return ps;
        }

        /// <summary>Re-entry plasma streaks around a ship.</summary>
        public static ParticleSystem Plasma(Transform parent, float scale)
        {
            var ps = Make("Plasma", parent.position, parent, Add, p =>
            {
                var m = p.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); m.startSpeed = 0;
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.6f * scale); m.maxParticles = 800; m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = p.emission; e.rateOverTime = 500;
                var s = p.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 1.6f * scale;
                var v = p.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.x = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(-40 * scale, -25 * scale);
                var col = p.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 0.95f, 0.8f, 1), 0), (new Color(1, 0.5f, 0.15f, 0.8f), 0.3f), (new Color(0.8f, 0.15f, 0.05f, 0), 1));
                var r = p.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2.5f;
            });
            return ps;
        }

        public static ParticleSystem Mist(Vector3 pos, Transform parent, Color c)
        {
            return Make("Mist", pos, parent, Alpha, ps =>
            {
                var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(2, 4); m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f); m.maxParticles = 80; m.startColor = c;
                var e = ps.emission; e.rateOverTime = 10;
                var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.3f;
                var col = ps.colorOverLifetime; col.enabled = true;
                col.color = Grad((new Color(1, 1, 1, 0), 0), (new Color(1, 1, 1, 0.5f), 0.3f), (new Color(1, 1, 1, 0), 1));
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.5f, 1, 2f));
            });
        }
    }
}
