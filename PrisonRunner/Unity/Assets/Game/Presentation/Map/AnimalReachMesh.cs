using UnityEngine;
using UnityEngine.Rendering;

namespace Muhanok.Presentation.Map
{
    // Two connected cubic curves, rebuilt into fixed buffers only when the pose changes.
    // Geometry stays attached to the shoulder/muzzle; gameplay collision remains separate.
    public sealed class AnimalReachMesh : MonoBehaviour
    {
        private const int Rings=41, Sides=10, Stride=Sides+1;
        private readonly Vector3[] vertices=new Vector3[Rings*Stride+2],normals=new Vector3[Rings*Stride+2];
        private readonly Vector3[] points=new Vector3[7];
        private Mesh mesh;
        private float baseRadius,tipRadius;
        public Vector3 Base=>transform.TransformPoint(points[0]);
        public Vector3 Tip=>transform.TransformPoint(points[6]);
        public int PoseUpdates {get;private set;}
        public void Configure(Material material,float startRadius,float endRadius)
        {
            baseRadius=startRadius;tipRadius=endRadius;
            mesh=new Mesh{name="Continuous animal reach"};mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            var uv=new Vector2[vertices.Length];var triangles=new int[(Rings-1)*Sides*6+Sides*6];int index=0;
            for(int ring=0;ring<Rings;ring++)for(int side=0;side<=Sides;side++)
                uv[ring*Stride+side]=new Vector2(side/(float)Sides,ring/(float)(Rings-1)*1.7f);
            for(int ring=0;ring<Rings-1;ring++)for(int side=0;side<Sides;side++)
            {
                int a=ring*Stride+side,b=a+Stride;
                triangles[index++]=a;triangles[index++]=a+1;triangles[index++]=b;
                triangles[index++]=a+1;triangles[index++]=b+1;triangles[index++]=b;
            }
            for(int side=0;side<Sides;side++)
            {
                triangles[index++]=vertices.Length-2;triangles[index++]=side+1;triangles[index++]=side;
                triangles[index++]=vertices.Length-1;triangles[index++]=(Rings-1)*Stride+side;triangles[index++]=(Rings-1)*Stride+side+1;
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;
        }
        public void Pose(Vector3 start,Vector3 a,Vector3 b,Vector3 middle,Vector3 c,Vector3 d,Vector3 end)
        {
            points[0]=start;points[1]=a;points[2]=b;points[3]=middle;points[4]=c;points[5]=d;points[6]=end;
            for(int ring=0;ring<Rings;ring++)
            {
                float t=ring/(float)(Rings-1);var center=Point(t);var tangent=Tangent(t).normalized;
                var side=Vector3.Cross(tangent,Mathf.Abs(Vector3.Dot(tangent,Vector3.forward))>.95f?Vector3.up:Vector3.forward).normalized;
                var up=Vector3.Cross(tangent,side).normalized;
                float radius=Mathf.Lerp(baseRadius,tipRadius,t);
                for(int j=0;j<=Sides;j++)
                {
                    float angle=j*Mathf.PI*2/Sides;var normal=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                    int index=ring*Stride+j;vertices[index]=center+normal*radius;normals[index]=normal;
                }
            }
            vertices[vertices.Length-2]=start;vertices[vertices.Length-1]=end;
            normals[normals.Length-2]=-Tangent(0).normalized;normals[normals.Length-1]=Tangent(1).normalized;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.RecalculateBounds();PoseUpdates++;
        }
        public Vector3 Point(float t)
        {
            int i=t<=.5f?0:3;float v=t<=.5f?t*2:(t-.5f)*2,u=1-v;
            return u*u*u*points[i]+3*u*u*v*points[i+1]+3*u*v*v*points[i+2]+v*v*v*points[i+3];
        }
        private Vector3 Tangent(float t)
        {
            int i=t<=.5f?0:3;float v=t<=.5f?t*2:(t-.5f)*2,u=1-v;
            return 3*u*u*(points[i+1]-points[i])+6*u*v*(points[i+2]-points[i+1])+3*v*v*(points[i+3]-points[i+2]);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
