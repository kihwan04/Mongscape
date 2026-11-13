using System.Collections.Generic;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Rendering;
namespace Muhanok.Presentation.Map
{
    // Pooled physical junction: deck, collision surface and three rails use exactly the path the cart samples.
    public sealed class RailForkView : MonoBehaviour
    {
        private const int Samples=49;
        public const float ExitHalfWidth=3f, ExitHeight=4.6f;
        private static readonly Dictionary<int,Mesh> decks=new Dictionary<int,Mesh>(), tunnels=new Dictionary<int,Mesh>(), supports=new Dictionary<int,Mesh>();
        private static Material deckMaterial,railMaterial,ravineMaterial,selectedRailMaterial,dimRailMaterial,signMaterial;
        private static Mesh signBoard;
        private static readonly Mesh[] signArrows=new Mesh[3];
        private readonly Renderer[] signs=new Renderer[3];
        private int visualLane=99;
        public int HighlightedBranch {get;private set;}
        private static readonly Material[] branchMaterials=new Material[3];
        private static readonly Color[] colors={new Color(.64f,.7f,.74f),new Color(.64f,.7f,.74f),new Color(.64f,.7f,.74f)};
        private Transform environment;
        private bool restoreEnvironment;
        private MeshFilter deck;
        private MeshFilter tunnel;
        private MeshFilter timber;
        private Renderer ground;
        private readonly Light[] lamps=new Light[3];
        private readonly Transform[] lampGlass=new Transform[3];
        private Camera observer;
        private int sequence,geometryMode=99;
        private bool materialsChosen;
        private MeshCollider collisionSurface;
        private readonly LineRenderer[] rails=new LineRenderer[6];
        public static void PrewarmMeshes()
        {
            for(int local=CartFork.FirstChunk;local<=CartFork.LastChunk;local++)for(int mode=-1;mode<=2;mode++)
            {
                if(local==CartFork.FirstChunk&&mode!=2)continue;int key=local*10+mode+1;
                if(!decks.TryGetValue(key,out var deck)||deck==null)
                {
                    decks[key]=deck=BuildDeck(1,local,2);Physics.BakeMesh(deck.GetInstanceID(),false);
                }
                if(!tunnels.TryGetValue(key,out var tunnel)||tunnel==null)
                {
                    tunnels[key]=tunnel=BuildTunnels(local,mode);Physics.BakeMesh(tunnel.GetInstanceID(),false);
                }
                if(!supports.TryGetValue(key,out var frame)||frame==null)supports[key]=BuildSupports(local,mode);
            }
        }
        public static void PrewarmInstance(MuhanokMapChunk chunk)
        {
            if(chunk.transform.Find("ThreeWayRails")!=null)return;
            var root=new GameObject("ThreeWayRails");root.transform.SetParent(chunk.transform,false);
            var view=root.AddComponent<RailForkView>();view.Create();chunk.ForkView=view;root.SetActive(false);
        }
        public static RailForkView Attach(MuhanokMapChunk chunk,int sequence)
        {
            var existing=chunk.transform.Find("ThreeWayRails");
            RailForkView view;
            if(existing!=null)view=existing.GetComponent<RailForkView>();
            else
            {
                var root=new GameObject("ThreeWayRails");root.transform.SetParent(chunk.transform,false);
                view=root.AddComponent<RailForkView>();view.Create();
            }
            int local=RouteSurface.ActivePlan.LocalSequence(sequence);
            chunk.ForkView=view;view.Configure(chunk,local);view.gameObject.SetActive(true);return view;
        }
        private static void Materials()
        {
            if(deckMaterial!=null)return;
            var shader=Resources.Load<Shader>("MuhanokUI/ForkSurface")??Shader.Find("Universal Render Pipeline/Lit");
            deckMaterial=new Material(shader){name="Fork timber",color=new Color(.32f,.20f,.105f)};
            railMaterial=new Material(shader){name="Fork lamp glass",color=new Color(1,.66f,.25f)};
            selectedRailMaterial=new Material(shader){name="Selected fork rail",color=new Color(1,.69f,.18f)};
            dimRailMaterial=new Material(shader){name="Unselected fork rail",color=new Color(.27f,.31f,.34f)};
            signMaterial=new Material(shader){name="Fork sign backing",color=new Color(.12f,.075f,.04f)};
            var plate=new RouteMesh();plate.Box(Vector3.zero,new Vector3(1.5f,.9f,.12f));signBoard=plate.Build("Fork direction board");
            for(int branch=-1;branch<=1;branch++)signArrows[branch+1]=BuildArrow(branch);
            for(int i=0;i<3;i++)branchMaterials[i]=new Material(shader){name="Fork rail "+i,color=colors[i]};
            ravineMaterial=new Material(shader){name="Fork stone",color=new Color(.13f,.115f,.095f)};
        }
        private void Create()
        {
            Materials();
            for(int branch=-1;branch<=1;branch++)
            {
                var marker=new GameObject("DirectionMarker_"+branch);marker.transform.SetParent(transform,false);
                marker.AddComponent<MeshFilter>().sharedMesh=signBoard;
                var board=marker.AddComponent<MeshRenderer>();board.sharedMaterial=signMaterial;board.shadowCastingMode=ShadowCastingMode.Off;
                var arrow=new GameObject("DirectionArrow");arrow.transform.SetParent(marker.transform,false);arrow.transform.localPosition=Vector3.back*.075f;
                arrow.AddComponent<MeshFilter>().sharedMesh=signArrows[branch+1];
                var icon=arrow.AddComponent<MeshRenderer>();icon.sharedMaterial=branchMaterials[branch+1];icon.shadowCastingMode=ShadowCastingMode.Off;
                signs[branch+1]=icon;
            }
            var surface=new GameObject("TraversableDeck");surface.transform.SetParent(transform,false);
            deck=surface.AddComponent<MeshFilter>();surface.AddComponent<MeshRenderer>().sharedMaterial=deckMaterial;
            collisionSurface=surface.AddComponent<MeshCollider>();
            for(int i=0;i<rails.Length;i++)
            {
                var rail=new GameObject("PhysicalRail_"+i).AddComponent<LineRenderer>();rail.transform.SetParent(transform,false);
                rail.useWorldSpace=false;rail.sharedMaterial=branchMaterials[i/2];rail.positionCount=Samples;
                rail.startWidth=rail.endWidth=.13f;rail.shadowCastingMode=ShadowCastingMode.Off;rail.receiveShadows=false;
                rail.startColor=rail.endColor=colors[i/2];rails[i]=rail;
            }
            var walls=new GameObject("ThreeUnknownTunnels");walls.transform.SetParent(transform,false);
            tunnel=walls.AddComponent<MeshFilter>();walls.AddComponent<MeshRenderer>().sharedMaterial=ravineMaterial;
            var frames=new GameObject("TimberTunnelPortals");frames.transform.SetParent(transform,false);
            timber=frames.AddComponent<MeshFilter>();frames.AddComponent<MeshRenderer>().sharedMaterial=deckMaterial;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="MineGround";
            floor.transform.SetParent(transform,false);floor.transform.localPosition=new Vector3(0,-.55f,12);floor.transform.localScale=new Vector3(30,1,24);
            ground=floor.GetComponent<Renderer>();ground.sharedMaterial=ravineMaterial;Destroy(floor.GetComponent<Collider>());
            observer=Camera.main;
            for(int i=0;i<3;i++)
            {
                var glass=GameObject.CreatePrimitive(PrimitiveType.Cube);glass.name="TunnelLantern";Destroy(glass.GetComponent<Collider>());
                glass.transform.SetParent(transform,false);glass.transform.localScale=new Vector3(.18f,.4f,.18f);lampGlass[i]=glass.transform;
                glass.GetComponent<Renderer>().sharedMaterial=railMaterial;
                var lamp=new GameObject("UnshadowedWarmLamp").AddComponent<Light>();lamp.transform.SetParent(glass.transform,false);
                lamp.type=LightType.Point;lamp.color=new Color(1,.66f,.31f);lamp.intensity=4;lamp.range=13;lamp.shadows=LightShadows.None;lamps[i]=lamp;
            }
        }
        private void Configure(MuhanokMapChunk chunk,int localSequence)
        {
            environment=chunk.transform.Find("EnvironmentRoot");
            restoreEnvironment=environment!=null&&environment.gameObject.activeSelf;
            Material rock=null,wood=null;
            if(!materialsChosen&&environment!=null)foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
            {
                if(material==null)continue;
                if(rock==null&&(material.name.Contains("Stone_")||material.name.Contains("MineRock")))rock=material;
                if(wood==null&&(material.name.Contains("Timber")||material.name.Contains("Wood")))wood=material;
            }
            if(rock!=null){tunnel.GetComponent<Renderer>().sharedMaterial=rock;ground.sharedMaterial=rock;}
            if(wood!=null){deck.GetComponent<Renderer>().sharedMaterial=wood;timber.GetComponent<Renderer>().sharedMaterial=wood;}
            materialsChosen=true;
            if(environment!=null)environment.gameObject.SetActive(false);
            sequence=chunk.Sequence;geometryMode=99;visualLane=99;RefreshSelection(chunk);ground.gameObject.SetActive(false);
            float lampZ=localSequence==CartFork.FirstChunk?23:18;
            float signZ=localSequence==CartFork.FirstChunk?21:18;
            for(int branch=-1;branch<=1;branch++)
            {
                var marker=signs[branch+1].transform.parent;
                marker.localPosition=new Vector3(CartFork.Offset(localSequence*24+signZ,branch),3.35f,signZ);
            }
            for(int branch=-1;branch<=1;branch++)lampGlass[branch+1].localPosition=new Vector3(CartFork.Offset(localSequence*24+lampZ,branch)-1.1f,3.6f,lampZ-.25f);
            for(int branch=-1;branch<=1;branch++)for(int side=0;side<2;side++)
            {
                var rail=rails[(branch+1)*2+side];
                for(int i=0;i<Samples;i++)
                {
                    float z=i*.5f;
                    rail.SetPosition(i,new Vector3(CartFork.Offset(localSequence*24+z,branch)+(side==0?-.7f:.7f),.12f,z));
                }
            }
        }
        public void RefreshPreview(int lane,bool locked)
        {
            lane=Mathf.Clamp(lane,-1,1);int key=(lane+1)*2+(locked?1:0);
            if(key==visualLane)return;visualLane=key;HighlightedBranch=lane;
            for(int branch=-1;branch<=1;branch++)
            {
                bool chosen=branch==lane;
                var material=chosen?selectedRailMaterial:locked?dimRailMaterial:branchMaterials[branch+1];
                signs[branch+1].sharedMaterial=material;
                for(int side=0;side<2;side++)
                {
                    var rail=rails[(branch+1)*2+side];rail.sharedMaterial=material;
                    rail.startWidth=rail.endWidth=chosen?.2f:.11f;
                }
            }
        }
        private static Mesh BuildArrow(int direction)
        {
            var v=new List<Vector3>();var t=new List<int>();
            // Rotate an upright arrow left/right; geometry remains readable without text or extra lights.
            float angle=-direction*45*Mathf.Deg2Rad;
            var outline=new[]{new Vector2(-.11f,-.3f),new Vector2(.11f,-.3f),new Vector2(.11f,.04f),new Vector2(.32f,.04f),new Vector2(0,.36f),new Vector2(-.32f,.04f),new Vector2(-.11f,.04f)};
            foreach(var p in outline)v.Add(new Vector3(p.x*Mathf.Cos(angle)-p.y*Mathf.Sin(angle),p.x*Mathf.Sin(angle)+p.y*Mathf.Cos(angle),0));
            // Rectangle stem plus arrow head, independent of polygon triangulation.
            t.AddRange(new[]{0,1,2,0,2,6,5,3,4});
            var mesh=new Mesh{name="ForkArrow_"+direction};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void RefreshSelection(MuhanokMapChunk chunk)
        {
            int local=RouteSurface.ActivePlan.LocalSequence(sequence);
            int mode=local==CartFork.LastChunk&&RouteSurface.ActivePlan.ForkLocked(sequence*24)?RouteSurface.ActivePlan.ChoiceAt(sequence):2;
            if(mode==geometryMode)return;geometryMode=mode;
            int key=local*10+mode+1;
            if(!decks.TryGetValue(key,out var mesh)||mesh==null)decks[key]=mesh=BuildDeck(chunk.RouteType,local,2);
            deck.sharedMesh=mesh;collisionSurface.sharedMesh=mesh;
            if(!tunnels.TryGetValue(key,out var enclosure)||enclosure==null)tunnels[key]=enclosure=BuildTunnels(local,mode);
            tunnel.sharedMesh=enclosure;
            var enclosureCollider=tunnel.GetComponent<MeshCollider>();if(enclosureCollider==null)enclosureCollider=tunnel.gameObject.AddComponent<MeshCollider>();enclosureCollider.sharedMesh=enclosure;
            if(!supports.TryGetValue(key,out var frameMesh)||frameMesh==null)supports[key]=frameMesh=BuildSupports(local,mode);
            timber.sharedMesh=frameMesh;
            // All three visible options terminate here; only the selected portal opens into the connector.
            for(int i=0;i<6;i++)rails[i].gameObject.SetActive(true);
            for(int i=0;i<3;i++)lampGlass[i].gameObject.SetActive(true);
        }
        private void LateUpdate()
        {
            if(observer==null)return;
            foreach(var lamp in lamps)lamp.enabled=Mathf.Abs(observer.transform.position.z-lamp.transform.position.z)<32&&Mathf.Abs(observer.transform.position.x-lamp.transform.position.x)<12;
        }
        public void Release()
        {
            if(environment!=null)environment.gameObject.SetActive(restoreEnvironment);
            gameObject.SetActive(false);
        }
        // Clip touching deck strips at their midpoint so their triangles never overlap at the join.
        private static void Edges(int type,int sequence,float z,int branch,out float left,out float right)
        {
            float center=0,spread=CartFork.Offset(sequence*24+z,1);
            float x=center+branch*spread;
            left=x-1.15f;right=x+1.15f;
            if(branch>=0)left=Mathf.Max(left,center+(branch-.5f)*spread);
            if(branch<=0)right=Mathf.Min(right,center+(branch+.5f)*spread);
        }
        private static void Section(int sequence,float z,int branch,bool only,out float left,out float right,out float width,out float height,out float spread)
        {
            float distance=sequence*24+z;spread=CartFork.Offset(distance,1);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(CartFork.Decision,CartFork.SplitEnd,distance));
            width=distance<=CartFork.Decision?5.9f-spread:Mathf.Lerp(3.7f,ExitHalfWidth,blend);
            height=Mathf.Lerp(6.1f,ExitHeight,blend);
            left=branch*spread-width;right=branch*spread+width;
            if(!only){if(branch>=0)left=Mathf.Max(left,(branch-.5f)*spread);if(branch<=0)right=Mathf.Min(right,(branch+.5f)*spread);}
        }
        private static Mesh BuildTunnels(int sequence,int selected)
        {
            var mesh=new RouteMesh();bool only=false;
            for(int i=0;i<Samples-1;i++)
            {
                float z=i*.5f,n=z+.5f;
                for(int branch=-1;branch<=1;branch++)
                {
                    if(only&&branch!=selected)continue;
                    Section(sequence,z,branch,only,out float a,out float b,out float w,out float h,out float spread);
                    Section(sequence,n,branch,only,out float c,out float d,out float nw,out float nh,out float nextSpread);
                    if(b-a<.001f&&d-c<.001f)continue;
                    mesh.Strip(z,n,a,b,c,d,-.55f,-.03f,-.55f,-.03f);
                    mesh.Strip(z,n,a,b,c,d,h,h+.25f,nh,nh+.25f);
                    bool gap=spread>2*w||nextSpread>2*nw;
                    if(only||branch==-1||gap)mesh.Strip(z,n,a-.2f,a,c-.2f,c,-.55f,h+.25f,-.55f,nh+.25f);
                    if(only||branch==1||gap)mesh.Strip(z,n,b,b+.2f,d,d+.2f,-.55f,h+.25f,-.55f,nh+.25f);
                }
            }
            if(sequence==CartFork.LastChunk&&selected!=2)for(int branch=-1;branch<=1;branch++)if(branch!=selected)
                mesh.Box(new Vector3(branch*CartFork.Spacing,ExitHeight*.5f,23.92f),new Vector3(ExitHalfWidth*2+.2f,ExitHeight+.2f,.2f));
            return mesh.Build("SealedFork_"+sequence+"_"+selected);
        }
        private static Mesh BuildSupports(int sequence,int selected)
        {
            var v=new List<Vector3>();var t=new List<int>();
            if(sequence==CartFork.FirstChunk)
            {
                Box(v,t,new Vector3(-5.6f,2.9f,8),new Vector3(.3f,5.8f,.35f));
                Box(v,t,new Vector3(5.6f,2.9f,8),new Vector3(.3f,5.8f,.35f));
                Box(v,t,new Vector3(0,5.7f,8),new Vector3(11.5f,.3f,.4f));
                Box(v,t,new Vector3(-4,1.95f,21),new Vector3(.2f,3.9f,.25f));
                Box(v,t,new Vector3(4,1.95f,21),new Vector3(.2f,3.9f,.25f));
                Box(v,t,new Vector3(0,3.95f,21),new Vector3(8.2f,.2f,.25f));
                for(int branch=-1;branch<=1;branch++)
                    Box(v,t,new Vector3(CartFork.Offset(117,branch),3.85f,21),new Vector3(.08f,.3f,.08f));
            }
            else for(int branch=-1;branch<=1;branch++)for(int row=0;row<2;row++)
            {
                float z=12+row*10,x=CartFork.Offset(sequence*24+z,branch);
                float spread=CartFork.Offset(sequence*24+z,1);
                float width=Mathf.Min(ExitHalfWidth-.25f,Mathf.Max(1.7f,spread*.45f));
                float height=ExitHeight-.4f;
                Box(v,t,new Vector3(x-width,height*.5f,z),new Vector3(.25f,height,.3f));
                Box(v,t,new Vector3(x+width,height*.5f,z),new Vector3(.25f,height,.3f));
                Box(v,t,new Vector3(x,height,z),new Vector3(width*2+.35f,.3f,.38f));
            }
            var mesh=new Mesh{name="ThreeTunnelTimbers_"+sequence};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Box(List<Vector3> v,List<int> t,Vector3 p,Vector3 size)
        {
            Vector3 a=p-size*.5f,b=p+size*.5f;
            Quad(v,t,new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z));
            Quad(v,t,new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z));
            Quad(v,t,new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z));
            Quad(v,t,new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z));
            Quad(v,t,new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z));
            Quad(v,t,new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z));
        }
        private static Mesh BuildDeck(int type,int sequence,int selected)
        {
            var vertices=new List<Vector3>(Samples*24);var triangles=new List<int>(Samples*72);
            for(int branch=-1;branch<=1;branch++)for(int i=0;i<Samples-1;i++)
            {
                if(selected!=2&&branch!=selected)continue;
                float z=i*.5f,next=z+.5f;
                Edges(type,sequence,z,branch,out float a,out float b);Edges(type,sequence,next,branch,out float c,out float d);
                if(b-a<.001f&&d-c<.001f)continue;
                Quad(vertices,triangles,new Vector3(a,0,z),new Vector3(c,0,next),new Vector3(b,0,z),new Vector3(d,0,next));
                Quad(vertices,triangles,new Vector3(a,-.18f,z),new Vector3(b,-.18f,z),new Vector3(c,-.18f,next),new Vector3(d,-.18f,next));
                Quad(vertices,triangles,new Vector3(a,-.18f,z),new Vector3(c,-.18f,next),new Vector3(a,0,z),new Vector3(c,0,next));
                Quad(vertices,triangles,new Vector3(b,0,z),new Vector3(d,0,next),new Vector3(b,-.18f,z),new Vector3(d,-.18f,next));
            }
            var mesh=new Mesh{name="ForkDeck_"+type+"_"+sequence};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start+2);triangles.Add(start+1);triangles.Add(start+3);
        }
    }
}
