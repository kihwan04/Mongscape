using System;

namespace Muhanok.Domain
{
    // Turns pose frames into game intent for one play style.
    // Lanes follow body position (left zone / centre / right zone) with hysteresis, so two lanes is simply a bigger step
    // and returning to the centre returns the runner. Jump is an edge; crouch and high knee are held states.
    // Full body: on foot the zone is where the hips stand (side step); in the cart it is how far the shoulders tilt over the hips.
    public sealed class MotionClassifier
    {
        private readonly MotionTuning tuning;
        private readonly float[] px = new float[PoseFrame.Count], py = new float[PoseFrame.Count], vis = new float[PoseFrame.Count];
        private bool smoothed;
        private PlayStyle style = PlayStyle.FullBody;
        // Riding the cart, and whether the zone must be re-read at once because the measure just changed.
        private bool cart, cartChanged;
        // Baseline (calibrated standing pose).
        private bool calibrated;
        private int samples;
        private float bCx, bSy, bHy, bScale, bKneeL, bKneeR, bAnkle, bNose, bTilt, bRoll;
        private float sumCx, sumSy, sumHy, sumScale, sumKneeL, sumKneeR, sumAnkle, sumNose, sumTilt, sumRoll;
        private int ankleSamples, noseSamples, kneeSamples;
        // Tracking state.
        private double lostSince = -1, crouchPendingSince = -1, crouchSince = -10, landingUntil, kneeUntil, lastStep, lastJump = -10, lastLaneChange = -10, pumpUntil;
        private bool airborne, hopping, handsUp, duckOnLoss;
        // Previous full-body frame, for the rise speed of shoulders and hips.
        private double lastTrackTime = -1;
        private float lastSy, lastHy;
        // How far the last tracked frame was toward a duck (1 = threshold); a loss after a dip counts as ducking.
        private float dropRatio;
        private double actionLockUntil, laneCandidateSince;
        private int laneCandidate;
        private bool Locked(double now) => now < actionLockUntil;
        private bool ActionSeen(int i) => vis[i] >= tuning.ActionVisibility;
        private void Lock(double now, double seconds) { if (now + seconds > actionLockUntil) actionLockUntil = now + seconds; }
        private int stepSign, pumpSign;
        private readonly double[] stepTimes = new double[8];
        private int stepIndex;
        public PoseStatus Status { get; private set; } = PoseStatus.Searching;
        public bool Calibrated => calibrated;
        public float CalibrationProgress => calibrated ? 1f : Math.Min(1f, samples / (float)Math.Max(1, tuning.CalibrationFrames));
        public readonly MotionReading Reading = new MotionReading();
        public float StepsPerMinute { get; private set; }
        public PlayStyle Style => style;
        public MotionClassifier(MotionTuning motionTuning) { tuning = motionTuning ?? new MotionTuning(); }
        public void SetStyle(PlayStyle next) { style = next; Recalibrate(); }
        // Boarding or leaving the cart switches the full body from side steps to leaning (and back).
        public void SetCart(bool riding) { if (riding == cart) return; cart = riding; cartChanged = true; }
        public void Recalibrate()
        {
            calibrated = false; samples = 0; smoothed = false; Reading.Clear(); airborne = hopping = handsUp = duckOnLoss = false; dropRatio = 0; actionLockUntil = 0; laneCandidate = 0;
            crouchPendingSince = -1; crouchSince = -10; lastTrackTime = -1; laneLastTime = -1; kneeUntil = pumpUntil = 0; stepSign = pumpSign = 0; StepsPerMinute = 0;
            Status = PoseStatus.Calibrating;
        }

