using System;
using System.IO;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Cinemachine;

namespace Muhanok.Editor
{
    [InitializeOnLoad]
    public static class MuhanokArtReview
    {
        private const string Key="Muhanok.ArtReview";
        private static GameBootstrap game;
        private static GameObject review;
        static MuhanokArtReview()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key,false))
                { SessionState.SetBool(Key,false); Debug.Log("MUHANOK_ART_REVIEW_PASS: title/idea/takeover/first-person/ordered route maps and boundaries"); if(UnityEngine.Application.isBatchMode) EditorApplication.Exit(0); }
            };
        }
        [MenuItem("Mongscape/Capture All Maps and Boundaries")]
        public static void Run()
        {
            MuhanokArtMaintenance.CleanRouteMeshes();
            MuhanokGameViewFit.Fit();
            SessionState.SetBool(Key,true); SessionState.SetInt(Key+"Step",0);
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity"); EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||Time.frameCount<120) return;
            if(game==null) game=UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(); if(game==null) return;
            int step=SessionState.GetInt(Key+"Step",0);
            if(step==0) { Capture("Title",game.OutputCamera); game.StartIntro(); Advance(step); }
            else if(step==1&&game.Intro.Elapsed>=3.35f) { Capture("Idea",game.OutputCamera); Advance(step); }
            else if(step==2&&game.Intro.Elapsed>=7.2f) { Capture("Takeover",game.OutputCamera); Advance(step); }
            else if(step==3&&game.Session.Score.Distance>=16)
            {
                Capture("FirstPerson",game.OutputCamera);
                game.OutputCamera.GetComponent<CinemachineBrain>().enabled=false;
                game.OutputCamera.GetComponent<WorldAtmosphere>().enabled=false;
                game.gameObject.SetActive(false); GameObject.Find("Mine Title Stage").SetActive(false);
                review=new GameObject("Art Review Stage"); Advance(step);
            }
            else if(step>=4&&step<4+RouteSurface.SequenceLength*2)
            {
                int sequence=(step-4)/2,index=RouteSurface.TypeAt(sequence); bool boundary=(step-4)%2==1;
                if(!boundary)
                {
                    for(int i=review.transform.childCount-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(review.transform.GetChild(i).gameObject);
                    for(int n=0;n<2;n++)
                    {
                        int type=RouteSurface.TypeAt(sequence+n);
                        var chunk=UnityEngine.Object.Instantiate(game.ChunkPrefabs[type],review.transform);
                        chunk.transform.localPosition=new Vector3(0,RouteSurface.EntryHeight(index)+(n==1?RouteSurface.Rise(index):0),24*n); chunk.Prepare(index+1+n,new HazardOdds(.85f,.72f,.85f,.82f),new System.Random(723));
                    }
                    bool outdoor=index>=11,mine=index<4;
                    RenderSettings.fogColor=WorldAtmosphere.Fog(index);
                    RenderSettings.fogDensity=outdoor?.011f:.027f;
                    game.OutputCamera.clearFlags=outdoor?CameraClearFlags.Skybox:CameraClearFlags.SolidColor; game.OutputCamera.backgroundColor=RenderSettings.fogColor;
                    RenderSettings.ambientLight=WorldAtmosphere.Ambient(index);
                    var fill=GameObject.Find("Soft Fill").GetComponent<Light>(); fill.intensity=outdoor?.45f:.9f; fill.color=outdoor?new Color(.42f,.57f,1):new Color(.92f,.88f,.80f); fill.shadows=outdoor?LightShadows.Soft:LightShadows.None;
                }
                float z=boundary?20:2.5f;
                var position=new Vector3(RouteSurface.LocalX(index,z),RouteSurface.EntryHeight(index)+RouteSurface.LocalHeight(index,z)+1.65f,z);
                var tangent=new Vector3(RouteSurface.LocalX(index,z+.25f)-RouteSurface.LocalX(index,z),RouteSurface.LocalHeight(index,z+.25f)-RouteSurface.LocalHeight(index,z),.25f);
                game.OutputCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(tangent));
                var atmosphere=game.OutputCamera.GetComponent<WorldAtmosphere>(); if(atmosphere.Moon!=null) atmosphere.Moon.gameObject.SetActive(index>=11);
                Capture(MuhanokContentBuilder.ChunkIds[index]+(boundary?"_Boundary":""),game.OutputCamera); Advance(step);
            }
            else if(step>=4+RouteSurface.SequenceLength*2) EditorApplication.isPlaying=false;
        }
        private static void Advance(int step)=>SessionState.SetInt(Key+"Step",step+1);
        private static void Capture(string id,Camera camera)
        {
            var rt=new RenderTexture(1280,720,GraphicsFormat.R8G8B8A8_SRGB,CoreUtils.GetDefaultDepthOnlyFormat()); rt.Create();
            var rect=camera.rect; camera.rect=new Rect(0,0,1,1); camera.aspect=16f/9f;
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt,mipLevel=0,slice=0,face=CubemapFace.Unknown}); camera.rect=rect;
            var old=RenderTexture.active; RenderTexture.active=rt; var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
            string destination=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Builds/QA/VisualReview/ArtV5/"+id+".png")); Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllBytes(destination,texture.EncodeToPNG());
            RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("ART_CAPTURE: "+id);
        }
    }
}
