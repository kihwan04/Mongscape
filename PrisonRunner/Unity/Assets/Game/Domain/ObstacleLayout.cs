using System;
using System.Collections.Generic;

namespace Muhanok.Domain
{
    public enum HazardAction { Jump, Crouch, HighKnee, Dodge }

    // One authored hazard position in a chunk (local z from the entry). Full rows block every lane and are
    // passed with the matching action; terrain rows (stairs, the closing stair door) belong to the map and stay on.
    public readonly struct ObstacleSlot
    {
        public readonly ObstacleKind Kind; public readonly int Lane; public readonly float Z; public readonly bool FullRow, Terrain;
        public ObstacleSlot(ObstacleKind kind, int lane, float z, bool fullRow, bool terrain = false)
        { Kind = kind; Lane = lane; Z = z; FullRow = fullRow || terrain; Terrain = terrain; }
        public HazardAction Action => FullRow ? ObstacleLayout.ActionFor(Kind) : HazardAction.Dodge;
    }
    public readonly struct LanePlacement
    {
        public readonly int Lane; public readonly float Z;
        public LanePlacement(int lane, float z) { Lane = lane; Z = z; }
    }

    // Seeded hazard placement for one chunk. Presentation only switches authored slots and places pooled extras from it,
    // so the same seed always gives the same run.
    public sealed class ObstacleLayout
    {
        public const float SeamClearance = 4f, ActionSpacing = 6f, CentreZ = 12f, ExerciseChance = .5f, RailGapHalfLength = 1.5f;
        // The zoo walk keeps fewer lane blockers than the other walks (it was .52 against .72).
        public const float ZooBlockerScale = .52f / .72f;
        // A zoo chunk without blockers shows its authored duck row instead of an animal this often (animals are all jumps).
        public const float ZooDuckChance = 1f / 3f;
        public static readonly float[] RailGapZ = { 8f, 18f };
        // The run opens with one full row whose action (jump or duck) is random but whose spot is fixed: the runner starts
        // at z 2, so the authored jump row at z 7 left about half a second to react. This is the duck row's spot, ~17 m ahead.
        public const float OpeningZ = 19f;
        // Per authored slot / floor item: switched on in this run.
        public readonly List<bool> Slots = new List<bool>(12), FloorItems = new List<bool>(2);
        public readonly List<LanePlacement> Blockers = new List<LanePlacement>(2), Bananas = new List<LanePlacement>(4);
        // Per rail-gap window: 0 intact, -1 missing left rail, 1 missing right rail.
        public readonly int[] RailGaps = new int[2];
        public bool Animal { get; private set; }
        public int SafeLane { get; private set; }
        public bool Blocked => Blockers.Count > 0;
        private readonly List<float> rows = new List<float>(4);
        private IReadOnlyList<ObstacleSlot> slots;
        private IReadOnlyList<LanePlacement> floor;
        // Authored z of the row moved to OpeningZ in the first chunk, NaN otherwise.
        private float openingFrom = float.NaN;
        // Where an authored slot stands in this run: the first chunk's opening row is moved to OpeningZ.
        public float ZOf(int slot) => slots[slot].FullRow && !slots[slot].Terrain && Same(slots[slot].Z, openingFrom) ? OpeningZ : slots[slot].Z;

        public static HazardAction ActionFor(ObstacleKind kind) => kind == ObstacleKind.Crate || kind == ObstacleKind.Cart ? HazardAction.Jump
            : kind == ObstacleKind.Pipe ? HazardAction.Crouch : kind == ObstacleKind.Stair ? HazardAction.HighKnee : HazardAction.Dodge;
        public static bool IsCart(int type) => type == 1 || type == 2;
        // Mine, corridor and zoo walks may swap their rows for lane blockers and banana rows on the extended route.
        public static bool LaneBlockType(int type) => type == 0 || type == 4 || type == 5 || type == 12;
        // The rail-gap window containing a cart-chunk local z, or -1.
        public static int RailGapAt(float localZ)
        {
            for (int i = 0; i < RailGapZ.Length; i++) if (Math.Abs(localZ - RailGapZ[i]) <= RailGapHalfLength) return i;
            return -1;
        }
        private static bool InsideSeams(float z) => z >= SeamClearance - .001f && z <= RouteSurface.Length - SeamClearance + .001f;