        public MotionReading Update(PoseFrame frame)
        {
            Reading.JumpEdge = false;
            double now = frame.Time;
            if (!frame.HasPose || !Smooth(frame) || !RequiredVisible())
            {
                laneLastTime = -1;
                if (lostSince < 0) { lostSince = now; duckOnLoss = calibrated && Status == PoseStatus.Tracking && dropRatio > .5f; }
                double lost = now - lostSince;
                if (calibrated && lost <= tuning.LostGraceSeconds && !duckOnLoss) return Reading;
                if (calibrated && duckOnLoss && lost <= tuning.DuckHoldSeconds) { Reading.Crouch = true; Reading.Active = true; Status = PoseStatus.Tracking; return Reading; }
                if (now - lostSince > tuning.LostSeconds && calibrated) Recalibrate();
                Status = PoseStatus.Searching; Reading.Crouch = Reading.HighKnee = Reading.Active = false; crouchPendingSince = -1;
                return Reading;
            }
            // Landmarks right after re-acquiring the body jump around; ignore actions for a moment.
            if (lostSince >= 0 && calibrated) Lock(now, tuning.ReacquireLockSeconds);
            lostSince = -1;
            if (!calibrated) { Calibrate(frame.Aspect); return Reading; }
            Status = PoseStatus.Tracking;
            if (style == PlayStyle.UpperBody) TrackUpper(frame.Aspect, now); else TrackFull(frame.Aspect, now);
            UpdateCadence(now);
            Reading.Active = now - lastStep < .8 || now - lastJump < 1 || Reading.Crouch || Reading.HighKnee || now - lastLaneChange < 1 || now < pumpUntil;
            return Reading;
        }

