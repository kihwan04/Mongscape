using System.Collections.Generic;
using UnityEngine;
namespace Muhanok.Presentation.Map
{
    // Closed opaque volumes. Shared section endpoints prevent cracks at pooled chunk boundaries.
    internal sealed class RouteMesh
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n+2);triangles.Add(n+1);triangles.Add(n+3);
        }
        public void Strip(float z0,float z1,float left0,float right0,float left1,float right1,float low0,float high0,float low1,float high1)
        {
            Vector3 a=new Vector3(left0,low0,z0),b=new Vector3(right0,low0,z0),c=new Vector3(left1,low1,z1),d=new Vector3(right1,low1,z1);
            Vector3 e=new Vector3(left0,high0,z0),f=new Vector3(right0,high0,z0),g=new Vector3(left1,high1,z1),h=new Vector3(right1,high1,z1);
            Quad(a,e,b,f);Quad(d,h,c,g);Quad(c,g,a,e);Quad(b,f,d,h);Quad(e,g,f,h);Quad(c,a,d,b);
        }
        public void Box(Vector3 center,Vector3 size)=>Strip(center.z-size.z*.5f,center.z+size.z*.5f,center.x-size.x*.5f,center.x+size.x*.5f,center.x-size.x*.5f,center.x+size.x*.5f,center.y-size.y*.5f,center.y+size.y*.5f,center.y-size.y*.5f,center.y+size.y*.5f);
        public Mesh Build(string name)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
    internal static class RouteMaterials
    {
        private static readonly Dictionary<int,Material> rocks=new Dictionary<int,Material>();
        private static Material fallback;
        public static Material Rock(MuhanokMapChunk chunk)
        {
            if(rocks.TryGetValue(chunk.RouteType,out var cached)&&cached!=null)return cached;
            var env=chunk.transform.Find("EnvironmentRoot");Material candidate=null;
            if(env!=null)foreach(var renderer in env.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
            {
                if(material==null)continue;
                if(material.name.Contains("Stone_")||material.name.Contains("MineRock")||material.name.Contains("Concrete"))candidate=material;
                if(candidate!=null)break;
            }
            if(candidate==null)
            {
                if(fallback==null)fallback=new Material(Resources.Load<Shader>("MuhanokUI/ForkSurface")){color=new Color(.16f,.18f,.18f)};
                candidate=fallback;
            }
            rocks[chunk.RouteType]=candidate;return candidate;
        }
    }
}
