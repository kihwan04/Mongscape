using System;
using System.Collections.Generic;
using System.IO;
using Muhanok.Bootstrap;
using Muhanok.Presentation;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private const string Root = "Assets/Game/Content";
        private const string Art = Root + "/ArtV2";
        private static Mesh box, sphere, coinMesh;
        private static Material rock, rockLight, wood, iron, brass, glow, fur, face, cream, orange, navy, black, red, pale, blue, tile, concrete;
        private static readonly Dictionary<string,GameObject> models = new Dictionary<string,GameObject>();
        private static readonly Dictionary<Material,Material> kitMaterials = new Dictionary<Material,Material>();
        private static System.Random rng;

        [MenuItem("Mongscape/Upgrade Art and All Maps")]
        public static void Upgrade()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string folder in new[] { "Materials", "Meshes", "Textures", "Prefabs" }) Directory.CreateDirectory(Art+"/"+folder);
            AssetDatabase.Refresh(); Prepare(); IndexModels(); AnimateCharacters();
            Character(false); Character(true); Cart(); Pickaxe(); Bulb(); Hands(); Hazards(); ZooLowLog();
            for (int i=0;i<MuhanokContentBuilder.ChunkIds.Length;i++) UpgradeChunk(i);
            UpgradeScene(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            MuhanokBuildTools.ValidateContent();
            Debug.Log("MUHANOK_ART_V2_PASS: 13 authored maps / CC0 props / faceted characters / lantern lighting / continuous boundaries / first-person hands");
        }
        private static void Prepare()
        {
            box = MeshAsset("BevelBox",()=>MuhanokArtGeometry.BevelBox()); sphere = MeshAsset("FacetedSphere",()=>MuhanokArtGeometry.FacetedSphere());
            coinMesh=MeshAsset("CoinDisc",()=>MuhanokArtGeometry.CoinDisc());
            rock=Mat("Slate",new Color(.36f,.32f,.28f),0,.17f,"stone"); rockLight=Mat("SlateEdge",new Color(.47f,.41f,.34f),0,.12f,"stone");
            wood=Mat("Timber",new Color(.46f,.25f,.095f),0,.25f,"wood"); iron=Mat("ForgedIron",new Color(.20f,.24f,.27f),.48f,.36f);
            brass=Mat("OreGold",new Color(1,.72f,.07f),.35f,.42f); Emit(brass,new Color(1,.55f,.025f)*.75f); glow=Mat("LanternGlass",new Color(1,.61f,.19f),0,.35f); Emit(glow,new Color(1,.39f,.07f)*4.5f);
            fur=Mat("MonkeyFur",new Color(.35f,.125f,.035f),0,.14f); face=Mat("MonkeyFace",new Color(.93f,.59f,.28f),0,.18f);
            cream=Mat("Ivory",new Color(.93f,.86f,.67f),0,.24f); orange=Mat("PrisonCloth",new Color(.9f,.245f,.045f),0,.12f,"cloth");
            navy=Mat("PoliceCloth",new Color(.075f,.16f,.30f),0,.18f,"cloth"); black=Mat("Charcoal",new Color(.023f,.026f,.035f),.08f,.33f);
            red=Mat("SecurityRed",new Color(.8f,.085f,.042f),.1f,.28f); pale=Mat("PaintedSteel",new Color(.56f,.65f,.67f),.35f,.34f);
            blue=Mat("ColdLamp",new Color(.44f,.72f,.94f),0,.5f); Emit(blue,new Color(.4f,.7f,1)*2.4f);
            concrete=Mat("PrisonConcrete",new Color(.27f,.32f,.34f),0,.12f,"stone"); tile=Mat("Ceramic",new Color(.47f,.59f,.59f),0,.35f);
        }
        private static Mesh MeshAsset(string name, Func<Mesh> factory)
        {
            string path=Art+"/Meshes/"+name+".asset"; var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null) { mesh=factory(); AssetDatabase.CreateAsset(mesh,path); } return mesh;
        }
        private static Material Mat(string name,Color color,float metallic,float smooth,string texture=null)
        {
            string path=Art+"/Materials/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smooth);
            if(texture!=null) mat.mainTexture=Texture(texture); EditorUtility.SetDirty(mat); return mat;
        }
        private static Texture2D Texture(string kind)
        {
            string path=Art+"/Textures/"+kind+".png"; var found=AssetDatabase.LoadAssetAtPath<Texture2D>(path); if(found!=null) return found;
            var image=new Texture2D(256,256); var pixels=new Color[256*256];
            for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            {
                float n=Mathf.PerlinNoise(x*.065f,y*.065f)*.13f+Mathf.PerlinNoise(x*.27f,y*.27f)*.07f;
                if(kind=="wood") n+=Mathf.Sin(x*.3f+Mathf.PerlinNoise(x*.015f,y*.035f)*12)*.07f;
                if(kind=="cloth") n+=(x%3==0||y%3==0)?-.06f:0;
                pixels[y*256+x]=new Color(.83f+n,.83f+n,.83f+n,1);
            }
            image.SetPixels(pixels); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void Emit(Material m,Color color)
        {
            m.SetColor("_EmissionColor",color); m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(m); m.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(m);
        }
        private static Transform Node(string name,Transform parent,Vector3 position)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t; }
        private static GameObject Shape(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool round=false)
        {
            var t=Node(name,parent,position); t.localScale=size; t.gameObject.AddComponent<MeshFilter>().sharedMesh=round?sphere:box;
            var r=t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.On; r.receiveShadows=true; return t.gameObject;
        }
        private static void Beam(string name,Transform parent,Vector3 a,Vector3 b,float width,Material mat)
        { var item=Shape(name,parent,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),mat); item.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a); }
        private static void Clear(Transform root) { for(int i=root.childCount-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject); }
        private static GameObject EditPrefab(string id)
        {
            string path=Root+"/Prefabs/"+id+".prefab", backup="../Builds/ArtBackups/BlockoutBackup/"+id+".prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); if(!File.Exists(backup)) File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path); Clear(root.transform); return root;
        }
        private static void Save(GameObject item,string path=null)
        { PrefabUtility.SaveAsPrefabAsset(item,path??Root+"/Prefabs/"+item.name+".prefab"); PrefabUtility.UnloadPrefabContents(item); }
        private static void IndexModels()
        {
            models.Clear(); kitMaterials.Clear();
            foreach(string guid in AssetDatabase.FindAssets("t:Model",new[]{"Assets/External/Staging"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid); string kit=path.Contains("/Blender/")?"blender":path.Contains("/AssetStoreMine/")?"assetstore-mine":path.Split('/')[4];
                models[kit+"/"+Path.GetFileNameWithoutExtension(path)]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.Log("KENNEY_MODELS_INDEXED: "+models.Count);
        }
        private static GameObject Prop(string kit,string name,Transform parent,Vector3 pos,float height,float angle=0,Material overrideMaterial=null)
        {
            if(!models.TryGetValue(kit+"/"+name,out var asset)) throw new InvalidOperationException("Imported model not found: "+kit+"/"+name);
            var wrapper=Node(name,parent,pos); var visual=(GameObject)PrefabUtility.InstantiatePrefab(asset); visual.transform.SetParent(wrapper,false);
            PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            Bounds bounds=BoundsOf(visual); float factor=height/Mathf.Max(.001f,bounds.size.y); visual.transform.localScale*=factor;
            bounds=BoundsOf(visual); visual.transform.localPosition-=wrapper.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
            wrapper.localRotation=Quaternion.Euler(0,angle,0);
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var source=renderer.sharedMaterials; var converted=new Material[source.Length];
                for(int i=0;i<source.Length;i++)
                {
                    if(overrideMaterial!=null) converted[i]=overrideMaterial;
                    else if(source[i]==null) converted[i]=pale;
                    else
                    {
                        if(!kitMaterials.TryGetValue(source[i],out var mat))
                        {
                            string label=kit+"_"+source[i].name.Replace(" ","_"); mat=Mat(label,source[i].HasProperty("_Color")?source[i].color:Color.white,0,.27f);
                            if(source[i].mainTexture!=null) mat.mainTexture=source[i].mainTexture;
                            kitMaterials[source[i]]=mat;
                        }
                        converted[i]=mat;
                    }
                }
                renderer.sharedMaterials=converted;
            }
            foreach(var collider in visual.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            return wrapper.gameObject;
        }
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(); var result=new Bounds();
            for(int i=0;i<renderers.Length;i++) { if(i==0) result=renderers[i].bounds; else result.Encapsulate(renderers[i].bounds); } return result;
        }
        private static void Character(bool guard)
        {
            if(RiggedCharacter(guard)) return;
            var root=EditPrefab(guard?"CHR_Police_Officer":"CHR_Monkey_Prisoner"); var body=Node("Body",root.transform,new Vector3(0,.92f,0));
            Shape("Torso",body,Vector3.zero,new Vector3(guard?.94f:.62f,.70f,.50f),guard?navy:orange,true);
            Shape("Belt",body,new Vector3(0,-.21f,.01f),new Vector3(guard?.86f:.63f,.11f,.50f),black,true);
            Shape("Buckle",body,new Vector3(0,-.21f,.267f),new Vector3(.13f,.10f,.045f),brass);
            var head=Node("Head",root.transform,new Vector3(0,1.48f,0));
            Shape("Cranium",head,Vector3.zero,new Vector3(guard?.76f:.91f,.84f,.75f),guard?face:fur,true);
            if(!guard) Shape("FaceMask",head,new Vector3(0,-.03f,.26f),new Vector3(.73f,.63f,.29f),face,true);
            Shape("Muzzle",head,new Vector3(0,-.23f,.38f),new Vector3(.60f,.34f,.29f),face,true);
            Shape("Mouth",head,new Vector3(0,-.29f,.524f),new Vector3(.37f,.17f,.05f),black,true);
            Shape("Teeth",head,new Vector3(0,-.24f,.55f),new Vector3(.29f,.055f,.035f),cream);
            Shape("Nose",head,new Vector3(0,-.12f,.525f),new Vector3(.19f,.12f,.09f),guard?face:fur,true);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Ear",head,new Vector3(side*.45f,-.025f,.025f),new Vector3(.32f,.38f,.22f),guard?face:fur,true);
                Shape("EarInner",head,new Vector3(side*.47f,-.025f,.117f),new Vector3(.20f,.26f,.09f),face,true);
                Shape("Eye",head,new Vector3(side*.175f,.105f,.375f),new Vector3(.29f,.35f,.13f),cream,true);
                Shape("Pupil",head,new Vector3(side*.145f,.105f,.445f),new Vector3(.10f,.155f,.048f),black,true);
                Shape("EyeCatchlight",head,new Vector3(side*.145f-.025f,.151f,.47f),new Vector3(.027f,.037f,.019f),cream,true);
                var brow=Shape("Brow",head,new Vector3(side*.175f,.28f,.39f),new Vector3(.28f,.065f,.095f),fur); brow.transform.localRotation=Quaternion.Euler(0,0,side*-12);
                string suffix=side<0?"L":"R"; var arm=Node("Arm_"+suffix,root.transform,new Vector3(side*(guard?.52f:.40f),1.17f,0));
                Shape("Sleeve",arm,new Vector3(side*.015f,-.16f,0),new Vector3(guard?.35f:.28f,.38f,.30f),guard?navy:orange,true);
                Shape("Forearm",arm,new Vector3(side*.02f,-.40f,.035f),new Vector3(.235f,.36f,.24f),guard?face:fur,true);
                MakeHand(arm,new Vector3(side*.025f,-.59f,.075f),guard?face:fur,.78f);
                var leg=Node("Leg_"+suffix,root.transform,new Vector3(side*.18f,.56f,0));
                Shape("Trouser",leg,new Vector3(0,-.20f,0),new Vector3(.29f,.46f,.32f),guard?navy:orange,true);
                Shape("Foot",leg,new Vector3(0,-.45f,.085f),new Vector3(.34f,.22f,.52f),guard?black:fur,true);
            }
            if(guard)
            {
                Shape("Cap",head,new Vector3(0,.375f,.015f),new Vector3(.91f,.23f,.80f),navy,true);
                Shape("CapPeak",head,new Vector3(0,.27f,.42f),new Vector3(.75f,.075f,.36f),black);
                Shape("CapBadge",head,new Vector3(0,.385f,.405f),new Vector3(.13f,.15f,.035f),brass);
                Shape("Moustache",head,new Vector3(0,-.195f,.545f),new Vector3(.42f,.09f,.055f),black,true);
                Shape("ChestBadge",body,new Vector3(-.19f,.12f,.255f),new Vector3(.13f,.18f,.045f),brass);
                Shape("Radio",body,new Vector3(.27f,-.07f,.235f),new Vector3(.14f,.20f,.10f),black);
            }
            else
            {
                for(int i=0;i<3;i++) { var tuft=Shape("HairTuft",head,new Vector3((i-1)*.11f,.40f,.04f),new Vector3(.16f,.24f,.21f),fur,true); tuft.transform.localRotation=Quaternion.Euler(-20,0,(i-1)*-18); }
                for(int i=0;i<7;i++) Shape("Tail",root.transform,new Vector3(Mathf.Sin(i*.45f)*.33f,.62f+i*.06f,-.34f-i*.075f),Vector3.one*.15f,fur,true);
                Shape("NumberPatch",body,new Vector3(0,.08f,-.255f),new Vector3(.39f,.23f,.025f),cream);
                Label("0723",body,new Vector3(0,.10f,-.272f),.068f,black.color);
            }
            root.GetComponent<Animator>().applyRootMotion=false; Save(root);
        }
        private static void MakeHand(Transform parent,Vector3 pos,Material mat,float scale)
        {
            var hand=Node("Hand",parent,pos); hand.localScale=Vector3.one*scale;
            Shape("Palm",hand,Vector3.zero,new Vector3(.28f,.27f,.18f),mat,true);
            for(int i=0;i<4;i++) Shape("Finger",hand,new Vector3((i-1.5f)*.065f,-.12f,.05f),new Vector3(.062f,.20f,.10f),mat,true);
            Shape("Thumb",hand,new Vector3(.155f,-.025f,.06f),new Vector3(.10f,.19f,.10f),mat,true);
        }
        private static void Hands()
        {
            string path=Art+"/Prefabs/FP_Hands.prefab"; var root=new GameObject("FP_Hands"); var visual=Node("Visual",root.transform,Vector3.zero);
            var left=Node("Left",visual,new Vector3(-.40f,-.39f,.62f)); var right=Node("Right",visual,new Vector3(.40f,-.39f,.62f));
            foreach(var hand in new[]{left,right})
            {
                Shape("Forearm",hand,new Vector3(0,-.09f,-.18f),new Vector3(.18f,.20f,.51f),fur,true);
                Prop("blender",hand==left?"FP_Hand_Right":"FP_Hand_Left",hand,new Vector3(0,-.035f,.015f),.145f,180,face);
            }
            var lip=Node("CartRideRim",visual,new Vector3(0,-1.36f,.07f));
            Prop("blender","MineCartEmpty",lip,Vector3.zero,1.1f);
            var rolling=lip.gameObject.AddComponent<AudioSource>();rolling.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/MineCart_Rumble.wav");
            rolling.loop=true;rolling.playOnAwake=false;rolling.volume=.20f;rolling.spatialBlend=0;
            var motion=root.AddComponent<FirstPersonHands>(); motion.Visual=visual; motion.Left=left; motion.Right=right; motion.CartRim=lip;motion.CartSound=rolling;
            PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
        }
        private static void Cart()
        {
            var root=EditPrefab("PROP_MineCart"); var t=root.transform;
            if(models.ContainsKey("blender/MineCart"))
            {
                Prop("blender","MineCart",t,Vector3.zero,1.4f);
                var lamp=Node("CartLamp",t,new Vector3(0,1.07f,1.13f)).gameObject.AddComponent<Light>();
                lamp.type=LightType.Point; lamp.color=new Color(1,.62f,.23f); lamp.intensity=3; lamp.range=7;
                var glass=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/blender_CartLampGlass.mat");
                if(glass!=null) Emit(glass,new Color(1,.48f,.09f)*3);
                Save(root); return;
            }
            Shape("Chassis",t,new Vector3(0,.32f,0),new Vector3(1.45f,.20f,1.9f),iron);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("BinWall",t,new Vector3(side*.71f,.85f,0),new Vector3(.13f,.98f,1.85f),iron);
                Shape("EndWall",t,new Vector3(0,.85f,side*.91f),new Vector3(1.52f,.98f,.12f),iron);
                for(int z=-1;z<=1;z+=2)
                {
                    Shape("Wheel",t,new Vector3(side*.8f,.26f,z*.60f),new Vector3(.20f,.49f,.49f),black,true);
                    Shape("WheelHub",t,new Vector3(side*.915f,.26f,z*.60f),new Vector3(.07f,.20f,.20f),brass,true);
                    Shape("Rivet",t,new Vector3(side*.60f,1.20f,z*.985f),Vector3.one*.075f,pale,true);
                }
                Shape("Rim",t,new Vector3(side*.73f,1.35f,0),new Vector3(.18f,.10f,1.95f),pale);
            }
            for(int i=0;i<8;i++) Shape("Ore",t,new Vector3(Mathf.Sin(i*2.4f)*.43f,.95f+(i%3)*.1f,Mathf.Cos(i*2.4f)*.59f),new Vector3(.46f,.37f,.43f),i%3==0?brass:rock,true);
            Shape("LampFrame",t,new Vector3(0,1.12f,1.02f),new Vector3(.48f,.48f,.17f),black);
            Shape("Headlamp",t,new Vector3(0,1.12f,1.125f),new Vector3(.34f,.34f,.09f),glow,true);
                var light=Node("CartLamp",t,new Vector3(0,1.12f,1.25f)).gameObject.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.62f,.23f); light.intensity=3; light.range=7;
            Save(root);
        }
        private static void Pickaxe()
        { var root=EditPrefab("PROP_Pickaxe"); var model=Prop("survival-kit","tool-pickaxe-upgraded",root.transform,new Vector3(0,-.55f,0),1.1f); model.transform.localRotation=Quaternion.Euler(0,90,0); Save(root); }
        private static void Bulb()
        {
            var root=EditPrefab("VFX_LightBulb_Idea"); Shape("Bulb",root.transform,Vector3.zero,Vector3.one*.42f,glow,true);
            Shape("Socket",root.transform,new Vector3(0,-.25f,0),new Vector3(.17f,.18f,.17f),brass);
            for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; Beam("IdeaRay",root.transform,new Vector3(Mathf.Cos(a)*.36f,Mathf.Sin(a)*.36f,0),new Vector3(Mathf.Cos(a)*.52f,Mathf.Sin(a)*.52f,0),.035f,glow); }
            var light=root.GetComponent<Light>(); if(light==null) light=root.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.7f,.2f); light.intensity=3; light.range=4; Save(root);
        }
        private static void Label(string text,Transform parent,Vector3 pos,float size,Color color,float yaw=0)
        {
            var t=Node("Sign_"+text,parent,pos); t.localRotation=Quaternion.Euler(0,yaw,0);
            var label=t.gameObject.AddComponent<TextMesh>(); label.text=text; label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=64; label.characterSize=size*.32f; label.anchor=TextAnchor.MiddleCenter; label.color=color;
            string path=Art+"/Materials/"+(color.r<.1f?"DarkSignText":"LightSignText")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Muhanok/DepthText")); AssetDatabase.CreateAsset(mat,path); }
            mat.shader=Shader.Find("Muhanok/DepthText");
            mat.mainTexture=label.font.material.mainTexture; mat.SetColor("_BaseColor",color);
            EditorUtility.SetDirty(mat); t.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
    }
}
