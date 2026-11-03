using System;
using System.Collections.Generic;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Runs the tutorial lessons on the ordinary run: slows the runner in front of the lesson obstacle, shows a big cue,
    // resumes on the right action and completes the step when the obstacle is passed. A mistake shows "다시 해 볼까요?"
    // and restarts in front of the same step. The bootstrap ticks it before the session moves.
    public sealed class TutorialDirector : MonoBehaviour
    {
        // Slowing starts this far ahead; the runner creeps from the hold point and the action counts from the act distance.
        public const float SlowDistance = 14f, HoldDistance = 2.4f, ActDistance = 3.6f, CreepScale = .08f;
        public const float RetrySeconds = 1.6f, DoneSeconds = 2.5f;
        private RunSession session;
        private ChunkSpawner map;
        private Func<bool> poseReady, visible;
        private Action<float> restart;
        private Action finished;
        private HudSkin skin;
        private readonly List<ObstacleController> lesson = new List<ObstacleController>(3);
        private int seenMisses;
        private float retryAt = -1, doneAt = -1;
        private string cue;
        public TutorialScript Script { get; private set; }
        public bool Active => Script != null && session != null && session.IsTutorial;
        // Distance to the current lesson (obstacle or fork decision); infinity when none is in sight.
        public float LessonDistance { get; private set; } = float.PositiveInfinity;
        // The runner is held in front of the lesson until the awaited action.
        public bool Waiting { get; private set; }
        public bool Resumed { get; private set; }
        public bool Retrying => retryAt >= 0;
        public string Message { get; private set; }

        public void Configure(RunSession run, ChunkSpawner spawner, Func<bool> calibrated, Action<float> restartAt, Action complete, Func<bool> canDraw)
        {
            session = run; map = spawner; poseReady = calibrated; restart = restartAt; finished = complete; visible = canDraw;
        }
        public void Begin(PlayStyle style, bool forks)
        {
            Script = new TutorialScript(style, forks); seenMisses = session.TutorialMisses; retryAt = doneAt = -1;
            Resumed = Waiting = false; LessonDistance = float.PositiveInfinity; Message = null; cue = null;
        }
        public void End() { Script = null; Waiting = Resumed = false; retryAt = doneAt = -1; Message = cue = null; if (session != null) session.SpeedScale = 1; }

        public void Tick(float now)
        {
            if (!Active || session.Flow.State != GameFlowState.Running || session.Paused) return;
            var runner = session.Runner;
            if (doneAt >= 0) { session.SpeedScale = .5f; if (now - doneAt >= DoneSeconds) { var done = finished; End(); done?.Invoke(); } return; }
            if (retryAt >= 0)
            {
                session.SpeedScale = 0;
                if (now < retryAt) return;
                retryAt = -1; Resumed = Waiting = false; Message = null;
                restart?.Invoke(TutorialCourse.RestartZ(Script.Current));
                seenMisses = session.TutorialMisses; return;
            }
            if (session.TutorialMisses > seenMisses) { Miss(now); return; }
            switch (Script.Condition)
            {
                case TutorialPass.Calibrated:
                    session.SpeedScale = 0; Waiting = true; Message = TutorialScript.Instruction(Script.Current, Script.Style);
                    if (poseReady == null || poseReady()) Next(now);
                    return;
                case TutorialPass.ForkChosen:
                {
                    var plan = RouteSurface.ActivePlan;
                    LessonDistance = TutorialCourse.ForkDecisionZ - runner.Z;
                    if (plan == null || !plan.Extended) { Next(now); return; }
                    if (plan.ForkLocked(runner.Z)) { if (plan.ChoiceAt(RouteSurface.SequenceAt(runner.Z)) != 0) Next(now); else Miss(now); return; }
                    // The whole fork window counts: a lean held until the decision chooses the branch.
                    Approach(plan.ForkWindow(runner.Z) && TutorialScript.Performed(Script.Current, runner));
                    return;
                }
                case TutorialPass.ObstaclePassed when Script.Current == TutorialStep.Rail:
                {
                    // The broken rail judges itself (a wrong lean counts as a miss); the lean can be held from the slowdown on.
                    var gap = FindRail();
                    if (gap == null) { LessonDistance = float.PositiveInfinity; Waiting = false; session.SpeedScale = 1; Message = null; return; }
                    if (gap.Passed) { Next(now); return; }
                    LessonDistance = gap.transform.position.z - runner.Z;
                    Approach(LessonDistance <= SlowDistance && TutorialScript.Performed(Script.Current, runner));
                    return;
                }
                case TutorialPass.ObstaclePassed:
                {
                    FindLesson();
                    if (lesson.Count == 0) { LessonDistance = float.PositiveInfinity; Waiting = false; session.SpeedScale = 1; Message = null; return; }
                    bool passed = true; foreach (var hazard in lesson) passed &= hazard.Passed;
                    if (passed) { Next(now); return; }
                    LessonDistance = lesson[0].transform.position.z - runner.Z;
                    Approach(LessonDistance <= ActDistance && TutorialScript.Performed(Script.Current, runner));
                    return;
                }
            }
        }
        private void Approach(bool performed)
        {
            if (performed) Resumed = true;
            float d = LessonDistance;
            Waiting = !Resumed && d <= SlowDistance;
            session.SpeedScale = Resumed || d > SlowDistance ? 1 : d <= HoldDistance ? CreepScale : Mathf.Lerp(CreepScale, 1, Mathf.InverseLerp(HoldDistance, SlowDistance, d));
            Message = d <= SlowDistance ? TutorialScript.Instruction(Script.Current, Script.Style) : null;
            cue = Waiting ? ActionCuePresenter.Find(session, map) : null;
        }
        private void Next(float now)
        {
            Script.Pass(); Resumed = Waiting = false; LessonDistance = float.PositiveInfinity; session.SpeedScale = 1; cue = null;
            Message = Script.Finished ? TutorialScript.Instruction(TutorialStep.Done, Script.Style) : null;
            if (Script.Finished) doneAt = now;
        }
        private void Miss(float now)
        {
            seenMisses = session.TutorialMisses; Script.Fail(); retryAt = now + RetrySeconds;
            Resumed = Waiting = false; session.SpeedScale = 0; cue = null; Message = "다시 해 볼까요?";
        }
        // Active hazards of this step's lesson chunk (one lane block or one full row).
        private void FindLesson()
        {
            lesson.Clear();
            int sequence = TutorialCourse.ChunkOf(Script.Current);
            if (map == null || sequence < 0) return;
            foreach (var item in map.Active)
            {
                if (item.Chunk.Sequence != sequence || !item.Chunk.gameObject.activeSelf) continue;
                Collect(item.Chunk.Obstacles); Collect(item.Chunk.ExerciseObstacles); Collect(item.Chunk.DynamicObstacles);
            }
        }
        // The lesson's broken rail window on the tutorial cart chunk, once that chunk is spawned.
        private CartRailGap FindRail()
        {
            if (map == null) return null;
            foreach (var item in map.Active)
            {
                if (item.Chunk.Sequence != TutorialCourse.RailChunk || !item.Chunk.gameObject.activeSelf) continue;
                var gaps = item.Chunk.RailGaps;
                if (gaps != null && gaps.Length > TutorialCourse.RailGapIndex && gaps[TutorialCourse.RailGapIndex] != null
                    && gaps[TutorialCourse.RailGapIndex].gameObject.activeSelf) return gaps[TutorialCourse.RailGapIndex];
            }
            return null;
        }
        private void Collect(IList<ObstacleController> hazards)
        {
            if (hazards == null) return;
            foreach (var hazard in hazards) if (hazard != null && hazard.gameObject.activeSelf) lesson.Add(hazard);
        }

        private void OnGUI()
        {
            if (!Active || session.Flow.State != GameFlowState.Running || session.Paused || (visible != null && !visible())) return;
            if (Event.current.type != EventType.Repaint) return;
            skin ??= new HudSkin();
            var old = GUI.matrix; GUI.matrix = ViewportLayout.GuiMatrix; GUI.depth = -5;
            var step = Script.Current;
            string progress = (Mathf.Min(Script.Index + 1, Script.Steps.Count - 1)) + " / " + (Script.Steps.Count - 1);
            var header = new Rect(440, 92, 400, 40);
            skin.Card(header, Palette.A(Palette.Ground, .85f));
            GUI.color = Palette.Gold;
            GUI.Label(header, step == TutorialStep.Done ? "튜토리얼 완료" : "튜토리얼 " + progress + " · " + TutorialScript.Title(step), skin.Centered(20, FontStyle.Bold));
            // Mistakes and the finish fill the banner; while held, the obstacle cue is big and the instruction sits below.
            string big = Retrying || Script.Finished ? Message : Waiting ? cue ?? TutorialScript.Title(step) : null;
            if (big != null)
            {
                var banner = new Rect(240, 196, 800, 110);
                skin.Card(banner, Palette.A(Palette.Ground, .88f));
                GUI.color = Retrying ? new Color(1, .7f, .3f) : Palette.Gold;
                GUI.Label(banner, big, skin.Centered(58, FontStyle.Bold));
            }
            if (!Retrying && !Script.Finished && !string.IsNullOrEmpty(Message))
            {
                var line = new Rect(240, 312, 800, 46);
                skin.Card(line, Palette.A(Palette.Ground, .82f));
                GUI.color = Palette.Cream; GUI.Label(line, Message, skin.Centered(22, FontStyle.Bold));
            }
            GUI.color = Color.white; GUI.matrix = old;
        }
        private void OnDestroy() { skin?.Dispose(); }
    }
}
