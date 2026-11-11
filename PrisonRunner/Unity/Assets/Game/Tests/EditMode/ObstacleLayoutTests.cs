using System;
using System.Collections.Generic;
using System.Text;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class ObstacleLayoutTests
    {
        private static readonly ObstacleKind C = ObstacleKind.Crate, P = ObstacleKind.Pipe, B = ObstacleKind.Barrier, K = ObstacleKind.Cart, L = ObstacleKind.Laser;
        // The authored chunk prefabs: six lane obstacles (two rows of three), then the full exercise rows.
        private static readonly ObstacleKind[][] lanes =
        {
            new[] { C, P, B, P, P, P }, new[] { P, B, K, L, C, P }, new[] { B, K, L, C, P, B }, new[] { C, C, C, P, P, P },
            new[] { B, B, B, B, B, B }, new[] { B, B, B, B, B, B }, new[] { B, B, B, P, P, P }, new[] { ObstacleKind.Stair, ObstacleKind.Stair, ObstacleKind.Stair, P, P, P },
            new[] { K, L, C, P, B, K }, new[] { L, C, P, B, K, L }, new[] { C, P, B, K, L, C }, new[] { C, C, C, B, B, B }, new[] { C, C, C, B, B, B }
        };
        private static List<ObstacleSlot> Slots(int type)
        {
            var slots = new List<ObstacleSlot>(12);
            if (type == 1 || type == 2) { for (int i = 0; i < 6; i++) slots.Add(new ObstacleSlot(lanes[type][i], i % 3 - 1, 7 + i / 3 * 12, false)); return slots; }
            for (int i = 0; i < 6; i++)
            {
                float z = type >= 4 && type <= 6 && i < 3 ? 23 : type == 7 && i < 3 ? 12 : 7 + i / 3 * 12;
                slots.Add(new ObstacleSlot(lanes[type][i], i % 3 - 1, z, false, type == 7));
            }
            for (int i = 0; i < (type == 7 ? 3 : 6); i++) slots.Add(new ObstacleSlot(i < 3 ? C : P, i % 3 - 1, type == 7 ? 3 : i < 3 ? 7 : 19, true));
            return slots;
        }
        private static List<LanePlacement> Floor(int type) =>
            type == 11 ? new List<LanePlacement> { new LanePlacement(-1, 10) } : type == 12 ? new List<LanePlacement> { new LanePlacement(-1, 10), new LanePlacement(1, 17) } : new List<LanePlacement>();
        // The peak odds the layout used before the difficulty curve, with a chosen single-lane row chance.
        private static HazardOdds Odds(float row) => new HazardOdds(row, .72f, .85f, .82f);
        // The shipped start of the difficulty curve.
        private static readonly HazardOdds StartOdds = new HazardOdds(.25f, .72f, .3f, .3f);
        private static ObstacleLayout Plan(int seed, int type, int sequence, bool extended = true, bool fork = false, float chance = .6f)
            => Plan(seed, type, sequence, extended, fork, Odds(chance));
        private static ObstacleLayout Plan(int seed, int type, int sequence, bool extended, bool fork, HazardOdds odds)
        {
            var layout = new ObstacleLayout();
            layout.Plan(new Random(seed), type, sequence, Slots(type), Floor(type), odds, extended, fork);
            return layout;
        }
        private static string Signature(ObstacleLayout layout)
        {
            var text = new StringBuilder();
            foreach (bool on in layout.Slots) text.Append(on ? '1' : '0');
            text.Append('|'); foreach (bool on in layout.FloorItems) text.Append(on ? '1' : '0');
            text.Append('|'); foreach (var block in layout.Blockers) text.Append(block.Lane).Append(',');
            text.Append('|'); foreach (var peel in layout.Bananas) text.Append(peel.Lane).Append('@').Append(peel.Z).Append(',');
            text.Append('|').Append(layout.Animal).Append('|').Append(string.Join(",", layout.RailGaps));
            return text.ToString();
        }

        [Test] public void SameSeedGivesTheSameLayoutAndSeedsVaryIt()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                var run = new StringBuilder();
                for (int sequence = 0; sequence < 13; sequence++)
                {
                    string first = Signature(Plan(seed, sequence, sequence)), again = Signature(Plan(seed, sequence, sequence));
                    Assert.AreEqual(first, again, "Seed " + seed + " type " + sequence);
                    run.Append(first).Append(';');
                }
                seen.Add(run.ToString());
            }
            Assert.Greater(seen.Count, 50, "Different seeds must give different runs");
        }

        [Test] public void FirstChunkOpensWithEitherTheJumpOrTheDuckRow()
        {
            int jump = 0, duck = 0;
            for (int seed = 0; seed < 500; seed++)
            {
                var layout = Plan(seed, 0, 0); var slots = Slots(0);
                int crates = 0, pipes = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!layout.Slots[i]) continue;
                    Assert.IsTrue(slots[i].FullRow, "Only one full exercise row opens the run");
                    // Random action, fixed spot: a jump row never stands right in front of the start line (z 2).
                    Assert.AreEqual(ObstacleLayout.OpeningZ, layout.ZOf(i), .001f, "The opening row stands at the fixed spot");
                    if (slots[i].Kind == C) crates++; else if (slots[i].Kind == P) pipes++;
                }
                Assert.IsTrue(crates == 3 && pipes == 0 || pipes == 3 && crates == 0, "Exactly one row: " + crates + "/" + pipes);
                if (crates > 0) jump++; else duck++;
                Assert.IsNull(layout.Violation());
            }
            Assert.GreaterOrEqual(jump, 150); Assert.GreaterOrEqual(duck, 150);
            // Later chunks keep the authored spots.
            var later = Plan(3, 0, 4, false, false, 1f); var authored = Slots(0);
            for (int i = 0; i < authored.Count; i++) Assert.AreEqual(authored[i].Z, later.ZOf(i), .001f);
        }

        [Test] public void EverySeedAndChunkTypeKeepsTheSafetyRules()
        {
            for (int seed = 0; seed < 500; seed++)
                for (int type = 0; type < 13; type++)
                    foreach (bool extended in new[] { false, true })
                        foreach (float chance in new[] { .25f, 1f })
                        {
                            var layout = Plan(seed * 13 + type, type, 1 + seed % 40, extended, false, chance);
                            Assert.IsNull(layout.Violation(), "Seed " + seed + " type " + type + " extended " + extended);
                            if (ObstacleLayout.IsCart(type) || layout.Blocked || layout.Animal) continue;
                            bool row = false; var slots = Slots(type);
                            for (int i = 0; i < slots.Count; i++) row |= layout.Slots[i] && slots[i].FullRow;
                            Assert.IsTrue(row, "Every walking chunk keeps at least one action row: type " + type);
                        }
        }

        // Zoo animals are all jumps, so a zoo chunk without blockers sometimes shows its authored duck row instead.
        [Test] public void ZooChunksMixDuckRowsWithAnimals()
        {
            int animals = 0, ducks = 0, slots = Slots(12).Count;
            for (int seed = 0; seed < 600; seed++)
            {
                var layout = Plan(seed, 12, 1 + seed % 40);
                if (layout.Blocked) continue;
                int on = 0; for (int i = 0; i < slots; i++) if (layout.Slots[i]) on++;
                if (layout.Animal) { animals++; Assert.AreEqual(0, on, "An animal chunk keeps its rows off"); continue; }
                ducks++; Assert.AreEqual(3, on, "Seed " + seed);
                for (int i = 0; i < slots; i++) if (layout.Slots[i]) Assert.AreEqual(HazardAction.Crouch, Slots(12)[i].Action);
                Assert.IsNull(layout.Violation());
            }
            float share = ducks / (float)(ducks + animals);
            Assert.That(share, Is.InRange(ObstacleLayout.ZooDuckChance - .08f, ObstacleLayout.ZooDuckChance + .08f), ducks + " ducks, " + animals + " animals");
        }

        [Test] public void StartOddsKeepTheSafetyRules()
        {
            for (int seed = 0; seed < 500; seed++)
                for (int type = 0; type < 13; type++)
                    foreach (bool extended in new[] { false, true })
                        Assert.IsNull(Plan(seed * 13 + type, type, 1 + seed % 40, extended, false, StartOdds).Violation(), "Seed " + seed + " type " + type + " extended " + extended);
        }

        [Test] public void StartOddsPlaceFewerBlockersAndBananasThanMaxOdds()
        {
            int start = 0, max = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var easy = Plan(seed, 0, 1, true, false, StartOdds); start += easy.Blockers.Count + easy.Bananas.Count;
                var hard = Plan(seed, 0, 1, true, false, Odds(.9f)); max += hard.Blockers.Count + hard.Bananas.Count;
            }
            Assert.Greater(max, 0);
            Assert.Less(start, max * .6f, "Start " + start + " vs max " + max);
        }

        // An easier start must remove hazards, not swap lane blockers for full-width actions (jump/duck rows, zoo animals).
        [Test] public void CurveStartNeverAddsFullWidthActions()
        {
            var director = new DifficultyDirector(); var config = new RunConfig();
            HazardOdds start = director.Odds(0, config), max = director.Odds(100000, config);
            foreach (int type in new[] { 0, 4, 5, 12 })
            {
                int easy = 0, hard = 0;
                for (int seed = 0; seed < 300; seed++) { easy += FullWidth(Plan(seed, type, 1, true, false, start), type); hard += FullWidth(Plan(seed, type, 1, true, false, max), type); }
                Assert.LessOrEqual(easy, hard, "Type " + type + ": start " + easy + " vs max " + hard);
            }
        }
        private static int FullWidth(ObstacleLayout layout, int type)
        {
            int count = layout.Animal ? 1 : 0; var slots = Slots(type);
            for (int i = 0; i < slots.Count; i++) if (layout.Slots[i] && slots[i].FullRow && !slots[i].Terrain) count++;
            return count;
        }

        [Test] public void WalkingRowsVaryPerRunAndLaneHazardsAppear()
        {
            var combinations = new HashSet<string>(); int lane = 0, gates = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var layout = Plan(seed, 0, 3, false, false, 1f); var slots = Slots(0); var key = new StringBuilder();
                for (int i = 0; i < slots.Count; i++) { if (slots[i].FullRow) key.Append(layout.Slots[i] ? '1' : '0'); else if (layout.Slots[i]) lane++; }
                combinations.Add(key.ToString());
                var corridor = Plan(seed, 4, 9, false, false, 1f);
                for (int i = 0; i < 6; i++) if (corridor.Slots[i]) gates++;
            }
            Assert.GreaterOrEqual(combinations.Count, 3, "Jump only, duck only and both rows all occur");
            Assert.Greater(lane, 0); Assert.Greater(gates, 0, "Corridor doors close a random lane");
        }

        [Test] public void MineTrackBreaksAtLeastOnceOnBothSidesAndForksStayWhole()
        {
            bool left = false, right = false, intact = false;
            for (int seed = 0; seed < 500; seed++)
            {
                var layout = Plan(seed, 1 + seed % 2, 1 + seed % 5);
                int broken = 0;
                foreach (int side in layout.RailGaps) { broken += side != 0 ? 1 : 0; left |= side < 0; right |= side > 0; intact |= side == 0; }
                Assert.GreaterOrEqual(broken, 1, "Every cart chunk has at least one broken rail");
                foreach (bool on in layout.Slots) Assert.IsFalse(on, "No walking hazards on the cart track");
                Assert.AreEqual(new[] { 0, 0 }, Plan(seed, 1, 4, true, true).RailGaps, "Fork chunks keep both rails");
            }
            Assert.IsTrue(left && right && intact);
            for (int seed = 0; seed < 200; seed++)
            {
                var boarding = new ObstacleLayout();
                boarding.Plan(new Random(seed), 1, 1, Slots(1), Floor(1), Odds(.6f), true, false, true);
                Assert.AreEqual(0, boarding.RailGaps[0], "The window right after boarding stays whole");
                Assert.AreNotEqual(0, boarding.RailGaps[1], "The boarding chunk still breaks its later window");
            }
            Assert.AreEqual(0, ObstacleLayout.RailGapAt(8)); Assert.AreEqual(1, ObstacleLayout.RailGapAt(19.5f)); Assert.AreEqual(-1, ObstacleLayout.RailGapAt(12));
        }

        [Test] public void ViolationReportsUnsafePatterns()
        {
            var slots = new List<ObstacleSlot> { new ObstacleSlot(B, -1, 10, false), new ObstacleSlot(B, 0, 10, false), new ObstacleSlot(B, 1, 10, false) };
            var layout = new ObstacleLayout(); layout.Plan(new Random(1), 0, 1, slots, new List<LanePlacement>(), Odds(1), false, false);
            Assert.IsNull(layout.Violation());
            for (int i = 0; i < 3; i++) layout.Slots[i] = true;
            StringAssert.Contains("blocks every lane", layout.Violation());
            var close = new List<ObstacleSlot> { new ObstacleSlot(C, -1, 8, true), new ObstacleSlot(P, -1, 12, true) };
            layout.Plan(new Random(1), 0, 1, close, new List<LanePlacement>(), Odds(1), false, false);
            layout.Slots[0] = layout.Slots[1] = true;
            StringAssert.Contains("closer than", layout.Violation());
            var seam = new List<ObstacleSlot> { new ObstacleSlot(B, 0, 23, false) };
            layout.Plan(new Random(1), 0, 1, seam, new List<LanePlacement>(), Odds(1), false, false);
            Assert.IsFalse(layout.Slots[0], "Nothing is placed next to a seam"); layout.Slots[0] = true;
            StringAssert.Contains("seam", layout.Violation());
        }
    }
}
