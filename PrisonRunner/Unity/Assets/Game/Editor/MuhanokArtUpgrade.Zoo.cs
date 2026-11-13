using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void ZooGroundDetails(Transform env,int type)
        {
            var water=Mat("NightWetStone",new Color(.08f,.20f,.31f),.55f,.95f);
            var grass=Mat("ZooGrass",new Color(.14f,.32f,.15f),0,.2f);
            var sand=Mat("ZooPaving",new Color(.47f,.40f,.26f),0,.34f,"stone");
            var earth=Mat("EnclosureEarth",new Color(.22f,.34f,.19f),0,.15f);
            for(int side=-1;side<=1;side+=2)
                for(int segment=0;segment<12;segment++)
                {
                    // Complete the actual enclosure floor; trees and animals must not sit over the void.
                    Shape("EnclosureGround",env,new Vector3(side*13.3f,-.16f,segment*2+1),new Vector3(17f,.30f,2.01f),earth);
                    if(segment%2==0)
                    {
                        Prop("nature-kit","grass_leafsLarge",env,new Vector3(side*5.1f,0,segment*2+.8f),.42f,Next()*360);
                        Prop("nature-kit","plant_bushDetailed",env,new Vector3(side*9.5f,0,segment*2+.4f),.7f,Next()*360);
                    }
                }
            for(int side=-1;side<=1;side+=2)
                for(int z=2;z<24;z+=5)
                {
                    Prop("nature-kit","tree_detailed_dark",env,new Vector3(side*(13.7f+Next()*2),0,z),7+Next()*1.5f,Next()*360);
                    Prop("nature-kit","tree_palmDetailedTall",env,new Vector3(side*(18.5f+Next()*2),0,z+2),6+Next()*2,Next()*360);
                    Prop("nature-kit","plant_bushDetailed",env,new Vector3(side*11.6f,0,z+1),1.5f,Next()*360);
                }
            for(int z=1;z<24;z+=2)
            {
                for(int x=-2;x<=2;x++)
                    Shape("PavingStone",env,new Vector3(x*1.2f,.025f,z),new Vector3(1.16f,.05f,1.92f),sand);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int tuft=0;tuft<3;tuft++)
                    {
                        var leaf=Shape("GrassBlade",env,new Vector3(side*(3.45f+Next()*.25f),.13f,z+Next()),new Vector3(.11f,.30f,.30f),grass);
                        leaf.transform.localRotation=Quaternion.Euler(0,Next()*180,side*25);
                    }
                    if(z%4==1) Lantern(env,new Vector3(side*3.65f,2.9f,z+.5f),false);
                }
            }
            for(int z=6;z<24;z+=9)
            {
                var puddle=Shape("EnclosureWater",env,new Vector3(z%2==0?-6.7f:6.7f,.065f,z),new Vector3(2.7f,.018f,3.1f),water,true);
                puddle.transform.localRotation=Quaternion.Euler(0,z*17,0);
            }
        }
        private static void BananaPeel(Transform parent,Vector3 position,float scale)
        {
            var banana=Node("BananaPeel",parent,position);banana.localScale=Vector3.one*scale;
            var peel=Mat("BananaPeelYellow",new Color(1,.72f,.06f),0,.35f);
            Shape("Stem",banana,new Vector3(0,.34f,0),new Vector3(.15f,.48f,.15f),peel);
            for(int petal=0;petal<4;petal++)
            {
                float angle=petal*Mathf.PI*.5f;
                var a=new Vector3(Mathf.Cos(angle)*.08f,.25f,Mathf.Sin(angle)*.08f);
                var b=new Vector3(Mathf.Cos(angle)*.37f,.045f,Mathf.Sin(angle)*.37f);
                var c=new Vector3(Mathf.Cos(angle)*.65f,.10f,Mathf.Sin(angle)*.65f);
                Beam("FoldedPeel",banana,a,b,.16f,peel);Beam("PeelTip",banana,b,c,.15f,peel);
            }
        }
        // Duck row of the zoo-looking chunks (Yard and zoo walk): a log on wooden posts instead of the industrial pipe.
        public const string ZooDuckId="OBS_ZooLowLog";
        // Rebuilds only the zoo duck visual and the exercise rows of the zoo-looking chunks, without re-running the whole art pass.
        [MenuItem("Mongscape/Apply Zoo Duck Obstacle")]
        public static void ApplyZooDuck()
        {
            // Prepare() would reset every shared material to its first-pass values, so only load what this needs.
            box=MeshAsset("BevelBox",()=>MuhanokArtGeometry.BevelBox()); sphere=MeshAsset("FacetedSphere",()=>MuhanokArtGeometry.FacetedSphere());
            black=Kept("Charcoal",new Color(.023f,.026f,.035f),.08f,.33f);
            ZooLowLog();
            for(int type=11;type<MuhanokContentBuilder.ChunkIds.Length;type++)
            {
                if(!RouteSurface.IsZoo(type))continue;
                string path=Root+"/Prefabs/"+MuhanokContentBuilder.ChunkIds[type]+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try { MuhanokGameplayUpgrade.Configure(root,type); PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("MONGSCAPE_ZOO_DUCK_PASS: "+ZooDuckId+" on the Yard and zoo duck rows");
        }
        // One lane: the log (underside 1.33 m) spans the lane so three lanes read as one log; posts and sign rise above
        // the 1.8 m jump peak so it never looks jumpable. Same ZooLog materials as the fallen jump log, but overhead.
        // An existing material as later passes left it; created with these values only when missing.
        private static Material Kept(string name,Color color,float metallic,float smooth,string texture=null)
        { var found=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+name+".mat"); return found!=null?found:Mat(name,color,metallic,smooth,texture); }
        private static void ZooLowLog()
        {
            string path=Root+"/Prefabs/"+ZooDuckId+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
            { var empty=new GameObject(ZooDuckId); empty.AddComponent<ObstacleController>(); PrefabUtility.SaveAsPrefabAsset(empty,path); Object.DestroyImmediate(empty); }
            var root=EditPrefab(ZooDuckId); root.GetComponent<ObstacleController>().Kind=ObstacleKind.Pipe;
            var bark=Kept("ZooLogBark",new Color(.26f,.13f,.055f),0,.18f,"wood");
            var heart=Kept("ZooLogHeart",new Color(.62f,.38f,.17f),0,.25f,"wood");
            var caution=Kept("ZooCaution",new Color(1,.74f,.08f),0,.3f);
            var vine=Kept("ZooVine",new Color(.18f,.42f,.14f),0,.2f);
            var t=root.transform;
            Beam("Log",t,new Vector3(-1.1f,1.5f,0),new Vector3(1.1f,1.5f,0),.34f,bark);
            for(int side=-1;side<=1;side+=2)
            {
                // On the lane edge, so neighbouring lanes share one post instead of standing two side by side.
                Shape("Post",t,new Vector3(side*1.1f,1.05f,0),new Vector3(.22f,2.1f,.22f),bark);
                Shape("PostTop",t,new Vector3(side*1.1f,2.12f,0),new Vector3(.26f,.08f,.26f),heart,true);
            }
            Shape("SignBoard",t,new Vector3(0,1.9f,0),new Vector3(1.1f,.38f,.06f),caution);
            for(int i=-2;i<=2;i++) { var stripe=Shape("SignStripe",t,new Vector3(i*.2f,1.9f,-.035f),new Vector3(.08f,.36f,.012f),black); stripe.transform.localRotation=Quaternion.Euler(0,0,-35); }
            for(int side=-1;side<=1;side+=2) Beam("SignRope",t,new Vector3(side*.42f,1.67f,0),new Vector3(side*.42f,1.71f,0),.03f,heart);
            // Hanging strands end by 1.0 m, still above the 0.85 m crouch camera.
            foreach(var (x,length) in new[]{(-.62f,.3f),(-.18f,.2f),(.25f,.33f),(.68f,.24f)})
            {
                Shape("Vine",t,new Vector3(x,1.33f-length*.5f,-.1f),new Vector3(.035f,length,.035f),vine);
                Shape("VineLeaves",t,new Vector3(x,1.33f-length,-.1f),new Vector3(.13f,.1f,.09f),vine,true);
            }
            Save(root,path);
        }
        private static void AddZooHazards(GameObject root,int type)
        {
            if(!RouteSurface.IsZoo(type)||RouteSurface.IsStair(type))return;
            foreach(var obstacle in root.GetComponent<MuhanokMapChunk>().Obstacles)
            {
                Clear(obstacle.transform);var old=obstacle.GetComponent<DescendingGate>();if(old!=null)Object.DestroyImmediate(old);
                var socket=obstacle.GetComponentInParent<MuhanokObstacleSocket>();
                if(socket.Row==0)
                {
                    obstacle.Kind=ObstacleKind.Crate;
                    var bark=Mat("ZooLogBark",new Color(.26f,.13f,.055f),0,.18f,"wood");
                    var heart=Mat("ZooLogHeart",new Color(.62f,.38f,.17f),0,.25f,"wood");
                    var log=Shape("FallenLog",obstacle.transform,new Vector3(0,.49f,0),new Vector3(1.9f,.95f,.8f),bark,true);
                    for(int side=-1;side<=1;side+=2) Shape("CutEnd",obstacle.transform,new Vector3(side*.84f,.49f,0),new Vector3(.08f,.73f,.66f),heart,true);
                }
                else
                {
                    obstacle.Kind=ObstacleKind.Barrier;
                    Prop("survival-kit","box-large",obstacle.transform,Vector3.zero,1.18f,8,wood);
                }
            }
        }
        private static void AddFloorItems(GameObject root,int type)
        {
            var old=root.transform.Find("FloorItems");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var chunk=root.GetComponent<MuhanokMapChunk>();chunk.FloorItems=new FloorItemController[type==11?1:type==12?2:0];
            if(!RouteSurface.IsZoo(type))return;
            var group=Node("FloorItems",root.transform,Vector3.zero);
            for(int index=0;index<chunk.FloorItems.Length;index++)
            {
                var node=Node(index==0?"BananaSlip":"PuddleSlip",group,new Vector3(index==0?-2.2f:2.2f,.035f,index==0?10:17));
                var item=node.gameObject.AddComponent<FloorItemController>();item.Kind=index==0?FloorItemKind.BananaPeel:FloorItemKind.Puddle;
                if(index==0)BananaPeel(node,new Vector3(0,.04f,0),.75f);
                else Shape("Puddle",node,Vector3.zero,new Vector3(1.65f,.025f,2),Mat("NightWetStone",new Color(.08f,.20f,.31f),.55f,.95f),true);
                chunk.FloorItems[index]=item;
            }
        }
        private static void ServiceExit(Transform env)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Shape("ExitLowWall",env,new Vector3(side*4.65f,.65f,20),new Vector3(.42f,1.30f,8.1f),concrete);
                for(int z=16;z<=24;z+=4)
                {
                    Shape("ServiceExitPost",env,new Vector3(side*4.25f,2.35f,z),new Vector3(.26f,4.7f,.26f),iron);
                    Lantern(env,new Vector3(side*3.65f,3.0f,z+.1f),false);
                    Prop("nature-kit","plant_bushDetailed",env,new Vector3(side*5.1f,0,z+1),.70f);
                }
                Prop("nature-kit","tree_palmDetailedTall",env,new Vector3(side*7.0f,0,21),4.6f);
            }
            Shape("ExitAwning",env,new Vector3(0,4.8f,18),new Vector3(9.5f,.22f,4),iron);
        }
    }
}
