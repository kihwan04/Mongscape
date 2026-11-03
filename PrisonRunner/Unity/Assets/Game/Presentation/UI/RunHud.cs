using System;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Corner counters and one contextual instruction. Detailed metrics live in pause/results.
    internal sealed class RunHud
    {
        private readonly HudSkin skin;
        private readonly ProgressHud progress;
        public readonly RunHudModel Model = new RunHudModel();
        private static readonly Rect NoticeArea = new Rect(400, 588, 480, 48);
        // Bottom-right webcam: small while tracking, larger with status once recognition is lost. Both stay right of the notice and below the counters.
        private static readonly Rect CameraArea = new Rect(1056, 514, 200, 150), RecoveryCameraArea = new Rect(1020, 490, 236, 174);
        public RunHud(HudSkin hudSkin, ProgressHud progressHud) { skin = hudSkin; progress = progressHud; }
        public void Draw(RunSession session, float fade, IHudData data, Action pause)
        {
            GUI.color = new Color(1, 1, 1, fade);
            // Only the pause control handles input events; all decoration draws on Repaint.
            if (skin.FlatButton(new Rect(1190, 24, 64, 56), "Ⅱ", false)) pause?.Invoke();
            if (Event.current.type != EventType.Repaint) { GUI.color = Color.white; return; }
            progress.Draw(session, data?.BestScore ?? -1);
            skin.Card(new Rect(24, 24, Model.CoinBoost ? 176 : 128, 48), Palette.A(Palette.Ground, .82f));
            GUI.DrawTexture(new Rect(35, 33, 29, 29), skin.CoinIcon);
            GUI.Label(new Rect(75, 30, 75, 36), Model.Coins, skin.Style(26, FontStyle.Bold));
            if (Model.CoinBoost)
            {
                GUI.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, fade);
                GUI.Label(new Rect(150, 30, 46, 36), "×2", skin.Style(26, FontStyle.Bold));
                GUI.color = new Color(1, 1, 1, fade);
            }
            if (Model.ShowWorkout)
            {
                skin.Card(new Rect(870, 120, 300, 104), Palette.A(Palette.Ground, .82f));
                GUI.Label(new Rect(886, 126, 268, 27), Model.GoalLabel, skin.Style(18, FontStyle.Normal));
                GUI.Label(new Rect(886, 153, 268, 32), Model.BurnLabel, skin.Style(23, FontStyle.Bold));
                GUI.Label(new Rect(886, 186, 268, 22), Model.WorkoutHint, skin.Style(14, FontStyle.Normal));
                GUI.color = session.Workout.GoalReached ? new Color(.4f, 1, .6f) : Palette.Gold;
                GUI.DrawTexture(new Rect(886, 214, 268 * Model.GoalProgress, 3), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            int row = 0;
            for (int i = 0; i < Model.ItemLabels.Length; i++)
            {
                if (Model.ItemLabels[i] == null) continue;
                var rect = new Rect(24, 86 + row++ * 34, 168, 29);
                skin.Card(rect, Palette.A(Palette.Ground, .82f));
                GUI.Label(rect, Model.ItemLabels[i], skin.Centered(16, FontStyle.Bold));
            }
            if (session.Flow.State == GameFlowState.Running)
            {
                if (Model.ShowSiren) DrawSiren();
                if (Model.ShowCamera && data?.CameraTexture != null)
                    PosePreview.Draw(skin, Model.ShowPoseRecovery ? RecoveryCameraArea : CameraArea, data, Model.ShowPoseRecovery);
                if (Model.ShowFork && Model.NoticeKind!=HudNoticeKind.Tracking && Model.NoticeKind!=HudNoticeKind.Obstacle) DrawFork();
                else if (data == null || data.StartBanner <= 0) DrawNotice();
            }
            if (data != null) DrawBanners(data, session.Flow.State);
            if (session.Flow.State == GameFlowState.Caught)
            {
                skin.Card(NoticeArea, Palette.A(Palette.Ground, .9f));
                GUI.Label(NoticeArea, session.Reason == CaptureReason.BrokenRail ? "철로에서 추락!" : "경찰에게 잡혔어요!", skin.Centered(25, FontStyle.Bold));
            }
            GUI.color = Color.white;
        }
        private static readonly string[] ForkArrows={"↖","↑","↗"};
        private void DrawSiren()
        {
            bool redOn = Mathf.Repeat(Time.unscaledTime, .56f) < .28f;
            var red = new Color(1, .12f, .16f);
            var blue = new Color(.15f, .48f, 1);
            skin.Card(new Rect(577, 24, 126, 68), Palette.A(Palette.Ground, .9f));
            skin.Card(new Rect(589, 32, 44, 29), redOn ? red : red * .3f);
            skin.Card(new Rect(647, 32, 44, 29), redOn ? blue * .3f : blue);
            GUI.color = new Color(1, 1, 1, .7f);
            GUI.DrawTexture(new Rect(596, 37, 5, 13), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(654, 37, 5, 13), Texture2D.whiteTexture);
            GUI.color = new Color(.65f, .69f, .74f);
            GUI.DrawTexture(new Rect(587, 60, 106, 5), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(579, 69, 122, 19), "경찰 접근", skin.Centered(14, FontStyle.Bold));
        }
        private void DrawFork()
        {
            if(!Model.ForkCommitted)
            {
                for(int lane=-1;lane<=1;lane++)
                {
                    var r=new Rect(550+(lane+1)*62,574,56,48);bool chosen=lane==Model.ForkLane;
                    skin.Card(r,chosen?Palette.Gold:Palette.A(Palette.Ground,.85f));
                    GUI.color=chosen?Palette.Outline:Palette.Cream;
                    GUI.Label(r,ForkArrows[lane+1],skin.Centered(30,FontStyle.Bold));
                }
                GUI.color=Palette.Gold;GUI.DrawTexture(new Rect(550,629,180*Model.ForkProgress,3),Texture2D.whiteTexture);
            }
            GUI.color=Color.white;
            skin.Card(new Rect(540,640,204,30),Palette.A(Palette.Ground,.9f));
            GUI.Label(new Rect(540,640,204,30),Model.ForkCaption,skin.Centered(17,FontStyle.Bold));
        }
        private void DrawNotice()
        {
            if (Model.Notice == null) return;
            skin.Card(NoticeArea, Palette.A(Palette.Ground, .9f));
            GUI.color = Model.NoticeKind == HudNoticeKind.Tracking || Model.NoticeKind == HudNoticeKind.Police ? Palette.Warn : Palette.Cream;
            GUI.Label(NoticeArea, Model.Notice, skin.Centered(23, FontStyle.Bold));
            GUI.color = Color.white;
        }
        private void DrawBanners(IHudData data, GameFlowState state)
        {
            if (data.StartBanner > 0 && state == GameFlowState.Running)
            {
                float opacity = Mathf.Clamp01(data.StartBanner * 2);
                bool calibrating = data.PoseStatus == PoseStatus.Calibrating || data.PoseStatus == PoseStatus.Searching;
                GUI.color = new Color(1, .82f, .3f, opacity);
                GUI.Label(new Rect(340, 265, 600, 90), calibrating ? "바르게 서 주세요" : data.StartBanner > 1 ? "준비!" : "출발!", skin.Centered(48, FontStyle.Bold));
            }
            if (data.CaughtFlash > 0)
            {
                GUI.color = new Color(.9f, .05f, .05f, data.CaughtFlash * .45f);
                GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