        public void Plan(Random random, int type, int sequence, IReadOnlyList<ObstacleSlot> authored, IReadOnlyList<LanePlacement> floorItems,
            HazardOdds odds, bool extended, bool fork, bool boarding = false)
        {
            slots = authored; floor = floorItems;
            Slots.Clear(); FloorItems.Clear(); Blockers.Clear(); Bananas.Clear(); Animal = false; SafeLane = 0; openingFrom = float.NaN;
            for (int i = 0; i < slots.Count; i++) Slots.Add(false);
            for (int i = 0; i < floor.Count; i++) FloorItems.Add(false);
            Array.Clear(RailGaps, 0, RailGaps.Length);
            if (IsCart(type)) { if (!fork) PlanRails(random, boarding); return; }
            for (int i = 0; i < slots.Count; i++) if (slots[i].Terrain) Slots[i] = true;
            if (sequence == 0)
            {
                // The first chunk opens with either the jump row or the duck row, always at OpeningZ.
                Rows(true);
                if (rows.Count > 0) { openingFrom = rows[random.Next(rows.Count)]; SetRow(openingFrom); }
                return;
            }
            SafeLane = random.Next(3) - 1;
            if (extended && LaneBlockType(type) && random.NextDouble() < (type == 12 ? odds.Blocker * ZooBlockerScale : odds.Blocker))
            {
                bool both = random.NextDouble() < odds.DoubleBlocker; int first = (SafeLane + 2) % 3 - 1;
                for (int lane = -1; lane <= 1; lane++)
                    if (lane != SafeLane && (both || lane == first)) Blockers.Add(new LanePlacement(lane, CentreZ));
            }
            else if (type == 12) { if (random.NextDouble() >= ZooDuckChance || !SetActionRow(HazardAction.Crouch)) Animal = true; }
            else
            {
                Rows(true); int on = 0;
                for (int i = 0; i < rows.Count; i++)
                    if (!Conflicts(rows[i], RowAction(rows[i])) && random.NextDouble() < ExerciseChance) { SetRow(rows[i]); on++; }
                if (on == 0 && !HasTerrain())
                {
                    for (int i = rows.Count - 1; i >= 0; i--) if (Conflicts(rows[i], RowAction(rows[i]))) rows.RemoveAt(i);
                    if (rows.Count > 0) SetRow(rows[random.Next(rows.Count)]);
                }
                Rows(false);
                for (int i = 0; i < rows.Count; i++)
                {
                    if (random.NextDouble() >= odds.Row) continue;
                    int pick = random.Next(3), seen = 0, slot = -1;
                    for (int s = 0; s < slots.Count; s++) if (!slots[s].FullRow && Same(slots[s].Z, rows[i]) && seen++ <= pick) slot = s;
                    // One lane per row, so the validator always finds an open lane.
                    if (slot < 0 || Conflicts(rows[i], HazardAction.Dodge) || !SafePatternValidator.IsSafe(1 << (slots[slot].Lane + 1))) continue;
                    Slots[slot] = true;
                }
            }
            bool dressed = extended && LaneBlockType(type);
            for (int i = 0; i < floor.Count; i++)
                FloorItems[i] = !dressed && !Animal && InsideSeams(floor[i].Z) && !Conflicts(floor[i].Z, HazardAction.Dodge);
            if (!dressed) return;
            // Banana rows sit exactly at the seam clearance and always spare the safe lane.
            foreach (float z in new[] { SeamClearance, RouteSurface.Length - SeamClearance })
            {
                if (Conflicts(z, HazardAction.Dodge)) continue;
                for (int lane = -1; lane <= 1; lane++) if (lane != SafeLane && random.NextDouble() < odds.Banana) Bananas.Add(new LanePlacement(lane, z));
            }
        }
        private void PlanRails(Random random, bool boarding)
        {
            for (int i = 0; i < RailGaps.Length; i++) RailGaps[i] = random.Next(3) - 1;
            // The cart is boarded at this chunk's entry and the lane resets there, so the first window comes too soon to lean for.
            if (boarding) RailGaps[0] = 0;
            bool any = false; foreach (int side in RailGaps) any |= side != 0;
            if (!any) RailGaps[boarding ? RailGaps.Length - 1 : random.Next(RailGaps.Length)] = random.Next(2) == 0 ? -1 : 1;
        }
        private static bool Same(float a, float b) => Math.Abs(a - b) < .01f;
        // Distinct z of full rows (true) or single-lane rows (false) that are not terrain and keep clear of the seams.
        private void Rows(bool full)
        {
            rows.Clear();
            foreach (var slot in slots)
            {
                if (slot.Terrain || slot.FullRow != full || !InsideSeams(slot.Z)) continue;
                bool known = false; foreach (float z in rows) known |= Same(z, slot.Z);
                if (!known) rows.Add(slot.Z);
            }
        }
        private HazardAction RowAction(float z)
        {
            foreach (var slot in slots) if (slot.FullRow && Same(slot.Z, z)) return slot.Action;
            return HazardAction.Dodge;
        }
        // Switches on the first authored full row with this action; false when the chunk has none.
        private bool SetActionRow(HazardAction action)
        {
            Rows(true);
            foreach (float z in rows) if (RowAction(z) == action) { SetRow(z); return true; }
            return false;
        }
        private void SetRow(float z) { for (int i = 0; i < slots.Count; i++) if (slots[i].FullRow && !slots[i].Terrain && Same(slots[i].Z, z)) Slots[i] = true; }
        private bool HasTerrain() { foreach (var slot in slots) if (slot.Terrain) return true; return false; }
        // Nothing within the seam clearance, and a different action needs the full action spacing.
        private bool Conflicts(float z, HazardAction action)
        {
            for (int i = 0; i < slots.Count; i++) if (Slots[i] && TooClose(ZOf(i), slots[i].Action, z, action)) return true;
            for (int i = 0; i < FloorItems.Count; i++) if (FloorItems[i] && TooClose(floor[i].Z, HazardAction.Dodge, z, action)) return true;
            foreach (var block in Blockers) if (TooClose(block.Z, HazardAction.Dodge, z, action)) return true;
            foreach (var peel in Bananas) if (TooClose(peel.Z, HazardAction.Dodge, z, action)) return true;
            return Animal && TooClose(CentreZ, HazardAction.Jump, z, action);
        }
        private static bool TooClose(float a, HazardAction first, float b, HazardAction second)
        {
            float gap = Math.Abs(a - b);
            return gap < SeamClearance - .001f || first != second && gap < ActionSpacing - .001f;
        }

