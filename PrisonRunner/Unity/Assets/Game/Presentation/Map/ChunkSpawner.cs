using System.Collections.Generic;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    public sealed class ChunkPool
    {
        private readonly Queue<MuhanokMapChunk>[] pools;
        private readonly MuhanokMapChunk[] prefabs;
        private readonly Transform parent;
        private readonly RenderBudget budget;
        public readonly Dictionary<MuhanokMapChunk, ChunkRenderBudget> Budgets = new Dictionary<MuhanokMapChunk, ChunkRenderBudget>();
        public int CreatedCount { get; private set; }
        public ChunkPool(MuhanokMapChunk[] chunkPrefabs, Transform root, RenderBudget renderBudget = null)
        {
            prefabs = chunkPrefabs; parent = root; budget = renderBudget;
            pools = new Queue<MuhanokMapChunk>[prefabs.Length];
            for (int i = 0; i < pools.Length; i++)
            {
                pools[i] = new Queue<MuhanokMapChunk>(3);
                // Interval laps repeat carts, corridors and the zoo wall; keep three of those warm.
                int warm = i == 0 || i == 12 ? 7 : i == 1 || i == 2 || i == 4 || i == 5 ? 4 : i == RouteSurface.MineStair || i == RouteSurface.ZooStair ? 2 : 1;
                for (int j = 0; j < warm; j++) pools[i].Enqueue(Create(i));
            }
        }
        private MuhanokMapChunk Create(int index)
        {
            var chunk = Object.Instantiate(prefabs[index], parent);
            chunk.gameObject.SetActive(false); CreatedCount++;
            SignFont.Apply(chunk.gameObject);
            if (budget != null) Budgets[chunk] = new ChunkRenderBudget(chunk, budget);
            return chunk;
        }
        public MuhanokMapChunk Rent(int index)
        {
            if (pools[index].Count == 0)
            {
                Debug.LogWarning("Chunk pool grew for type " + index + "; consider a larger prewarm.");
                return Create(index);
            }
            return pools[index].Dequeue();
        }
        public void Return(int index, MuhanokMapChunk chunk) { chunk.gameObject.SetActive(false); pools[index].Enqueue(chunk); }
    }
    public sealed class ChunkSpawner
    {
        public readonly struct ActiveChunk
        {
            public readonly int Type; public readonly MuhanokMapChunk Chunk;
            public ActiveChunk(int type, MuhanokMapChunk chunk) { Type = type; Chunk = chunk; }
        }
        // Optional runtime decoration (items, zoo animals) applied after the authored layout.
        public interface IChunkDecorator
        {
            void Decorate(MuhanokMapChunk chunk, int sequence, int type, System.Random random);
            void Release(MuhanokMapChunk chunk);
        }
        private readonly List<ActiveChunk> active = new List<ActiveChunk>(8);
        private readonly ChunkPool pool;
        private readonly int types;
        private readonly RunSession session;
        private System.Random random;
        private int sequence, seed = 723;
        private readonly RenderBudget budget;
        private float runnerZ;
        private float nextBudgetUpdate;
        // Graphics quality scales the detail/light distances (1 = full).
        public float DetailScale { get; set; } = 1f;
        private Vector3 nextPosition;
        public IChunkDecorator Decorator { get; set; }
        public IReadOnlyList<ActiveChunk> Active => active;
        public int Spawned => sequence;
        public int CreatedCount => pool.CreatedCount;
        public ChunkSpawner(MuhanokMapChunk[] prefabs, Transform root, RunSession run, RenderBudget renderBudget = null)
        {
            types = prefabs.Length; session = run; budget = renderBudget; pool = new ChunkPool(prefabs, root, renderBudget); Reset();
        }
        // A new seed gives a different obstacle layout; the authored seed stays the default.
        public void Reset(int layoutSeed)
        {
            seed = layoutSeed; Reset();
        }
        public void Reset()
        {
            for (int i = 0; i < active.Count; i++) Recycle(active[i]);
            active.Clear(); random = new System.Random(seed); nextPosition = Vector3.zero; sequence = 0; nextBudgetUpdate = 0; Tick(2f);
        }
        private void Recycle(ActiveChunk item)
        {
            Decorator?.Release(item.Chunk);
            pool.Return(item.Type, item.Chunk);
        }
        public void Tick(float z)
        {
            var plan = RouteSurface.ActivePlan;
            bool wasLocked = plan != null && plan.ForkLocked(z);
            plan?.UpdateChoice(z, Mathf.Clamp(Mathf.RoundToInt(session.Runner.X/session.Config.LaneSpacing),-1,1));
            if (!wasLocked && plan != null && plan.ForkLocked(z))
            {
                foreach(var item in active)
                {
                    item.Chunk.PlaceOnRoute(item.Chunk.Sequence);item.Chunk.UpdateCartRoute();
                    item.Chunk.ForkView?.RefreshSelection(item.Chunk);
                }
                if(active.Count>0)nextPosition=active[active.Count-1].Chunk.Exit.position;
            }
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Chunk.Exit.position.z < z - 30f) { Recycle(active[i]); active.RemoveAt(i); }
            runnerZ = z;
            float horizon=z+(plan!=null&&plan.Extended?96f:budget!=null?budget.LookAhead:96f);
            horizon=Mathf.Min(horizon,plan?.PrefetchLimit(z)??float.MaxValue);
            while (nextPosition.z < horizon)
            {
                int type = RouteSurface.TypeAt(sequence);
                var chunk = pool.Rent(type);
                chunk.PlaceOnRoute(sequence);
                // Odds for where this chunk starts, not where the runner is: chunks are spawned up to 96 m ahead.
                chunk.Prepare(sequence, session.Difficulty.Odds(sequence * RouteSurface.Length, session.Config), random);
                chunk.gameObject.SetActive(true); active.Add(new ActiveChunk(type, chunk));
                Decorator?.Decorate(chunk, sequence, type, random);
                nextPosition = chunk.Exit.position; sequence++;
            }
            if (budget != null && Time.unscaledTime >= nextBudgetUpdate)
            {
                nextBudgetUpdate = Time.unscaledTime + .1f;
                foreach (var item in active)
                    if (pool.Budgets.TryGetValue(item.Chunk, out var limits)) limits.Apply(item.Chunk.Entry.position.z, runnerZ, budget, DetailScale);
            }
        }
        public void Evaluate(float previousZ)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var obstacles = active[i].Chunk.Obstacles;
                for (int j = 0; j < obstacles.Length && session.Flow.State == GameFlowState.Running; j++) obstacles[j].Evaluate(session, previousZ);
                var exercises=active[i].Chunk.ExerciseObstacles;
                if(exercises!=null)foreach(var exercise in exercises)exercise.Evaluate(session,previousZ);
                foreach(var hazard in active[i].Chunk.DynamicObstacles)hazard.Evaluate(session,previousZ);
                foreach(var pickup in active[i].Chunk.Pickups)pickup.Evaluate(session,previousZ);
                var coins=active[i].Chunk.Coins;
                if(coins!=null) for(int j=0;j<coins.Length;j++) coins[j].Evaluate(session,previousZ);
                var items=active[i].Chunk.FloorItems;
                if(items!=null)foreach(var item in items)item.Evaluate(session,previousZ);
                foreach(var item in active[i].Chunk.DynamicFloorItems)item.Evaluate(session,previousZ);
                var gaps=active[i].Chunk.RailGaps;
                if(gaps!=null)foreach(var gap in gaps)gap.Evaluate(session,previousZ);
            }
        }
        public void AnimateGates(float runnerZ)
        {
            var plan=RouteSurface.ActivePlan;
            foreach(var item in active)
            {
                item.Chunk.AnimateGates(runnerZ);
                // Cached component on the pooled chunk; no hierarchy search in the per-frame path.
                if(item.Chunk.ForkView!=null && item.Chunk.ForkView.gameObject.activeSelf)
                {
                    bool locked=plan?.ForkLocked(item.Chunk.Sequence*24)??false;
                    item.Chunk.ForkView.RefreshPreview(locked?plan.ChoiceAt(item.Chunk.Sequence):session.Runner.Lane,locked);
                }
            }
        }
    }
}
