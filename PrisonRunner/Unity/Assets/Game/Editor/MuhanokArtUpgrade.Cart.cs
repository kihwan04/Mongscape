using System.Linq;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void EnsureCartAnimation()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animations/Monkey.controller");
            var state=controller.layers[0].stateMachine.states.FirstOrDefault(s=>s.state.name=="AN_Monkey_CartRide").state;
            if(state==null)state=controller.layers[0].stateMachine.AddState("AN_Monkey_CartRide");
            state.motion=AssetDatabase.LoadAllAssetsAtPath(Characters+"/AN_Monkey_CartRide.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            EditorUtility.SetDirty(controller);
        }
        private static GameObject CreateRideCart()
        {
            var root=new GameObject("PROP_CartRide");
            Prop("blender","MineCartEmpty",root.transform,Vector3.zero,1.10f);
            var source=root.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/MineCart_Rumble.wav");
            source.loop=true;source.playOnAwake=false;source.volume=.24f;source.spatialBlend=0;
            var fill=Node("RiderSoftLight",root.transform,new Vector3(0,1.5f,-1.1f)).gameObject.AddComponent<Light>();
            fill.type=LightType.Point;fill.color=new Color(1,.76f,.52f);fill.intensity=1.35f;fill.range=3.3f;fill.shadows=LightShadows.None;
            for(int side=-1;side<=1;side+=2)
            {
                Shape("ForwardLamp",root.transform,new Vector3(side*.46f,.60f,.66f),Vector3.one*.16f,glow,true);
                Shape("Grip",root.transform,new Vector3(side*.52f,.93f,.43f),new Vector3(.24f,.07f,.09f),iron);
            }
            string path=Root+"/Prefabs/PROP_CartRide.prefab";
            PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        private static void CartCavern(Transform env,int type)
        {
            var abyss=Mat("CavernDepth",new Color(.055f,.09f,.12f),0,.1f);
            Shape("RavineFarFloor",env,new Vector3(0,-12,12),new Vector3(38,.5f,24),abyss);
            Shape("CavernCeiling",env,new Vector3(0,11,12),new Vector3(34,1,24),rock);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("RavineCliffBacking",env,new Vector3(side*16,-1,12),new Vector3(3,22,24),rock);
                for(int z=1;z<24;z+=4)
                {
                    Prop("blender","CaveWall",env,new Vector3(side*14,-10,z),14,side<0?0:180);
                    Prop("blender","RockPile",env,new Vector3(side*(7+Next()*4),-8,z),2.4f,Next()*360);
                    Beam("SuspendedBridgeBrace",env,new Vector3(side*.96f,-.13f,z),new Vector3(side*13,-7,z),.23f,wood);
                    Beam("DistantGalleryRail",env,new Vector3(side*11,-3,z),new Vector3(side*11,-3,z+4),.10f,iron);
                    Shape("DistantGallery",env,new Vector3(side*11,-4,z+2),new Vector3(2,.15f,4),wood);
                    Lantern(env,new Vector3(side*11,-2.8f,z),true);
                }
                for(int z=2;z<24;z+=6)
                {
                    Shape("BridgeLampPost",env,new Vector3(side*1.38f,1.4f,z),new Vector3(.20f,2.8f,.25f),wood);
                    Shape("BridgeIronShoe",env,new Vector3(side*1.38f,.20f,z),new Vector3(.28f,.4f,.30f),iron);
                    Lantern(env,new Vector3(side*1.2f,2.65f,z),false,side<0);
                }
            }
            // Rails, sleepers and deck edges are left out of the baked bridge around each gap window; AddRailGaps bakes
            // switchable halves there, so a broken side opens a hole in the deck instead of losing only its rail.
            for(int station=0;station<48;station++)
            {
                float z=station*.5f+.25f;
                if(ObstacleLayout.RailGapAt(z)>=0)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("SingleRail",env,new Vector3(side*.70f,.11f,z),new Vector3(.095f,.14f,.502f),iron);
                    Shape("RailFastener",env,new Vector3(side*.70f,.05f,z),new Vector3(.18f,.045f,.13f),pale);
                    Shape("DeckEdge",env,new Vector3(side*1.02f,-.10f,z),new Vector3(.09f,.18f,.502f),iron);
                }
                Shape("BridgeSleeper",env,new Vector3(0,-.035f,z),new Vector3(2.04f,.09f,.22f),wood);
            }
            for(int z=5;z<24;z+=12)Prop("blender","CaveArch",env,new Vector3(0,7,z),4.3f);
            AmbientMotes(env,new Vector3(0,2,12),24,true);
        }
        private static void CartDock(Transform env,bool exit)
        {
            float z=exit?2:22;
            for(int side=-1;side<=1;side+=2)
            {
                Shape("DockPlatform",env,new Vector3(side*2.0f,.1f,z),new Vector3(1.9f,.2f,4),wood);
                Shape("PlatformEdge",env,new Vector3(side*1.05f,.24f,z),new Vector3(.12f,.08f,4),brass);
                PropCart(env,new Vector3(side*4.1f,0,z),.75f);
            }
            for(int side=-1;side<=1;side+=2)Shape("DockSingleTrack",env,new Vector3(side*.7f,.12f,z),new Vector3(.10f,.15f,4),iron);
        }
        private static void AddRailGaps(GameObject root,int type)
        {
            var prior=root.transform.Find("CartRailGaps");if(prior!=null)Object.DestroyImmediate(prior.gameObject);
            var chunk=root.GetComponent<MuhanokMapChunk>();chunk.RailGaps=new CartRailGap[type==1||type==2?2:0];
            if(chunk.RailGaps.Length==0)return;
            var group=Node("CartRailGaps",root.transform,Vector3.zero);
            for(int i=0;i<ObstacleLayout.RailGapZ.Length;i++)
            {
                float z=ObstacleLayout.RailGapZ[i];var gap=Node("BrokenRail_"+i,group,new Vector3(0,0,z)).gameObject.AddComponent<CartRailGap>();
                // Authored default only; every run sets the side from its seeded layout.
                gap.RequiredLean=(i+type)%2==0?-1:1;chunk.RailGaps[i]=gap;
                gap.Pieces=Node("RailWindow_"+i,group,Vector3.zero);
                gap.LeftRail=WindowRail(gap.Pieces,"LeftRail",-1,z,type);gap.RightRail=WindowRail(gap.Pieces,"RightRail",1,z,type);
                gap.LeftBreak=BrokenRailDressing(gap.Pieces,"LeftBreak",-1,z,type);gap.RightBreak=BrokenRailDressing(gap.Pieces,"RightBreak",1,z,type);
                gap.Configure(-gap.RequiredLean);
            }
        }
        // One side of the bridge, one piece per 0.5m station following the curved route: the same rail, fastener and deck edge
        // boxes as the baked bridge, half of its sleeper, and half of the deck that the editable route floor leaves open here.
        private static GameObject WindowRail(Transform parent,string name,int side,float gapZ,int type)
        {
            var rail=Node(name,parent,Vector3.zero);
            for(int station=0;station<48;station++)
            {
                float z=station*.5f+.25f;if(ObstacleLayout.RailGapAt(z)<0||Mathf.Abs(z-gapZ)>ObstacleLayout.RailGapHalfLength)continue;
                float slope=(RouteSurface.LocalX(type,z+.01f)-RouteSurface.LocalX(type,z-.01f))/.02f,length=.502f*Mathf.Sqrt(1+slope*slope);
                var turn=Quaternion.Euler(0,Mathf.Atan(slope)*Mathf.Rad2Deg,0);
                Shape("SingleRail",rail,RoutePoint(new Vector3(side*.70f,.11f,z),type),new Vector3(.095f,.14f,length),iron).transform.localRotation=turn;
                Shape("RailFastener",rail,RoutePoint(new Vector3(side*.70f,.05f,z),type),new Vector3(.18f,.045f,.13f),pale).transform.localRotation=turn;
                Shape("DeckEdge",rail,RoutePoint(new Vector3(side*1.02f,-.10f,z),type),new Vector3(.09f,.18f,length),iron).transform.localRotation=turn;
                Shape("BridgeSleeper",rail,RoutePoint(new Vector3(side*.51f,-.035f,z),type),new Vector3(1.02f,.09f,.22f),wood).transform.localRotation=turn;
                Shape("DeckHalf",rail,RoutePoint(new Vector3(side*.525f,-.02f,z),type),new Vector3(1.05f,.04f,length),wood).transform.localRotation=turn;
            }
            return rail.gameObject;
        }
        // Hanging rail and splinters under the missing side, and a warning sign that points to the side to lean.
        private static GameObject BrokenRailDressing(Transform parent,string name,int missing,float z,int type)
        {
            var dressing=Node(name,parent,Vector3.zero);int safe=-missing;
            var broken=Shape("HangingBrokenRail",dressing,RoutePoint(new Vector3(missing*.7f,-.75f,z-.5f),type),new Vector3(.10f,.14f,1.7f),iron);
            broken.transform.localRotation=Quaternion.Euler(50,0,missing*22);
            for(int i=0;i<3;i++)
            {
                var plank=Shape("SplinteredSleeper",dressing,RoutePoint(new Vector3(missing*(.5f+i*.12f),-.2f-i*.32f,z-1+i),type),new Vector3(.65f,.10f,.24f),wood);
                plank.transform.localRotation=Quaternion.Euler(i*27,15,missing*35);
            }
            // Blender hazard sign (TrackHazardSign.fbx, 2.6m tall, 1.5m diamond) standing right in the hole: its post rises from
            // deep in the ravine through the missing half of the deck, and the model is lifted so the diamond (bottom about
            // 2.6m) and the arrow board (about 2.2m) pass above the rider's head (camera about 1.6m) instead of through it.
            float signZ=z,slope=(RouteSurface.LocalX(type,signZ+.01f)-RouteSurface.LocalX(type,signZ-.01f))/.02f;
            Beam("HazardSignPost",dressing,RoutePoint(new Vector3(missing*HazardSignOffset,HazardSignFoot,signZ),type),RoutePoint(new Vector3(missing*HazardSignOffset,HazardSignDepth,signZ),type),.12f,iron);
            var sign=Node("TrackWarningSign",dressing,RoutePoint(new Vector3(missing*HazardSignOffset,HazardSignFoot,signZ),type));
            sign.localRotation=Quaternion.Euler(0,Mathf.Atan(slope)*Mathf.Rad2Deg,0);
            var hazard=HazardYellow();HazardPaint(Prop("blender","TrackHazardSign",sign,Vector3.zero,2.6f),hazard);
            // Arrow to the safe side on a black board fixed to the striped post, under the diamond.
            Shape("ArrowBoard",sign,new Vector3(0,.62f,-.11f),new Vector3(1.10f,.42f,.06f),black);
            Beam("ArrowShaft",sign,new Vector3(-safe*.36f,.62f,-.16f),new Vector3(safe*.36f,.62f,-.16f),.09f,hazard);
            for(int wing=-1;wing<=1;wing+=2)Beam("ArrowHead",sign,new Vector3(safe*.38f,.62f,-.16f),new Vector3(safe*.18f,.62f+wing*.15f,-.16f),.09f,hazard);
            for(int i=0;i<3;i++)Shape("SafeSideGoldMarker",dressing,RoutePoint(new Vector3(safe*.92f,.12f,z-2.7f+i*.65f),type),new Vector3(.12f,.06f,.32f),brass);
            return dressing.gameObject;
        }
        // Over the missing rail (0.7m) and a little outward, model foot 1.6m above the deck, post down to 8m below it.
        private const float HazardSignOffset=.8f,HazardSignFoot=1.6f,HazardSignDepth=-8f;
        private static Material HazardYellow()
        {
            var m=Mat("HazardSignYellow",new Color(1,.78f,.06f),0,.3f);Emit(m,new Color(1,.62f,.03f)*1.4f);return m;
        }
        // The sign's FBX slots are named HazardYellow/HazardBlack: yellow glows in the dark ravine, black stays charcoal.
        private static void HazardPaint(GameObject model,Material hazard)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null)continue;
                    if(materials[i].name.Contains("Yellow"))materials[i]=hazard;
                    else if(materials[i].name.Contains("HazardBlack"))materials[i]=black;
                }
                renderer.sharedMaterials=materials;
            }
        }
        // Rebuilds only the two cart chunks and their editable route floors, e.g. after changing the rail windows.
        [MenuItem("Mongscape/Rebuild Cart Chunks")]
        public static void RebuildCartChunks()
        {
            Prepare(); IndexModels(); BuildEditableRoutes(true);
            foreach(int type in new[]{1,2})
            {
                UpgradeChunk(type);
                // Keep the shadow budget that the later material pass applies to every chunk.
                string path=Root+"/Prefabs/"+MuhanokContentBuilder.ChunkIds[type]+".prefab";
                var chunk=PrefabUtility.LoadPrefabContents(path);MuhanokVisualPolish.ConfigureShadows(chunk);
                PrefabUtility.SaveAsPrefabAsset(chunk,path);PrefabUtility.UnloadPrefabContents(chunk);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("MUHANOK_CART_CHUNKS_PASS: open rail windows with switchable rail pieces");
        }
        private static void MatchWalkingMotions(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>();
            foreach(var obstacle in chunk.Obstacles)
            {
                int row=obstacle.GetComponentInParent<MuhanokObstacleSocket>().Row;
                string replacement=type==0&&row==1||type==3&&row==1||type==15&&row==1||type==6&&row==1?"OBS_LowPipe":(type==3||type==15)&&row==0?"OBS_Crate_Low":null;
                if(replacement!=null)
                {
                    Clear(obstacle.transform);var gate=obstacle.GetComponent<DescendingGate>();if(gate!=null)Object.DestroyImmediate(gate);
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+replacement+".prefab");
                    var instance=Object.Instantiate(source);obstacle.Kind=instance.GetComponent<ObstacleController>().Kind;
                    while(instance.transform.childCount>0)instance.transform.GetChild(0).SetParent(obstacle.transform,false);
                    Object.DestroyImmediate(instance);
                }
            }
            chunk.Gates=root.GetComponentsInChildren<DescendingGate>(true);
            if(type==1||type==2)return;
            // Motion guidance lives in the HUD; the actual route remains visually clear.
        }
    }
}
