using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Presentation
{
    // Picks the instruction for the nearest obstacle the runner must answer next.
    public static class ActionCuePresenter
    {
        public static string Find(RunSession session, ChunkSpawner map, PlayStyle style = PlayStyle.FullBody)
        {
            if (map == null || session.Paused) return null;
            float nearest = float.MaxValue; string cue = null;
            var chunks = map.Active;
            for (int index = 0; index < chunks.Count; index++)
            {
                var active = chunks[index];
                if (session.Runner.Riding)
                {
                    foreach (var gap in active.Chunk.RailGaps)
                    {
                        if (!gap.gameObject.activeInHierarchy) continue;
                        float d = gap.transform.position.z - session.Runner.Z;
                        if (gap.Passed || d < 0 || d > 14 || d >= nearest) continue;
                        nearest = d; cue = gap.RequiredLean < 0 ? "←  왼쪽으로" : "오른쪽으로  →";
                    }
                }
                else
                {
                    ConsiderStair(session, active.Chunk.Sequence, style, ref nearest, ref cue);
                    Consider(session, active.Chunk.Obstacles, style, ref nearest, ref cue);
                    Consider(session, active.Chunk.ExerciseObstacles, style, ref nearest, ref cue);
                    Consider(session, active.Chunk.DynamicObstacles, style, ref nearest, ref cue);
                }
            }
            return cue;
        }
        // A stair chunk ahead (or the runner still at its foot, before the first judged step) asks for alternating knees.
        private static void ConsiderStair(RunSession session, int sequence, PlayStyle style, ref float nearest, ref string cue)
        {
            if (!RouteSurface.IsStair(RouteSurface.TypeAt(sequence))) return;
            float start = sequence * RouteSurface.Length, z = session.Runner.Z;
            if (z >= start + StairJudge.From + 1f) return;
            float d = Mathf.Max(0f, start - z);
            if (d > Mathf.Max(9f, session.TargetSpeed * 1.35f) || d >= nearest) return;
            nearest = d; cue = StairCue(style);
        }
        public static string StairCue(PlayStyle style) => style == PlayStyle.Keyboard ? "W 연타!" : style == PlayStyle.UpperBody ? "팔 번갈아!" : "걷듯이 무릎!";
        public const string JumpCue = "↑  점프!", CrouchCue = "↓  숙이기!", LaneCue = "←  옆으로 피하기  →";
        public enum CueAction { Lane, Jump, Crouch }
        public static CueAction ActionOf(ObstacleController hazard) =>
            hazard.GetComponentInParent<ZooHazard>() != null ? CueAction.Jump : hazard.GetComponent<ClosingSlideGate>() != null ? CueAction.Crouch
            : hazard.Kind == ObstacleKind.Pipe ? CueAction.Crouch : hazard.Kind == ObstacleKind.Crate || hazard.Kind == ObstacleKind.Cart ? CueAction.Jump : CueAction.Lane;
        // Jump and crouch calls wait for their timing (ActionTiming); a side step can be called early.
        public static bool CueDue(RunSession session, PlayStyle style, CueAction action, float distance)
        {
            float speed = session.CurrentSpeed > 0 ? session.CurrentSpeed : session.TargetSpeed;
            if (action == CueAction.Jump) return ActionTiming.Due(distance, speed, ActionTiming.JumpCueSeconds(session.Config, style));
            if (action == CueAction.Crouch) return ActionTiming.Due(distance, speed, ActionTiming.CrouchCueSeconds(session.Config, style));
            return distance >= 0 && distance <= Mathf.Max(9f, session.TargetSpeed * 1.35f);
        }
        public static string CueText(CueAction action) => action == CueAction.Jump ? JumpCue : action == CueAction.Crouch ? CrouchCue : LaneCue;
        // The nearest obstacle in the runner's way decides the call; a jump or crouch not yet due shows nothing (not a farther obstacle's call).
        private static void Consider(RunSession session, System.Collections.Generic.IList<ObstacleController> hazards, PlayStyle style, ref float nearest, ref string cue)
        {
            if (hazards == null) return;
            float lookAhead = Mathf.Max(9f, session.TargetSpeed * 1.35f);
            for (int index = 0; index < hazards.Count; index++)
            {
                var hazard = hazards[index];
                float d = hazard.transform.position.z - session.Runner.Z;
                if (!hazard.gameObject.activeSelf || hazard.Passed || d < 0 || d > lookAhead || d >= nearest) continue;
                if (!hazard.FullExerciseRow)
                {
                    RouteSurface.Sample(hazard.transform.position.z, out float offset, out _);
                    if (Mathf.Abs(session.Runner.Lane * session.Config.LaneSpacing + offset - hazard.transform.position.x) > .95f) continue;
                }
                nearest = d;
                var action = ActionOf(hazard);
                cue = CueDue(session, style, action, d) ? CueText(action) : null;
            }
        }
    }
}
