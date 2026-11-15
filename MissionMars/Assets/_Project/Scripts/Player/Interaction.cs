using System;
using UnityEngine;

namespace MissionMars
{
    /// <summary>Anything the player can use. Put on an object that has (or whose children have) a collider.</summary>
    public class Interactable : MonoBehaviour
    {
        public string Verb = "Use";
        public string Label = "Object";
        public float HoldTime = 0f;
        public float Range = 3.6f;
        public Action OnUse;
        public bool Highlight = true;
        Renderer[] rends; MaterialPropertyBlock mpb;

        public static Interactable Add(GameObject g, string verb, string label, Action onUse, float hold = 0, bool addCollider = true)
        {
            var i = g.GetComponent<Interactable>();
            if (!i) i = g.AddComponent<Interactable>();
            i.Verb = verb; i.Label = label; i.OnUse = onUse; i.HoldTime = hold;
            if (addCollider && !g.GetComponentInChildren<Collider>())
            {
                var b = Util.Bounds(g);
                var bc = g.AddComponent<BoxCollider>();
                bc.center = g.transform.InverseTransformPoint(b.center);
                var ls = g.transform.lossyScale;
                bc.size = new Vector3(b.size.x / Mathf.Max(0.001f, ls.x), b.size.y / Mathf.Max(0.001f, ls.y), b.size.z / Mathf.Max(0.001f, ls.z));
            }
            return i;
        }

        public void Use() { OnUse?.Invoke(); }
    }

    public class Interactor : MonoBehaviour
    {
        public PlayerController P;
        public Interactable Current { get; private set; }
        float hold;
        public float HoldProgress => Current && Current.HoldTime > 0 ? hold / Current.HoldTime : 0;

        void Update()
        {
            Current = null;
            if (UIState.Modal || UIState.Cinematic || P.Frozen) { hold = 0; return; }
            var cam = P.Cam.transform;
            var hits = Physics.SphereCastAll(cam.position, 0.3f, cam.forward, 4.2f, ~0, QueryTriggerInteraction.Collide);
            float best = 999;
            foreach (var h in hits)
            {
                var it = h.collider.GetComponentInParent<Interactable>();
                if (!it || !it.enabled || !it.gameObject.activeInHierarchy) continue;
                if (h.distance > it.Range || h.distance >= best) continue;
                best = h.distance; Current = it;
            }
            if (!Current) { hold = 0; return; }
            if (Current.HoldTime <= 0)
            {
                if (Input.GetKeyDown(KeyCode.E)) { var c = Current; Sfx.Play("click"); c.Use(); }
            }
            else
            {
                if (Input.GetKey(KeyCode.E))
                {
                    hold += Time.deltaTime;
                    if (hold >= Current.HoldTime) { hold = 0; var c = Current; Sfx.Play("click"); c.Use(); }
                }
                else hold = Mathf.Max(0, hold - Time.deltaTime * 2);
            }
        }
    }

}
