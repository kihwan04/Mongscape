#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Muhanok.Domain;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace Muhanok.Bootstrap
{
    // Opt-in video demonstration. Sends ordinary commands; never seeks, shields or edits the run rules.
    [DefaultExecutionOrder(-150)]
    public sealed class RuntimeGameplayRecording : MonoBehaviour
    {
        [Serializable] private sealed class Chapter {public string name;public float seconds,distance;}
        [Serializable] private sealed class Result
        {
            public bool passed,automaticKeyboard=true,teleports=false,realCamera=false;
            public string error,gpu,capture="Unity final game framebuffer + game audio listener";
            public int frames,fps=30,width,height,animalsSeen,bananaHits,encoderExit;
            public float distance,seconds;public List<Chapter> chapters=new List<Chapter>();
        }
        private readonly Result result=new Result();
        private GameBootstrap game;private Process encoder;private Task<string> encoderError;
        private RecordingAudioTap audioTap;
        private RenderTexture target;private Texture2D image;private byte[] pixels;
        private string output,encoderPath,failure;private bool recording,finishing,driving,pilot;
        private double started,nextFrame;private float firstSlip=-1,deadline;
        private int previousType=-1;private FloorItemController deliberatePeel;
        private GUIStyle caption;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--mongscape-recording")>=0)new GameObject("Gameplay recording").AddComponent<RuntimeGameplayRecording>();
        }
        private static string Argument(string key)
        {var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;}
        private IEnumerator Start()
        {
            output=Path.GetFullPath(Argument("--record-output")??Path.Combine(UnityEngine.Application.persistentDataPath,"GameplayRecording"));Directory.CreateDirectory(output);
            var dataPath=Argument("--muhanok-data");
            if(string.IsNullOrEmpty(dataPath)||!Path.GetFullPath(dataPath).StartsWith(output+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            {Fail("Recording needs an isolated PlayerData directory inside its output folder");yield break;}
            encoderPath=Argument("--record-encoder");pilot=Array.IndexOf(Environment.GetCommandLineArgs(),"--record-pilot")>=0;
            if(string.IsNullOrEmpty(encoderPath)||!File.Exists(encoderPath)){Fail("Video encoder missing");yield break;}
            UnityEngine.Application.runInBackground=true;yield return null;
            game=FindFirstObjectByType<GameBootstrap>();if(game==null){Fail("Bootstrap missing");yield break;}
            var settings=((ISettingsHost)game).Settings;settings.ApplyPreset(QualityPreset.Balanced);settings.FrameCap=30;settings.Fullscreen=false;
            settings.AutoPerformance=false;settings.HandMenus=false;settings.PerformanceOverlay=false;settings.MusicVolume=.22f;settings.EffectsVolume=.7f;game.ApplySettings(false);
            game.SetDeveloperMode(false,false);var data=(IHudData)game;
            data.SaveProfile(new PlayerProfile{Name="PLAY DEMO",Weight="70",Style=PlayStyle.Keyboard,CalorieGoal=50});data.PreviewStyle(PlayStyle.Keyboard);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(1);
            try{StartCapture();}catch(Exception e){Fail(e.Message);yield break;}
            ChapterAt("타이틀");yield return new WaitForSecondsRealtime(2);
            game.OpenMainMenu();ChapterAt("메인 메뉴");yield return new WaitForSecondsRealtime(2);
            game.OpenStartSetup();ChapterAt("키보드 플레이 준비");yield return new WaitForSecondsRealtime(2);
            game.BeginRun("70");
            yield return new WaitUntil(()=>!game.IsLoading&&!game.IsRevealing&&game.Session.Flow.State==GameFlowState.Running);
            ChapterAt("플레이 시작");driving=true;
            while(game.Session.Flow.State==GameFlowState.Running&&!finishing)
            {
                if(pilot&&Time.realtimeSinceStartupAsDouble-started>18){driving=false;yield return Finish();yield break;}
                yield return null;
            }
            driving=false;
            if(finishing)yield break;
            ChapterAt("경찰에게 잡힘");
            if(result.animalsSeen!=7||game.Session.SlipStrikes<2||game.Session.Reason!=CaptureReason.SlipPursuit)failure="Run ended before all zoo encounters and pursuit after two real banana contacts";
            yield return new WaitUntil(()=>game.Session.Flow.State==GameFlowState.Result);
            ChapterAt("결과 화면");yield return new WaitForSecondsRealtime(4);yield return Finish();
        }
        private void StartCapture()
        {
            result.width=Screen.width;result.height=Screen.height;result.gpu=SystemInfo.graphicsDeviceName;
            target=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
            image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);pixels=new byte[Screen.width*Screen.height*3];
            var start=new ProcessStartInfo{FileName=encoderPath,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardError=true,
                Arguments="-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgb24 -video_size "+Screen.width+"x"+Screen.height+" -framerate 30 -i pipe:0 -c:v libx264 -preset veryfast -crf 23 -threads 3 -pix_fmt yuv420p -movflags +faststart \""+Path.Combine(output,"video-silent.mp4")+"\""};
            encoder=Process.Start(start);encoderError=encoder.StandardError.ReadToEndAsync();
            var listener=FindFirstObjectByType<AudioListener>();if(listener==null)throw new InvalidOperationException("Audio listener missing");
            audioTap=listener.gameObject.AddComponent<RecordingAudioTap>();audioTap.Begin(Path.Combine(output,"game-audio.wav"),AudioSettings.outputSampleRate);
            started=nextFrame=Time.realtimeSinceStartupAsDouble;deadline=Time.realtimeSinceStartup+600;recording=true;StartCoroutine(CaptureFrames());
        }
        private IEnumerator CaptureFrames()
        {
            var end=new WaitForEndOfFrame();
            while(recording)
            {
                yield return end;if(!recording)yield break;
                double now=Time.realtimeSinceStartupAsDouble;if(now<nextFrame)continue;
                try
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(target);var old=RenderTexture.active;RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);RenderTexture.active=old;image.GetRawTextureData<byte>().CopyTo(pixels);
                    while(nextFrame<=now){encoder.StandardInput.BaseStream.Write(pixels,0,pixels.Length);result.frames++;nextFrame+=1.0/30;}
                }
                catch(Exception e){Fail("Capture: "+e.Message);yield break;}
            }
        }
        private void ChapterAt(string name)
        {
            result.chapters.Add(new Chapter{name=name,seconds=(float)(Time.realtimeSinceStartupAsDouble-started),distance=game?.Session.Runner.Z??0});
            Debug.Log("GAMEPLAY_RECORDING_CHAPTER: "+name+" · "+Mathf.RoundToInt(game?.Session.Runner.Z??0)+"m");
        }
        private void Update()
        {
            if(recording&&Time.realtimeSinceStartup>deadline){Fail("Recording timeout");return;}
            if(!driving||finishing||game==null||game.Session.Flow.State!=GameFlowState.Running||game.Session.Paused)return;
            var run=game.Session;float z=run.Runner.Z,speed=Mathf.Max(1,run.CurrentSpeed);
            int type=RouteSurface.TypeAt(RouteSurface.SequenceAt(z));
            if(type!=previousType)
            {
                if(type==0&&(previousType<0||previousType>3))ChapterAt("광산");
                else if(type==1&&previousType==0)ChapterAt("수레 철로");
                else if(type==7)ChapterAt("감옥 진입 · 하이니");
                else if(type==4&&previousType!=5)ChapterAt("감옥 구간");
                else if(type==12&&previousType!=12)ChapterAt("동물원 구간");previousType=type;
            }
            int before=result.animalsSeen;
            foreach(var chunk in game.Map.Active)foreach(var animated in chunk.Chunk.Animated)
            {
                if(!(animated is Component component))continue;float d=component.transform.position.z-z;if(d<0||d>22)continue;
                if(animated is ZooHazard animal)result.animalsSeen|=animal.Giraffe?1:2;
                else if(animated is FallenTreeHazard)result.animalsSeen|=4;
            }
            if((before^result.animalsSeen)!=0)ChapterAt((result.animalsSeen^before)==1?"기린 목 장애물":(result.animalsSeen^before)==2?"코끼리 코 장애물":"쓰러지는 나무");
            if(run.SlipStrikes>result.bananaHits){result.bananaHits=run.SlipStrikes;firstSlip=z;deliberatePeel=null;ChapterAt(run.SlipStrikes==1?"바나나 접촉 · 사이렌 경고":"두 번째 바나나 · 경찰 추격");}
            if(run.Runner.Riding)
            {
                var plan=RouteSurface.ActivePlan;
                if(plan.ForkWindow(z))
                {
                    int lap=plan.CycleAt(RouteSurface.SequenceAt(z)),destination=lap==0?0:-1,choice=0;
                    for(int lane=-1;lane<=1;lane++){var probe=new RoutePlan(run.Config,723);probe.UpdateChoice(lap*RoutePlan.ExtendedLapChunks*RouteSurface.Length+120,lane);if(probe.DestinationAt(lap*RoutePlan.ExtendedLapChunks+6)==destination){choice=lane;break;}}
                    run.Runner.Command(choice<0?MotionCommand.Left:choice>0?MotionCommand.Right:MotionCommand.Center);return;
                }
                foreach(var chunk in game.Map.Active)foreach(var gap in chunk.Chunk.RailGaps)
                    if(gap.gameObject.activeInHierarchy&&!gap.Passed&&gap.transform.position.z>=z&&gap.transform.position.z-z<speed*.8f)run.Runner.Command(gap.RequiredLean<0?MotionCommand.Left:MotionCommand.Right);
                return;
            }
            bool seekBanana=result.animalsSeen==7&&z>900&&run.SlipStrikes<2&&(run.SlipStrikes==0||run.StrikeClearRemaining>1.5f);// the second contact must land before the first strike clears
            if(deliberatePeel!=null&&(deliberatePeel.Passed||!deliberatePeel.gameObject.activeInHierarchy||deliberatePeel.transform.position.z<z))deliberatePeel=null;
            if(seekBanana&&deliberatePeel==null)
                foreach(var chunk in game.Map.Active)foreach(var peel in chunk.Chunk.DynamicFloorItems)
                    if(peel.Kind==FloorItemKind.BananaPeel&&!peel.Passed&&peel.gameObject.activeInHierarchy&&peel.transform.position.z-z>1&&peel.transform.position.z-z<7){deliberatePeel=peel;break;}
            float row=float.PositiveInfinity;
            foreach(var chunk in game.Map.Active)
            {
                FindRow(chunk.Chunk.Obstacles,z,speed,ref row);FindRow(chunk.Chunk.ExerciseObstacles,z,speed,ref row);FindRow(chunk.Chunk.DynamicObstacles,z,speed,ref row);
                FindFloor(chunk.Chunk.FloorItems,z,speed,ref row);FindFloor(chunk.Chunk.DynamicFloorItems,z,speed,ref row);
            }
            int mask=0;
            foreach(var chunk in game.Map.Active)
            {
                Mask(chunk.Chunk.Obstacles,row,ref mask);Mask(chunk.Chunk.ExerciseObstacles,row,ref mask);Mask(chunk.Chunk.DynamicObstacles,row,ref mask);
                MaskFloor(chunk.Chunk.FloorItems,row,ref mask);MaskFloor(chunk.Chunk.DynamicFloorItems,row,ref mask);
            }
            int desired=run.Runner.Lane;
            if((mask&(1<<(desired+1)))!=0)
                for(int lane=-1;lane<=1;lane++)if((mask&(1<<(lane+1)))==0){desired=lane;break;}
            if(deliberatePeel!=null&&!deliberatePeel.Passed&&deliberatePeel.transform.position.z>z&&deliberatePeel.transform.position.z-z<6)
            {int lane=Lane(deliberatePeel.transform);if((mask&(1<<(lane+1)))==0)desired=lane;}
            if(desired!=run.Runner.Lane)run.Runner.Command(desired<run.Runner.Lane?MotionCommand.Left:MotionCommand.Right);
            foreach(var chunk in game.Map.Active){Actions(chunk.Chunk.Obstacles,z,speed);Actions(chunk.Chunk.ExerciseObstacles,z,speed);Actions(chunk.Chunk.DynamicObstacles,z,speed);}
        }
        private static int Lane(Transform item){RouteSurface.Sample(item.position.z,out float x,out _);return Mathf.Clamp(Mathf.RoundToInt((item.position.x-x)/2.2f),-1,1);}
        private static void FindRow(IEnumerable<ObstacleController> items,float z,float speed,ref float row)
        {foreach(var h in items)if(h.Kind==ObstacleKind.Barrier&&h.gameObject.activeInHierarchy&&!h.Passed&&h.transform.position.z>=z&&h.transform.position.z-z<speed*1.1f)row=Mathf.Min(row,h.transform.position.z);}
        private void FindFloor(IEnumerable<FloorItemController> items,float z,float speed,ref float row)
        {if(items==null)return;foreach(var h in items)if(h!=deliberatePeel&&h.gameObject.activeInHierarchy&&!h.Passed&&h.transform.position.z>=z&&h.transform.position.z-z<speed*1.1f)row=Mathf.Min(row,h.transform.position.z);}
        private static void Mask(IEnumerable<ObstacleController> items,float row,ref int mask)
        {foreach(var h in items)if(h.Kind==ObstacleKind.Barrier&&h.gameObject.activeInHierarchy&&!h.Passed&&Mathf.Abs(h.transform.position.z-row)<1)mask|=1<<(Lane(h.transform)+1);}
        private void MaskFloor(IEnumerable<FloorItemController> items,float row,ref int mask)
        {if(items==null)return;foreach(var h in items)if(h!=deliberatePeel&&h.gameObject.activeInHierarchy&&!h.Passed&&Mathf.Abs(h.transform.position.z-row)<1)mask|=1<<(Lane(h.transform)+1);}
        private void Actions(IEnumerable<ObstacleController> items,float z,float speed)
        {
            foreach(var h in items)
            {
                float d=h.transform.position.z-z;if(h.Passed||!h.gameObject.activeInHierarchy||d<0||d>speed*.3f||(!h.FullExerciseRow&&Lane(h.transform)!=game.Session.Runner.Lane))continue;
                if(h.Kind==ObstacleKind.Crate||h.Kind==ObstacleKind.Cart){if(game.Session.Runner.Y<=.001f)game.Session.Runner.Command(MotionCommand.Jump);}
                else if(h.Kind==ObstacleKind.Pipe)game.Session.Runner.Command(MotionCommand.Crouch);
                else if(h.Kind==ObstacleKind.Stair)game.Session.Runner.Command(MotionCommand.KneeStep);
            }
        }
        private IEnumerator Finish()
        {
            if(finishing)yield break;finishing=true;driving=false;recording=false;audioTap?.End();encoder?.StandardInput.Close();
            while(encoder!=null&&!encoder.HasExited)yield return null;
            result.encoderExit=encoder?.ExitCode??-1;result.seconds=result.frames/30f;result.distance=game.Session.Runner.Z;
            if(encoderError!=null)File.WriteAllText(Path.Combine(output,"encoder.log"),encoderError.Result);
            result.error=failure;result.passed=string.IsNullOrEmpty(failure)&&result.encoderExit==0;
            File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(result,true));
            Debug.Log(result.passed?"MONGSCAPE_RECORDING_PASS":"MONGSCAPE_RECORDING_FAIL: "+failure);UnityEngine.Application.Quit(result.passed?0:1);
        }
        private void Fail(string message){failure=message;if(recording)StartCoroutine(Finish());else{Debug.LogError(message);UnityEngine.Application.Quit(1);}}
        private void OnGUI()
        {
            if(!recording||Event.current.type!=EventType.Repaint)return;
            caption??=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold};var old=GUI.matrix;GUI.matrix=ViewportLayout.GuiMatrix;
            GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(14,682,174,24),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(20,684,168,22),"자동 키보드 데모",caption);GUI.matrix=old;
        }
        private void OnDestroy()
        {
            recording=false;audioTap?.End();if(encoder!=null&&!encoder.HasExited){try{encoder.StandardInput.Close();}catch{}}
            if(image!=null)Destroy(image);if(target!=null){target.Release();Destroy(target);}
        }
    }
    public sealed class RecordingAudioTap : MonoBehaviour
    {
        private readonly object gate=new object();private FileStream file;private byte[] bytes=new byte[32768];private int sampleRate,channels=2;private long count;
        public void Begin(string path,int rate){lock(gate){sampleRate=rate;file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read,65536);file.Write(new byte[44],0,44);}}
        private void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(gate)
            {
                if(file==null)return;channels=channelCount;int length=data.Length*2;if(bytes.Length<length)bytes=new byte[length];
                for(int i=0;i<data.Length;i++){short value=(short)(Math.Max(-1,Math.Min(1,data[i]))*32767);bytes[i*2]=(byte)value;bytes[i*2+1]=(byte)(value>>8);}
                file.Write(bytes,0,length);count+=length;
            }
        }
        public void End()
        {
            lock(gate)
            {
                if(file==null)return;file.Position=0;
                using(var writer=new BinaryWriter(file)){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write((int)(count+36));writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)channels);writer.Write(sampleRate);writer.Write(sampleRate*channels*2);writer.Write((short)(channels*2));writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write((int)count);}file=null;
            }
        }
        private void OnDestroy()=>End();
    }
}
#endif
