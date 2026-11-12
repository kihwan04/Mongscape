#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Bootstrap
{
    // Only runs with an explicit CLI flag; captures the actual player frame including HUD.
    public sealed class RuntimeVisualReview : MonoBehaviour
    {
        private GameBootstrap game;
        private int step;
        private float started;
        private bool capturing;
        private string destination;
        private bool maps;
        private string suffix;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--muhanok-capture")<0) return;
            new GameObject("Player Visual Review").AddComponent<RuntimeVisualReview>();
        }
        private void Start()
        {
            UnityEngine.Application.runInBackground=true;
            game=FindFirstObjectByType<GameBootstrap>(); started=Time.realtimeSinceStartup;
            destination=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../../Builds/QA/VisualReview/ArtV10"));
            maps=Array.IndexOf(Environment.GetCommandLineArgs(),"--muhanok-mapcapture")>=0;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--muhanok-detailcapture")>=0){maps=true;step=35;}
            suffix="_"+Screen.width+"x"+Screen.height;
            Directory.CreateDirectory(destination);
        }
        private void Update()
        {
            if(Time.realtimeSinceStartup-started>120) { Debug.LogError("MUHANOK_PLAYER_CAPTURE_TIMEOUT"); UnityEngine.Application.Quit(1); return; }
            if(game!=null&&game.Session!=null&&game.enabled&&!game.IsLoading&&!game.IsRevealing)Drive();
            if(capturing||game==null||game.Session==null) return;
            if(step==0&&Time.realtimeSinceStartup-started>3) StartCoroutine(Capture("Player_Title"));
            else if(step==1&&game.StartSetupOpen) StartCoroutine(Capture("Player_WeightSetup"));
            else if(step==2&&game.IsLoading) StartCoroutine(Capture("Player_Loading"));
            else if(step==3&&game.IsRevealing&&game.GetComponent<Muhanok.Presentation.HUDPresenter>().RevealOpacity<.80f) StartCoroutine(Capture("Player_LoadingReveal"));
            else if(step==4&&!game.IsRevealing&&game.Session.Flow.State==GameFlowState.Running)StartCoroutine(Capture("Player_FirstLiveFrame"));
            else if(step==5&&game.Session.Runner.Riding&&RouteSurface.CartProgress(game.Session.Runner.Z)>.15f)StartCoroutine(Capture("Player_CartRide"));
            else if(step==6&&game.Session.Runner.Riding&&RouteSurface.CartProgress(game.Session.Runner.Z)>.40f)StartCoroutine(Capture("Player_CartBank"));
            else if(step==7&&game.Session.Runner.Z>=76&&!game.Session.Runner.Riding)StartCoroutine(Capture("Player_CartExit"));
            else if(step>=8&&step<28&&maps)StartCoroutine(CaptureMap((step-8)/2,(step-8)%2==1));
            else if(step==28&&maps)StartCoroutine(CaptureEntrance(109,"Player_ClosingGate"));
            else if(step==29&&maps)StartCoroutine(CaptureEntrance(114.5f,"Player_SlideUnderGate"));
            else if(step==30&&maps)StartCoroutine(CaptureEntrance(123.5f,"Player_PrisonEntry"));
            else if(step==31&&maps)StartCoroutine(CaptureSlip());
            else if(step==32&&maps)StartCoroutine(CaptureBananaCatch());
            else if(step==33&&maps)StartCoroutine(CaptureRailCrash());
            else if(step==34&&maps&&game.Session.Flow.State==GameFlowState.Result)StartCoroutine(Capture("Player_GameOver"));
            else if(step==35&&maps)StartCoroutine(CaptureDetail("Player_CellCloseup",false));
            else if(step==36&&maps)StartCoroutine(CaptureDetail("Player_CharacterContact",true));
            else if(step==37&&maps)StartCoroutine(CapturePause());
        }
        private void Drive()
        {
            if(game.Session.Flow.State!=GameFlowState.Running)return;
            bool[] blocked=new bool[3];float z=game.Session.Runner.Z;
            if(game.Session.Runner.Riding)
            {
                Muhanok.Presentation.Map.CartRailGap closest=null;
                foreach(var active in game.Map.Active)foreach(var gap in active.Chunk.RailGaps)
                    if(gap.gameObject.activeInHierarchy&&!gap.Passed&&gap.transform.position.z>z&&(closest==null||gap.transform.position.z<closest.transform.position.z))closest=gap;
                if(closest!=null)game.Session.Runner.Command(closest.RequiredLean<0?MotionCommand.Left:MotionCommand.Right);
                return;
            }
            foreach(var active in game.Map.Active)foreach(var obstacle in active.Chunk.Obstacles)
            {
                float at=obstacle.transform.position.z;
                if(!obstacle.gameObject.activeSelf||obstacle.Passed||at-z<.05f||at-z>10)continue;
                RouteSurface.Sample(at,out float offset,out _);
                int lane=Mathf.RoundToInt((obstacle.transform.position.x-offset)/2.2f)+1;
                if(lane>=0&&lane<3)blocked[lane]=true;
                if(at-z<4&&obstacle.Kind==ObstacleKind.Crate||at-z<4&&obstacle.Kind==ObstacleKind.Cart)game.Session.Runner.Command(MotionCommand.Jump);
                if(at-z<5&&obstacle.Kind==ObstacleKind.Pipe)game.Session.Runner.Command(MotionCommand.Crouch);
                if(at-z<5&&obstacle.Kind==ObstacleKind.Stair)game.Session.Runner.Command(MotionCommand.KneeStep);
            }
            foreach(var active in game.Map.Active)foreach(var item in active.Chunk.FloorItems)
            {
                float d=item.transform.position.z-z;if(item.Passed||d<0||d>6)continue;
                RouteSurface.Sample(item.transform.position.z,out float offset,out _);int lane=Mathf.RoundToInt((item.transform.position.x-offset)/2.2f)+1;
                if(lane>=0&&lane<3)blocked[lane]=true;
            }
            int desired=game.Session.Runner.Lane;
            if(blocked[desired+1])for(int lane=-1;lane<=1;lane++)if(!blocked[lane+1]){desired=lane;break;}
            if(desired!=game.Session.Runner.Lane)game.Session.Runner.Command(desired<game.Session.Runner.Lane?MotionCommand.Left:MotionCommand.Right);
        }
        private IEnumerator CaptureMap(int index,bool boundary)
        {
            capturing=true; float target=index*24+(boundary?20:3);
            while(game.Session.Runner.Z<target) {Drive();float prior=game.Session.Runner.Z;game.Session.Tick(.02f);game.Map.Tick(game.Session.Runner.Z);game.Map.Evaluate(prior);if(game.Session.Flow.State!=GameFlowState.Running)throw new InvalidOperationException("Capture driver hit a solid hazard");}
            game.Hero.Apply(game.Session.Runner);if(game.Session.Runner.Riding)game.CameraRig.FollowCart(game.Hero.Head,1,-game.Session.Runner.CartLean*12);else game.CameraRig.Follow(game.Hero.Head,game.Session.Runner.Crouching,1);game.Map.AnimateGates(game.Session.Runner.Z);
            yield return new WaitForSecondsRealtime(.8f);
            yield return Capture("Player_Map_"+index.ToString("00")+(boundary?"_Boundary":""));
        }
        private IEnumerator CaptureSlip()
        {
            capturing=true;game.Session.Reset();game.Map.Reset();game.Map.Tick(225);
            game.Session.Runner.Command(MotionCommand.Left);game.Session.Runner.Tick(.25f,0);game.Session.Runner.SetStartZ(225.5f);
            float prior=game.Session.Runner.Z;game.Session.Tick(.1f);game.Map.Evaluate(prior);
            if(!game.Session.Runner.Slowing)throw new InvalidOperationException("Authored banana did not slow actual player");
            game.Hero.Apply(game.Session.Runner);game.CameraRig.Follow(game.Hero.Head,false,1);
            yield return new WaitForSecondsRealtime(.2f);yield return Capture("Player_Slip");
        }
        private IEnumerator CaptureEntrance(float target,string name)
        {
            capturing=true;game.enabled=false;game.Session.Reset();game.Map.Reset();
            while(game.Session.Runner.Z<target)
            {
                Drive();float prior=game.Session.Runner.Z;game.Session.Tick(.02f);game.Map.Tick(game.Session.Runner.Z);game.Map.Evaluate(prior);
                if(game.Session.Flow.State!=GameFlowState.Running)throw new InvalidOperationException("Entrance driver failed actual motion/rail checks");
            }
            game.Hero.Apply(game.Session.Runner);game.CameraRig.Follow(game.Hero.Head,game.Session.Runner.Crouching,1);game.Map.AnimateGates(game.Session.Runner.Z);
            yield return new WaitForSecondsRealtime(.8f);yield return Capture(name);
        }
        private IEnumerator CaptureBananaCatch()
        {
            capturing=true;
            Muhanok.Presentation.Map.FloorItemController banana=null;
            foreach(var active in game.Map.Active)if(active.Type==12)banana=active.Chunk.FloorItems[0];
            banana.ResetItem();game.Session.Runner.SetStartZ(banana.transform.position.z);banana.Evaluate(game.Session,banana.transform.position.z-1);
            var actors=FindObjectsByType<Muhanok.Presentation.RunnerView>(FindObjectsSortMode.None);
            for(int i=0;i<13;i++)
            {
                game.Session.Tick(.1f);game.Hero.Apply(game.Session.Runner);
                if(game.Session.Flow.State==GameFlowState.Running)foreach(var actor in actors)if(actor!=game.Hero)
                {actor.Pose(new Vector3(game.Session.Runner.X,0,game.Session.Runner.Z-Mathf.Lerp(15,1.8f,game.Session.Chase.Pressure)),true);actor.Play("AN_Guard_ChaseRun");}
                yield return null;
            }
            if(game.Session.Flow.State!=GameFlowState.Caught||game.Session.Reason!=CaptureReason.SlipPursuit)throw new InvalidOperationException("Two actual banana contacts did not cause pursuit capture");
            game.enabled=true;yield return new WaitForSecondsRealtime(.40f);yield return Capture("Player_BananaCatch");
        }
        private IEnumerator CaptureRailCrash()
        {
            capturing=true;
            if(game.Session.Flow.State==GameFlowState.Caught)game.Session.Flow.Transition(GameFlowState.Result);
            game.Retry();game.BeginRun("");yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            game.Map.Tick(30);game.Session.Runner.SetStartZ(30);
            Muhanok.Presentation.Map.CartRailGap target=null;
            foreach(var active in game.Map.Active)if(active.Type==1)foreach(var gap in active.Chunk.RailGaps)if(gap.gameObject.activeSelf)target=gap;
            game.Session.Runner.Command(target.RequiredLean<0?MotionCommand.Right:MotionCommand.Left);game.Session.Runner.Tick(.25f,0);
            game.Session.Runner.SetStartZ(target.transform.position.z);target.Evaluate(game.Session,target.transform.position.z-1);
            if(game.Session.Reason!=CaptureReason.BrokenRail)throw new InvalidOperationException("Wrong cart lean did not cause rail fall");
            game.Hero.Apply(game.Session.Runner);game.CameraRig.FollowCart(game.Hero.Head,1,-game.Session.Runner.CartLean*12);
            game.enabled=true;yield return new WaitForSecondsRealtime(.15f);yield return Capture("Player_RailCrash");
        }
        private IEnumerator Capture(string name)
        {
            capturing=true; yield return null;
            string path=Path.Combine(destination,name+suffix+".png"); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.5f);
            if(!File.Exists(path)) { Debug.LogError("MUHANOK_PLAYER_CAPTURE_MISSING: "+path); UnityEngine.Application.Quit(1); yield break; }
            Debug.Log("PLAYER_CAPTURE: "+name); step++; capturing=false;
            if(step==1) game.OpenStartSetup();
            if(step==2) game.BeginRun("70");
            if(step==8&&maps)
            {
                game.enabled=false; game.Session.Reset(); game.Map.Reset();
            }
            if(step==(maps?38:8)) { Debug.Log("MUHANOK_PLAYER_CAPTURE_PASS"); UnityEngine.Application.Quit(0); }
        }
        private IEnumerator CaptureDetail(string name,bool character)
        {
            capturing=true;
            if(game.Session.Flow.State==GameFlowState.Result)game.Retry();
            if(game.Session.Flow.State==GameFlowState.TitleIdle)
            {
                game.enabled=true;game.BeginRun("");yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);
            }
            game.enabled=false;game.GetComponent<Muhanok.Presentation.HUDPresenter>().enabled=false;
            game.GetComponent<Muhanok.Presentation.CartRideView>().enabled=false;
            game.Session.Reset();game.Map.Reset();game.Session.Runner.SetStartZ(character?12:126);game.Map.Tick(game.Session.Runner.Z);
            game.Hero.Apply(game.Session.Runner);game.Hero.SetVisible(character);
            foreach(var actor in FindObjectsByType<Muhanok.Presentation.RunnerView>(FindObjectsSortMode.None))if(actor!=game.Hero)actor.SetVisible(false);
            foreach(var hands in FindObjectsByType<Muhanok.Presentation.FirstPersonHands>(FindObjectsSortMode.None))hands.gameObject.SetActive(false);
            game.CameraRig.Select(game.CameraRig.IntroA);
            Vector3 position,target;
            if(character)
            {
                position=new Vector3(2.3f,1.1f,14.5f);target=new Vector3(0,.9f,12);game.Hero.Play("AN_Monkey_Run");
                var animator=game.Hero.GetComponentInChildren<Animator>();var feet=animator.GetComponent<Muhanok.Presentation.FootGrounding>();
                for(int frame=0;frame<20;frame++){animator.Play("AN_Monkey_Run",0,frame/20f);animator.Update(0);if(feet.ContactCount>0)break;}
                Debug.Log("CHARACTER_CONTACT_REVIEW: "+feet.ContactCount+" planted feet / residual penetration "+feet.MaximumPenetration);animator.speed=0;
            }
            else {position=new Vector3(-2.75f,4.65f,123.9f);target=new Vector3(-4.7f,4.2f,125.9f);}
            game.CameraRig.IntroA.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));game.CameraRig.IntroA.Lens.FieldOfView=character?52:59;
            yield return new WaitForSecondsRealtime(.9f);yield return Capture(name);
        }
        private IEnumerator CapturePause()
        {
            capturing=true;game.Hero.SetVisible(false);
            game.GetComponent<Muhanok.Presentation.HUDPresenter>().enabled=true;
            foreach(var hands in FindObjectsByType<Muhanok.Presentation.FirstPersonHands>(FindObjectsInactive.Include,FindObjectsSortMode.None))hands.gameObject.SetActive(true);
            game.CameraRig.Follow(game.Hero.Head,false,1);game.TogglePause();
            yield return null;yield return Capture("Player_Pause");
        }
    }
}
#endif