        private bool Smooth(PoseFrame frame)
        {
            float a = smoothed ? Clamp(tuning.Smoothing, .05f, 1f) : 1f;
            for (int i = 0; i < PoseFrame.Count; i++)
            {
                var p = frame.Points[i];
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y)) return false;
                // Faint joints keep their last position instead of jumping to a guess.
                if (!smoothed || p.Visibility >= MinVisibility * .6f) { px[i] += (p.X * frame.Aspect - px[i]) * a; py[i] += (p.Y - py[i]) * a; }
                vis[i] = p.Visibility;
            }
            smoothed = true; return true;
        }
        private float MinVisibility => style == PlayStyle.UpperBody ? tuning.UpperMinVisibility : tuning.MinVisibility;
        private bool Seen(int i) => vis[i] >= MinVisibility;
        private bool RequiredVisible() => style == PlayStyle.UpperBody
            ? Seen(PoseJoint.LeftShoulder) && Seen(PoseJoint.RightShoulder)
            : Seen(PoseJoint.LeftShoulder) && Seen(PoseJoint.RightShoulder) && Seen(PoseJoint.LeftHip) && Seen(PoseJoint.RightHip);

        private float ShoulderX => (px[PoseJoint.LeftShoulder] + px[PoseJoint.RightShoulder]) * .5f;
        private float ShoulderY => (py[PoseJoint.LeftShoulder] + py[PoseJoint.RightShoulder]) * .5f;
        private float HipX => (px[PoseJoint.LeftHip] + px[PoseJoint.RightHip]) * .5f;
        private float HipY => (py[PoseJoint.LeftHip] + py[PoseJoint.RightHip]) * .5f;
        private float ShoulderWidth => Distance(PoseJoint.LeftShoulder, PoseJoint.RightShoulder);
        private float Torso => (float)Math.Sqrt((ShoulderX - HipX) * (ShoulderX - HipX) + (ShoulderY - HipY) * (ShoulderY - HipY));
        private float Distance(int a, int b) => (float)Math.Sqrt((px[a] - px[b]) * (px[a] - px[b]) + (py[a] - py[b]) * (py[a] - py[b]));
        // The full body follows where the hips stand: stepping into the side zone changes lane, leaning the shoulders
        // alone does not. The upper body only has the shoulders.
        private float CentreX => style == PlayStyle.UpperBody ? ShoulderX : HipX;
        // Sideways offset of the shoulders over the hips (raw, divide by the body scale): the cart lean.
        private float Tilt => ShoulderX - HipX;
        private float CurrentScale => style == PlayStyle.UpperBody ? ShoulderWidth : Torso;
        // Shoulder line tilt in degrees, positive when the shoulder further along +X is lower (a lean toward +X).
        // The slope does not depend on which shoulder is which, so a mirrored camera reads the same.
        private float Roll
        {
            get
            {
                float dx = px[PoseJoint.RightShoulder] - px[PoseJoint.LeftShoulder];
                if (Math.Abs(dx) < 1e-4f) return 0f;
                return (float)(Math.Atan((py[PoseJoint.RightShoulder] - py[PoseJoint.LeftShoulder]) / dx) * 180 / Math.PI);
            }
        }
        private bool KneesSeen => Seen(PoseJoint.LeftKnee) && Seen(PoseJoint.RightKnee);
        private bool AnklesSeen => Seen(PoseJoint.LeftAnkle) && Seen(PoseJoint.RightAnkle);
        private float AnkleY => (py[PoseJoint.LeftAnkle] + py[PoseJoint.RightAnkle]) * .5f;
        private float KneeDrop(int knee, int hip) => py[knee] - py[hip];

        private void Calibrate(float aspect)
        {
            Status = PoseStatus.Calibrating;
            float scale = CurrentScale;
            if (scale < .02f) { samples = 0; return; }
            // Restart when the player is still moving: the baseline must be a calm standing pose.
            if (samples > 0)
            {
                float meanX = sumCx / samples, meanY = sumSy / samples, meanScale = sumScale / samples;
                if (Math.Abs(CentreX - meanX) > tuning.CalibrationStillness * meanScale || Math.Abs(ShoulderY - meanY) > tuning.CalibrationStillness * meanScale) samples = 0;
                // The full body's centre is the hips only; a shoulder sway must also restart, or it becomes the cart's "upright" lean.
                else if (style != PlayStyle.UpperBody && Math.Abs(Tilt / scale - sumTilt / samples) > tuning.CalibrationStillness) samples = 0;
                // The upper body's lean is also the shoulder tilt: a rocking shoulder line restarts too.
                else if (style == PlayStyle.UpperBody && Math.Abs(Roll - sumRoll / samples) > tuning.UpperLeanEnterDegrees * .5f) samples = 0;
            }
            if (samples == 0) { sumCx = sumSy = sumHy = sumScale = sumKneeL = sumKneeR = sumAnkle = sumNose = sumTilt = sumRoll = 0; ankleSamples = noseSamples = kneeSamples = 0; }
            samples++;
            sumCx += CentreX; sumSy += ShoulderY; sumScale += scale;
            // Standing straight is rarely exactly upright to the camera; the cart lean is measured from this posture.
            if (style != PlayStyle.UpperBody) { sumHy += HipY; sumTilt += Tilt / scale; } else sumRoll += Roll;
            if (KneesSeen && style != PlayStyle.UpperBody) { sumKneeL += KneeDrop(PoseJoint.LeftKnee, PoseJoint.LeftHip); sumKneeR += KneeDrop(PoseJoint.RightKnee, PoseJoint.RightHip); kneeSamples++; }
            if (AnklesSeen && style != PlayStyle.UpperBody) { sumAnkle += AnkleY; ankleSamples++; }
            if (Seen(PoseJoint.Nose)) { sumNose += py[PoseJoint.Nose]; noseSamples++; }
            if (samples < tuning.CalibrationFrames) return;
            bCx = sumCx / samples; bSy = sumSy / samples; bHy = sumHy / samples; bScale = sumScale / samples; bTilt = sumTilt / samples; bRoll = sumRoll / samples;
            bKneeL = kneeSamples > 0 ? sumKneeL / kneeSamples : float.NaN; bKneeR = kneeSamples > 0 ? sumKneeR / kneeSamples : float.NaN;
            bAnkle = ankleSamples > 0 ? sumAnkle / ankleSamples : float.NaN; bNose = noseSamples > 0 ? sumNose / noseSamples : float.NaN;
            calibrated = true; Reading.Clear(); Status = PoseStatus.Tracking;
        }

        private float Scale() => Clamp(CurrentScale, bScale * .6f, bScale * 1.6f);

        // Sideways speed of the lane offset (body units per second), for reading the lane slightly ahead.
        private float laneSpeed, laneLastOffset;
        private double laneLastTime = -1;
        private float LaneLead(float offset, double now)
        {
            double dt = now - laneLastTime;
            if (laneLastTime < 0 || dt <= 0 || dt > .25) laneSpeed = 0;
            else laneSpeed += ((float)((offset - laneLastOffset) / dt) - laneSpeed) * .5f;
            laneLastTime = now; laneLastOffset = offset;
            return Clamp(laneSpeed * tuning.LanePredictSeconds, -tuning.LanePredictMax, tuning.LanePredictMax);
        }

        private void UpdateLane(float offset, float enter, float exit, double now, bool locked)
        {
            offset += LaneLead(offset, now);
            int lane = Reading.Lane;
            if (lane == 0) { if (offset < -enter) lane = -1; else if (offset > enter) lane = 1; }
            else if (lane < 0) { if (offset > -exit) lane = offset > enter ? 1 : 0; }
            else if (offset < exit) lane = offset < -enter ? -1 : 0;
            // While locked, or until the new zone has held for a moment, the lane does not change.
            if (locked || lane == Reading.Lane) { laneCandidate = Reading.Lane; return; }
            if (lane != laneCandidate) { laneCandidate = lane; laneCandidateSince = now; return; }
            if (now - laneCandidateSince < tuning.LaneHoldSeconds) return;
            Reading.Lane = lane; lastLaneChange = now;
        }

        private void TrackFull(float aspect, double now)
        {
            float scale = Scale();
            // On foot: where the hips stand. In the cart: how far the shoulders tilt over the hips; stepping aside does nothing.
            float offset = cart ? Tilt / scale - bTilt : (CentreX - bCx) / scale;
            float enter = cart ? tuning.CartLeanEnter : tuning.LaneEnter, exit = cart ? tuning.CartLeanExit : tuning.LaneExit;
            float drop = (ShoulderY - bSy) / scale, hipRise = (bHy - HipY) / scale, shoulderRise = (bSy - ShoulderY) / scale;
            // Rise speed of the slower of shoulders and hips, in body units per second.
            double dt = now - lastTrackTime;
            float riseSpeed = lastTrackTime >= 0 && dt > 0 && dt < .2 ? Math.Min(lastSy - ShoulderY, lastHy - HipY) / scale / (float)dt : 0f;
            lastTrackTime = now; lastSy = ShoulderY; lastHy = HipY;
            dropRatio = drop / tuning.CrouchDrop;
            // A fresh crouch may be a jump wind-up; it keeps the lane until the cancel window has passed.
            bool windUp = now - crouchSince < tuning.CrouchJumpCancelSeconds;
            // A dip moves the shoulders; it must not read as a sideways step.
            bool laneLocked = Locked(now) || airborne || Reading.Crouch || windUp || dropRatio > .6f;
            // Boarding or leaving: read the zone afresh, the other measure's zone means nothing here.
            // Mid-jump or crouching the body is not settled, so it starts from the centre and the usual hold applies from there.
            if (cartChanged) { cartChanged = false; laneLastTime = -1; Reading.Lane = laneCandidate = !laneLocked && Math.Abs(offset) > enter ? Math.Sign(offset) : 0; }
            UpdateLane(offset, enter, exit, now, laneLocked);
            bool feetUp = !AnklesSeen || float.IsNaN(bAnkle) || (bAnkle - AnkleY) / scale > tuning.JumpRise * .5f;
            bool up = hipRise > tuning.JumpRise && shoulderRise > tuning.JumpRise * .8f && feetUp;
            // Jump wins over a crouch that only just confirmed: the dip was the take-off. No refractory lock.
            if (up && Reading.Crouch && windUp) Reading.Crouch = false;
            if (up && !airborne && now - lastJump > .25 && !Locked(now) && !Reading.Crouch)
            {
                Reading.JumpEdge = true; lastJump = now;
                // A dip right before take-off is part of the jump, not a slide.
                if (!Reading.Crouch) crouchPendingSince = -1;
            }
            if (up) airborne = true;
            else if (airborne && hipRise < tuning.JumpRise * .4f) { airborne = false; landingUntil = now + tuning.LandingGraceSeconds; }
            // A low hop never reaches the jump line; a fast rise still earns the landing grace once the hips settle.
            if (riseSpeed > tuning.JumpAttemptSpeed) hopping = true;
            else if (hopping && hipRise < tuning.JumpRise * .4f) { hopping = false; landingUntil = Math.Max(landingUntil, now + tuning.LandingGraceSeconds); }
            UpdateCrouch(drop > tuning.CrouchDrop && !airborne && now >= landingUntil, drop < tuning.CrouchDrop * .65f, now, tuning.CrouchConfirmSeconds, tuning.CrouchJumpCancelSeconds);
            // High knee: one knee rises toward its hip; squats and tucks move both knees, so require asymmetry.
            if (KneesSeen && ActionSeen(PoseJoint.LeftKnee) && ActionSeen(PoseJoint.RightKnee) && !float.IsNaN(bKneeL))
            {
                float liftL = (bKneeL - KneeDrop(PoseJoint.LeftKnee, PoseJoint.LeftHip)) / scale;
                float liftR = (bKneeR - KneeDrop(PoseJoint.RightKnee, PoseJoint.RightHip)) / scale;
                float diff = liftL - liftR;
                if (!airborne && Math.Max(liftL, liftR) > tuning.HighKneeLift && Math.Abs(diff) > tuning.HighKneeLift * .7f) kneeUntil = now + tuning.HighKneeHoldSeconds;
                // Alternating small lifts are running in place.
                int sign = Math.Abs(diff) > tuning.StepLift ? Math.Sign(diff) : 0;
                if (sign != 0 && sign != stepSign) { if (stepSign != 0) RegisterStep(now); stepSign = sign; }
            }
            Reading.HighKnee = now < kneeUntil;
            if (Reading.Lane == 0 && !airborne && !Reading.Crouch && !Reading.HighKnee && Math.Abs(offset) < exit * .5f) Drift();
        }

        private void TrackUpper(float aspect, double now)
        {
            // The upper body leans the same way on foot and in the cart.
            cartChanged = false;
            // Leaning toward the camera widens the shoulders; the calibrated width keeps the lane scale steady.
            float scale = bScale;
            float offset = (CentreX - bCx) / scale;
            float drop = (ShoulderY - bSy) / scale;
            float noseDrop = Seen(PoseJoint.Nose) && !float.IsNaN(bNose) ? (py[PoseJoint.Nose] - bNose) / scale : 0f;
            bool noseDuck = noseDrop > tuning.UpperCrouchDrop * 1.3f;
            dropRatio = Math.Max(drop / tuning.UpperCrouchDrop, noseDrop / (tuning.UpperCrouchDrop * 1.3f));
            bool ducking = Reading.Crouch || crouchPendingSince >= 0 || dropRatio > .5f;
            if (ducking) Lock(now, tuning.RefractorySeconds);
            bool wristsSeen = ActionSeen(PoseJoint.LeftWrist) && ActionSeen(PoseJoint.RightWrist);
            // A sideways lean tilts the shoulder line too, and the tilt needs far less movement than shifting the shoulders
            // across. Raising a hand or a big arm pump lifts one shoulder, so the tilt is ignored then.
            bool handRising = ActionSeen(PoseJoint.LeftWrist) && py[PoseJoint.LeftWrist] < ShoulderY || ActionSeen(PoseJoint.RightWrist) && py[PoseJoint.RightWrist] < ShoulderY;
            bool bigPump = wristsSeen && Math.Abs(py[PoseJoint.LeftWrist] - py[PoseJoint.RightWrist]) / scale > tuning.ArmPumpAmplitude;
            float tilt = handRising || bigPump ? 0f : (Roll - bRoll) * tuning.UpperLaneEnter / Math.Max(.1f, tuning.UpperLeanEnterDegrees);
            float lean = Math.Abs(tilt) > Math.Abs(offset) ? tilt : offset;
            UpdateLane(lean, tuning.UpperLaneEnter, tuning.UpperLaneExit, now, Locked(now));
            // Hands must rise above the calibrated head height; the live nose drops when ducking and would fake a jump.
            float head = !float.IsNaN(bNose) ? bNose : bSy - bScale;
            bool hands = wristsSeen && py[PoseJoint.LeftWrist] < head - tuning.UpperHandsAboveNose * scale && py[PoseJoint.RightWrist] < head - tuning.UpperHandsAboveNose * scale;
            if (hands && !handsUp && now - lastJump > .25 && !Locked(now)) { Reading.JumpEdge = true; lastJump = now; crouchPendingSince = -1; }
            if (hands) handsUp = true;
            else if (!wristsSeen || py[PoseJoint.LeftWrist] > ShoulderY || py[PoseJoint.RightWrist] > ShoulderY) handsUp = false;
            UpdateCrouch((drop > tuning.UpperCrouchDrop || noseDuck) && !handsUp, drop < tuning.UpperCrouchDrop * .65f && !noseDuck, now, tuning.UpperCrouchConfirmSeconds, 0f);
            // Arm pumping replaces knee lifts: big alternation is the stair exercise, small alternation counts as running.
            if (wristsSeen && !handsUp)
            {
                float diff = (py[PoseJoint.LeftWrist] - py[PoseJoint.RightWrist]) / scale;
                int sign = Math.Abs(diff) > tuning.ArmSwingAmplitude ? Math.Sign(diff) : 0;
                if (sign != 0 && sign != pumpSign)
                {
                    if (pumpSign != 0) { RegisterStep(now); pumpUntil = now + .8; if (Math.Abs(diff) > tuning.ArmPumpAmplitude) kneeUntil = now + tuning.HighKneeHoldSeconds + .25; }
                    pumpSign = sign;
                }
            }
            Reading.HighKnee = now < kneeUntil;
            if (Reading.Lane == 0 && !handsUp && !Reading.Crouch && Math.Abs(lean) < tuning.UpperLaneExit * .5f) Drift();
        }

        // A crouch released within cancelSeconds of confirming ends without the refractory lock, so the jump it led into can count.
        private void UpdateCrouch(bool down, bool released, double now, float confirmSeconds, float cancelSeconds)
        {
            if (Reading.Crouch) { if (released) { Reading.Crouch = false; if (now - crouchSince >= cancelSeconds) Lock(now, tuning.RefractorySeconds); } return; }
            if (!down) { crouchPendingSince = -1; return; }
            if (crouchPendingSince < 0) crouchPendingSince = now;
            if (now - crouchPendingSince >= confirmSeconds) { Reading.Crouch = true; crouchPendingSince = -1; crouchSince = now; }
        }

        // Slowly follow a player who drifts while standing still, so the centre zone stays centred.
        private void Drift()
        {
            float d = tuning.BaselineDrift;
            // Only the measure in use follows the player: a long lean on foot must not move the cart's upright.
            if (style != PlayStyle.UpperBody && cart) bTilt += (Tilt / Scale() - bTilt) * d * .5f;
            else bCx += (CentreX - bCx) * d * .5f;
            if (style == PlayStyle.UpperBody) bRoll += (Roll - bRoll) * d * .5f;
            bSy += (ShoulderY - bSy) * d; bScale += (CurrentScale - bScale) * d;
            if (style != PlayStyle.UpperBody) bHy += (HipY - bHy) * d;
            if (AnklesSeen && !float.IsNaN(bAnkle)) bAnkle += (AnkleY - bAnkle) * d;
            if (Seen(PoseJoint.Nose) && !float.IsNaN(bNose)) bNose += (py[PoseJoint.Nose] - bNose) * d;
        }

        private void RegisterStep(double now) { lastStep = now; stepTimes[stepIndex] = now; stepIndex = (stepIndex + 1) % stepTimes.Length; }
        private void UpdateCadence(double now)
        {
            int recent = 0; foreach (double t in stepTimes) if (t > 0 && now - t < 3) recent++;
            StepsPerMinute = recent * 20f;
        }
        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
