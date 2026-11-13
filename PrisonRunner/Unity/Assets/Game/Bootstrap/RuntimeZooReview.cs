#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Muhanok.Domain;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using Unity.Cinemachine;
using UnityEngine;
namespace Muhanok.Bootstrap
{
    // Explicit visual QA only. No real camera, face requests, saved profiles or leaderboard writes.
    public sealed class RuntimeZooReview : MonoBehaviour
    {
        [Serializable] private sealed class Result {public bool passed;public string error,gpu;public int kinds,width,height;public float maximumAttachmentError;}
        private GameBootstrap game;private string output;private bool finished;private float began;
        private readonly Result result=new Result();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--mongscape-zoo-review")>=0)new GameObject("Zoo visual review").AddComponent<RuntimeZooReview>();
        }
        private void Awake()
        {
            began=Time.realtimeSinceStartup;UnityEngine.Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--review-output");
            output=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.Combine(UnityEngine.Application.persistentDataPath,"ZooReview");
            Directory.CreateDirectory(output);UnityEngine.Application.logMessageReceived+=Log;
        }
        private void Log(string message,string stack,LogType kind){if(kind==LogType.Exception&&!finished)Finish(message);}
        private IEnumerator Start()
        {
            yield return null;game=FindFirstObjectByType<GameBootstrap>();if(game==null){Finish("Bootstrap missing");yield break;}
            var settings=((ISettingsHost)game).Settings;settings.ApplyPreset(QualityPreset.Balanced);settings.Fullscreen=false;
            settings.PerformanceOverlay=false;settings.AutoPerformance=false;settings.MusicVolume=settings.EffectsVolume=0;game.ApplySettings(false);
            Screen.SetResolution(1280,720,false);
            game.StartPlayerRun(new PlayerProfile{Name="Zoo QA",Weight="70",Style=PlayStyle.Keyboard});
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing);game.enabled=false;
            result.gpu=SystemInfo.graphicsDeviceName;result.width=Screen.width;result.height=Screen.height;
            float[] targets={-1,-1,-1};ResetMap();
            for(float z=180;z<1000;z+=24)
            {
                game.Session.Runner.SetStartZ(z);game.Map.Tick(z);
                foreach(var chunk in game.Map.Active)foreach(var item in chunk.Chunk.Animated)
                {
                    int kind=Kind(item);if(kind<0)continue;var component=(Component)item;
                    if(targets[kind]<0)targets[kind]=component.transform.position.z;
                }
                if(targets[0]>=0&&targets[1]>=0&&targets[2]>=0)break;
            }
            string[] names={"Giraffe","Elephant","FallingTree"};
            for(int kind=0;kind<3;kind++)
            {
                if(targets[kind]<0){Finish("Missing encounter "+names[kind]);yield break;}
                ResetMap();float target=targets[kind];
                for(float z=2;z<target-38;z+=12){game.Session.Runner.SetStartZ(z);game.Map.Tick(z);}
                foreach(float distance in new[]{38f,23f,7f})
                {
                    Frame(target-distance);yield return new WaitForSecondsRealtime(.4f);
                    string phase=distance>30?"Warning":distance>14?"Moving":"Blocking";
                    yield return Capture(names[kind]+"_"+phase);
                }
                ZooHazard animal=null;FallenTreeHazard tree=null;
                foreach(var chunk in game.Map.Active)foreach(var item in chunk.Chunk.Animated)
                    if(Kind(item)==kind&&Mathf.Abs(((Component)item).transform.position.z-target)<.01f){animal=item as ZooHazard;tree=item as FallenTreeHazard;}
                if(animal!=null)
                {
                    float error=Vector3.Distance(animal.Attachment.position,animal.Reach.Base);result.maximumAttachmentError=Mathf.Max(error,result.maximumAttachmentError);
                    if(error>.01f){Finish("Disconnected animal "+names[kind]);yield break;}
                }
                else if(tree==null||tree.Progress<.99f){Finish("Tree did not finish falling");yield break;}
                var brain=game.OutputCamera.GetComponent<CinemachineBrain>();brain.enabled=false;
                var hud=game.GetComponent<HUDPresenter>();hud.Hidden=true;
                RouteSurface.Sample(target,out float x,out float y);
                game.OutputCamera.transform.position=new Vector3(x-4.5f,y+3.5f,target-9);
                game.OutputCamera.transform.LookAt(new Vector3(x+2.5f,y+1.8f,target));
                yield return new WaitForSecondsRealtime(.3f);yield return Capture(names[kind]+"_Connected_Side");
                brain.enabled=true;hud.Hidden=false;result.kinds++;
            }
            Finish(null);
        }
        private static int Kind(IRunnerAnimated item)=>item is ZooHazard animal?(animal.Giraffe?0:1):item is FallenTreeHazard?2:-1;
        private void ResetMap()
        {
            RoutePlan plan=null;
            for(int lane=-1;lane<=1;lane++)
            {
                var p=new RoutePlan(game.Session.Config,723);p.UpdateChoice(120,lane);if(p.DestinationAt(6)<0){plan=p;break;}
            }
            if(plan==null)throw new InvalidOperationException("Zoo route missing");
            RouteSurface.UsePlan(plan);game.Session.Reset();game.Session.Runner.SetStartZ(2);game.Map.Reset(723);
        }
        private void Frame(float z)
        {
            game.Session.Runner.SetStartZ(z);game.Map.Tick(z);game.Map.AnimateGates(z);game.Hero.Apply(game.Session.Runner);
            game.CameraRig.Follow(game.Hero.Head,false,1);game.Session.Score.Advance(Mathf.Max(0,z-game.Session.Score.Distance));
        }
        private IEnumerator Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;}
        private void Update(){if(!finished&&Time.realtimeSinceStartup-began>100)Finish("Review timeout");}
        private void Finish(string error)
        {
            if(finished)return;finished=true;result.error=error;result.passed=string.IsNullOrEmpty(error)&&result.kinds==3;
            File.WriteAllText(Path.Combine(output,"review.json"),JsonUtility.ToJson(result,true));
            Debug.Log(result.passed?"MONGSCAPE_ZOO_REVIEW_PASS":"MONGSCAPE_ZOO_REVIEW_FAIL: "+error);UnityEngine.Application.Quit(result.passed?0:1);
        }
        private void OnDestroy(){UnityEngine.Application.logMessageReceived-=Log;}
    }
}
#endif
