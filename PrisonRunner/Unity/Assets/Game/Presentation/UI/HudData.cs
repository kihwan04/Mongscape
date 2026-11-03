using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // What the screens need from the running game; implemented by the bootstrap.
    public interface IHudData
    {
        // Who plays the current or next run: the saved profile, or a per-run name in developer mode.
        PlayerProfile Player { get; }
        PlayerProfile SavedProfile { get; }
        bool DeveloperMode { get; }
        Texture CameraTexture { get; }
        PoseFrame PoseFrame { get; }
        PoseStatus PoseStatus { get; }
        string PoseMessage { get; }
        string PoseDiagnostics { get; }
        // What the classifier sees right now, so players can check recognition before and during a run.
        MotionReading Motion { get; }
        float SecondsSinceJump { get; }
        float CalibrationProgress { get; }
        float StepsPerMinute { get; }
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
    }

    // Draws the mirrored webcam image with the detected skeleton; returns the video rect for overlays.
    internal static class PosePreview
    {
        private static readonly Color Bone = new Color(.3f, 1f, .55f), Joint = new Color(1f, .85f, .2f);
        public static Rect Draw(HudSkin skin, Rect area, IHudData data, bool showStatus, bool drawSkeleton = true)
        {
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
            string text = PoseStatusText.Korean(status);
            if (status == PoseStatus.Tracking && data.StepsPerMinute > 0) text += " (분당 " + Mathf.RoundToInt(data.StepsPerMinute) + "걸음)";
            var label = new Rect(area.x, area.yMax - 34, area.width, 30);
            skin.Card(label, new Color(0, 0, 0, .6f));
            GUI.Label(label, text, skin.Centered(18, FontStyle.Bold));
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
        // Live feedback: which lane zone the body is in and which action is being recognised.
        private static void DrawMotion(HudSkin skin, Rect area, IHudData data)
        {
            var motion = data.Motion;
            var lane = new Rect(area.x + 8, area.y + 8, 120, 34);
            skin.Card(lane, new Color(0, 0, 0, .6f));
            GUI.Label(lane, motion.Lane < 0 ? "◀ 왼쪽" : motion.Lane > 0 ? "오른쪽 ▶" : "가운데", skin.Centered(18, FontStyle.Bold));
            string action = data.SecondsSinceJump < .6f ? "점프!" : motion.Crouch ? "숙이기" : motion.HighKnee ? "무릎·팔 들기" : null;
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
