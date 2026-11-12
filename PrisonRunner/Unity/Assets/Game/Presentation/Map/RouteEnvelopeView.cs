using System.Collections.Generic;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Rendering;
namespace Muhanok.Presentation.Map
{
    // Always-visible structural backing, separate from distance-culled decorations.
    public sealed class RouteEnvelopeView : MonoBehaviour
    {
        private static readonly Dictionary<int,Mesh> shells=new Dictionary<int,Mesh>();
        private static Material earth;
        public MeshCollider Surface { get; private set; }
        public static RouteEnvelopeView Attach(MuhanokMapChunk chunk)
        {
            var view=chunk.GetComponentInChildren<RouteEnvelopeView>(true);
            if(view==null)
            {
                var node=new GameObject("ContinuousOpaqueEnvelope");node.transform.SetParent(chunk.transform,false);view=node.AddComponent<RouteEnvelopeView>();
                node.AddComponent<MeshFilter>();var renderer=node.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;
                view.Surface=node.AddComponent<MeshCollider>();
            }
            int type=chunk.RouteType;
            if(!shells.TryGetValue(type,out var mesh)||mesh==null)shells[type]=mesh=Build(type);
            view.GetComponent<MeshFilter>().sharedMesh=mesh;view.Surface.sharedMesh=mesh;
            if(earth==null)earth=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.12f,.18f,.095f)};
            view.GetComponent<MeshRenderer>().sharedMaterial=RouteSurface.IsOutdoor(type)?earth:RouteMaterials.Rock(chunk);
            view.gameObject.SetActive(true);return view;
        }
        private static float Width(int type,float z)
        {
            if(RouteSurface.IsOutdoor(type))return 22;
            if(type==3)return Mathf.Lerp(5.9f,8.4f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,20,z)));
            if(type==6)return Mathf.Lerp(8.4f,22,Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,24,z)));
            return type>=4&&type<=7?8.4f:5.9f;
        }
        private static float Bottom(int type,float z)=>RouteSurface.LocalHeight(type,z)-.45f-(type==1||type==2?Mathf.Pow(Mathf.Sin(z*Mathf.PI/24),2)*3:0);
        private static Mesh Build(int type)
        {
            var mesh=new RouteMesh();
            for(int i=0;i<24;i++)
            {
                float z=i,n=i+1,x=RouteSurface.LocalX(type,z),nx=RouteSurface.LocalX(type,n),w=Width(type,z),nw=Width(type,n),y=Bottom(type,z),ny=Bottom(type,n);
                mesh.Strip(z,n,x-w,x+w,nx-nw,nx+nw,y-.45f,y,ny-.45f,ny);
                float high=RouteSurface.LocalHeight(type,z)+(RouteSurface.IsOutdoor(type)?8:6.1f),nhigh=RouteSurface.LocalHeight(type,n)+(RouteSurface.IsOutdoor(type)?8:6.1f);
                for(int side=-1;side<=1;side+=2)
                {
                    float edge=x+side*w,next=nx+side*nw;
                    mesh.Strip(z,n,edge-.18f,edge+.18f,next-.18f,next+.18f,y,high,ny,nhigh);
                }
                if(!RouteSurface.IsOutdoor(type) && !(type==6&&i>=18))mesh.Strip(z,n,x-w,x+w,nx-nw,nx+nw,high,high+.25f,nhigh,nhigh+.25f);
            }
            return mesh.Build("SealedRouteBacking_"+type);
        }
        public void Release()=>gameObject.SetActive(false);
    }
}
