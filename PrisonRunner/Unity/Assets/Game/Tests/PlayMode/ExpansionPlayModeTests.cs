using System.Collections;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Muhanok.Tests
{
    public sealed class ExpansionPlayModeTests
    {
        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("GameScene");yield return null;
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            game.Session.Config.ExtendedRoutes=true;game.Pose.SetStyle(PlayStyle.Keyboard);game.BeginRun("70");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);
            game.enabled=false;
        }
        private static void Seek(GameBootstrap game,float z,int branch)
        {
            RouteSurface.UsePlan(new RoutePlan(game.Session.Config,723));game.Session.Reset();
            game.Session.Runner.Command(branch<0?MotionCommand.Left:branch>0?MotionCommand.Right:MotionCommand.Center);
            game.Session.Runner.Tick(.25f,0);game.Map.Reset();
            for(float at=2;at<z;at+=12)game.Map.Tick(at);
            game.Map.Tick(z);game.Session.Runner.SetStartZ(z);game.Hero.Apply(game.Session.Runner);game.Map.AnimateGates(z);
        }
        [UnityTest] public IEnumerator CartAndPhysicalSurfaceAgreeOnAllThreeBranches()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();
            for(int branch=-1;branch<=1;branch++)
            {
                Seek(game,132,branch);yield return null;Physics.SyncTransforms();
                RouteSurface.Sample(132,out float expected,out _);
                Assert.AreEqual(CartFork.Offset(132,branch),expected,.001f);
                var cart=game.transform.Find("Actual track cart");Assert.IsNotNull(cart);
                Assert.AreEqual(expected,cart.position.x,.001f,"Cart must travel on the selected physical rail");
                MuhanokMapChunk chunk=null;foreach(var active in game.Map.Active)if(active.Chunk.Entry.position.z==120)chunk=active.Chunk;
                Assert.IsNotNull(chunk);Assert.IsFalse(chunk.transform.Find("EnvironmentRoot").gameObject.activeSelf);
                var floor=chunk.transform.Find("ThreeWayRails/TraversableDeck").GetComponent<MeshCollider>();Assert.IsNotNull(floor);
                Assert.IsTrue(floor.Raycast(new Ray(new Vector3(expected,2,132),Vector3.down),out var hit,4),"Physical deck missing under selected branch");
                Assert.AreEqual(0,hit.point.y,.001f);
                foreach(var coin in chunk.Coins)
                {
                    RouteSurface.Sample(coin.transform.position.z,out float x,out _);Assert.AreEqual(x,coin.transform.position.x,.001f);
                }
                foreach(var gap in chunk.RailGaps)Assert.IsFalse(gap.gameObject.activeSelf);
            }
        }
        [UnityTest] public IEnumerator ForkDirectionMarkersFollowPreviewThenLockedBranchWithoutNewPoolObjects()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,119,0);
            int created=game.Map.CreatedCount;
            foreach(int lane in new[]{-1,0,1})
            {
                game.Session.Runner.Command(lane<0?MotionCommand.Left:lane>0?MotionCommand.Right:MotionCommand.Center);
                game.Session.Runner.Tick(.25f,0);game.Map.AnimateGates(119);yield return null;
                foreach(var item in game.Map.Active)
                {
                    var view=item.Chunk.ForkView;if(view==null||!view.isActiveAndEnabled)continue;
                    Assert.AreEqual(lane,view.HighlightedBranch);
                    for(int branch=-1;branch<=1;branch++)
                    {
                        var marker=view.transform.Find("DirectionMarker_"+branch+"/DirectionArrow");Assert.IsNotNull(marker);
                        var rail=view.transform.Find("PhysicalRail_"+((branch+1)*2)).GetComponent<LineRenderer>();
                        Assert.AreEqual(branch==lane?.2f:.11f,rail.startWidth,.001f);
                        Assert.AreSame(rail.sharedMaterial,marker.GetComponent<Renderer>().sharedMaterial);
                    }
                }
            }
            game.Map.Tick(120);game.Map.AnimateGates(120);yield return null;
            foreach(var item in game.Map.Active)if(item.Chunk.ForkView!=null&&item.Chunk.ForkView.isActiveAndEnabled)
                Assert.AreEqual(RouteSurface.ActivePlan.ChoiceAt(item.Chunk.Sequence),item.Chunk.ForkView.HighlightedBranch);
            Assert.AreEqual(created,game.Map.CreatedCount);
        }
        // Zoo chunks hold either an animal or (ZooDuckChance) the duck row, so walk the zoo branch until one shows.
        private static IEnumerable ZooChunks(GameBootstrap game,int branch)
        {
            for(float z=480;z<=960;z+=48)
            {
                Seek(game,z,branch);
                foreach(var active in game.Map.Active)if(active.Type==12)yield return active.Chunk;
            }
        }
        [UnityTest] public IEnumerator ZooAnimalRowsRequireAndAcceptARealJump()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();int branch=ZooBranch(game);
            int animals=0;
            foreach(MuhanokMapChunk chunk in ZooChunks(game,branch))
            {
                foreach(var animal in chunk.GetComponentsInChildren<ZooHazard>())
                {
                    animals++;Assert.AreEqual(3,animal.Hazards.Length);
                    foreach(var obstacle in chunk.Obstacles)Assert.IsFalse(obstacle.gameObject.activeSelf);
                    foreach(var obstacle in chunk.ExerciseObstacles)Assert.IsFalse(obstacle.gameObject.activeSelf);
                    foreach(var hazard in animal.Hazards){Assert.AreEqual(ObstacleKind.Crate,hazard.Kind);Assert.IsTrue(hazard.FullExerciseRow);}
                    var z=animal.Hazards[1].transform.position.z;game.Session.Reset();game.Session.Runner.SetStartZ(z);
                    game.Session.Runner.Command(MotionCommand.Jump);game.Session.Runner.Tick(.2f,0);
                    foreach(var hazard in animal.Hazards){hazard.ResetPass();hazard.Evaluate(game.Session,z-1);}
                    Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"A jump must pass the animal row");
                    game.Session.Reset();game.Session.Runner.SetStartZ(z);
                    foreach(var hazard in animal.Hazards){hazard.ResetPass();hazard.Evaluate(game.Session,z-1);}
                    Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State,"Standing must fail the animal row");
                    break;
                }
                if(animals>0)break;
            }
            Assert.Greater(animals,0);
        }
        [UnityTest] public IEnumerator ZooDuckRowIsALogThatNeedsACrouch()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();int branch=ZooBranch(game);
            ObstacleController[] row=null;
            foreach(MuhanokMapChunk chunk in ZooChunks(game,branch))
            {
                int on=0;foreach(var obstacle in chunk.ExerciseObstacles)if(obstacle.gameObject.activeSelf)on++;
                if(on==0)continue;
                Assert.AreEqual(0,chunk.GetComponentsInChildren<ZooHazard>().Length,"A duck row replaces the animal");
                row=System.Array.FindAll(chunk.ExerciseObstacles,o=>o.gameObject.activeSelf);break;
            }
            Assert.IsNotNull(row,"No zoo duck row on the seeded zoo branch");Assert.AreEqual(3,row.Length);
            foreach(var hazard in row){Assert.AreEqual(ObstacleKind.Pipe,hazard.Kind);Assert.IsNotNull(hazard.transform.Find("Log"));Assert.IsTrue(hazard.FullExerciseRow);}
            float z=row[1].transform.position.z;game.Session.Reset();game.Session.Runner.SetStartZ(z);
            game.Session.Runner.Command(MotionCommand.Crouch);game.Session.Runner.Tick(.1f,0);
            foreach(var hazard in row){hazard.ResetPass();hazard.Evaluate(game.Session,z-1);}
            Assert.AreEqual(GameFlowState.Running,game.Session.Flow.State,"A crouch must pass the log");
            game.Session.Reset();game.Session.Runner.SetStartZ(z);
            game.Session.Runner.Command(MotionCommand.Jump);game.Session.Runner.Tick(.2f,0);
            foreach(var hazard in row){hazard.ResetPass();hazard.Evaluate(game.Session,z-1);}
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State,"A jump must not pass the log");
        }
        private static int ZooBranch(GameBootstrap game)
        {
            for(int lane=-1;lane<=1;lane++){var p=new RoutePlan(game.Session.Config,723);p.UpdateChoice(144,lane);if(p.DestinationAt(6)<0)return lane;}
            throw new System.Exception("Missing zoo branch");
        }
        private static int BranchFor(GameBootstrap game,int destination)
        {
            for(int lane=-1;lane<=1;lane++){var plan=new RoutePlan(game.Session.Config,723);plan.UpdateChoice(144,lane);if(plan.DestinationAt(6)==destination)return lane;}
            throw new System.Exception("Missing randomized destination");
        }
        [UnityTest] public IEnumerator ForkHasOpaqueFloorWallsAndRoofAndClosesOnlyUnchosenExits()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,110,0);yield return null;Physics.SyncTransforms();
            foreach(float z in new[]{96.01f,108f,119.95f,120f,125f,132f,139f,143.9f})
            {
                MuhanokMapChunk chunk=null;foreach(var active in game.Map.Active)if(active.Chunk.Entry.position.z<=z&&active.Chunk.Exit.position.z>z)chunk=active.Chunk;
                Assert.IsNotNull(chunk);var shell=chunk.transform.Find("ThreeWayRails/ThreeUnknownTunnels").GetComponent<MeshCollider>();
                for(int branch=-1;branch<=1;branch++)
                {
                    var origin=new Vector3(CartFork.Offset(z,branch),1.7f,z);
                    foreach(var direction in new[]{Vector3.left,Vector3.right,Vector3.up,Vector3.down})
                        Assert.IsTrue(shell.Raycast(new Ray(origin,direction),out _,48),"Visible structural hole at "+z+" branch "+branch+" direction "+direction);
                }
            }
            for(int chosen=-1;chosen<=1;chosen++)
            {
                Seek(game,132,chosen);yield return null;Physics.SyncTransforms();MuhanokMapChunk junction=null;
                foreach(var item in game.Map.Active)if(item.Chunk.Sequence==CartFork.LastChunk)junction=item.Chunk;
                var shell=junction.transform.Find("ThreeWayRails/ThreeUnknownTunnels").GetComponent<MeshCollider>();
                for(int branch=-1;branch<=1;branch++)
                {
                    bool capped=shell.Raycast(new Ray(new Vector3(branch*CartFork.Spacing,1.6f,CartFork.End-.5f),Vector3.forward),out _,1);
                    Assert.AreEqual(branch!=chosen,capped,"Only the chosen exit must remain open");
                }
                foreach(var item in game.Map.Active)if(item.Chunk.Sequence==CartFork.ConnectorChunk)
                {
                    Assert.IsNull(item.Chunk.transform.Find("ThreeWayRails"),"No extra cart corridor after the junction");
                    Assert.AreEqual(chosen*CartFork.Spacing,item.Chunk.Entry.position.x,.001f);
                }
            }
        }
        [UnityTest] public IEnumerator EveryRandomBiomeHasASealedConnectionAndContinuousThreeLaneFloor()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();
            for(int destination=-1;destination<=1;destination++)
            {
                Seek(game,156,BranchFor(game,destination));yield return null;Physics.SyncTransforms();
                Assert.AreEqual(destination<0?12:destination>0?0:7,RouteSurface.TypeAt(CartFork.DestinationChunk));
                MuhanokMapChunk connector=null;foreach(var item in game.Map.Active)if(item.Chunk.Sequence==CartFork.ConnectorChunk)connector=item.Chunk;
                Assert.IsNotNull(connector.GetComponentInChildren<RouteTransitionView>());
                foreach(float z in new[]{143.9f,144.1f,156f,167.9f,168.1f})
                {
                    RouteSurface.Sample(z,out float x,out float y);
                    for(int lane=-1;lane<=1;lane++)Assert.IsTrue(Physics.Raycast(new Vector3(x+lane*2.2f,y+1.7f,z),Vector3.down,out var hit,3),"Floor gap at biome boundary "+z);
                }
                var shell=connector.GetComponentInChildren<RouteTransitionView>().Surface;
                RouteSurface.Sample(150,out float center,out _);
                Assert.IsTrue(shell.Raycast(new Ray(new Vector3(center,1.6f,150),Vector3.up),out _,10));
                if(destination!=1)
                {
                    Seek(game,1140,BranchFor(game,destination));yield return null;Physics.SyncTransforms();
                    MuhanokMapChunk back=null;foreach(var item in game.Map.Active)if(item.Chunk.Sequence==47)back=item.Chunk;
                    Assert.IsNotNull(back.GetComponentInChildren<RouteTransitionView>());
                    RouteSurface.Sample(1150,out center,out _);Assert.IsTrue(back.GetComponentInChildren<RouteTransitionView>().Surface.Raycast(new Ray(new Vector3(center,1.6f,1150),Vector3.up),out _,10));
                }
            }
        }
        [UnityTest] public IEnumerator RandomBlockersAndBananasAlwaysLeaveAClearLaneAndResetInPools()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();int branch=ZooBranch(game);Seek(game,480,branch);
            int blocks=0,peels=0;
            for(int step=0;step<30;step++)
            {
                float z=480+step*12;game.Session.Runner.SetStartZ(z);game.Map.Tick(z);
                foreach(var entry in game.Map.Active)
                {
                    var chunk=entry.Chunk;int mask=0;
                    foreach(var block in chunk.GetComponentsInChildren<ThemedLaneBlock>())
                    {
                        blocks++;Assert.AreEqual(ObstacleKind.Barrier,block.Hazard.Kind);Assert.GreaterOrEqual(block.Hazard.CollisionHalfWidth,1.1f);
                        RouteSurface.Sample(block.transform.position.z,out float x,out _);mask|=1<<(Mathf.RoundToInt((block.transform.position.x-x)/2.2f)+1);
                        Assert.IsFalse(SafePatternValidator.Avoided(block.Hazard.Kind,2,true,true));
                    }
                    foreach(var item in chunk.DynamicFloorItems)
                    {
                        peels++;Assert.AreEqual(FloorItemKind.BananaPeel,item.Kind);
                        RouteSurface.Sample(item.transform.position.z,out float x,out _);mask|=1<<(Mathf.RoundToInt((item.transform.position.x-x)/2.2f)+1);
                    }
                    Assert.AreNotEqual(7,mask,"At least one lane must avoid both the blockers and all peels in a chunk");
                }
            }
            Assert.Greater(blocks,0);Assert.Greater(peels,15);
            game.Map.Reset();foreach(var entry in game.Map.Active)foreach(var item in entry.Chunk.DynamicFloorItems)Assert.IsFalse(item.Passed);
        }
        [UnityTest] public IEnumerator CaptureHasAnApproachThreeBonksAndThenResultWithoutAdvancingDistance()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,480,ZooBranch(game));
            float z=game.Session.Runner.Z;game.Session.Hit(true);
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);Assert.AreEqual(0,game.Capture.Strikes);
            game.enabled=true;yield return new WaitForSecondsRealtime(1.15f);
            Assert.AreEqual(GameFlowState.Caught,game.Session.Flow.State);Assert.AreEqual(z,game.Session.Runner.Z);
            Assert.AreEqual(1,game.Capture.ProneBlend,.001f);Assert.Less(Vector3.Dot(game.Hero.Visual.up,Vector3.up),.1f,"Captured monkey must be horizontal in a supported prone pose");
            yield return new WaitForSecondsRealtime(2.95f);
            Assert.AreEqual(3,game.Capture.Strikes);Assert.AreEqual(GameFlowState.Result,game.Session.Flow.State);
            game.Retry();Assert.AreEqual(0,game.Capture.Strikes);Assert.AreEqual(GameFlowState.TitleIdle,game.Session.Flow.State);
        }
        [UnityTest] public IEnumerator VisibleForkAllowsRealSideStepsAndLateChoiceBeforeCommitment()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,119,0);
            Assert.IsTrue(RouteSurface.ActivePlan.ForkWindow(119));Assert.LessOrEqual(game.Map.Spawned,CartFork.ConnectorChunk,"Hidden map must not spawn before the choice");
            var input=new PoseGameInput();var source=new PoseCommandSource(input,()=>game.Session.Runner.Lane,()=>false);source.Reset(0);
            foreach(int lane in new[]{-1,0,1})
            {
                source.Feed(new MotionReading{Lane=lane},lane+3);
                while(input.TryRead(out var command))game.Session.Runner.Command(command);
                game.Session.Runner.Tick(.25f,0);game.Hero.Apply(game.Session.Runner);game.Map.Tick(119);yield return null;
                Assert.AreEqual(lane,game.Session.Runner.Lane);Assert.AreEqual(lane*CartFork.Offset(119,1),game.Session.Runner.WorldLaneOffset,.001f);
                var cart=game.transform.Find("Actual track cart");Assert.AreEqual(lane*CartFork.Offset(119,1),cart.position.x,.001f);
                game.CameraRig.FollowCart(game.Hero.Head,1,0);Assert.AreEqual(cart.position.x,game.CameraRig.Chase.transform.position.x,.001f);
                Assert.AreEqual(int.MinValue,RouteSurface.ActivePlan.DestinationAt(6));
            }
            game.Map.Tick(119.99f);Assert.IsFalse(RouteSurface.ActivePlan.ForkLocked(119.99f));
            game.Map.Tick(120);game.Session.Runner.SetStartZ(120);game.Hero.Apply(game.Session.Runner);yield return null;
            Assert.AreEqual(1,RouteSurface.ActivePlan.ChoiceAt(6));Assert.AreEqual(2.2f,game.transform.Find("Actual track cart").position.x,.001f);
        }
        [UnityTest] public IEnumerator OnePeelShowsOnlySirenAlertAndCapturedFaceFollowsProneHead()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,480,ZooBranch(game));
            FloorItemController peel=null;foreach(var entry in game.Map.Active)if(entry.Chunk.DynamicFloorItems.Count>0){peel=entry.Chunk.DynamicFloorItems[0];break;}
            Assert.IsNotNull(peel);float z=peel.transform.position.z;RouteSurface.Sample(z,out float x,out _);
            int lane=Mathf.RoundToInt((peel.transform.position.x-x)/2.2f);game.Session.Runner.SetStartZ(z);
            while(game.Session.Runner.Lane!=lane)game.Session.Runner.Command(lane<game.Session.Runner.Lane?MotionCommand.Left:MotionCommand.Right);game.Session.Runner.Tick(.25f,0);
            peel.Evaluate(game.Session,z-1);peel.Evaluate(game.Session,z-1);yield return null;
            Assert.AreEqual(1,game.Session.BananaHits);Assert.IsTrue(game.PursuitCue.Visible);Assert.IsFalse(game.Capture.BatonVisible);
            yield return null;Assert.IsTrue(game.GetComponent<HUDPresenter>().RunModel.ShowSiren);
            Assert.IsNull(game.OutputCamera.transform.Find("NearPoliceHandAndBaton"),"The warning must not create camera-space geometry");
            game.Session.Paused=true;yield return null;Assert.IsFalse(game.PursuitCue.Visible);
            Assert.IsFalse(game.GetComponent<HUDPresenter>().RunModel.ShowSiren);
            game.Session.Paused=false;yield return null;Assert.IsTrue(game.PursuitCue.Visible);
            var avatar=game.Hero.GetComponent<FaceAvatarView>();avatar.Apply(Texture2D.whiteTexture);game.Hero.Apply(game.Session.Runner);
            game.Session.Hit(true);game.enabled=true;yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(game.PursuitCue.Visible);Assert.AreEqual(1,game.Capture.ProneBlend,.001f);
            var head=game.Hero.RigAnimator.GetBoneTransform(HumanBodyBones.Head);var badge=game.Hero.transform.Find("PlayerFaceBadge");
            Assert.IsTrue(badge.GetComponent<Renderer>().enabled);Assert.Less(Vector3.Distance(head.position,badge.position),.8f);
            Assert.Less(head.position.y,1.5f,"Head must lower with the prone body");
            game.Capture.Tick(5);game.Session.Flow.Transition(GameFlowState.Result);game.Retry();yield return null;
            Assert.IsTrue(game.Hero.RigAnimator.enabled);Assert.Greater(Vector3.Dot(game.Hero.Visual.up,Vector3.up),.99f);Assert.IsFalse(game.PursuitCue.Visible);
        }
        [UnityTest] public IEnumerator CustomFloorsHaveOneOwnerAndNormalFloorsDoNotDriftWhenReused()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();
            foreach(float z in new[]{132f,156f})
            {
                Seek(game,z,0);yield return null;
                foreach(var item in game.Map.Active)
                {
                    bool custom=RouteSurface.ActivePlan.IsForkTrackChunk(item.Chunk.Sequence)||item.Type==3;
                    var original=item.Chunk.transform.Find("RouteStructure");if(original==null)continue;
                    Assert.AreEqual(!custom,original.gameObject.activeSelf,"A replacement surface must hide the authored surface too");
                    if(!custom)foreach(var filter in original.GetComponentsInChildren<MeshFilter>())if(filter.name=="Editable route floor")Assert.AreEqual(-.025f,filter.transform.localPosition.y,.0001f);
                }
                float sample=z<CartFork.End?120.137f:150.137f;RouteSurface.Sample(sample,out float center,out _);
                MuhanokMapChunk under=null;foreach(var item in game.Map.Active)if(item.Chunk.Entry.position.z<=sample&&item.Chunk.Exit.position.z>sample)under=item.Chunk;
                Assert.AreEqual(1,VisibleFloorLayers(under,new Vector3(center,0,sample)),"Coplanar visible floors would cause depth flicker");
            }
            for(int i=0;i<3;i++)game.Map.Reset();
            foreach(var item in game.Map.Active)if(!RouteSurface.ActivePlan.IsForkTrackChunk(item.Chunk.Sequence))
                foreach(var filter in item.Chunk.transform.Find("RouteStructure").GetComponentsInChildren<MeshFilter>())if(filter.name=="Editable route floor")Assert.AreEqual(-.025f,filter.transform.localPosition.y,.0001f,"Surface offset must not accumulate on pool reuse");
        }
        [UnityTest] public IEnumerator HeroAndCartUseTheCommittedRouteInTheSameFrame()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,119.99f,-1);game.enabled=true;yield return new WaitUntil(()=>game.Session.Runner.Z>=120);
            Assert.IsTrue(RouteSurface.ActivePlan.RailChoiceLocked(game.Session.Runner.Z));RouteSurface.Sample(game.Session.Runner.Z,out float x,out _);
            Assert.AreEqual(x,game.Hero.transform.position.x,.001f,"Do not render one frame on the old route when the choice commits");
        }
        private static int VisibleFloorLayers(MuhanokMapChunk chunk,Vector3 p)
        {
            int layers=0;
            foreach(var filter in chunk.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||filter.sharedMesh==null||!filter.sharedMesh.isReadable)continue;
                var v=filter.sharedMesh.vertices;var t=filter.sharedMesh.triangles;var matrix=filter.transform.localToWorldMatrix;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=matrix.MultiplyPoint3x4(v[t[i]]),b=matrix.MultiplyPoint3x4(v[t[i+1]]),c=matrix.MultiplyPoint3x4(v[t[i+2]]);
                    Vector3 normal=Vector3.Cross(b-a,c-a);if(normal.y<.000001f)continue;
                    float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.000001f)continue;
                    float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/den;
                    float w=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/den;
                    if(u<0||w<0||u+w>1)continue;
                    float height=u*a.y+w*b.y+(1-u-w)*c.y;
                    if(Mathf.Abs(height-p.y)<.001f)layers++;
                }
            }
            return layers;
        }
        [UnityTest] public IEnumerator ForkRecyclingRestoresAuthoredCartAndDoesNotGrowThePool()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,2,0);int created=game.Map.CreatedCount;
            for(int i=0;i<100;i++)
            {
                float z=i*24+2;game.Session.Runner.SetStartZ(z);game.Map.Tick(z);
                for(int k=1;k<game.Map.Active.Count;k++)Assert.Less(Vector3.Distance(game.Map.Active[k-1].Chunk.Exit.position,game.Map.Active[k].Chunk.Entry.position),.001f,"Chosen-route sockets must stay joined across laps");
                foreach(var active in game.Map.Active)
                {
                    var env=active.Chunk.transform.Find("EnvironmentRoot");if(env==null)continue;
                    bool fork=RouteSurface.ActivePlan.IsForkTrackChunk(RouteSurface.SequenceAt(active.Chunk.Entry.position.z));
                    if(active.Type==1||active.Type==2)Assert.AreEqual(!fork,env.gameObject.activeSelf);
                }
            }
            Assert.AreEqual(created,game.Map.CreatedCount);
        }
        [UnityTest] public IEnumerator FaceBadgeAttachesAndHidesWithTheCharacter()
        {
            yield return Load();var game=Object.FindFirstObjectByType<GameBootstrap>();Seek(game,132,0);yield return null;
            var avatar=game.Hero.GetComponent<FaceAvatarView>();avatar.Apply(Texture2D.whiteTexture);yield return null;
            var badge=game.Hero.transform.Find("PlayerFaceBadge");Assert.IsNotNull(badge);
            Assert.AreSame(Texture2D.whiteTexture,badge.GetComponent<Renderer>().sharedMaterial.mainTexture);
            game.Hero.SetVisible(false);game.GetComponent<CartRideView>().enabled=false;yield return null;
            Assert.IsFalse(badge.GetComponent<Renderer>().enabled);
            avatar.Apply(null);Assert.IsFalse(badge.gameObject.activeSelf);
        }
    }
}
