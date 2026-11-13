using System.Collections.Generic;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Rendering;
namespace Muhanok.Presentation.Map
{
    // Authored obstacles remain separate. This replaces only the incorrect mine-to-prison scenery when entering another biome.
    public sealed class RouteTransitionView : MonoBehaviour
    {
        private static readonly Dictionary<int,Mesh> surfaces=new Dictionary<int,Mesh>(),frames=new Dictionary<int,Mesh>();
        private static Material timber;
        private Transform environment;private bool restore;
        private MeshFilter floor,frame;private Light lamp;private Camera camera;
        public MeshCollider Surface { get; private set; }
        public static RouteTransitionView Attach(MuhanokMapChunk chunk)
        {
            var view=chunk.GetComponentInChildren<RouteTransitionView>(true);
            if(view==null)
            {
                var root=new GameObject("BiomeConnection");root.transform.SetParent(chunk.transform,false);view=root.AddComponent<RouteTransitionView>();view.Create();
            }
            view.Configure(chunk);view.gameObject.SetActive(true);return view;
        }
        private void Create()
        {
            floor=gameObject.AddComponent<MeshFilter>();gameObject.AddComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;Surface=gameObject.AddComponent<MeshCollider>();
            var structure=new GameObject("CoveredEntranceAndRailTermination");structure.transform.SetParent(transform,false);
            frame=structure.AddComponent<MeshFilter>();structure.AddComponent<MeshRenderer>();
            if(timber==null)timber=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.34f,.22f,.12f)};
            frame.GetComponent<Renderer>().sharedMaterial=timber;
            lamp=new GameObject("EntranceLamp").AddComponent<Light>();lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(0,4.5f,12);
            lamp.type=LightType.Point;lamp.range=16;lamp.intensity=4;lamp.shadows=LightShadows.None;camera=Camera.main;
        }
        private void Configure(MuhanokMapChunk chunk)
        {
            int previous=RouteSurface.TypeAt(Mathf.Max(0,chunk.Sequence-1)),next=RouteSurface.TypeAt(chunk.Sequence+1);
            bool fromOutside=RouteSurface.IsOutdoor(previous),toOutside=RouteSurface.IsOutdoor(next);
            bool fromFork=RouteSurface.ActivePlan?.IsForkTrackChunk(chunk.Sequence-1)??false;
            int key=(fromFork?1000:0)+(fromOutside?100:0)+(toOutside?10:0)+(next==7?1:0);
            if(!surfaces.TryGetValue(key,out var mesh)||mesh==null)surfaces[key]=mesh=BuildShell(fromOutside,toOutside,next==7,fromFork);
            if(!frames.TryGetValue(key,out var beams)||beams==null)frames[key]=beams=BuildFrames(fromOutside,toOutside,next==7,previous==1||previous==2,fromFork);
            floor.sharedMesh=mesh;Surface.sharedMesh=mesh;frame.sharedMesh=beams;
            GetComponent<Renderer>().sharedMaterial=RouteMaterials.Rock(chunk);
            environment=chunk.transform.Find("EnvironmentRoot");restore=environment!=null&&environment.gameObject.activeSelf;
            if(environment!=null)environment.gameObject.SetActive(false);
            foreach(var h in chunk.Obstacles)h.gameObject.SetActive(false);
            foreach(var h in chunk.ExerciseObstacles)h.gameObject.SetActive(false);
            if(chunk.FloorItems!=null)foreach(var item in chunk.FloorItems)item.gameObject.SetActive(false);
            if(chunk.Coins!=null)foreach(var coin in chunk.Coins){var p=coin.transform.position;p.x=chunk.transform.position.x;coin.transform.position=p;}
            lamp.color=toOutside?new Color(.7f,.83f,1):new Color(1,.7f,.4f);
        }
        private static float Width(float z,bool fromOutside,bool toOutside,bool prison,bool fromFork)
        {
            if(fromFork)return Mathf.Lerp(RailForkView.ExitHalfWidth,toOutside?22:prison?8.4f:5.9f,Mathf.SmoothStep(0,1,z/24));
            if(fromOutside)return Mathf.Lerp(22,5.9f,Mathf.SmoothStep(0,1,z/12));
            if(toOutside)return Mathf.Lerp(5.9f,22,Mathf.SmoothStep(0,1,(z-12)/12));
            return Mathf.Lerp(5.9f,prison?8.4f:5.9f,Mathf.SmoothStep(0,1,z/24));
        }
        private static float Height(float z,bool fromFork)=>fromFork?Mathf.Lerp(RailForkView.ExitHeight,6.1f,Mathf.SmoothStep(0,1,z/12)):6.1f;
        private static Mesh BuildShell(bool fromOutside,bool toOutside,bool prison,bool fromFork)
        {
            var mesh=new RouteMesh();
            for(int i=0;i<24;i++)
            {
                float w=Width(i,fromOutside,toOutside,prison,fromFork),n=Width(i+1,fromOutside,toOutside,prison,fromFork);
                float h=Height(i,fromFork),nh=Height(i+1,fromFork);
                mesh.Strip(i,i+1,-w,w,-n,n,-.35f,0,-.35f,0);
                for(int side=-1;side<=1;side+=2)mesh.Strip(i,i+1,side*w-.2f,side*w+.2f,side*n-.2f,side*n+.2f,-.35f,h,-.35f,nh);
                bool covered=fromOutside?i>=12:toOutside?i<12:true;
                if(covered)mesh.Strip(i,i+1,-w,w,-n,n,h,h+.3f,nh,nh+.3f);
            }
            return mesh.Build("ContinuousBiomeConnection");
        }
        private static Mesh BuildFrames(bool fromOutside,bool toOutside,bool prison,bool cart,bool fromFork)
        {
            var mesh=new RouteMesh();
            foreach(float z in new[]{.15f,12f,23.85f})
            {
                float width=fromFork?Mathf.Min(5.2f,Width(z,fromOutside,toOutside,prison,true)-.25f):fromOutside&&z<12||toOutside&&z>12?4.2f:prison&&z>12?4.05f:5.2f;
                float height=Height(z,fromFork)-.4f;
                for(int side=-1;side<=1;side+=2)mesh.Box(new Vector3(side*width,height*.5f,z),new Vector3(.3f,height,.4f));
                mesh.Box(new Vector3(0,height,z),new Vector3(width*2+.4f,.35f,.45f));
            }
            if(cart)for(int i=0;i<8;i++)for(int side=-1;side<=1;side+=2)mesh.Box(new Vector3(side*.7f,.035f,i+.5f),new Vector3(.09f,.07f,1));
            return mesh.Build("BiomeEntranceFrames");
        }
        private void LateUpdate(){if(camera!=null)lamp.enabled=Mathf.Abs(camera.transform.position.z-lamp.transform.position.z)<30;}
        public void Release(){if(environment!=null)environment.gameObject.SetActive(restore);gameObject.SetActive(false);}
    }
}
