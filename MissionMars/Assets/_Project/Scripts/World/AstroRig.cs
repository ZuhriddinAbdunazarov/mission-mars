using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    /// <summary>The astronaut model (Jungle Jim, CC-BY: Assets/Paid/Resources/Paid/AstronautJJ.fbx) with a simple skeleton made in code:
    /// hips, two legs and two arms. It can stand relaxed (arms down) or walk. The file has no skeleton of its own.
    /// If the file is missing (a fresh copy from GitHub), the NASA astronaut model is used instead (standing only).</summary>
    public class AstroRig : MonoBehaviour
    {
        public Transform Hips, LLeg, RLeg, LArm, RArm;
        /// <summary>0 = standing, 1 = walking.</summary>
        public float Walk;
        /// <summary>How much the arms hang down from the A-pose of the model (degrees).</summary>
        public float ArmsDown = 22f;
        float phase; Vector3 hips0;

        const string ModelName = "P_AstronautJJ";
        public static bool HasModel => R.Model(ModelName);

        /// <summary>A standing astronaut on the ground at groundPos, facing yaw, about height metres tall.</summary>
        public static GameObject Spawn(Vector3 groundPos, float yaw, float height, Transform parent)
        {
            var prefab = R.Model(ModelName);
            if (!prefab)
            {   // fallback: the NASA astronaut (no walking)
                var old = Util.PlaceUpright("Astronaut", groundPos, height, parent);
                old.transform.rotation = Quaternion.Euler(0, yaw, 0); Object.Destroy(old.GetComponent<BoxCollider>());
                return old;
            }
            var root = new GameObject("Astronaut"); if (parent) root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0, yaw, 0));
            var inst = Object.Instantiate(prefab, root.transform);
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var rig = root.AddComponent<AstroRig>();
            rig.Build(inst.transform);
            root.transform.localScale = Vector3.one * height; // the model is 1 unit tall, feet at 0
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }

        // model space of the file (1 unit tall, facing +Z, feet at y = 0), measured from the mesh
        static readonly Vector3 HipL = new Vector3(-0.085f, 0.47f, 0.03f), HipR = new Vector3(0.085f, 0.47f, 0.03f);
        static readonly Vector3 ShoulderL = new Vector3(-0.19f, 0.77f, 0.03f), ShoulderR = new Vector3(0.19f, 0.77f, 0.03f);

        void Build(Transform model)
        {
            Hips = Bone("Hips", new Vector3(0, 0.5f, 0), transform);
            LLeg = Bone("LegL", HipL, Hips); RLeg = Bone("LegR", HipR, Hips);
            LArm = Bone("ArmL", ShoulderL, Hips); RArm = Bone("ArmR", ShoulderR, Hips);
            hips0 = Hips.localPosition;
            var bones = new[] { Hips, LLeg, RLeg, LArm, RArm };
            var toRoot = transform.worldToLocalMatrix;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh; if (!src || !src.isReadable) continue;
                var m2v = toRoot * mf.transform.localToWorldMatrix;
                var verts = src.vertices; var w = new BoneWeight[verts.Length];
                for (int i = 0; i < verts.Length; i++) w[i] = Weigh(m2v.MultiplyPoint3x4(verts[i]));
                var mesh = Instantiate(src); mesh.name = src.name + " (rigged)";
                mesh.boneWeights = w;
                var bind = new Matrix4x4[bones.Length];
                for (int b = 0; b < bones.Length; b++) bind[b] = bones[b].worldToLocalMatrix * mf.transform.localToWorldMatrix;
                mesh.bindposes = bind;
                var go = mf.gameObject; var mats = go.GetComponent<MeshRenderer>().sharedMaterials;
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>()); Object.DestroyImmediate(mf);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = Hips; smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = true; // only a few astronauts: always keep their bounds right
            }
        }

        Transform Bone(string n, Vector3 at, Transform parent)
        {
            var t = new GameObject(n).transform; t.SetParent(parent, false);
            t.position = transform.TransformPoint(at); t.localRotation = Quaternion.identity; return t;
        }

        /// <summary>Which bone moves this point: legs below the hips, arms outside the body, the rest with the hips (smooth blend at the joints).</summary>
        static BoneWeight Weigh(Vector3 p)
        {
            float ax = Mathf.Abs(p.x);
            // arms: outside the body, between the hands (y 0.38) and the shoulders (y 0.8)
            float armEdge = Mathf.Lerp(0.215f, 0.185f, Mathf.InverseLerp(0.45f, 0.78f, p.y));
            if (p.y > 0.36f && p.y < 0.84f && ax > armEdge - 0.03f)
            {
                float k = Mathf.Clamp01((ax - (armEdge - 0.03f)) / 0.04f);
                if (p.y > 0.72f) k *= Mathf.Clamp01((ax - 0.15f) / 0.08f); // soft shoulder
                return Two(0, p.x < 0 ? 3 : 4, k);
            }
            // legs: below the hips, split left / right
            if (p.y < 0.5f)
            {
                float k = Mathf.Clamp01((0.5f - p.y) / 0.07f);
                return Two(0, p.x < 0 ? 1 : 2, k);
            }
            return Two(0, 0, 0);
        }
        static BoneWeight Two(int a, int b, float kb) => new BoneWeight { boneIndex0 = b, weight0 = kb, boneIndex1 = a, weight1 = 1 - kb };

        void LateUpdate()
        {
            if (!Hips) return;
            phase += Time.deltaTime * Mathf.Lerp(0, 6.5f, Walk);
            float s = Mathf.Sin(phase) * 26f * Walk;
            LLeg.localRotation = Quaternion.Euler(s, 0, 0);
            RLeg.localRotation = Quaternion.Euler(-s, 0, 0);
            LArm.localRotation = Quaternion.Euler(-s * 0.55f, 0, ArmsDown);
            RArm.localRotation = Quaternion.Euler(s * 0.55f, 0, -ArmsDown);
            Hips.localPosition = hips0 + Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * 0.018f * Walk;
        }
    }
}
