using System;
using System.Collections.Generic;
using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    // Exercise plan stage 4: big movements (half squat, high jump, knee to hip height, wide side step and the upper-body
    // equivalents), their in-game effects, the upper-body pull-down on the stairs, and the coach subtitles.
    public sealed class BigMoveTests
    {
        // A front-facing synthetic body in image units (y down). Squat lowers the hips and bends the knees forward-down.
        private sealed class Body
        {
            public float Dx, ShoulderDx, Rise, Squat, KneeL, KneeR, ShoulderDrop, Roll;
            public float WristL = .6f, WristR = .6f, ElbowL = .5f, ElbowR = .5f; public bool Legs = true, StraightArms;
        }
        private static PoseFrame Frame(Body b, double time)
        {
            var f = new PoseFrame { HasPose = true, Time = time, Aspect = 4f / 3f };
            void Set(int i, float x, float y, float v = .95f) => f.Points[i] = new PoseLandmark { X = x, Y = y, Visibility = v };
            for (int i = 0; i < PoseFrame.Count; i++) Set(i, .5f, .5f, .2f);
            float lift = -b.Rise, legs = b.Legs ? .95f : .1f;
            float sx = b.Dx + b.ShoulderDx, sy = .35f + lift + b.ShoulderDrop + b.Squat * .9f, hy = .62f + lift + b.Squat;
            Set(0, .5f + sx, .25f + lift + b.ShoulderDrop + b.Squat * .9f);
            Set(11, .44f + sx, sy - b.Roll); Set(12, .56f + sx, sy + b.Roll);
            Set(23, .46f + b.Dx, hy, legs); Set(24, .54f + b.Dx, hy, legs);
            // Squatting brings the knees up toward the hips and out; the ankles stay on the floor.
            float ky = .78f + lift + b.Squat * .35f;
            Set(25, .46f + b.Dx - b.Squat * .5f, ky - b.KneeL, legs); Set(26, .54f + b.Dx + b.Squat * .5f, ky - b.KneeR, legs);
            Set(27, .46f + b.Dx, .93f + lift * (b.Rise > 0 ? 1 : 0), legs); Set(28, .54f + b.Dx, .93f + lift * (b.Rise > 0 ? 1 : 0), legs);
            // Arms: elbows halfway between shoulder and wrist (straight) or bent out to the side.
            float bend = b.StraightArms ? 0 : .06f;
            Set(13, .42f + sx - bend, (sy + b.WristL) * .5f); Set(14, .58f + sx + bend, (sy + b.WristR) * .5f);
            Set(15, .42f + sx, b.WristL); Set(16, .58f + sx, b.WristR);
            return f;
        }
        private sealed class Run { public MotionClassifier C; public List<string> Events = new List<string>(); }
        private static Run Play(PlayStyle style, Func<double, Body> script, double seconds, bool stairs = false)
        {
            var c = new MotionClassifier(new MotionTuning()); c.SetStyle(style);
            for (int i = 0; i < 30; i++) c.Update(Frame(new Body { Legs = style == PlayStyle.FullBody }, i / 30.0));
            Assert.AreEqual(PoseStatus.Tracking, c.Status);
            var run = new Run { C = c }; bool deep = false;
            for (double t = 0; t < seconds; t += 1 / 30.0)
            {
                var body = script(t); body.Legs = style == PlayStyle.FullBody; c.SetStairs(stairs);
                var r = c.Update(Frame(body, 1 + t));
                if (r.JumpEdge) run.Events.Add("jump"); if (r.BigJump) run.Events.Add("bigjump");
                if (r.KneeStep) run.Events.Add("knee"); if (r.BigKnee) run.Events.Add("bigknee");
                if (r.BigLane) run.Events.Add("biglane");
                if (r.CrouchDeep != deep) { deep = r.CrouchDeep; if (deep) run.Events.Add("deep"); }
            }
            return run;
        }
        private static bool In(double t, double a, double b) => t >= a && t < b;

        [Test] public void AHalfSquatIsDeepAShallowDuckIsNot()
        {
            var shallow = Play(PlayStyle.FullBody, t => new Body { ShoulderDrop = In(t, .2, 1.2) ? .1f : 0 }, 1.6).Events;
            CollectionAssert.DoesNotContain(shallow, "deep", "A duck that passes the bar is not a squat");
            var squat = Play(PlayStyle.FullBody, t => new Body { Squat = In(t, .2, 1.2) ? .14f : 0 }, 1.6).Events;
            CollectionAssert.Contains(squat, "deep");
        }

        [Test] public void AHighJumpIsBigAHopJustPasses()
        {
            var high = Play(PlayStyle.FullBody, t => new Body { Rise = In(t, .2, .7) ? .12f : 0 }, 1.2).Events;
            CollectionAssert.AreEqual(new[] { "jump", "bigjump" }, high.FindAll(e => e.Contains("jump")));
            var quick = Play(PlayStyle.FullBody, t => new Body { Rise = In(t, .2, .33) ? .12f : 0 }, 1.2).Events;
            CollectionAssert.Contains(quick, "jump"); CollectionAssert.DoesNotContain(quick, "bigjump", "Under 0.2 s in the air");
        }

        [Test] public void KneesToHipHeightAreBig()
        {
            Func<double, Body> steps(float lift) => t => { int s = (int)(t / .4) % 2; bool on = In(t, .2, 2.2); return new Body { KneeL = on && s == 0 ? lift : 0, KneeR = on && s == 1 ? lift : 0 }; };
            var low = Play(PlayStyle.FullBody, steps(.06f), 2.4, true).Events;
            Assert.Greater(low.FindAll(e => e == "knee").Count, 2); CollectionAssert.DoesNotContain(low, "bigknee");
            var high = Play(PlayStyle.FullBody, steps(.15f), 2.4, true).Events;
            Assert.AreEqual(high.FindAll(e => e == "knee").Count, high.FindAll(e => e == "bigknee").Count, "Every hip-high step is big");
        }

        [Test] public void AWideSideStepIsBigALeanIsNot()
        {
            CollectionAssert.Contains(Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, 1) ? -.2f : 0 }, 1.4).Events, "biglane");
            CollectionAssert.DoesNotContain(Play(PlayStyle.FullBody, t => new Body { Dx = In(t, .2, 1) ? -.1f : 0 }, 1.4).Events, "biglane", "A normal step changes lane only");
        }

        [Test] public void UpperBodyArmSwingsAreStepsAndThePullDownIsTheStairExercise()
        {
            Func<double, Body> swings = t => { int s = (int)(t / .3) % 2; bool on = In(t, .2, 1.8); return new Body { WristL = on ? (s == 0 ? .4f : .62f) : .6f, WristR = on ? (s == 1 ? .4f : .62f) : .6f }; };
            var swing = Play(PlayStyle.UpperBody, swings, 2, true);
            CollectionAssert.DoesNotContain(swing.Events, "knee", "Big arm swings are steps, not knee steps");
            Assert.Greater(swing.C.StepCount, 3);
            Func<double, Body> pulls(float bottom) => t => { double p = (t - .2) % 1.0; bool up = t > .2 && p < .5; return new Body { WristL = up ? .08f : bottom, WristR = up ? .08f : bottom }; };
            var full = Play(PlayStyle.UpperBody, pulls(.6f), 2.1, true).Events;
            Assert.AreEqual(4, full.FindAll(e => e == "knee").Count, "Up and down are a step each");
            Assert.AreEqual(2, full.FindAll(e => e == "bigknee").Count, "Down below the shoulders is big");
            CollectionAssert.DoesNotContain(full, "jump", "Hands up on the stairs are not a jump");
            var half = Play(PlayStyle.UpperBody, pulls(.3f), 2.1, true).Events;
            Assert.AreEqual(4, half.FindAll(e => e == "knee").Count); CollectionAssert.DoesNotContain(half, "bigknee");
            CollectionAssert.DoesNotContain(Play(PlayStyle.UpperBody, pulls(.6f), 2.1, false).Events, "knee", "Off the stairs hands up is the jump");
        }

        [Test] public void UpperBodyBigJumpDuckAndLean()
        {
            CollectionAssert.Contains(Play(PlayStyle.UpperBody, t => new Body { WristL = In(t, .2, .6) ? .02f : .6f, WristR = In(t, .2, .6) ? .02f : .6f, StraightArms = true }, 1).Events, "bigjump");
            CollectionAssert.DoesNotContain(Play(PlayStyle.UpperBody, t => new Body { WristL = In(t, .2, .6) ? .08f : .6f, WristR = In(t, .2, .6) ? .08f : .6f }, 1).Events, "bigjump", "Bent arms pass but are not big");
            CollectionAssert.Contains(Play(PlayStyle.UpperBody, t => new Body { ShoulderDrop = In(t, .2, 1) ? .12f : 0 }, 1.3).Events, "deep");
            CollectionAssert.DoesNotContain(Play(PlayStyle.UpperBody, t => new Body { ShoulderDrop = In(t, .2, 1) ? .06f : 0 }, 1.3).Events, "deep");
        }

        [Test] public void BigMovesChangeTheRunWithoutCoinsOnTheSpot()
        {
            var run = new RunSession(new RunConfig()); run.Flow.Transition(GameFlowState.TitleIdle); run.Flow.Transition(GameFlowState.LoadingIntro); run.Flow.Transition(GameFlowState.Running);
            Assert.IsFalse(run.Big(RunSession.BigMove.Jump), "Not airborne: nothing to boost");
            run.Runner.Command(MotionCommand.Jump); run.Tick(.05f);
            Assert.IsTrue(run.Big(RunSession.BigMove.Jump));
            float apex = 0; for (int i = 0; i < 80; i++) { run.Tick(.02f); apex = Math.Max(apex, run.Runner.Y); }
            Assert.AreEqual(run.Config.JumpHeight * run.Config.BigJumpHeight, apex, .08f, "The big jump reaches 1.35x");
            Assert.AreEqual(0, run.Score.Coins, "Coins come from the coins placed in the air, not on the spot"); Assert.AreEqual(0, run.BigCoins);
            run.Runner.Command(MotionCommand.Crouch); Assert.IsFalse(run.Runner.DeepSliding); Assert.IsTrue(run.Big(RunSession.BigMove.Crouch));
            Assert.IsTrue(run.Runner.DeepSliding);
            run.Tick(run.Config.CrouchSeconds + .1f); Assert.IsTrue(run.Runner.Crouching, "The deep slide lasts longer"); Assert.IsTrue(run.Runner.DeepSliding);
            run.Tick(run.Config.CrouchSeconds); Assert.IsFalse(run.Runner.Crouching); Assert.IsFalse(run.Runner.DeepSliding, "Ends with the slide");
            run.PacePressure = .5f; run.Big(RunSession.BigMove.Knee);
            Assert.AreEqual(.5f - run.Config.BigKneeRelief, run.PacePressure, 1e-5); Assert.AreEqual(0, run.Score.Coins);
            run.CollectBonusCoin(); Assert.AreEqual(1, run.Score.Coins); Assert.AreEqual(1, run.BigCoins);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 0 }, run.BigMoves);
        }

        [Test] public void TheCoachEncouragesWithoutGradesAndKeepsQuiet()
        {
            var coach = new Coach();
            Assert.AreEqual(CoachLine.PraiseHigh, coach.Tick(0, .1f, new Coach.Input { BigJump = true }));
            Assert.AreEqual(CoachLine.None, coach.Tick(1, .1f, new Coach.Input { BigLane = true }), "One line at a time, and a gap after it");
            Assert.AreEqual(CoachLine.PaceUp, coach.Tick(3, .1f, new Coach.Input { Lagging = true }), "A slow pace may interrupt the gap");
            CoachLine knees = CoachLine.None;
            for (int i = 0; i < 3; i++) knees = coach.Tick(20 + i, .1f, new Coach.Input { OnStairs = true, KneeStep = true });
            Assert.AreEqual(CoachLine.KneesHigher, knees, "Three low knee steps on the stairs");
            coach.Tick(40, .1f, new Coach.Input { CrouchStart = true }); coach.Tick(40.5f, .1f, new Coach.Input { CrouchEnd = true });
            coach.Tick(41, .1f, new Coach.Input { CrouchStart = true });
            Assert.AreEqual(CoachLine.DuckDeeper, coach.Tick(41.5f, .1f, new Coach.Input { CrouchEnd = true }));
            foreach (CoachLine line in Enum.GetValues(typeof(CoachLine)))
                foreach (var style in new[] { PlayStyle.FullBody, PlayStyle.UpperBody })
                { string text = Coach.Text(line, style); StringAssert.DoesNotContain("완벽", text); StringAssert.DoesNotContain("콤보", text); }
            Assert.AreEqual("팔을 어깨 아래까지 당겨요!", Coach.Text(CoachLine.KneesHigher, PlayStyle.UpperBody));
        }
    }
}
