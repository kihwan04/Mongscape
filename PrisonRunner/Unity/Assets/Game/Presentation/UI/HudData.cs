using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // The visible calibration before a camera run starts its countdown (and before the tutorial's first lesson).
    public enum RunCalibrationState { None, Measuring, Done }

    // What the screens need from the running game; implemented by the bootstrap.
    public interface IHudData
    {
        // Who plays the current or next run: the saved profile, or a per-run name in developer mode.
        PlayerProfile Player { get; }
        PlayerProfile SavedProfile { get; }
        bool DeveloperMode { get; }
        Texture CameraTexture { get; }
        // A webcam is plugged in. Without one no screen shows a camera view.
        bool CameraDetected { get; }
        // Full-body and upper-body play can be chosen: a camera is connected, or developer mode is on.
        bool BodyStylesAllowed { get; }
        PoseFrame PoseFrame { get; }
        PoseStatus PoseStatus { get; }
        string PoseMessage { get; }
        string PoseDiagnostics { get; }
        // What the classifier sees right now, so players can check recognition before and during a run.
        MotionReading Motion { get; }
        float SecondsSinceJump { get; }
        float SecondsSinceKneeStep { get; }
        float CalibrationProgress { get; }
        // Full body calibration waits for both knees to be in view.
        bool PoseKneesMissing { get; }
        // Calibration restarted just now because the player moved ("움직이지 마세요").
        bool PoseCalibrationMoving { get; }
        // How the player stands in the camera picture; Ok when everything from the head to the ankles shows.
        FramingIssue PoseFraming { get; }
        float StepsPerMinute { get; }
        CadenceStage Cadence { get; }
        // The run is held on the calibration screen (Measuring), or shows "보정 완료" for a second (Done).
        RunCalibrationState RunCalibration { get; }
        // A tutorial run: the calibration screen explains itself once.
        bool TutorialRun { get; }
        // Developer mode joint recording (F6) is on.
        bool PoseRecording { get; }
        bool BodyActive { get; }
        PlayerWallet Wallet { get; }
        Leaderboard Board { get; }
        PlayerStats Stats { get; }
        // The run that just finished: its entry, rank among all runs and today, whether it beat the best score for its play style, and that best.
        RankingEntry LastEntry { get; }
        // A guest's last run (outside developer mode) can still be renamed from "게스트" on this PC's ranking.
        bool CanNameLastRun { get; }
        bool NameLastRun(string name);
        int LastRank { get; }
        int LastTodayRank { get; }
        // A member's run is compared with the member's best for its play style from /stats, taken before this run was saved;
        // LastBestScore is that earlier best, -1 when there is none to show (guests, or the server did not answer).
        bool LastNewBest { get; }
        int LastBestScore { get; }
        // The member's totals from /stats after this run was saved: null while waiting (or for a guest); LastStatsFailed when it could not be read.
        StatsReply LastStats { get; }
        bool LastStatsFailed { get; }
        // The member's best score for the current play style, shown on the HUD and pause screen; -1 for a guest.
        int BestScore { get; }
        // The member's last run could not be saved on the account server (kept in memory and retried with the next run).
        bool RunSaveFailed { get; }
        float StartBanner { get; }
        float CaughtFlash { get; }
        void PreviewStyle(PlayStyle style);
        // done(ok, reason): members buy on the account server; guests and developer mode cannot buy.
        void Unlock(ItemKind kind, System.Action<bool, string> done);
        void SaveProfile(PlayerProfile profile);
        void Recalibrate();
        // "달리기 속도 조절" (setup screen): on by default, remembered on this PC.
        bool CadenceSpeed { get; }
        void SetCadenceSpeed(bool on);
        // The running ranking strip (loaded at the start of a run; not in the tutorial), whether a hazard is close
        // (the strip stays small and holds its overtake effect), and the cadence pace pressure (red screen edges).
        RankRace Race { get; }
        bool HazardNear { get; }
        float PacePressure { get; }
        // The coach subtitle showing now, or "".
        string CoachText { get; }
        // The course done least this week, marked "추천" on the fork preview.
        ExerciseKind Recommended { get; }
        // Weekly goal (moderate minutes, of 150) and the daily streak of the current player; today's challenge state.
        double WeekMinutes { get; }
        int Streak { get; }
        // Streak coins the last run earned (0 when none), and whether the last run was today's challenge (with its day rank).
        int LastStreakCoins { get; }
        bool ChallengeRun { get; }
        bool DailyChallenge { get; }
        void SetDailyChallenge(bool on);
        // The tutorial's joint comparison panel is open (bottom middle); the HUD notice moves aside then (QA 2026-11-25 M7).
        bool JointCompareOpen => false;
        // A banana is just ahead and this player has not been warned yet this session.
        bool BananaWarning { get; }
    }

    // Draws the mirrored webcam image with the detected skeleton; returns the video rect for overlays.
    internal static class PosePreview
    {
        private static readonly Color Bone = new Color(.3f, 1f, .55f), Joint = new Color(1f, .85f, .2f);
        public const string KneesHint = "무릎까지 보이게 뒤로 물러나 주세요";
        // A webcam is plugged in and already delivering frames.
        public static bool Live(IHudData data)
        {
            var texture = data?.CameraTexture;
            return data != null && data.CameraDetected && texture != null && texture.width > 16 && data.PoseStatus != PoseStatus.NoCamera;
        }
        // The small mirrored webcam corner of the menu screens: only while a camera is live, nothing otherwise.
        public static void DrawCorner(HudSkin skin, Rect area, IHudData data)
        {
            if (!Live(data) || Event.current.type != EventType.Repaint) return;
            skin.Panel(area, .95f);
            var inner = new Rect(area.x + 4, area.y + 4, area.width - 8, area.height - 8);
            // Fill the frame: crop the video's centre to the frame's shape instead of leaving side bars; mirrored like a mirror.
            var texture = data.CameraTexture;
            float video = texture.width / (float)texture.height, frame = inner.width / inner.height;
            var uv = video > frame ? new Rect(.5f + frame / video * .5f, 0, -frame / video, 1) : new Rect(1, .5f - video / frame * .5f, -1, video / frame);
            GUI.color = Color.white; GUI.DrawTextureWithTexCoords(inner, texture, uv);
        }
        // Without a webcam the video area becomes a short notice instead of an empty frame; returns Rect.zero then.
        public static Rect Draw(HudSkin skin, Rect area, IHudData data, bool showStatus, bool drawSkeleton = true)
        {
            if (!data.CameraDetected) { NoCamera(skin, area); return Rect.zero; }
            skin.Panel(area, .9f);
            var inner = new Rect(area.x + 6, area.y + 6, area.width - 12, area.height - 12);
            var texture = data.CameraTexture;
            var frame = data.PoseFrame;
            Rect view = inner;
            if (texture != null && texture.width > 16)
            {
                float aspect = texture.width / (float)texture.height;
                view = inner.width / inner.height > aspect ? new Rect(inner.center.x - inner.height * aspect * .5f, inner.y, inner.height * aspect, inner.height)
                    : new Rect(inner.x, inner.center.y - inner.width / aspect * .5f, inner.width, inner.width / aspect);
                GUI.color = Color.white;
                // Mirror horizontally so the player sees themselves like a mirror.
                GUI.DrawTextureWithTexCoords(view, texture, new Rect(1, 0, -1, 1));
            }
            if (drawSkeleton && frame != null && frame.HasPose)
            {
                for (int b = 0; b < PoseJoint.Bones.GetLength(0); b++)
                {
                    var p = frame.Points[PoseJoint.Bones[b, 0]]; var q = frame.Points[PoseJoint.Bones[b, 1]];
                    if (p.Visibility < .5f || q.Visibility < .5f) continue;
                    GUI.color = Bone;
                    for (int s = 0; s <= 10; s++)
                    {
                        float t = s / 10f;
                        Dot(view, Mathf.Lerp(p.X, q.X, t), Mathf.Lerp(p.Y, q.Y, t), 4);
                    }
                }
                GUI.color = Joint;
                for (int i = 11; i < 29; i++) if (frame.Points[i].Visibility >= .5f) Dot(view, frame.Points[i].X, frame.Points[i].Y, 8);
                if (frame.Points[0].Visibility >= .5f) Dot(view, frame.Points[0].X, frame.Points[0].Y, 10);
            }
            GUI.color = Color.white;
            if (!showStatus) return view;
            var status = data.PoseStatus;
            if ((status == PoseStatus.NoCamera || status == PoseStatus.NoModel) && !string.IsNullOrEmpty(data.PoseMessage))
                GUI.Label(new Rect(area.x + 16, area.y + 16, area.width - 32, area.height - 70), data.PoseMessage, skin.Style(16, FontStyle.Normal));
            string text = status == PoseStatus.Calibrating && data.PoseKneesMissing ? KneesHint : PoseStatusText.Korean(status);
            if (status == PoseStatus.Tracking && data.StepsPerMinute > 0) text += " (분당 " + Mathf.RoundToInt(data.StepsPerMinute) + "걸음 · " + Domain.Cadence.Korean(data.Cadence) + ")";
            // Camera setup: a body cut off at the picture's edge (or standing too far or to one side) gets a hint instead.
            string hint = FramingHint(data, status);
            if (hint != null) text = hint;
            var label = new Rect(area.x, area.yMax - 34, area.width, 30);
            skin.Card(label, new Color(0, 0, 0, .6f));
            if (hint != null) GUI.color = Palette.Warn;
            GUI.Label(label, text, skin.Centered(18, FontStyle.Bold));
            GUI.color = Color.white;
            if (status == PoseStatus.Tracking && data.Motion != null) DrawMotion(skin, area, data);
            // Diagnostics for tuning: tracking mode, shoulder confidence, detector score, inference rate.
            if (!string.IsNullOrEmpty(data.PoseDiagnostics) && area.width > 300)
            {
                GUI.color = new Color(1, 1, 1, .8f); GUI.Label(new Rect(area.x + 10, area.yMax - 64, area.width - 20, 22), data.PoseDiagnostics, skin.Style(13, FontStyle.Normal)); GUI.color = Color.white;
            }
            if (status == PoseStatus.Calibrating)
            {
                GUI.color = new Color(1, 1, 1, .25f); GUI.DrawTexture(new Rect(area.x + 20, area.yMax - 44, area.width - 40, 6), Texture2D.whiteTexture);
                GUI.color = new Color(.3f, 1f, .55f); GUI.DrawTexture(new Rect(area.x + 20, area.yMax - 44, (area.width - 40) * data.CalibrationProgress, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            return view;
        }
        // The camera setup hint for this frame, or null (also null without a body style, a camera or a model).
        public static string FramingHint(IHudData data, PoseStatus status)
        {
            if (data?.Player == null || (status != PoseStatus.Tracking && status != PoseStatus.Calibrating && status != PoseStatus.Searching)) return null;
            var issue = data.PoseFraming;
            if (status == PoseStatus.Calibrating && data.PoseKneesMissing) return KneesHint;
            return issue == FramingIssue.Ok ? null : CameraFraming.Hint(issue, data.Player.Style);
        }
        private static void NoCamera(HudSkin skin, Rect area)
        {
            skin.Panel(area, .9f);
            GUI.color = Palette.Cream; GUI.Label(new Rect(area.x, area.center.y - 34, area.width, 36), "카메라가 감지되지 않았어요", skin.Centered(24, FontStyle.Bold));
            GUI.color = Palette.Sand; GUI.Label(new Rect(area.x + 16, area.center.y + 6, area.width - 32, 28), "웹캠을 연결하면 자동으로 켜집니다", skin.Centered(17, FontStyle.Normal));
            GUI.color = Color.white;
        }
        // Live feedback: which lane zone the body is in and which action is being recognised.
        private static void DrawMotion(HudSkin skin, Rect area, IHudData data)
        {
            var motion = data.Motion;
            var lane = new Rect(area.x + 8, area.y + 8, 120, 34);
            skin.Card(lane, new Color(0, 0, 0, .6f));
            GUI.Label(lane, motion.Lane < 0 ? "◀ 왼쪽" : motion.Lane > 0 ? "오른쪽 ▶" : "가운데", skin.Centered(18, FontStyle.Bold));
            string action = data.SecondsSinceJump < .6f ? "점프!" : motion.Crouch ? "숙이기" : data.SecondsSinceKneeStep < .7f ? "무릎·팔 당기기" : null;
            if (action == null) return;
            var badge = new Rect(area.xMax - 158, area.y + 8, 150, 34);
            skin.Card(badge, Palette.A(Palette.Gold, .9f));
            GUI.color = Palette.Outline; GUI.Label(badge, action, skin.Centered(19, FontStyle.Bold)); GUI.color = Color.white;
        }
        private static void Dot(Rect view, float x, float y, float size)
        {
            if (x < 0 || x > 1 || y < 0 || y > 1) return;
            GUI.DrawTexture(new Rect(view.x + x * view.width - size * .5f, view.y + y * view.height - size * .5f, size, size), Texture2D.whiteTexture);
        }
    }
}
