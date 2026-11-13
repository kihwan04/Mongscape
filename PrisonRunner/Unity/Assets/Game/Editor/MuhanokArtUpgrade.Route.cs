using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static GameObject PropCart(Transform parent,Vector3 position,float scale)
        {
            var cart=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PROP_MineCart.prefab"));
            cart.transform.SetParent(parent,false); cart.transform.localPosition=position; cart.transform.localScale=Vector3.one*scale;
            foreach(var audio in cart.GetComponentsInChildren<AudioSource>()) Object.DestroyImmediate(audio);
            return cart;
        }
        private static void AddBoundaryConnectors(Transform env,int type)
        {
            if(type==1||type==2)return; // Narrow trestle gantries are authored with the dedicated cart route.
            // Repeat the same service frame on both sides of a join, leaving all lanes open.
            for(int end=0;end<2;end++)
            {
                float z=end==0?.10f:23.90f;
                if(type!=12) Portal(env,z,RouteSurface.IsMine(type)?wood:iron); // no gate frame on every zoo walk chunk
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("ServiceFoot",env,new Vector3(side*3.9f,.16f,z),new Vector3(.45f,.32f,.5f),iron);
                    Shape("RouteGuide",env,new Vector3(side*3.1f,.023f,z),new Vector3(.10f,.03f,.20f),brass);
                }
            }
            if(type==3)
            {
                for(int side=-1;side<=1;side+=2) for(int i=0;i<5;i++)
                {
                    float z=8+i*2;
                    Shape("RockToConcrete",env,new Vector3(side*4.65f,1.0f+i*.32f,z),new Vector3(.3f,2+i*.64f,1.95f),concrete);
                    Shape("TransitionPipe",env,new Vector3(side*4.25f,3.75f,z),new Vector3(.16f,.16f,2.02f),red);
                }
                Portal(env,10,wood); Lantern(env,new Vector3(-3.35f,3.1f,11),false);
                for(int i=12;i<18;i++) for(int lane=-1;lane<=1;lane++) for(int side=-1;side<=1;side+=2)
                    Shape("RailTermination",env,new Vector3(lane*2.2f+side*.53f,.04f,i+.5f),new Vector3(.085f,.065f,1.002f),iron);
            }
            if(type==RouteSurface.PrisonExit)
            {
                // Mirror of the mine-to-prison transition: concrete gives way to rock towards the mine portal at the exit end.
                for(int side=-1;side<=1;side+=2) for(int i=0;i<5;i++)
                {
                    float z=16-i*2;
                    Shape("ConcreteToRock",env,new Vector3(side*4.65f,1.0f+i*.32f,z),new Vector3(.3f,2+i*.64f,1.95f),concrete);
                    Shape("TransitionPipe",env,new Vector3(side*4.25f,3.75f,z),new Vector3(.16f,.16f,2.02f),red);
                }
                Portal(env,14,wood); Lantern(env,new Vector3(-3.35f,3.1f,13),false);
            }
            if(type==6||type==10)
            {
                Shape("OpenServiceExit",env,new Vector3(0,5.0f,22),new Vector3(9.7f,.27f,4),concrete);
            }
            if(type==11)
            {
                Shape("ExitVestibule",env,new Vector3(0,5.0f,2),new Vector3(9.7f,.27f,4),concrete);
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("VestibuleWall",env,new Vector3(side*4.8f,2.4f,2),new Vector3(.24f,4.8f,4),concrete);
                    for(int z=0;z<24;z++) Beam("OutdoorRampRail",env,new Vector3(side*3.42f,1.02f,z),new Vector3(side*3.42f,1.02f,z+1),.09f,wood);
                }
            }
            if(type==4)
            {
                for(int side=-1;side<=1;side+=2) Prop("nature-kit","plant_bush",env,new Vector3(side*4.0f,0,.7f),.42f);
            }
        }
        private static Vector3 RoutePoint(Vector3 position,int type)
        {
            position.x+=RouteSurface.LocalX(type,position.z); position.y+=RouteSurface.LocalHeight(type,position.z); return position;
        }
        private static void ApplyRouteSurface(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>(); chunk.RouteType=type;
            chunk.Entry.localPosition=Vector3.zero; chunk.Exit.localPosition=new Vector3(0,RouteSurface.Rise(type),24);
            Transform env=root.transform.Find("EnvironmentRoot"); int index=0;
            foreach(var filter in env.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null) continue;
                // Persistent copies prevent shared FBX assets being deformed for other chunks.
                var mesh=Object.Instantiate(filter.sharedMesh); mesh.name=chunk.AssetId+"_Route_"+index; var vertices=mesh.vertices;
                Matrix4x4 toRoot=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                Matrix4x4 fromRoot=toRoot.inverse;
                for(int v=0;v<vertices.Length;v++) vertices[v]=fromRoot.MultiplyPoint3x4(RoutePoint(toRoot.MultiplyPoint3x4(vertices[v]),type));
                mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path=Art+"/Meshes/"+chunk.AssetId+"_Route_"+(index++)+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(previous==null) AssetDatabase.CreateAsset(mesh,path);
                else { EditorUtility.CopySerialized(mesh,previous); Object.DestroyImmediate(mesh); mesh=previous; EditorUtility.SetDirty(mesh); }
                filter.sharedMesh=mesh;
            }
            // Lights, text and particle origins follow the mesh, with no second mesh deformation.
            foreach(var light in env.GetComponentsInChildren<Light>()) MoveOrigin(light.transform,root.transform,type);
            foreach(var text in env.GetComponentsInChildren<TextMesh>()) MoveOrigin(text.transform,root.transform,type);
            foreach(var dust in env.GetComponentsInChildren<ParticleSystem>()) MoveOrigin(dust.transform,root.transform,type);
            foreach(var obstacle in chunk.Obstacles)
            {
                var socket=obstacle.transform.parent; var contract=socket.GetComponent<MuhanokObstacleSocket>();
                socket.localPosition=new Vector3(contract.Lane*2.2f,0,7+contract.Row*12);
                MoveOrigin(socket,root.transform,type);
            }
            foreach(var coin in chunk.Coins) MoveOrigin(coin.transform,root.transform,type);
            if(chunk.FloorItems!=null)foreach(var item in chunk.FloorItems)MoveOrigin(item.transform,root.transform,type);
            if(chunk.RailGaps!=null)foreach(var gap in chunk.RailGaps)MoveOrigin(gap.transform,root.transform,type);
        }
        private static void MoveOrigin(Transform transform,Transform root,int type)
        {
            transform.position=root.TransformPoint(RoutePoint(root.InverseTransformPoint(transform.position),type));
        }
    }
}
