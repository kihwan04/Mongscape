using System.Collections.Generic;
using UnityEngine;

namespace Muhanok.Editor
{
    internal static class MuhanokArtGeometry
    {
        public static Mesh BevelBox(float bevel = .075f)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            float h = .5f, k = h - bevel;
            for (int axis = 0; axis < 3; axis++) for (int side = -1; side <= 1; side += 2)
            {
                int a = (axis + 1) % 3, b = (axis + 2) % 3;
                var p = new Vector3[4];
                int[] signsA = { -1, 1, 1, -1 }, signsB = { -1, -1, 1, 1 };
                for (int i = 0; i < 4; i++) { p[i][axis] = side * h; p[i][a] = signsA[i] * k; p[i][b] = signsB[i] * k; }
                Face(vertices, triangles, uv, p);
            }
            for (int axis = 0; axis < 3; axis++) for (int s = -1; s <= 1; s += 2) for (int t = -1; t <= 1; t += 2)
            {
                int a = (axis + 1) % 3, b = (axis + 2) % 3; var p = new Vector3[4];
                p[0][axis] = p[1][axis] = -k; p[2][axis] = p[3][axis] = k;
                p[0][a] = p[3][a] = s * h; p[0][b] = p[3][b] = t * k;
                p[1][a] = p[2][a] = s * k; p[1][b] = p[2][b] = t * h;
                Face(vertices, triangles, uv, p);
            }
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                Face(vertices, triangles, uv, new[] { new Vector3(x*h,y*k,z*k), new Vector3(x*k,y*h,z*k), new Vector3(x*k,y*k,z*h) });
            return Finish("ChamferedBox", vertices, triangles, uv);
        }
        public static Mesh FacetedSphere(int segments = 12, int rings = 8)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int y = 0; y < rings; y++) for (int x = 0; x < segments; x++)
            {
                Vector3 a = Point(x,y,segments,rings), b = Point(x+1,y,segments,rings), c = Point(x+1,y+1,segments,rings), d = Point(x,y+1,segments,rings);
                if (y == 0) Face(vertices, triangles, uv, new[] { a,c,d });
                else if (y == rings-1) Face(vertices, triangles, uv, new[] { a,b,c });
                else Face(vertices, triangles, uv, new[] { a,b,c,d });
            }
            return Finish("FacetedSphere", vertices, triangles, uv);
        }
        private static Vector3 Point(int x, int y, int segments, int rings)
        {
            float p = Mathf.PI * y / rings, a = 2*Mathf.PI*x/segments;
            return new Vector3(Mathf.Sin(p)*Mathf.Cos(a), Mathf.Cos(p), Mathf.Sin(p)*Mathf.Sin(a))*.5f;
        }
        public static Mesh CoinDisc()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>(); var uv=new List<Vector2>();
            var front=new Vector3[20]; var back=new Vector3[20];
            for(int i=0;i<20;i++)
            {
                float a=i*2*Mathf.PI/20,b=(i+1)*2*Mathf.PI/20;
                front[i]=new Vector3(Mathf.Cos(a)*.43f,Mathf.Sin(a)*.43f,-.10f); back[i]=new Vector3(Mathf.Cos(a)*.43f,Mathf.Sin(a)*.43f,.10f);
                var fa=new Vector3(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f,-.055f); var fb=new Vector3(Mathf.Cos(b)*.5f,Mathf.Sin(b)*.5f,-.055f);
                var ba=new Vector3(fa.x,fa.y,.055f); var bb=new Vector3(fb.x,fb.y,.055f);
                Face(vertices,triangles,uv,new[]{front[i],new Vector3(Mathf.Cos(b)*.43f,Mathf.Sin(b)*.43f,-.10f),fb,fa});
                Face(vertices,triangles,uv,new[]{fa,fb,bb,ba});
                Face(vertices,triangles,uv,new[]{back[i],new Vector3(Mathf.Cos(b)*.43f,Mathf.Sin(b)*.43f,.10f),bb,ba});
            }
            Face(vertices,triangles,uv,front); Face(vertices,triangles,uv,back); return Finish("CoinDisc",vertices,triangles,uv);
        }
        private static void Face(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, Vector3[] points)
        {
            Vector3 center = Vector3.zero; foreach (var point in points) center += point; center /= points.Length;
            bool reverse = Vector3.Dot(Vector3.Cross(points[1]-points[0], points[2]-points[0]), center) < 0;
            int begin = vertices.Count;
            foreach (var point in points) { vertices.Add(point); uv.Add(new Vector2(point.x+point.z+.5f,point.y+.5f)); }
            for (int i = 1; i < points.Length-1; i++)
            { triangles.Add(begin); triangles.Add(begin+(reverse?i+1:i)); triangles.Add(begin+(reverse?i:i+1)); }
        }
        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uv)
        {
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetUVs(0,uv); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
