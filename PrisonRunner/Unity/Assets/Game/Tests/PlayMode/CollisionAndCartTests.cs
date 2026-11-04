using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Muhanok.Tests
{
    public sealed class CollisionAndCartTests
    {
        [UnityTest] public IEnumerator StairEntryDoorAllowsSlideThenClosesBehindBeforePrison()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;game.Map.Tick(103);
            MuhanokMapChunk stair=null,prison=null;
            foreach(var active in game.Map.Active){if(active.Type==7)stair=active.Chunk;if(active.Type==4)prison=active.Chunk;}
            Assert.IsNotNull(stair);Assert.IsNotNull(prison);Assert.Less(Vector3.Distance(stair.Exit.position,prison.Entry.position),.001f);
            Assert.AreEqual(3,stair.SlideGates.Length);
            foreach(var gate in stair.SlideGates)
            {
                float z=gate.transform.position.z;gate.Preview(z-16);Assert.AreEqual(gate.OpenBottom,gate.Bottom,.001f);
                gate.Preview(z);Assert.AreEqual(.95f,gate.Bottom,.001f,"Leave a physical opening for the slide at contact");
                gate.Preview(z+3);Assert.AreEqual(0,gate.Bottom,.001f,"Close completely behind the runner");
                var hazard=gate.GetComponent<ObstacleController>();Assert.AreEqual(ObstacleKind.Pipe,hazard.Kind);Assert.IsTrue(hazard.gameObject.activeSelf);
                game.Session.Reset();game.Session.Runner.SetStartZ(z-1);
                int lane=hazard.GetComponentInParent<MuhanokObstacleSocket>().Lane;
                if(lane!=0)game.Session.Runner.Command(lane<0?MotionCommand.Left:MotionCommand.Right);
                game.Session.Runner.Tick(.25f,0);game.Session.Runner.Command(MotionCommand.Crouch);game.Session.Runner.SetStartZ(z);
                hazard.Evaluate(game.Session,z-1);Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
                gate.ResetGate();Assert.AreEqual(gate.OpenBottom,gate.Bottom);
            }
            var middle=stair.Obstacles[4];middle.ResetPass();game.Session.Reset();game.Session.Runner.SetStartZ(middle.transform.position.z);
            middle.Evaluate(game.Session,middle.transform.position.z-1);Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State,"Standing cannot pass the closing entrance");
        }
        [UnityTest] public IEnumerator MovingBetweenLanesCannotBypassFullWidthExerciseRow()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            var mine=game.Map.Active[0].Chunk;game.Session.Runner.Command(MotionCommand.Right);game.Session.Runner.Tick(.10f,0);
            Assert.AreEqual(1.1f,game.Session.Runner.X,.001f);
            // The first chunk keeps either its jump row or its duck row for this seed; cross whichever is there.
            ObstacleController row=null;foreach(var obstacle in mine.ExerciseObstacles)if(obstacle.gameObject.activeSelf)row=obstacle;
            Assert.IsNotNull(row,"The first chunk opens with one full exercise row");
            Assert.AreEqual(Muhanok.Domain.ObstacleLayout.OpeningZ,mine.transform.InverseTransformPoint(row.transform.position).z,.05f,"Jump or duck, the opening row stands at the fixed spot");
            game.Session.Runner.SetStartZ(row.transform.position.z);
            foreach(var obstacle in mine.ExerciseObstacles)if(obstacle.FullExerciseRow)obstacle.Evaluate(game.Session,game.Session.Runner.Z-1);
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);
        }
        [UnityTest] public IEnumerator DedicatedCartTrackHasOnlyRailHazardsAndCorrectLeanPassesBothSides()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            CartRailGap last=null;
            foreach(var active in game.Map.Active)
            {
                if(active.Type!=1&&active.Type!=2)continue;
                Assert.AreEqual(2,active.Chunk.RailGaps.Length);
                foreach(var walking in active.Chunk.Obstacles)Assert.IsFalse(walking.gameObject.activeSelf);
                foreach(var gap in active.Chunk.RailGaps)
                {
                    if(!gap.gameObject.activeSelf)continue;
                    game.Session.Reset();game.Session.Runner.SetStartZ(gap.transform.position.z-1);
                    game.Session.Runner.Command(gap.RequiredLean<0?MotionCommand.Left:MotionCommand.Right);
                    game.Session.Runner.Tick(.25f,0);game.Session.Runner.SetStartZ(gap.transform.position.z);
                    gap.Evaluate(game.Session,gap.transform.position.z-1);
                    Assert.IsTrue(gap.Passed);Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
                    Assert.AreEqual(1,game.Session.Score.CleanPasses);Assert.AreEqual(0,game.Session.Score.Score);
                    Assert.LessOrEqual(Mathf.Abs(game.Session.Runner.WorldLaneOffset),.161f);last=gap;
                }
            }
            Assert.IsNotNull(last);last.ResetGap();game.Session.Reset();game.Session.Runner.SetStartZ(last.transform.position.z-1);
            game.Session.Runner.Command(last.RequiredLean<0?MotionCommand.Right:MotionCommand.Left);game.Session.Runner.Tick(.25f,0);
            game.Session.Runner.SetStartZ(last.transform.position.z);last.Evaluate(game.Session,last.transform.position.z-1);
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);Assert.AreEqual(CaptureReason.BrokenRail,game.Session.Reason);
        }
        [UnityTest] public IEnumerator RailWindowsFollowTheSeededLayoutAndChangeWhenThePooledChunkIsReused()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            MuhanokMapChunk cart=null;foreach(var active in game.Map.Active)if(cart==null&&active.Type==1)cart=active.Chunk;
            Assert.IsNotNull(cart);
            var layouts=new System.Collections.Generic.HashSet<string>();CartRailGap brokenGap=null;
            for(int seed=0;seed<12;seed++)
            {
                if(seed>0)cart.Prepare(cart.Sequence,1,new System.Random(seed));
                layouts.Add(string.Join(",",cart.Layout.RailGaps));int broken=0;
                for(int i=0;i<cart.RailGaps.Length;i++)
                {
                    var gap=cart.RailGaps[i];int side=cart.Layout.RailGaps[i];broken+=side!=0?1:0;
                    Assert.AreEqual(side>=0,gap.LeftRail.activeSelf,"Left rail pieces");Assert.AreEqual(side<=0,gap.RightRail.activeSelf,"Right rail pieces");
                    Assert.AreEqual(side<0,gap.LeftBreak.activeSelf);Assert.AreEqual(side>0,gap.RightBreak.activeSelf);
                    Assert.IsTrue(gap.Pieces.gameObject.activeInHierarchy);Assert.AreEqual(side!=0,gap.gameObject.activeSelf);
                    foreach(var piece in gap.LeftRail.GetComponentsInChildren<MeshRenderer>(true))
                        Assert.AreEqual(piece.name=="SingleRail"||piece.name=="DeckEdge"?"ForgedIron":piece.name=="RailFastener"?"PaintedSteel":"Timber",
                            piece.sharedMaterial.name,"Rail pieces match the baked bridge: "+piece.name);
                    if(side!=0){Assert.AreEqual(-side,gap.RequiredLean);brokenGap=gap;continue;}
                    // Riding straight through an intact window is never judged.
                    float z=gap.transform.position.z;
                    game.Session.Reset();game.Session.Runner.SetStartZ(z-1);game.Session.Runner.Tick(.25f,0);game.Session.Runner.SetStartZ(z);
                    Assert.IsTrue(game.Session.Runner.Riding);gap.Evaluate(game.Session,z-1);
                    Assert.IsFalse(gap.Passed);Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"Intact window "+i);
                }
                Assert.GreaterOrEqual(broken,1);
            }
            Assert.Greater(layouts.Count,1,"Reusing the pooled chunk applies the new layout");
            // A broken window catches a rider who does not lean away from it.
            float at=brokenGap.transform.position.z;brokenGap.ResetGap();
            game.Session.Reset();game.Session.Runner.SetStartZ(at-1);game.Session.Runner.Tick(.25f,0);game.Session.Runner.SetStartZ(at);
            brokenGap.Evaluate(game.Session,at-1);
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);Assert.AreEqual(CaptureReason.BrokenRail,game.Session.Reason);
        }
        [UnityTest] public IEnumerator ActualExerciseRowsMatchJumpDuckAndHighKneeWithoutAnEmptyLane()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;game.Map.Tick(90);
            foreach(var active in game.Map.Active)
            {
                if(active.Type!=3&&active.Type!=7)continue;
                var hazards=new System.Collections.Generic.List<ObstacleController>(active.Chunk.Obstacles);
                hazards.AddRange(active.Chunk.ExerciseObstacles);
                foreach(var hazard in hazards)
                {
                    if(!hazard.gameObject.activeSelf)continue;
                    game.Session.Reset();float z=hazard.transform.position.z;game.Session.Runner.SetStartZ(z-1);
                    var socket=hazard.GetComponentInParent<MuhanokObstacleSocket>();
                    if(socket.Lane!=0)game.Session.Runner.Command(socket.Lane<0?MotionCommand.Left:MotionCommand.Right);
                    game.Session.Runner.Tick(.25f,0);
                    game.Session.Runner.Command(hazard.Kind==ObstacleKind.Crate?MotionCommand.Jump:hazard.Kind==ObstacleKind.Pipe?MotionCommand.Crouch:MotionCommand.HighKnee);
                    game.Session.Runner.Tick(.15f,0);game.Session.Runner.SetStartZ(z);hazard.Evaluate(game.Session,z-1);
                    Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"Wrong response to "+hazard.Kind);
                }
            }
        }
        [UnityTest] public IEnumerator EveryWalkingMapHasJumpAndSlideAndRecycledActionsRemainPassableAtMaxSpeed()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            foreach(var prefab in game.ChunkPrefabs)
            {
                if(prefab.RouteType==1||prefab.RouteType==2){Assert.IsEmpty(prefab.ExerciseObstacles);continue;}
                bool jump=false,slide=false;
                foreach(var hazard in prefab.ExerciseObstacles){jump|=hazard.Kind==ObstacleKind.Crate;slide|=hazard.Kind==ObstacleKind.Pipe;}
                foreach(var hazard in prefab.Obstacles)slide|=hazard.Kind==ObstacleKind.Pipe;
                Assert.IsTrue(jump&&slide,"Missing jump/slide in "+prefab.AssetId);
            }
            // Cross two complete map loops at the cap, including stairs, closing doors and cart transitions.
            var run=new RunSession(new RunConfig{BaseSpeed=17f,CartSpeed=17f,MaxSpeed=17f});
            run.Flow.Transition(GameFlowState.TitleIdle);run.Flow.Transition(GameFlowState.LoadingIntro);run.Flow.Transition(GameFlowState.Running);
            int created=game.Map.CreatedCount;
            for(int frame=0;frame<1600&&run.Runner.Z<485f;frame++)
            {
                foreach(var active in game.Map.Active)
                {
                    foreach(var gap in active.Chunk.RailGaps)
                    {
                        float ahead=gap.transform.position.z-run.Runner.Z;
                        if(!gap.Passed&&ahead>0&&ahead<run.TargetSpeed*.3f&&run.Runner.Riding)
                            run.Runner.Command(gap.RequiredLean<0?MotionCommand.Left:MotionCommand.Right);
                    }
                    foreach(var hazard in active.Chunk.Obstacles)PrepareAction(run,hazard);
                    foreach(var hazard in active.Chunk.ExerciseObstacles)PrepareAction(run,hazard);
                }
                float previous=run.Runner.Z;run.Tick(1f/30f);game.Map.Tick(run.Runner.Z);
                foreach(var active in game.Map.Active)
                {
                    foreach(var hazard in active.Chunk.Obstacles)hazard.Evaluate(run,previous);
                    foreach(var hazard in active.Chunk.ExerciseObstacles)hazard.Evaluate(run,previous);
                    foreach(var gap in active.Chunk.RailGaps)gap.Evaluate(run,previous);
                }
                Assert.AreEqual(GameFlowState.Running,run.Flow.State,"Unpassable pattern near "+run.Runner.Z);
            }
            Assert.GreaterOrEqual(run.Runner.Z,485f);Assert.AreEqual(created,game.Map.CreatedCount);
        }
        private static void PrepareAction(RunSession session,ObstacleController hazard)
        {
            float ahead=hazard.transform.position.z-session.Runner.Z;
            if(!hazard.gameObject.activeSelf||hazard.Passed||ahead<=0)return;
            if(hazard.Kind==ObstacleKind.Barrier)
            {
                if(ahead>session.TargetSpeed*.6f)return;
                var socket=hazard.GetComponentInParent<MuhanokObstacleSocket>();
                if(session.Runner.Lane==socket.Lane)session.Runner.Command(socket.Lane<=0?MotionCommand.Right:MotionCommand.Left);
                return;
            }
            if(ahead>session.TargetSpeed*.25f)return;
            if(hazard.Kind==ObstacleKind.Crate&&session.Runner.Y<=.001f)session.Runner.Command(MotionCommand.Jump);
            else if(hazard.Kind==ObstacleKind.Pipe)session.Runner.Command(MotionCommand.Crouch);
            else if(hazard.Kind==ObstacleKind.Stair)session.Runner.Command(MotionCommand.HighKnee);
        }
        [UnityTest] public IEnumerator RealFloorItemUsesSweptContactJumpAvoidanceAndPoolReset()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);
            game.enabled=false;game.Map.Tick(218);MuhanokMapChunk zoo=null;
            foreach(var active in game.Map.Active)if(active.Type==12)zoo=active.Chunk;
            Assert.IsNotNull(zoo);Assert.AreEqual(2,zoo.FloorItems.Length);
            // Animal jump chunks reserve their floor. Enable this authored item only for its isolated contact regression.
            var item=zoo.FloorItems[0];item.gameObject.SetActive(true);float target=item.transform.position.z;
            game.Session.Runner.Command(MotionCommand.Left);game.Session.Runner.Tick(.25f,0);
            game.Session.Runner.SetStartZ(target);item.Evaluate(game.Session,target-.4f);
            Assert.IsTrue(item.Passed);Assert.IsTrue(game.Session.Runner.Slowing);Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State);
            game.Session.Tick(3);item.ResetItem();game.Session.Runner.SetStartZ(target-1);
            game.Session.Runner.Command(MotionCommand.Jump);game.Session.Tick(.15f);game.Session.Runner.SetStartZ(target);
            item.Evaluate(game.Session,target-1);Assert.IsTrue(item.Passed);Assert.IsFalse(game.Session.Runner.Slowing,"A jump must avoid a floor item");
            zoo.Prepare(9,1,new System.Random(1));Assert.IsFalse(item.Passed);Assert.AreEqual(zoo.Layout.FloorItems[0],item.gameObject.activeSelf,"Reuse follows the new layout");
        }
        [UnityTest] public IEnumerator ActualCartVisualBanksAndSolidObstacleEndsRunOnFirstContact()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();game.Session.Config.ExtendedRoutes=false;game.BeginRun("");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            game.Session.Runner.SetStartZ(35);game.Session.Runner.Command(MotionCommand.Right);game.Session.Tick(.25f);game.Hero.Apply(game.Session.Runner);
            var hands=Object.FindFirstObjectByType<FirstPersonHands>();yield return null;
            Assert.IsFalse(hands.Visual.gameObject.activeSelf);Assert.IsTrue(game.GetComponent<CartRideView>().Visible);
            Assert.IsTrue(game.Hero.GetComponentInChildren<Animator>().HasState(0,Animator.StringToHash("AN_Monkey_CartRide")));
            Assert.IsNotNull(game.CartRidePrefab.GetComponent<AudioSource>().clip);
            game.CameraRig.FollowCart(game.Hero.Head,1,-11);
            Assert.Greater(Quaternion.Angle(game.CameraRig.Chase.transform.rotation,Quaternion.identity),5);
            var hazard=new GameObject("Fatal swept contact");var obstacle=hazard.AddComponent<ObstacleController>();obstacle.Kind=ObstacleKind.Barrier;
            RouteSurface.Sample(game.Session.Runner.Z,out float offset,out _);
            hazard.transform.position=new Vector3(game.Session.Runner.X+offset,0,game.Session.Runner.Z);
            obstacle.Evaluate(game.Session,game.Session.Runner.Z-1);Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);
            foreach(var actor in Object.FindObjectsByType<RunnerView>(FindObjectsSortMode.None))if(actor!=game.Hero)
            {
                Assert.Greater(Vector3.Dot(actor.transform.forward,(game.Hero.transform.position-actor.transform.position).normalized),.9f,"Caught police must face the player");
                Assert.Greater(Vector3.Distance(actor.transform.position,game.Hero.transform.position),2,"Caught model must stay out of the first-person camera");
            }
            float z=game.Session.Runner.Z;game.Session.Tick(1);Assert.AreEqual(z,game.Session.Runner.Z);
            Object.Destroy(hazard);
        }
    }
}
