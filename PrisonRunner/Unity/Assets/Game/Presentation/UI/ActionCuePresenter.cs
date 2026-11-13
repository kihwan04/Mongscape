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
                    Consider(session, active.Chunk.Obstacles, ref nearest, ref cue);
                    Consider(session, active.Chunk.ExerciseObstacles, ref nearest, ref cue);
                    Consider(session, active.Chunk.DynamicObstacles, ref nearest, ref cue);
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
        public static string StairCue(PlayStyle style) => style == PlayStyle.Keyboard ? "W 연타!" : style == PlayStyle.UpperBody ? "팔 번갈아!" : "무릎 번갈아!";
        private static void Consider(RunSession session, System.Collections.Generic.IList<ObstacleController> hazards, ref float nearest, ref string cue)
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
                cue = hazard.GetComponentInParent<ZooHazard>() != null ? "↑  점프!" : hazard.GetComponent<ClosingSlideGate>() != null ? "↓  숙이기!" : hazard.Kind == ObstacleKind.Pipe ? "↓  숙이기!" :  hazard.Kind == ObstacleKind.Crate || hazard.Kind == ObstacleKind.Cart ? "↑  점프!" : "←  옆으로 피하기  →";
            }
        }
    }
}
