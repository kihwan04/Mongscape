#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Muhanok.Domain;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UIElements;
namespace Muhanok.Bootstrap
{
    // Opt-in local review only. Normal play never installs this driver and it never saves player scores/profiles.
    [DefaultExecutionOrder(-100)]
    public sealed class RuntimeExpansionReview : MonoBehaviour
    {
        [Serializable] private sealed class Sample { public string preset,area;public int frames;public float fps,meanMs,p95Ms,worstMs,renderScale;public int cap; }
        [Serializable] private sealed class Report { public string device,gpu,cameraStatus,started;public int width,height,cameraFrames;public bool passed;public string error;public List<Sample> samples=new List<Sample>(); }
        private GameBootstrap game; private string destination,error;private float started;private bool driving,detailsOnly;
        private readonly Report report=new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--mongscape-review")<0)return;
            new GameObject("Mongscape expansion review").AddComponent<RuntimeExpansionReview>();
        }
        private void Awake()
        {
            started=Time.realtimeSinceStartup;UnityEngine.Application.runInBackground=true;
            destination=Path.Combine(UnityEngine.Application.persistentDataPath,"ExpansionReview");
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--review-output");if(at>=0&&at+1<args.Length)destination=Path.GetFullPath(args[at+1]);
            detailsOnly=Array.IndexOf(args,"--review-details")>=0;
            Directory.CreateDirectory(destination);report.device=SystemInfo.deviceModel;report.gpu=SystemInfo.graphicsDeviceName;report.started=DateTime.Now.ToString("O");
            UnityEngine.Application.logMessageReceived+=Log;
        }
        private void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Exception && error==null)error=message;
        }
        private IEnumerator Start()
        {
            yield return null;game=FindFirstObjectByType<GameBootstrap>();if(game==null){Finish("Bootstrap missing");yield break;}
            var host=(ISettingsHost)game;
            host.Settings.ApplyPreset(QualityPreset.Balanced);host.Settings.Fullscreen=false;host.Settings.MusicVolume=0;host.Settings.EffectsVolume=0;host.Settings.PerformanceOverlay=false;game.ApplySettings(false);
            Screen.SetResolution(1280,720,false);((IHudData)game).PreviewStyle(PlayStyle.Keyboard);
            yield return new WaitForSecondsRealtime(2);report.width=Screen.width;report.height=Screen.height;
            yield return Capture("01_Title");
            game.enabled=false;
            yield return PointAt(new Vector2(640,650),.95f);
            if(!game.MenuOpen){Finish("Synthetic hand could not open title menu");yield break;}
            yield return Capture("19_Hand_Menu");
            yield return PointAt(new Vector2(1020,510),.95f);
            var window=FindFirstObjectByType<SettingsWindow>();
            if(window==null||!window.IsOpen){Finish("Synthetic hand could not open settings");yield break;}
            yield return Capture("20_Settings_Auto_Performance");
            var root=window.GetComponent<UIDocument>().rootVisualElement;
            yield return PointAt(ElementCenter(root,root.Q<Button>("tab-body")),.95f);
            yield return PointAt(ElementCenter(root,root.Q<Button>("phone-camera-guide")),.95f);
            if(root.Q<VisualElement>("phone-guide").resolvedStyle.display==DisplayStyle.None){Finish("Synthetic hand could not open phone guide");yield break;}
            yield return Capture("21_Phone_Camera_Guide");
            window.Close(false);GesturePointer.Reset();
            game.OpenStartSetup();yield return new WaitForSecondsRealtime(.3f);yield return Capture("02_Setup");game.enabled=true;
            game.StartPlayerRun(new PlayerProfile{Name="QA",Style=PlayStyle.Keyboard,Weight="70",CalorieGoal=50});
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            yield return new WaitForSecondsRealtime(2.5f);
            Seek(116,0);yield return new WaitForSecondsRealtime(.8f);yield return Capture("03_Three_Unknown_Paths");
            var player=((IHudData)game).Player;player.Style=PlayStyle.FullBody;
            game.Session.Workout.SetWeight("70");game.Session.Workout.SetGoal(50);game.Session.Workout.Met=PlayStyleInfo.Met(PlayStyle.FullBody);game.Session.Workout.TickActivity(60,true,true,true,0);
            yield return new WaitForSecondsRealtime(.2f);yield return Capture("22_Workout_Synthetic_4_9kcal");player.Style=PlayStyle.Keyboard;game.Session.Workout.Met=PlayStyleInfo.Met(PlayStyle.Keyboard);
            for(int branch=-1;branch<=1;branch++)
            {
                Seek(132,branch);yield return new WaitForSecondsRealtime(.8f);yield return Capture("03_Fork_"+(branch+1));
                RouteSurface.Sample(132,out float x,out _);
                var cart=game.transform.Find("Actual track cart");if(cart==null||Mathf.Abs(cart.position.x-x)>.03f){Finish("Cart route mismatch");yield break;}
            }
            Seek(119,0);
            foreach(int lane in new[]{-1,0,1})
            {
                game.Session.Runner.Command(lane<game.Session.Runner.Lane?MotionCommand.Left:lane>game.Session.Runner.Lane?MotionCommand.Right:MotionCommand.Center);
                game.Session.Runner.Tick(.3f,0);game.Hero.Apply(game.Session.Runner);game.CameraRig.FollowCart(game.Hero.Head,1,0);
                yield return new WaitForSecondsRealtime(.5f);yield return Capture("14_Fork_Sidestep_"+(lane+1));
            }
            foreach(var preset in new[]{QualityPreset.Balanced,QualityPreset.Low,QualityPreset.High})
            {
                if(detailsOnly)break;
                host.Settings.ApplyPreset(preset);host.Settings.Fullscreen=false;host.Settings.PerformanceOverlay=true;game.ApplySettings(false);
                foreach(float z in new[]{132f,340f,580f,950f})
                {
                    Seek(z,z==132?0:BranchFor(z==340?0:z==580?1:-1));game.Session.Score.Advance(Mathf.Max(0,z-2));driving=true;game.enabled=true;
                    yield return new WaitForSecondsRealtime(2);
                    var frames=new List<float>(700);float until=Time.realtimeSinceStartup+7;
                    while(Time.realtimeSinceStartup<until&&error==null)
                    {
                        frames.Add(Time.unscaledDeltaTime*1000);yield return null;
                    }
                    driving=false;game.enabled=false;
                    if(game.Session.Flow.State!=GameFlowState.Running){Finish("Review driver collision in "+RouteSurface.AreaAt(game.Session.Runner.Z));yield break;}
                    var sample=new Sample{preset=preset.ToString(),area=z==132?"Fork":z==340?"Prison":z==580?"Mine":"Zoo",frames=frames.Count,cap=host.Settings.FrameCap,renderScale=host.Settings.RenderScale};
                    float total=0;foreach(float f in frames)total+=f;frames.Sort();sample.meanMs=total/Mathf.Max(1,frames.Count);sample.fps=1000/sample.meanMs;
                    sample.p95Ms=frames[Mathf.Clamp((int)(frames.Count*.95f),0,frames.Count-1)];sample.worstMs=frames[frames.Count-1];report.samples.Add(sample);
                    yield return Capture("04_"+preset+"_"+sample.area);
                }
            }
            host.Settings.PerformanceOverlay=false;game.ApplySettings(false);
            for(int destinationType=-1;destinationType<=1;destinationType++)
            {
                Seek(140,BranchFor(destinationType));yield return new WaitForSecondsRealtime(.6f);yield return Capture("15_Rail_Exit_"+(destinationType+1));
                Seek(150,BranchFor(destinationType));yield return new WaitForSecondsRealtime(.6f);yield return Capture("16_Biome_Entry_"+(destinationType+1));
                Seek(176,BranchFor(destinationType));yield return new WaitForSecondsRealtime(.6f);yield return Capture("17_Biome_Start_"+(destinationType+1));
            }
            Seek(RoutePlan.ExtendedLapChunks*24-12,BranchFor(-1));yield return new WaitForSecondsRealtime(.6f);yield return Capture("18_Outdoor_Return_To_Mine");
            Seek(480,BranchFor(-1));
            foreach(var active in game.Map.Active)
            {
                foreach(var animal in active.Chunk.GetComponentsInChildren<ZooHazard>())
                {
                    FrameAt(animal.transform.position.z-6);yield return new WaitForSecondsRealtime(.5f);
                    yield return Capture(animal.Giraffe?"07_Giraffe_Jump":"06_Elephant_Jump");
                }
            }
            for(int theme=0;theme<3;theme++)
            {
                Seek(theme==0?580:theme==1?340:480,BranchFor(theme==2?-1:theme==0?1:0));
                bool found=false;
                for(int step=0;step<12&&!found;step++)
                {
                    foreach(var active in game.Map.Active)
                    {
                        foreach(var block in active.Chunk.GetComponentsInChildren<ThemedLaneBlock>())
                        {
                            if(block.Theme!=theme)continue;
                            FrameAt(block.transform.position.z-7);yield return new WaitForSecondsRealtime(.6f);
                            yield return Capture("09_LaneBlock_"+theme);found=true;break;
                        }
                        if(found)break;
                    }
                    if(!found)game.Map.Tick((theme==0?580:theme==1?340:480)+(step+1)*12);
                }
            }
            game.GetComponent<HUDPresenter>().ShowIdentity();yield return new WaitForSecondsRealtime(.2f);yield return Capture("08_FaceLogin_Menu");game.GetComponent<HUDPresenter>().CloseSetup();
            host.Settings.ApplyPreset(QualityPreset.Balanced);game.ApplySettings(false);Seek(132,0);
            var portrait=new Texture2D(32,32,TextureFormat.RGB24,false);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)portrait.SetPixel(x,y,(x/8+y/8)%2==0?new Color(1,.7f,.2f):new Color(.2f,.65f,1));portrait.Apply();
            game.Hero.GetComponent<FaceAvatarView>().Apply(portrait);
            var brain=game.OutputCamera.GetComponent<CinemachineBrain>();brain.enabled=false;
            game.OutputCamera.transform.position=game.Hero.Head+game.Hero.transform.forward*2.3f;
            game.OutputCamera.transform.LookAt(game.Hero.Head);game.GetComponent<HUDPresenter>().Hidden=true;
            yield return new WaitForSecondsRealtime(.7f);yield return Capture("05_FaceBadge_TestPattern");
            game.Hero.GetComponent<FaceAvatarView>().Apply(null);Destroy(portrait);brain.enabled=true;game.GetComponent<HUDPresenter>().Hidden=false;
            bool deferCamera=Array.IndexOf(Environment.GetCommandLineArgs(),"--review-no-camera")>=0;
            if(!deferCamera)game.Pose.SetStyle(PlayStyle.UpperBody);double last=0;float probeUntil=Time.realtimeSinceStartup+(deferCamera?0:8);
            while(Time.realtimeSinceStartup<probeUntil)
            {
                game.Pose.Tick(Time.realtimeSinceStartupAsDouble,false);
                if(game.Pose.LastFrame.Time!=last){report.cameraFrames++;last=game.Pose.LastFrame.Time;}
                yield return null;
            }
            report.cameraStatus=deferCamera?"Deferred by user":game.Pose.Status+": "+game.Pose.Message;game.Pose.SetStyle(PlayStyle.Keyboard);
            Seek(480,BranchFor(-1));game.Session.Slip(SlipSource.Banana);
            yield return new WaitForSecondsRealtime(.4f);yield return Capture("13_Banana_Siren");
            var caughtPortrait=new Texture2D(32,32,TextureFormat.RGB24,false);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)caughtPortrait.SetPixel(x,y,(x/8+y/8)%2==0?new Color(1,.7f,.2f):new Color(.2f,.65f,1));caughtPortrait.Apply();
            game.Hero.GetComponent<FaceAvatarView>().Apply(caughtPortrait);
            game.Session.Hit(true);game.enabled=true;
            yield return new WaitForSecondsRealtime(.55f);yield return Capture("10_Police_Approach");
            yield return new WaitForSecondsRealtime(1.5f);yield return Capture("11_Police_Cartoon_Bonks");
            yield return new WaitForSecondsRealtime(1.9f);
            if(game.Capture.Strikes!=3||game.Session.Flow.State!=GameFlowState.Result){Finish("Capture sequence did not complete");yield break;}
            yield return Capture("12_Result");game.enabled=false;
            game.Hero.GetComponent<FaceAvatarView>().Apply(null);Destroy(caughtPortrait);
            // Review settings are never saved. Keep the current window mode through shutdown;
            // restoring fullscreen just before Quit can dispatch a Win32 callback after Unity tears its window down.
            yield return new WaitForSecondsRealtime(.8f);
            Finish(error);
        }
        private int BranchFor(int destinationType)
        {
            for(int branch=-1;branch<=1;branch++)
            {
                var plan=new RoutePlan(game.Session.Config,723);plan.UpdateChoice(144,branch);
                if(plan.DestinationAt(6)==destinationType)return branch;
            }
            throw new InvalidOperationException("Missing hidden destination");
        }
        private void FrameAt(float z)
        {
            game.Session.Runner.SetStartZ(z);game.Hero.Apply(game.Session.Runner);game.Map.AnimateGates(z);
            if(game.Session.Runner.Riding)game.CameraRig.FollowCart(game.Hero.Head,1,0);else game.CameraRig.Follow(game.Hero.Head,false,1);
        }
        private void Seek(float z,int branch)
        {
            game.enabled=false;RouteSurface.UsePlan(new RoutePlan(game.Session.Config,723));game.Session.Reset();game.Map.Reset();
            game.Session.Runner.Command(branch<0?MotionCommand.Left:branch>0?MotionCommand.Right:MotionCommand.Center);game.Session.Runner.Tick(.25f,0);
            for(float at=2;at<z;at+=12)game.Map.Tick(at);game.Map.Tick(z);game.Session.Runner.SetStartZ(z);game.Hero.Apply(game.Session.Runner);game.Map.AnimateGates(z);
            if(game.Session.Runner.Riding)game.CameraRig.FollowCart(game.Hero.Head,1,-game.Session.Runner.CartLean*12);else game.CameraRig.Follow(game.Hero.Head,false,1);
        }
        private IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(destination,name+".png"));yield return null;yield return null;
        }
        private void Update()
        {
            if(Time.realtimeSinceStartup-started>240){Finish("Review timeout");return;}
            if(!driving||game==null)return;
            var run=game.Session;float z=run.Runner.Z,speed=Mathf.Max(1,run.CurrentSpeed);
            if(run.Runner.Riding)
            {
                if(RouteSurface.ActivePlan.ForkWindow(z)){run.Runner.Command(MotionCommand.Center);return;}
                foreach(var active in game.Map.Active)foreach(var gap in active.Chunk.RailGaps)
                    if(gap.gameObject.activeInHierarchy&&!gap.Passed&&gap.transform.position.z>=z&&gap.transform.position.z-z<speed*.8f)run.Runner.Command(gap.RequiredLean<0?MotionCommand.Left:MotionCommand.Right);
                return;
            }
            float row=float.PositiveInfinity;
            foreach(var active in game.Map.Active)
            {
                FindRow(active.Chunk.Obstacles,z,speed,ref row);FindRow(active.Chunk.ExerciseObstacles,z,speed,ref row);FindRow(active.Chunk.DynamicObstacles,z,speed,ref row);
                FindFloorRow(active.Chunk.FloorItems,z,speed,ref row);FindFloorRow(active.Chunk.DynamicFloorItems,z,speed,ref row);
            }
            int blocked=0;
            foreach(var active in game.Map.Active)
            {
                MaskRow(active.Chunk.Obstacles,row,ref blocked);MaskRow(active.Chunk.ExerciseObstacles,row,ref blocked);MaskRow(active.Chunk.DynamicObstacles,row,ref blocked);
                MaskFloorRow(active.Chunk.FloorItems,row,ref blocked);MaskFloorRow(active.Chunk.DynamicFloorItems,row,ref blocked);
            }
            if((blocked&(1<<(run.Runner.Lane+1)))!=0)
            {
                int best=run.Runner.Lane,distance=4;
                for(int lane=-1;lane<=1;lane++)if((blocked&(1<<(lane+1)))==0&&Mathf.Abs(lane-run.Runner.Lane)<distance){best=lane;distance=Mathf.Abs(lane-run.Runner.Lane);}
                if(best!=run.Runner.Lane)run.Runner.Command(best<run.Runner.Lane?MotionCommand.Left:MotionCommand.Right);
            }
            foreach(var active in game.Map.Active)
            {
                DriveHazards(active.Chunk.Obstacles,z,speed);DriveHazards(active.Chunk.ExerciseObstacles,z,speed);DriveHazards(active.Chunk.DynamicObstacles,z,speed);
            }
        }
        private static void FindRow(IEnumerable<ObstacleController> hazards,float z,float speed,ref float row)
        {
            foreach(var h in hazards)if(h.Kind==ObstacleKind.Barrier&&h.gameObject.activeInHierarchy&&!h.Passed&&h.transform.position.z>=z&&h.transform.position.z-z<speed*1.1f)row=Mathf.Min(row,h.transform.position.z);
        }
        private static void FindFloorRow(IEnumerable<FloorItemController> items,float z,float speed,ref float row)
        {
            if(items==null)return;foreach(var h in items)if(h.gameObject.activeInHierarchy&&!h.Passed&&h.transform.position.z>=z&&h.transform.position.z-z<speed*1.1f)row=Mathf.Min(row,h.transform.position.z);
        }
        private static int LaneMask(Transform t){RouteSurface.Sample(t.position.z,out float x,out _);return 1<<Mathf.Clamp(1+Mathf.RoundToInt((t.position.x-x)/2.2f),0,2);}
        private static void MaskRow(IEnumerable<ObstacleController> hazards,float row,ref int mask)
        {
            foreach(var h in hazards)if(h.Kind==ObstacleKind.Barrier&&h.gameObject.activeInHierarchy&&!h.Passed&&Mathf.Abs(h.transform.position.z-row)<1)mask|=LaneMask(h.transform);
        }
        private static void MaskFloorRow(IEnumerable<FloorItemController> items,float row,ref int mask)
        {
            if(items==null)return;foreach(var h in items)if(h.gameObject.activeInHierarchy&&!h.Passed&&Mathf.Abs(h.transform.position.z-row)<1)mask|=LaneMask(h.transform);
        }
        private void DriveHazards(IEnumerable<ObstacleController> hazards,float z,float speed)
        {
            foreach(var hazard in hazards)
            {
                if(!hazard.gameObject.activeSelf||hazard.Passed)continue;float d=hazard.transform.position.z-z;if(d<0||d>speed*.3f)continue;
                var run=game.Session;RouteSurface.Sample(hazard.transform.position.z,out float x,out _);
                if(!hazard.FullExerciseRow&&Mathf.Abs(run.Runner.Lane*2.2f+x-hazard.transform.position.x)>1.1f)continue;
                if(hazard.Kind==ObstacleKind.Crate||hazard.Kind==ObstacleKind.Cart){if(run.Runner.Y<=.001f)run.Runner.Command(MotionCommand.Jump);}
                else if(hazard.Kind==ObstacleKind.Pipe)run.Runner.Command(MotionCommand.Crouch);
                else if(hazard.Kind==ObstacleKind.Stair)run.Runner.Command(MotionCommand.KneeStep);
            }
        }
        private static Vector2 ElementCenter(VisualElement root,VisualElement element)
        {
            var b=element.worldBound;var viewport=ViewportLayout.Current;float scale=viewport.width/1280f;
            return new Vector2((b.center.x/root.layout.width*Screen.width-viewport.x)/scale,(b.center.y/root.layout.height*Screen.height-(Screen.height-viewport.yMax))/scale);
        }
        private static IEnumerator PointAt(Vector2 position,float seconds)
        {
            GesturePointer.Reset();float until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until)
            {
                var f=new PoseFrame{HasPose=true,Time=Time.realtimeSinceStartupAsDouble,Aspect=4f/3};
                f.Points[11]=new PoseLandmark{X=.4f,Y=.4f,Visibility=1};f.Points[12]=new PoseLandmark{X=.6f,Y=.4f,Visibility=1};
                f.Points[14]=new PoseLandmark{X=.6f,Y=.5f,Visibility=1};
                f.Points[16]=new PoseLandmark{X=.5f+(position.x/1280-.5f)*.56f,Y=.4f-.2f*(4f/3)*1.25f+position.y/720*(.2f*(4f/3)*2.1f),Visibility=1};
                GesturePointer.Feed(f,true,f.Time);yield return null;
            }
            GesturePointer.Reset();yield return null;
        }
        private void Finish(string failure)
        {
            report.error=failure;report.passed=string.IsNullOrEmpty(failure);File.WriteAllText(Path.Combine(destination,detailsOnly?"details.json":"review.json"),JsonUtility.ToJson(report,true));
            Debug.Log(report.passed?"MONGSCAPE_EXPANSION_REVIEW_PASS":"MONGSCAPE_EXPANSION_REVIEW_FAIL: "+failure);enabled=false;UnityEngine.Application.Quit(report.passed?0:1);
        }
        private void OnDestroy(){UnityEngine.Application.logMessageReceived-=Log;}
    }
}
#endif
