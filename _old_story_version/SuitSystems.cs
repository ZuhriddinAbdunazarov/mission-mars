using UnityEngine;

namespace MissionMars
{
    /// <summary>Oxygen, radiation sickness and temperature. Tuned to be forgiving for a live demo:
    /// running out of O2 never ends the game, it sends you back to the habitat.</summary>
    public class SuitSystems : MonoBehaviour
    {
        public float O2 = 100f;          // %
        public float Sickness = 0f;      // 0..100 (radiation)
        public float DoseMSv = 0f;       // accumulated dose shown on HUD
        public float Burns = 0f;         // 0..1 visual
        public bool Outside = true;
        public bool HelmetOn = true;
        public bool MoxieOnline;
        public bool Active;              // systems ticking
        public float SolarEvent;         // 0..1 solar particle event strength
        public float O2DrainPerSec = 0.22f;
        public float Cold;               // 0..1 frost overlay
        public System.Action OnO2Empty;
        bool emptyFired;

        public float DoseRate // mSv per hour (displayed)
        {
            get
            {
                if (!Outside) return 0.004f;
                float day = Game.Sky ? Game.Sky.Day : 1;
                return 0.028f * (0.4f + day) + SolarEvent * 48f; // ~0.67 mSv/sol baseline (Curiosity RAD)
            }
        }

        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            var p = Game.Player;
            if (Outside)
            {
                float drain = O2DrainPerSec * (p && p.Speed01 > 0.7f ? 1.5f : 1f);
                O2 = Mathf.Max(0, O2 - drain * dt);
                DoseMSv += DoseRate / 3600f * dt * 60f; // game-time compressed (1 s = 1 min)
                Sickness = Mathf.Min(100, Sickness + SolarEvent * 9f * dt);
                Burns = Mathf.Min(1, Burns + SolarEvent * 0.08f * dt);
                float temp = Game.Sky ? Game.Sky.OutsideTempC : -40;
                Cold = Mathf.MoveTowards(Cold, Mathf.InverseLerp(-55, -78, temp), dt * 0.2f);
            }
            else
            {
                O2 = MoxieOnline ? Mathf.Min(100, O2 + 12f * dt) : Mathf.Max(0, O2 - 0.05f * dt);
                Cold = Mathf.MoveTowards(Cold, 0, dt * 0.35f);
            }
            if (p) p.Sway = Mathf.Clamp01((Sickness - 25) / 75f) * 0.9f;

            if (O2 <= 0 && !emptyFired) { emptyFired = true; OnO2Empty?.Invoke(); }
            if (O2 > 5) emptyFired = false;
        }

        public void Refill(float amt) { O2 = Mathf.Min(100, O2 + amt); }
    }
}