        // Safety rules for a planned chunk; null when they all hold. Used by the tests and editor checks.
        public string Violation()
        {
            var hazards = new List<(float z, HazardAction action, int mask, bool terrain, bool full)>();
            for (int i = 0; i < Slots.Count; i++) if (Slots[i])
            {
                var slot = slots[i];
                if (slot.FullRow && !SafePatternValidator.IsActionRowSafe(7, slot.Kind)) return "Full row cannot be passed: " + slot.Kind;
                hazards.Add((ZOf(i), slot.Action, slot.FullRow ? 0 : 1 << (slot.Lane + 1), slot.Terrain, slot.FullRow));
            }
            for (int i = 0; i < FloorItems.Count; i++) if (FloorItems[i]) hazards.Add((floor[i].Z, HazardAction.Dodge, 1 << (floor[i].Lane + 1), false, false));
            foreach (var block in Blockers) hazards.Add((block.Z, HazardAction.Dodge, 1 << (block.Lane + 1), false, false));
            foreach (var peel in Bananas) hazards.Add((peel.Z, HazardAction.Dodge, 1 << (peel.Lane + 1), false, false));
            if (Animal) hazards.Add((CentreZ, HazardAction.Jump, 0, false, true));
            int chunkMask = 0;
            foreach (var hazard in hazards) if (!hazard.full) chunkMask |= hazard.mask;
            if (Blocked && !SafePatternValidator.IsSafe(chunkMask)) return "Blockers and bananas close every lane";
            for (int i = 0; i < hazards.Count; i++)
            {
                var a = hazards[i];
                if (!a.terrain && !InsideSeams(a.z)) return "Hazard within " + SeamClearance + "m of a seam at z " + a.z;
                int mask = 0;
                for (int j = 0; j < hazards.Count; j++)
                {
                    var b = hazards[j];
                    if (Same(a.z, b.z) && !b.full) mask |= b.mask;
                    if (j > i && a.action != b.action && Math.Abs(a.z - b.z) < ActionSpacing - .001f) return a.action + " and " + b.action + " closer than " + ActionSpacing + "m";
                }
                if (!SafePatternValidator.IsSafe(mask)) return "Row at z " + a.z + " blocks every lane";
            }
            return null;
        }
    }
}
