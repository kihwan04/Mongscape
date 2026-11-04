using Muhanok.Domain;

namespace Muhanok.Presentation.Map
{
    // Tutorial runs keep the chunk visuals but replace the random layout: every hazard, item, coin and broken rail
    // is removed and only the lesson obstacle (or the lesson's broken rail) listed by TutorialCourse is placed. Ordinary runs pass straight through.
    public sealed class TutorialLayout : ChunkSpawner.IChunkDecorator
    {
        private readonly ChunkSpawner.IChunkDecorator inner;
        private readonly RunSession session;
        public TutorialLayout(ChunkSpawner.IChunkDecorator content, RunSession run) { inner = content; session = run; }
        public void Decorate(MuhanokMapChunk chunk, int sequence, int type, System.Random random)
        {
            inner?.Decorate(chunk, sequence, type, random);
            if (!session.IsTutorial) return;
            Clear(chunk);
            var hazard = TutorialCourse.HazardAt(sequence);
            if (hazard == TutorialHazard.LaneBlock && inner is RuntimeContent content)
                content.PlaceLaneBlock(chunk, TutorialCourse.BlockedLane, chunk.Entry.position.z + TutorialCourse.LaneBlockZ);
            else if (hazard == TutorialHazard.JumpRow) Row(chunk.ExerciseObstacles, 0);
            else if (hazard == TutorialHazard.CrouchRow) Row(chunk.ExerciseObstacles, 1);
            else if (hazard == TutorialHazard.StairRow) Row(chunk.Obstacles, 0);
            else if (hazard == TutorialHazard.RailGap && chunk.RailGaps != null && chunk.RailGaps.Length > TutorialCourse.RailGapIndex
                && chunk.RailGaps[TutorialCourse.RailGapIndex] != null)
                chunk.RailGaps[TutorialCourse.RailGapIndex].Configure(TutorialCourse.RailMissingSide);
        }
        public void Release(MuhanokMapChunk chunk) => inner?.Release(chunk);

        private static void Row(ObstacleController[] row, int index)
        {
            if (row == null) return;
            for (int lane = 0; lane < 3 && index * 3 + lane < row.Length; lane++)
            {
                var hazard = row[index * 3 + lane]; if (hazard == null) continue;
                hazard.ResetPass(); hazard.FullExerciseRow = true; hazard.gameObject.SetActive(true);
            }
        }
        // Pooled pieces stay listed by the content decorator, which returns them on release; the authored ones are reset by Prepare.
        private static void Clear(MuhanokMapChunk chunk)
        {
            Hide(chunk.Obstacles); Hide(chunk.ExerciseObstacles);
            foreach (var hazard in chunk.DynamicObstacles) if (hazard != null && hazard.GetComponentInParent<ZooHazard>() == null) hazard.gameObject.SetActive(false);
            foreach (var animal in chunk.Animated) if (animal is UnityEngine.Component part) part.gameObject.SetActive(false);
            foreach (var item in chunk.DynamicFloorItems) if (item != null) item.gameObject.SetActive(false);
            foreach (var pickup in chunk.Pickups) if (pickup != null) pickup.gameObject.SetActive(false);
            if (chunk.FloorItems != null) foreach (var item in chunk.FloorItems) if (item != null) item.gameObject.SetActive(false);
            if (chunk.Coins != null) foreach (var coin in chunk.Coins) if (coin != null) coin.gameObject.SetActive(false);
            // Intact rails: both shown and nothing to judge (the seeded layout may have broken one).
            if (chunk.RailGaps != null) foreach (var gap in chunk.RailGaps) if (gap != null) gap.Configure(0);
            chunk.DynamicObstacles.Clear(); chunk.DynamicFloorItems.Clear(); chunk.Pickups.Clear(); chunk.Animated.Clear();
        }
        private static void Hide(ObstacleController[] hazards) { if (hazards != null) foreach (var hazard in hazards) if (hazard != null) hazard.gameObject.SetActive(false); }
    }
}
