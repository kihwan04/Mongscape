using System;

namespace Muhanok.Domain
{
    // Running speed from the cadence (steps a minute over the last 3 s): walking (up to 100) is 70 % of the base speed,
    // running (100-150) 100 %, sprinting (150 and up) 130 %, blended smoothly around the stage lines.
    // Below the target cadence for more than LagSeconds the police close in (PacePressure) and the screen edges redden;
    // that pressure alone never makes a capture.
    public sealed class CadencePace
    {
        public const float Walk = .7f, Run = 1f, Sprint = 1.3f;
        // Half widths of the blends around RunCadence and SprintCadence (steps a minute).
        public const float WalkBlend = 15f, SprintBlend = 10f;
        // Scale change per second (smooths step noise), lag before the police react, pressure rise and fall per second.
        public const float ScaleRate = .6f, LagSeconds = 3f, PressureRise = .22f, PressureFall = .5f;
        // The police come this close at most because of the pace (ChaseSystem pressure units; 1 = caught).
        public const float MaxPressure = .75f;
        // Target cadence: the bottom of the running stage, a little under RunCadence so a steady jog never lags.
        public const float TargetBelowRun = 10f;
        public float Scale { get; private set; } = 1f;
        public float LagFor { get; private set; }
        public bool Lagging => LagFor > LagSeconds;
        public float Pressure { get; private set; }
        public static float Factor(float stepsPerMinute, MotionTuning tuning)
        {
            tuning ??= new MotionTuning();
            float spm = float.IsNaN(stepsPerMinute) ? 0 : Math.Max(0, stepsPerMinute);
            float run = tuning.RunCadence, sprint = tuning.SprintCadence;
            if (spm <= run - WalkBlend) return Walk;
            if (spm < run + WalkBlend) return Lerp(Walk, Run, (spm - (run - WalkBlend)) / (2 * WalkBlend));
            if (spm <= sprint - SprintBlend) return Run;
            if (spm < sprint + SprintBlend) return Lerp(Run, Sprint, (spm - (sprint - SprintBlend)) / (2 * SprintBlend));
            return Sprint;
        }
        public static float Target(MotionTuning tuning) => (tuning ?? new MotionTuning()).RunCadence - TargetBelowRun;
        // enabled: a camera run with "달리기 속도 조절" on, on foot. Off (keyboard, the option off, riding): base speed, no pace pressure.
        public void Tick(float dt, bool enabled, float stepsPerMinute, MotionTuning tuning, float target)
        {
            if (dt <= 0 || float.IsNaN(dt)) return;
            float goal = enabled ? Factor(stepsPerMinute, tuning) : Run;
            Scale = Scale < goal ? Math.Min(goal, Scale + ScaleRate * dt) : Math.Max(goal, Scale - ScaleRate * dt);
            LagFor = enabled && stepsPerMinute < target ? LagFor + dt : 0;
            Pressure = Lagging ? Math.Min(MaxPressure, Pressure + PressureRise * dt) : Math.Max(0, Pressure - PressureFall * dt);
        }
        public void Reset() { Scale = 1f; LagFor = 0; Pressure = 0; }
        private static float Lerp(float a, float b, float t) { t = t < 0 ? 0 : t > 1 ? 1 : t; t = t * t * (3 - 2 * t); return a + (b - a) * t; }
    }
}
