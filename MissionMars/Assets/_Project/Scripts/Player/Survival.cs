using System.Collections.Generic;
using UnityEngine;

namespace MissionMars
{
    public class Item
    {
        public string Id, Name, Icon; public Color Color; public bool Heavy;
        public Item(string id, string name, string icon, Color c, bool heavy = false) { Id = id; Name = name; Icon = icon; Color = c; Heavy = heavy; }
    }

    /// <summary>Oxygen, water, health and radiation. Forgiving by design: hitting zero never ends the game,
    /// the robot drags you back to the bunker and gives a tip.</summary>
    public class Survival : MonoBehaviour
    {
        public float O2 = 100, Water = 100, Health = 100;
        public float DoseMSv;                // accumulated dose
        public bool Inside;                  // inside the bunker
        public bool Active;                  // systems ticking
        public bool MoxieOn, WaterOn;        // machines installed in the bunker
        public float Storm;                  // 0..1 solar storm strength outside right now
        public float SunGlare;               // 0..1 how much the player is looking into the sun
        public float O2Drain = 0.32f, WaterDrain = 0f; // O2 is now LIFE SUPPORT (air + water together)
        public System.Action<string> OnCollapse;
        public readonly List<Item> Items = new List<Item>();
        public int Selected;
        bool collapsed;

        public const int Slots = 6;

        public float DoseRate // mSv per sol shown on HUD
        {
            get
            {
                if (Inside) return 0.02f;
                return 0.67f + Storm * 60f; // Curiosity RAD: ~0.67 mSv per sol on the surface
            }
        }

        public bool Has(string id) { foreach (var i in Items) if (i.Id == id) return true; return false; }
        public Item Get(string id) { foreach (var i in Items) if (i.Id == id) return i; return null; }
        public bool CarryingHeavy { get { foreach (var i in Items) if (i.Heavy) return true; return false; } }
        public bool Add(Item it)
        {
            if (Items.Count >= Slots) return false;
            Items.Add(it); Selected = Items.Count - 1;
            if (Game.Hud) Game.Hud.RefreshHotbar(true);
            return true;
        }
        public void Remove(string id)
        {
            Items.RemoveAll(i => i.Id == id);
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, Items.Count - 1));
            if (Game.Hud) Game.Hud.RefreshHotbar(false);
        }

        void Update()
        {
            var p = Game.Player;
            if (p) p.SpeedMul = CarryingHeavy ? 0.72f : 1f;
            for (int k = 0; k < Slots; k++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + k) && k < Items.Count) { Selected = k; Sfx.Play("click", 0.4f); Game.Hud?.RefreshHotbar(false); }
            if (Items.Count > 0 && Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f && !UIState.Modal)
            {
                Selected = (Selected + (Input.mouseScrollDelta.y < 0 ? 1 : -1) + Items.Count) % Items.Count; Game.Hud?.RefreshHotbar(false);
            }

            if (!Active || UIState.Modal) return;
            float dt = Time.deltaTime;

            // how much are we staring at the sun?
            SunGlare = 0;
            if (!Inside && Game.Sky && Game.Cam)
            {
                float d = Vector3.Dot(Game.Cam.transform.forward, Game.Sky.SunDir);
                SunGlare = Mathf.Clamp01((d - 0.9f) / 0.09f) * Game.Sky.Day;
            }

            if (!Inside)
            {
                float sprint = p && p.Speed01 > 0.7f ? 1.4f : 1f;
                O2 = Mathf.Max(0, O2 - O2Drain * sprint * dt);
                Health = Mathf.Max(0, Health - Storm * 2.6f * dt - SunGlare * Storm * 3f * dt);
            }
            else
            {
                { float dust = Game.Sky ? Game.Sky.DustStorm : 0; float make = (MoxieOn ? 7f * (1 - 0.65f * dust) : 0) + (WaterOn ? 5f : 0); O2 = make > 0 ? Mathf.Min(100, O2 + make * dt) : Mathf.Max(0, O2 - 0.06f * dt); }
                if (O2 > 30) Health = Mathf.Min(100, Health + 2.5f * dt);
            }
            Water = Mathf.Max(0, Water - WaterDrain * dt);
            if (O2 <= 0) Health = Mathf.Max(0, Health - 6f * dt);
            DoseMSv += (Inside ? 0.02f : 0.67f) * dt / (Game.Story ? Game.Story.SecondsPerDay : 1f) + (Inside ? 0 : Storm * 1.2f * dt); // Curiosity RAD: ~0.67 mSv per sol; a big flare adds ~30 mSv (May 2024 event)

            if (p) p.Sway = Mathf.Clamp01((40 - Health) / 40f) * 0.7f;

            if (!collapsed && Health <= 0)
            {
                collapsed = true;
                string why = O2 <= 0 ? "air" : "radiation";
                OnCollapse?.Invoke(why);
            }
        }

        public void Restore(float o2, float water, float health) { O2 = Mathf.Max(O2, o2); Water = Mathf.Max(Water, water); Health = Mathf.Max(Health, health); collapsed = false; }
        public void Drink(float amt) { O2 = Mathf.Min(100, O2 + amt); }
    }
}
