using System.IO;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Muhanok.Domain;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void Hazards()
        {
            foreach(string id in new[]{"OBS_Crate_Low","OBS_LowPipe","OBS_Barrier_Left","OBS_Barrier_Right","OBS_Cart_Crossing","OBS_LaserGate"})
            {
                var root=EditPrefab(id); Transform t=root.transform;
                if(id=="OBS_Crate_Low") Prop("survival-kit","box-large",t,Vector3.zero,.83f,0,wood);
                else if(id=="OBS_LowPipe")
                {
                    Beam("Pipe",t,new Vector3(-.97f,1.52f,0),new Vector3(.97f,1.52f,0),.35f,iron);
                    for(int side=-1;side<=1;side+=2) { Shape("Collar",t,new Vector3(side*.75f,1.52f,0),Vector3.one*.44f,pale,true); Beam("Leg",t,new Vector3(side*.97f,0,0),new Vector3(side*.97f,1.52f,0),.23f,iron); }
                }
                else if(id=="OBS_LaserGate")
                {
                    for(int side=-1;side<=1;side+=2) Shape("Emitter",t,new Vector3(side*.87f,1.1f,0),new Vector3(.19f,2.2f,.25f),iron);
                    var laser=Mat("LaserGlow",new Color(1,.02f,.01f),0,.3f); Emit(laser,Color.red*5);
                    for(int i=0;i<3;i++) Shape("Beam",t,new Vector3(0,.44f+i*.6f,0),new Vector3(1.8f,.045f,.06f),laser);
                }
                else if(id=="OBS_Cart_Crossing")
                {
                    var cart=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PROP_MineCart.prefab")); cart.transform.SetParent(t,false); cart.transform.localRotation=Quaternion.Euler(0,90,0); cart.transform.localScale=Vector3.one*.85f;
                    foreach(var audio in cart.GetComponentsInChildren<AudioSource>()) UnityEngine.Object.DestroyImmediate(audio);
                }
                else
                {
                    for(int side=-1;side<=1;side+=2) { Shape("Foot",t,new Vector3(side*.65f,.1f,0),new Vector3(.28f,.2f,.65f),iron); Shape("Post",t,new Vector3(side*.65f,.70f,0),new Vector3(.13f,1.4f,.14f),iron); }
                    Shape("StopBoard",t,new Vector3(0,.78f,0),new Vector3(1.8f,1.16f,.22f),cream);
                    for(int i=-2;i<=2;i++) { var stripe=Shape("RedStripe",t,new Vector3(i*.36f,.78f,-.132f),new Vector3(.19f,1.12f,.028f),red); stripe.transform.localRotation=Quaternion.Euler(0,0,-24); }
                    Shape("TopRail",t,new Vector3(0,1.39f,0),new Vector3(1.93f,.11f,.28f),iron);
                    for(int side=-1;side<=1;side+=2) Shape("WarningLamp",t,new Vector3(side*.64f,1.48f,0),new Vector3(.15f,.13f,.16f),glow,true);
                }
                Save(root);
            }
        }
        private static void UpgradeChunk(int index)
        {
            string id=MuhanokContentBuilder.ChunkIds[index], path=Root+"/Prefabs/"+id+".prefab", backup="../Builds/ArtBackups/BlockoutBackup/"+id+".prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); if(!File.Exists(backup)) File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path); var env=root.transform.Find("EnvironmentRoot"); Clear(env);
            var priorRoute=root.transform.Find("RouteStructure"); if(priorRoute!=null) UnityEngine.Object.DestroyImmediate(priorRoute.gameObject);
            AddCoins(root);
            rng=new System.Random(723+index*71);
            if(index==1||index==2) CartCavern(env,index);
            else if(index<3) {Mine(env,0,24,index);CartDock(env,false);}
            else if(index==RouteSurface.MineStair) MineStairLights(env);
            else if(index==3)
            {
                Mine(env,0,12,index); CartDock(env,true); PrisonShell(env,12,24,index);
                for(int slab=12;slab<24;slab++)Shape("ServiceFloorSlab",env,new Vector3(0,.008f,slab+.5f),new Vector3(9.8f,.016f,1.002f),concrete);
                Portal(env,12,wood); Portal(env,15,iron); Label("SERVICE ACCESS",env,new Vector3(0,4.05f,15.15f),.16f,cream.color);
                Prop("factory-kit","pipe-large-valve",env,new Vector3(3.6f,.3f,13),1.4f,-90,iron);
            }
            else if(index==RouteSurface.ZooStair) ZooStairProps(env);
            else if(index>=11) Outdoors(env,index);
            else if(index==6){PrisonShell(env,0,16,index);RoomDetails(env,index);ServiceExit(env);}
            else { PrisonShell(env,0,24,index); RoomDetails(env,index); }
            // Identical floor height, width and clear runner envelope at every 24m boundary.
            for(int segment=0;segment<24&&index!=1&&index!=2&&!HasStairMesh(index);segment++)
            {
                if(!File.Exists(Authoring+"/"+id+".prefab")) ProBuilderBox("ContinuousFloor",env,new Vector3(0,-.19f,segment+.5f),new Vector3(9.8f,.38f,1.002f),RouteSurface.IsMine(index)?rock:concrete);
                for(int side=-1;side<=1;side+=2) Shape("BoundaryCurb",env,new Vector3(side*3.53f,.075f,segment+.5f),new Vector3(.14f,.15f,1.002f),RouteSurface.IsMine(index)?rockLight:iron);
            }
            if(!HasStairMesh(index)) AddBoundaryConnectors(env,index);
            AddPrisonGates(root,index);
            AddZooHazards(root,index);
            MatchWalkingMotions(root,index);
            AddClosingPrisonEntry(root,index);
            ResetStairObstacles(root,index);
            AddRailGaps(root,index);
            AddFloorItems(root,index);
            var collider=root.GetComponent<BoxCollider>(); if(collider==null) collider=root.AddComponent<BoxCollider>(); collider.center=new Vector3(0,-.19f,12); collider.size=new Vector3(9.8f,.38f,24);collider.enabled=index!=1&&index!=2;
            AssetDatabase.StartAssetEditing();
            try { BakeEnvironment(env,id); ApplyRouteSurface(root,index); }
            finally { AssetDatabase.StopAssetEditing(); }
            AddStairMesh(env,index);
            if(HasStairMesh(index)) Node("RouteStructure",root.transform,Vector3.zero); // the Blender mesh owns the whole stair surface
            else AddBakedAuthoringRoute(root,id);
            AddCellActors(root,index);
            MuhanokGameplayUpgrade.Configure(root,index);
            Save(root);
        }
        // Stair chunks keep the six obstacle sockets of the chunk contract, but nothing sits in them: the old stair hazard
        // instances are replaced by inert, inactive controllers.
        private static void ResetStairObstacles(GameObject root,int type)
        {
            if(!RouteSurface.IsStair(type))return;
            var chunk=root.GetComponent<MuhanokMapChunk>();
            for(int i=0;i<chunk.Obstacles.Length;i++)
            {
                var socket=chunk.Obstacles[i].transform.parent;
                Object.DestroyImmediate(chunk.Obstacles[i].gameObject);
                var inert=new GameObject("NoHazard_"+i);inert.transform.SetParent(socket,false);
                var obstacle=inert.AddComponent<ObstacleController>();obstacle.Kind=ObstacleKind.Barrier;inert.SetActive(false);
                chunk.Obstacles[i]=obstacle;
            }
        }
        private static void Mine(Transform env,float begin,float end,int variation)
        {
            float middle=(begin+end)*.5f, length=end-begin;
            if(begin>=0&&(variation==1||variation==2))Label(variation==1?"CART ROUTE / STEER":"SERVICE LANDING / EXIT",env,new Vector3(0,4.05f,variation==1?3:21),.14f,cream.color);
            Shape("DarkRoof",env,new Vector3(0,5.6f,middle),new Vector3(10.8f,.6f,length),rock);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("RockBacking",env,new Vector3(side*5.5f,2.8f,middle),new Vector3(.5f,5.6f,length),rock);
                for(float z=begin+.9f;z<end;z+=2.7f)
                {
                    Prop("blender","CaveWall",env,new Vector3(side*4.95f,0,z),4.75f,side<0?0:180);
                    Prop("blender","RockPile",env,new Vector3(side*4.03f,0,z+.65f),.70f,Next()*360);
                    for(int j=0;j<3;j++)
                    {
                        Shape("Rubble",env,new Vector3(side*(3.8f+Next()*.7f),.11f,z+j*.5f),new Vector3(.26f+Next()*.5f,.22f+Next()*.30f,.37f+Next()*.3f),j==0?brass:rockLight,true);
                    }
                    if((int)z%3==0) for(int j=0;j<5;j++)
                    {
                        var ore=Shape("EmbeddedOre",env,new Vector3(side*4.32f,1.08f+j*.14f,z+(j%3)*.17f),
                            new Vector3(.20f,.22f+Next()*.23f,.22f),brass,true);
                        ore.transform.localRotation=Quaternion.Euler(15+j*19,j*31,j*18);
                    }
                }
                for(float z=begin+2;z<end;z+=6)
                {
                    Shape("TimberUpright",env,new Vector3(side*3.85f,2.35f,z),new Vector3(.37f,4.7f,.42f),wood);
                    Shape("IronFoot",env,new Vector3(side*3.85f,.22f,z),new Vector3(.48f,.44f,.49f),iron);
                    Shape("IronStrap",env,new Vector3(side*3.85f,3.25f,z),new Vector3(.40f,.18f,.45f),iron);
                    Shape("UpperIronStrap",env,new Vector3(side*3.85f,4.46f,z),new Vector3(.41f,.22f,.46f),iron);
                    for(int rivet=0;rivet<2;rivet++) Shape("BeamRivet",env,new Vector3(side*3.60f,4.41f+rivet*.12f,z),Vector3.one*.073f,brass,true);
                    Beam("DiagonalBrace",env,new Vector3(side*3.85f,3.35f,z),new Vector3(side*2.45f,4.8f,z),.24f,wood);
                    Lantern(env,new Vector3(side*3.35f,3.13f,z+.18f),false,side==-1);
                }
            }
            for(float z=begin+2;z<end;z+=6)
            {
                Shape("TimberLintel",env,new Vector3(0,4.72f,z),new Vector3(8.1f,.36f,.44f),wood);
                Prop("blender","CaveArch",env,new Vector3(0,3.5f,z+1),2.2f,0);
            }
            for(int lane=-1;lane<=1;lane++)
            {
                for(int side=-1;side<=1;side+=2) for(float z=begin;z<end;z+=1) Shape("Rail",env,new Vector3(lane*2.2f+side*.53f,.075f,z+.5f),new Vector3(.085f,.13f,1.002f),iron);
                for(float z=begin+.35f;z<end;z+=1.22f)
                {
                    Shape("Sleeper",env,new Vector3(lane*2.2f,.038f,z),new Vector3(1.55f,.065f,.23f),wood);
                    for(int side=-1;side<=1;side+=2) Shape("RailFastener",env,new Vector3(lane*2.2f+side*.53f,.09f,z),new Vector3(.19f,.045f,.16f),pale);
                }
                for(float z=begin+.6f;z<end;z+=2.8f) Shape("GroundGravel",env,new Vector3(lane*2.2f+.18f,.03f,z),new Vector3(.25f,.065f,.18f),rockLight,true);
            }
            Prop("survival-kit","barrel",env,new Vector3(-3.98f,0,begin+4),.95f,12,wood);
            Prop("survival-kit","workbench",env,new Vector3(4.0f,0,begin+10),.9f,-90,wood);
            AmbientMotes(env,new Vector3(0,2,middle),length,true);
            if(variation==1&&begin>=0)
            {
                for(float z=3;z<end;z+=4)
                {
                    Shape("TrestleDeck",env,new Vector3(0,-.02f,z),new Vector3(7.2f,.15f,.8f),wood);
                    for(int side=-1;side<=1;side+=2) { Beam("TrestleGuard",env,new Vector3(side*3.35f,.4f,z),new Vector3(side*3.35f,1.08f,z),.10f,wood); }
                    if(z<end-3) PropCart(env,new Vector3(3.9f,0,z),.68f);
                }
            }
        }
        private static float Next()=> (float)rng.NextDouble();
        private static void AddCoins(GameObject root)
        {
            var previous=root.transform.Find("Collectibles"); if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group=Node("Collectibles",root.transform,Vector3.zero); var chunk=root.GetComponent<MuhanokMapChunk>(); chunk.Coins=new CoinController[6];
            var goldFace=Mat("CoinGoldFace",new Color(1,.68f,.035f),.35f,.5f); Emit(goldFace,new Color(1,.45f,.01f)*.35f);
            for(int i=0;i<6;i++)
            {
                var coin=Node("BananaCoin_"+i,group,new Vector3(0,1.0f,4+i*3.5f));
                var disc=Shape("Rim",coin,Vector3.zero,Vector3.one*.53f,brass); disc.GetComponent<MeshFilter>().sharedMesh=coinMesh;
                Shape("Face",coin,new Vector3(0,0,-.055f),new Vector3(.39f,.39f,.014f),goldFace,true);
                for(int j=0;j<6;j++)
                {
                    float a=Mathf.Lerp(-2.5f,.12f,j/6f), b=Mathf.Lerp(-2.5f,.12f,(j+1)/6f);
                    Beam("BananaMark",coin,new Vector3(Mathf.Cos(a)*.14f,Mathf.Sin(a)*.14f+.04f,-.066f),new Vector3(Mathf.Cos(b)*.14f,Mathf.Sin(b)*.14f+.04f,-.066f),.045f,wood);
                }
                chunk.Coins[i]=coin.gameObject.AddComponent<CoinController>();
            }
        }
        private static void Lantern(Transform env,Vector3 pos,bool cold,bool shadow=false)
        {
            var root=Node("Lantern",env,pos);
            Shape("Bracket",root,new Vector3(0,.26f,0),new Vector3(.10f,.30f,.14f),iron);
            Shape("Glass",root,Vector3.zero,new Vector3(.22f,.34f,.22f),cold?blue:glow);
            Shape("Cap",root,new Vector3(0,.22f,0),new Vector3(.37f,.13f,.37f),iron);
            Shape("Base",root,new Vector3(0,-.22f,0),new Vector3(.32f,.10f,.32f),iron);
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2) Shape("Cage",root,new Vector3(x*.125f,0,z*.125f),new Vector3(.03f,.40f,.03f),iron);
            var light=Node("Practical",root,Vector3.zero).gameObject.AddComponent<Light>(); light.type=LightType.Point; light.color=cold?new Color(.42f,.73f,1):new Color(1,.52f,.16f);
            light.intensity=cold?2.3f:3.5f; light.range=7.0f; light.shadows=shadow?LightShadows.Soft:LightShadows.None; light.shadowBias=.035f; light.shadowNormalBias=.16f;
        }
        private static void Portal(Transform env,float z,Material material)
        {
            for(int side=-1;side<=1;side+=2) Shape("PortalPillar",env,new Vector3(side*4.08f,2.45f,z),new Vector3(.35f,4.9f,.32f),material);
            Shape("PortalHeader",env,new Vector3(0,4.8f,z),new Vector3(8.5f,.27f,.34f),material);
        }
        private static void PrisonShell(Transform env,float begin,float end,int index)
        {
            float middle=(begin+end)*.5f,length=end-begin;
            bool cellBlock=index==4||index==5;
            float wallX=cellBlock?7.9f:4.92f;
            Shape("Ceiling",env,new Vector3(0,5.1f,middle),new Vector3(cellBlock?16:10,.25f,length),concrete);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("ConcreteWall",env,new Vector3(side*wallX,2.45f,middle),new Vector3(.30f,4.9f,length),concrete);
                Shape("DadoStripe",env,new Vector3(side*(wallX-.2f),1.05f,middle),new Vector3(.065f,.18f,length),navy);
                Shape("ServiceDuct",env,new Vector3(side*4.28f,4.45f,middle),new Vector3(.23f,.25f,length),iron);
                for(float z=begin+2;z<end;z+=5.5f)
                {
                    Shape("WallColumn",env,new Vector3(side*4.6f,2.45f,z),new Vector3(.25f,4.9f,.20f),pale);
                    Lantern(env,new Vector3(side*3.97f,3.45f,z),true,side==-1&&z==begin+2);
                    for(int row=0;row<4;row++) Shape("WallJoint",env,new Vector3(side*(wallX-.18f),1.7f+row*.72f,z+1.75f),new Vector3(.015f,.028f,3.4f),iron);
                }
                for(float z=begin+.5f;z<end;z+=1.5f) Shape("AisleMark",env,new Vector3(side*3.12f,.018f,z),new Vector3(.07f,.022f,.8f),cream);
            }
            Portal(env,begin+.16f,pale); Portal(env,end-.16f,pale);
            for(float z=begin+3;z<end;z+=6) { Beam("RoofPipe",env,new Vector3(-4,4.78f,z),new Vector3(4,4.78f,z),.16f,iron); Shape("CeilingStrip",env,new Vector3(0,4.77f,z+.4f),new Vector3(1.7f,.08f,.15f),blue); }
            AmbientMotes(env,new Vector3(0,2,middle),length,false);
        }
        private static void Cell(Transform env,int side,float z,int number)
        {
            var room=Node("EnclosedCell_"+number,env,Vector3.zero);
            Shape("CellFloor",room,new Vector3(side*5.95f,-.06f,z+2.1f),new Vector3(3.9f,.12f,4.8f),concrete);
            foreach(float edge in new[]{z-.3f,z+4.5f})
                Shape("CellPartition",room,new Vector3(side*5.95f,2.05f,edge),new Vector3(3.9f,4.1f,.18f),concrete);
            Shape("CellLintel",room,new Vector3(side*3.97f,3.75f,z+2.1f),new Vector3(.26f,.7f,4.8f),concrete);
            for(int i=0;i<=16;i++)Beam("CellBar",room,new Vector3(side*3.97f,.06f,z-.15f+i*.28f),new Vector3(side*3.97f,3.4f,z-.15f+i*.28f),.055f,iron);
            foreach(float h in new[]{.1f,1.15f,3.4f})Shape("CellCrossbar",room,new Vector3(side*3.97f,h,z+2.1f),new Vector3(.08f,.08f,4.6f),pale);
            // Separate door frame, visible latch and hinges make this read as a locked cell.
            foreach(float edge in new[]{z+1.30f,z+2.70f})Shape("DoorJamb",room,new Vector3(side*3.92f,1.72f,edge),new Vector3(.13f,3.44f,.12f),iron);
            Shape("DoorLatch",room,new Vector3(side*3.87f,1.15f,z+2.55f),new Vector3(.16f,.20f,.30f),brass);
            foreach(float h in new[]{.6f,2.6f})Shape("DoorHinge",room,new Vector3(side*3.91f,h,z+1.3f),new Vector3(.18f,.23f,.12f),pale);
            Prop("furniture-kit","bedBunk",room,new Vector3(side*6.95f,0,z+2.9f),1.48f,side<0?-90:90);
            Shape("FoldedBlanket",room,new Vector3(side*6.95f,.64f,z+2.9f),new Vector3(.85f,.09f,.70f),navy);
            Shape("CellBench",room,new Vector3(side*6.8f,.38f,z+.7f),new Vector3(1.6f,.16f,.48f),wood);
            Shape("WaterBowl",room,new Vector3(side*5.1f,.08f,z+.55f),new Vector3(.28f,.12f,.28f),pale,true);
            Shape("NumberPlate",room,new Vector3(side*3.84f,3.65f,z+2),new Vector3(.06f,.3f,.7f),navy);
            Label(number.ToString("000"),room,new Vector3(side*3.78f,3.65f,z+2),.1f,cream.color,side<0?-90:90);
            Lantern(room,new Vector3(side*7.55f,2.85f,z+2),false);
        }
        private static void RoomDetails(Transform env,int index)
        {
            if(index==4||index==5)
            {
                for(int side=-1;side<=1;side+=2) for(int z=4;z<20;z+=6) Cell(env,side,z,100+index*20+z+(side>0?10:0));
                Label(index==4?"CELL BLOCK A":"CELL BLOCK B",env,new Vector3(0,4.25f,23.8f),.18f,cream.color);
            }
            else if(index==6)
            {
                Label("SECURITY CHECKPOINT",env,new Vector3(0,4.15f,18),.17f,cream.color);
                Prop("factory-kit","screen-panel-wide",env,new Vector3(-3.85f,.15f,7),1.4f,-90);
                // The kit door includes a solid panel; an open frame preserves all three lanes.
                Portal(env,14,iron);
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("OpenGatePanel",env,new Vector3(side*4.1f,1.7f,14.7f),new Vector3(.15f,3.4f,1.8f),iron);
                    Shape("GateStatus",env,new Vector3(side*3.95f,3.5f,14),new Vector3(.25f,.15f,.18f),glow);
                }
                for(int side=-1;side<=1;side+=2) { Prop("factory-kit","scanner-high",env,new Vector3(side*3.85f,0,16),2.6f,side<0?-90:90); Shape("RedBeacon",env,new Vector3(side*3.75f,3.0f,14),Vector3.one*.20f,red,true); Lantern(env,new Vector3(side*3.45f,3.25f,14),false); }
            }
            else if(index==7)
            {
                for(int tread=1;tread<24;tread++)
                {
                    Shape("StairNosing",env,new Vector3(0,.026f,tread),new Vector3(6.6f,.05f,.09f),brass);
                    float rise=RouteSurface.LocalHeight(7,tread)-RouteSurface.LocalHeight(7,tread-1);
                    Shape("StairRiser",env,new Vector3(0,-rise*.5f,tread),new Vector3(6.6f,rise,.045f),concrete);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    for(int z=0;z<24;z++) Beam("StairHandrail",env,new Vector3(side*3.4f,1.06f,z),new Vector3(side*3.4f,1.06f,z+1),.09f,brass);
                    for(int z=0;z<=24;z+=3) Beam("StairPost",env,new Vector3(side*3.4f,.1f,z),new Vector3(side*3.4f,1.05f,z),.07f,brass);
                    for(int z=3;z<24;z+=5) { Shape("RedServicePipe",env,new Vector3(side*3.8f,3.6f,z),new Vector3(.27f,.27f,4.9f),red); Lantern(env,new Vector3(side*3.30f,2.6f,z),false); }
                }
                for(int side=-1;side<=1;side+=2) { Prop("factory-kit","catwalk-stairs",env,new Vector3(side*4.10f,0,8),2.8f,side<0?-90:90,iron); Beam("LandingRail",env,new Vector3(side*3.7f,1.1f,3),new Vector3(side*3.7f,3.8f,12),.10f,pale); }
                Label("UTILITY LEVEL / KEEP MOVING",env,new Vector3(0,4.2f,22),.14f,cream.color);
            }
            else if(index==8)
            {
                for(int side=-1;side<=1;side+=2) for(int z=3;z<23;z+=4) { bool stacked=(z/4)%2==0; Prop("furniture-kit",stacked?"washerDryerStacked":"washer",env,new Vector3(side*4.05f,0,z),stacked?2.4f:1.35f,side<0?-90:90); Shape("Backsplash",env,new Vector3(side*4.72f,1.0f,z),new Vector3(.07f,1.9f,3.8f),tile); }
                Prop("furniture-kit","bench",env,new Vector3(-4.0f,0,18),.65f,-90); Label("LAUNDRY 08",env,new Vector3(0,4.2f,22),.19f,cream.color);
            }
            else if(index==9)
            {
                for(int side=-1;side<=1;side+=2) for(int z=3;z<23;z+=4) { Prop("furniture-kit",z%3==0?"kitchenStove":"kitchenSink",env,new Vector3(side*4.15f,0,z),1.10f,side<0?-90:90); Prop("furniture-kit","hoodLarge",env,new Vector3(side*4.2f,2.0f,z),.7f,side<0?-90:90); }
                Prop("furniture-kit","kitchenFridgeLarge",env,new Vector3(-4.2f,0,17),2.1f,-90); Label("KITCHEN / MESS HALL",env,new Vector3(0,4.2f,22),.17f,cream.color);
            }
            else if(index==10)
            {
                for(int side=-1;side<=1;side+=2) for(int z=3;z<24;z+=5)
                { Prop("factory-kit","pipe-large-valve",env,new Vector3(side*4.18f,.15f,z),1.4f,side<0?-90:90,iron); Prop("factory-kit",z%2==0?"machine-window-bar":"machine-fortified",env,new Vector3(side*4.24f,0,z+1.4f),2.0f,side<0?-90:90); }
                Label("MAINTENANCE / YARD ACCESS",env,new Vector3(0,4.2f,22),.15f,cream.color);
            }
        }
        private static void Outdoors(Transform env,int index)
        {
            ZooGroundDetails(env,index);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=1;z<24;z+=4)
                {
                    Prop("nature-kit",z%8==1?"tree_palmDetailedTall":"tree_detailed_dark",env,new Vector3(side*(7.1f+Next()*1.7f),0,z),4.0f+Next()*1.3f,Next()*360);
                    Prop("nature-kit","plant_bushDetailed",env,new Vector3(side*3.95f,0,z+1.7f),.55f,Next()*360);
                    Prop("nature-kit","stone_largeA",env,new Vector3(side*5.8f,0,z),.9f,Next()*360);
                }
                var animal=Prop("blender",side<0?"ZooGiraffe":"ZooElephant",env,new Vector3(side*5.75f,0,index==11?10:16),side<0?3.5f:2.45f,side<0?70:-70);
                animal.AddComponent<AmbientAnimal>();
                for(int z=1;z<24;z+=4)
                {
                    Beam("FencePost",env,new Vector3(side*4.05f,0,z),new Vector3(side*4.05f,2.5f,z),.14f,iron);
                    for(int i=0;i<8;i++) { float zz=z+i*.50f; Beam("FenceWire",env,new Vector3(side*4.05f,.35f,zz),new Vector3(side*4.05f,2.4f,zz),.045f,iron); }
                    for(int row=0;row<3;row++) Shape("FenceRail",env,new Vector3(side*4.05f,.45f+row*.94f,z+1.75f),new Vector3(.055f,.065f,4.01f),iron);
                    Shape("EnclosurePlinth",env,new Vector3(side*4.05f,.16f,z+1.5f),new Vector3(.45f,.32f,4.01f),rockLight);
                }
                Beam("LampMast",env,new Vector3(side*3.9f,0,12),new Vector3(side*3.9f,5.6f,12),.15f,iron);
                Lantern(env,new Vector3(side*3.65f,5.3f,12),false);
                Prop("furniture-kit","bench",env,new Vector3(side*5.0f,0,6),.70f,side<0?-90:90);
                for(int z=2;z<24;z+=3) Shape("PavingMark",env,new Vector3(side*3.1f,.018f,z),new Vector3(.06f,.02f,1.7f),cream);
                if(index==12)
                {
                    Shape("WatchTower",env,new Vector3(side*7.5f,4.6f,17),new Vector3(2.4f,9.2f,2.4f),concrete);
                    Shape("ObservationBox",env,new Vector3(side*7.5f,9.0f,17),new Vector3(3.4f,1.8f,3.4f),navy);
                    Shape("TowerWindow",env,new Vector3(side*7.5f,9.15f,15.27f),new Vector3(2.8f,.75f,.06f),blue);
                    Shape("TowerRoof",env,new Vector3(side*7.5f,10.1f,17),new Vector3(3.9f,.3f,3.9f),iron);
                }
            }
            Portal(env,.16f,pale); Portal(env,23.84f,pale);
            Label(index==11?"ANIMAL ENCLOSURES / NIGHT ROUTE":index==RouteSurface.ZooStair?"OVERLOOK STAIR / KEEP STEPPING":"MINE SERVICE RETURN",env,new Vector3(0,4.4f,23.5f),.15f,cream.color);
            if(index==12) { Shape("ReturnCanopy",env,new Vector3(0,5.1f,20),new Vector3(9,.24f,8),concrete); Lantern(env,new Vector3(3.8f,3.4f,22),true); }
        }
        private static void AmbientMotes(Transform parent,Vector3 pos,float length,bool warm)
        {
            var obj=Node("AmbientDust",parent,pos); var ps=obj.gameObject.AddComponent<ParticleSystem>();
            var main=ps.main; main.startLifetime=6; main.startSpeed=.035f; main.startSize=.028f; main.maxParticles=24; main.startColor=warm?new Color(1,.7f,.3f,.22f):new Color(.45f,.75f,1,.13f);
            var emission=ps.emission; emission.rateOverTime=2; var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(6.5f,3.5f,length);
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=warm?glow:blue; renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static void UpgradeScene()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity");
            var game=UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            game.Settings.Run.SlipSeconds=2.4f;game.Settings.Run.SlipSpeedMultiplier=.5f;game.Settings.Run.CartSpeed=10.5f;EditorUtility.SetDirty(game.Settings);
            game.HandsPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/FP_Hands.prefab");
            game.CartRidePrefab=CreateRideCart();game.Settings.Run.BananaCatchSeconds=1.2f;
            game.TitleLogo=ConfigureTitleLogoTexture();
            game.LoadingImage=ConfigureLoadingTexture();
            var stage=GameObject.Find("Mine Title Stage"); Clear(stage.transform); rng=new System.Random(71); Mine(stage.transform,-24,0,1);
            Shape("StageFloor",stage.transform,new Vector3(0,-.19f,-12),new Vector3(9.8f,.38f,24),rock);
            Shape("BackMineRock",stage.transform,new Vector3(0,2.7f,-24),new Vector3(10,5.4f,1),rock);
            var key=Node("CharacterKey",stage.transform,new Vector3(1.5f,2.4f,4)).gameObject.AddComponent<Light>(); key.type=LightType.Point; key.color=new Color(1,.84f,.64f); key.intensity=2.5f; key.range=6;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.32f,.27f,.22f);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Exponential; RenderSettings.fogColor=new Color(.11f,.07f,.045f); RenderSettings.fogDensity=.027f;
            var fill=GameObject.Find("Soft Fill").GetComponent<Light>(); fill.intensity=.55f; fill.color=new Color(.72f,.79f,.92f); fill.shadows=LightShadows.None; fill.transform.rotation=Quaternion.Euler(35,160,0);
            var bounce=GameObject.Find("Warm Bounce"); if(bounce==null) bounce=new GameObject("Warm Bounce"); var bounceLight=bounce.GetComponent<Light>(); if(bounceLight==null) bounceLight=bounce.AddComponent<Light>(); bounceLight.type=LightType.Directional; bounceLight.color=new Color(.85f,.73f,.60f); bounceLight.intensity=.30f; bounceLight.transform.rotation=Quaternion.Euler(25,-15,0);
            var camera=game.OutputCamera; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=RenderSettings.fogColor; camera.allowHDR=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true; camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var atmosphere=camera.GetComponent<WorldAtmosphere>(); if(atmosphere==null) atmosphere=camera.gameObject.AddComponent<WorldAtmosphere>(); atmosphere.Fill=fill;
            var sky=Mat("OutdoorSky",Color.white,0,0);sky.shader=Shader.Find("Muhanok/NightSky");sky.shaderKeywords=new string[0];
            sky.SetColor("_Horizon",new Color(.22f,.38f,.65f));sky.SetColor("_Zenith",new Color(.055f,.10f,.23f));sky.SetColor("_Cloud",new Color(.10f,.20f,.36f));RenderSettings.skybox=sky;
            var oldMoon=GameObject.Find("Yard Moon"); if(oldMoon!=null) Object.DestroyImmediate(oldMoon);
            atmosphere.Moon=null; // Moon and halo are rendered by the actual night sky shader.
            var existing=GameObject.Find("Muhanok Art Volume"); if(existing!=null) UnityEngine.Object.DestroyImmediate(existing);
            string volumePath=Art+"/MuhanokVolume.asset"; var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,volumePath); }
            var bloom=profile.TryGet<Bloom>(out var bl)?bl:profile.Add<Bloom>(); bloom.intensity.Override(.65f); bloom.threshold.Override(.9f); bloom.scatter.Override(.70f);
            var color=profile.TryGet<ColorAdjustments>(out var ca)?ca:profile.Add<ColorAdjustments>(); color.postExposure.Override(.9f); color.contrast.Override(8); color.saturation.Override(3);
            var tone=profile.TryGet<Tonemapping>(out var tm)?tm:profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var depth=profile.TryGet<DepthOfField>(out var df)?df:profile.Add<DepthOfField>();
            depth.mode.Override(DepthOfFieldMode.Bokeh);depth.focusDistance.Override(3.4f);
            depth.focalLength.Override(65);depth.aperture.Override(3.2f);
            if(camera.GetComponent<CinematicFocus>()==null)camera.gameObject.AddComponent<CinematicFocus>();
            var vignette=profile.TryGet<Vignette>(out var vi)?vi:profile.Add<Vignette>(); vignette.intensity.Override(.16f); vignette.smoothness.Override(.35f);
            var volume=new GameObject("Muhanok Art Volume").AddComponent<Volume>(); volume.isGlobal=true; volume.priority=10; volume.sharedProfile=profile;
            camera.GetComponent<CinematicFocus>().Volume=volume;
            // Add persistent subassets for Volume components newly created by code.
            foreach(var component in profile.components) if(!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);
            BakeEnvironment(stage.transform,"TitleStage");
            var rig=game.CameraRig; rig.Menu.transform.position=new Vector3(3.0f,2.0f,4.8f); rig.Menu.transform.rotation=Quaternion.LookRotation(new Vector3(-.4f,1.0f,2)-rig.Menu.transform.position); rig.Menu.Lens.FieldOfView=58;
            EditorUtility.SetDirty(game); EditorSceneManager.SaveScene(scene);
        }
        private static void BakeEnvironment(Transform env,string id)
        {
            var groups=new System.Collections.Generic.Dictionary<Material,System.Collections.Generic.List<CombineInstance>>();
            foreach(var filter in env.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.GetComponentInParent<AmbientAnimal>()!=null) continue;
                if(filter.sharedMesh==null||filter.GetComponent<TextMesh>()!=null) continue;
                var renderer=filter.GetComponent<MeshRenderer>(); if(renderer==null) continue;
                for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                    if(material==null) continue;
                    if(!groups.TryGetValue(material,out var items)) { items=new System.Collections.Generic.List<CombineInstance>(); groups[material]=items; }
                    items.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=env.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
                UnityEngine.Object.DestroyImmediate(renderer); UnityEngine.Object.DestroyImmediate(filter);
            }
            foreach(var group in groups)
            {
                string path=Art+"/Meshes/"+id+"_"+group.Key.name+".asset";
                var combined=new Mesh{name=id+"_"+group.Key.name,indexFormat=IndexFormat.UInt32}; combined.CombineMeshes(group.Value.ToArray(),true,true);
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null) { EditorUtility.CopySerialized(combined,existing); UnityEngine.Object.DestroyImmediate(combined); combined=existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(combined,path);
                var item=Node("Batched_"+group.Key.name,env,Vector3.zero); item.gameObject.AddComponent<MeshFilter>().sharedMesh=combined; item.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
        }
    }
}
