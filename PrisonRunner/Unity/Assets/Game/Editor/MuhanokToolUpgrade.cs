using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Muhanok.Domain;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private const string Characters="Assets/External/Staging/Blender/Characters";
        private const string Authoring="Assets/Game/Authoring/ProBuilder";
        private static readonly string[] HumanoidBones={"Hips","Spine","Chest","Neck","Head",
            "LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand",
            "LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes"};

        [MenuItem("Mongscape/Apply Blender Characters and ProBuilder Maps")]
        public static void UpgradeTools()
        {
            AssetDatabase.Refresh();
            foreach(string path in Directory.GetFiles(Characters,"*.fbx"))
            {
                if(path.Contains("For_Mixamo")) continue;
                ConfigureHumanoid(path.Replace('\\','/'));
            }
            EnsureCartAnimation(); EnsureCellAnimation(); Prepare(); BuildEditableRoutes(true); Upgrade();
            ValidateTools();
            Debug.Log("MUHANOK_TOOLS_V4_PASS: Blender humanoids and ProBuilder authoring + baked pooled runtime maps; Mixamo clips require separate verified import");
        }
        private static void ConfigureHumanoid(string path)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras=false; importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            var desc=importer.humanDescription;
            desc.human=HumanoidBones.Select(name=>new HumanBone{boneName=name,humanName=name,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
            desc.armStretch=.05f; desc.legStretch=.05f; desc.upperArmTwist=.5f; desc.lowerArmTwist=.5f;
            desc.upperLegTwist=.5f; desc.lowerLegTwist=.5f; desc.feetSpacing=0; desc.hasTranslationDoF=false;
            importer.humanDescription=desc;
            var defaults=importer.defaultClipAnimations;
            if(defaults.Length>0)
            {
                string id=Path.GetFileNameWithoutExtension(path);
                defaults[0].name=id;
                defaults[0].loopTime=id.Contains("Run")||id.Contains("HighKnee")||id.Contains("MinePickaxe")||id.Contains("Notice")||id.Contains("CartRide")||id.Contains("CellIdle");
                defaults[0].lockRootPositionXZ=true; defaults[0].lockRootRotation=true; defaults[0].lockRootHeightY=true;
                defaults[0].keepOriginalOrientation=true; defaults[0].keepOriginalPositionXZ=true;
                importer.clipAnimations=defaults;
            }
            importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if(avatar==null||!avatar.isValid||!avatar.isHuman) throw new InvalidOperationException("Invalid humanoid avatar: "+path);
        }
        private static bool RiggedCharacter(bool guard)
        {
            string modelPath=Characters+(guard?"/Police.fbx":"/Monkey.fbx");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath); if(model==null) return false;
            var avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if(avatar==null||!avatar.isHuman) ConfigureHumanoid(modelPath);
            avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First();
            var root=EditPrefab(guard?"CHR_Police_Officer":"CHR_Monkey_Prisoner");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            // Preserve FBX skeleton paths at the prefab root so custom transform bindings also work.
            while(instance.transform.childCount>0) instance.transform.GetChild(0).SetParent(root.transform,false);
            UnityEngine.Object.DestroyImmediate(instance);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen=true; renderer.localBounds=new Bounds(new Vector3(0,1,0),Vector3.one*4);
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>Mat("Blender_"+source.name.Replace(' ','_'),source.color,source.name.Contains("brass")?.4f:0,.22f)).ToArray();
            }
            var animator=root.GetComponent<Animator>(); animator.avatar=avatar; animator.applyRootMotion=false;
            if(root.GetComponent<Muhanok.Presentation.FootGrounding>()==null)root.AddComponent<Muhanok.Presentation.FootGrounding>();
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root+"/Animations/"+(guard?"Guard":"Monkey")+".controller");
            string overridePath=Art+"/"+(guard?"Police":"Monkey")+"Humanoid.overrideController";
            var replacement=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if(replacement==null) { replacement=new AnimatorOverrideController(controller); AssetDatabase.CreateAsset(replacement,overridePath); }
            replacement.runtimeAnimatorController=controller;
            var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>(); replacement.GetOverrides(overrides);
            for(int i=0;i<overrides.Count;i++)
            {
                var source=overrides[i].Key;
                var imported=MuhanokMixamoImport.Find(source.name)??AssetDatabase.LoadAllAssetsAtPath(Characters+"/"+source.name+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
                if(imported==null) throw new InvalidOperationException("Missing authored motion: "+source.name);
                overrides[i]=new KeyValuePair<AnimationClip,AnimationClip>(source,imported);
            }
            replacement.ApplyOverrides(overrides); EditorUtility.SetDirty(replacement); animator.runtimeAnimatorController=replacement;
            if(!guard&&root.GetComponent<Muhanok.Presentation.FacialPerformance>()==null)root.AddComponent<Muhanok.Presentation.FacialPerformance>();
            Save(root); return true;
        }
        private static void ProBuilderBox(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool editable=false)
        {
            var pb=ShapeGenerator.GenerateCube(PivotLocation.Center,size); pb.name=name;
            pb.transform.SetParent(parent,false); pb.transform.localPosition=position;
            pb.GetComponent<MeshRenderer>().sharedMaterial=material; pb.ToMesh(); pb.Refresh();
            if(!editable) { pb.preserveMeshAssetOnDestroy=true; UnityEngine.Object.DestroyImmediate(pb); }
        }
        private static void BuildEditableRoutes(bool rebuildCart=false)
        {
            Directory.CreateDirectory(Authoring); AssetDatabase.Refresh();
            for(int type=0;type<MuhanokContentBuilder.ChunkIds.Length;type++)
            {
                string id=MuhanokContentBuilder.ChunkIds[type];
                if(File.Exists(Authoring+"/"+id+".prefab")&&(!rebuildCart||type!=1&&type!=2)) continue;
                var root=new GameObject(id+"_Editable");
                // The actual route surface is built as editable quads at 0.5m stations.
                var vertices=new List<Vector3>(); var faces=new List<Face>();
                for(int segment=0;segment<48;segment++)
                {
                    float a=segment*.5f,b=(segment+1)*.5f; int n=vertices.Count;
                    bool cart=type==1||type==2;
                    // The cart deck is open in the rail-gap windows; each side's deck half is a switchable rail piece (AddRailGaps).
                    if(cart&&ObstacleLayout.RailGapAt(a+.25f)>=0) continue;
                    float left=cart?-1.05f:-4.9f,right=cart?1.05f:4.9f;
                    vertices.Add(RoutePoint(new Vector3(left,0,a),type)); vertices.Add(RoutePoint(new Vector3(left,0,b),type));
                    vertices.Add(RoutePoint(new Vector3(right,0,b),type)); vertices.Add(RoutePoint(new Vector3(right,0,a),type));
                    faces.Add(new Face(new[]{n,n+1,n+2,n,n+2,n+3}));
                }
                var floor=ProBuilderMesh.Create(vertices,faces); floor.name="Editable route floor"; floor.transform.SetParent(root.transform,false);
                floor.GetComponent<MeshRenderer>().sharedMaterial=type==1||type==2?wood:RouteSurface.IsMine(type)?rock:concrete; floor.ToMesh(); floor.Refresh();
                // Reusable open doorway: no collision or decorative geometry intrudes into the three lanes.
                foreach(float z in new[]{0f,24f})
                {
                    float h=RouteSurface.LocalHeight(type,z),x=RouteSurface.LocalX(type,z);
                    bool cart=type==1||type==2;
                    for(int side=-1;side<=1;side+=2) ProBuilderBox("Editable connector post",root.transform,new Vector3(x+side*(cart?1.4f:3.9f),h+(cart?1.7f:2.4f),z),new Vector3(.24f,cart?3.4f:4.8f,.24f),RouteSurface.IsMine(type)?wood:iron,true);
                    ProBuilderBox("Editable connector header",root.transform,new Vector3(x,h+(cart?3.45f:4.85f),z),new Vector3(cart?3.1f:8.1f,.28f,.30f),RouteSurface.IsMine(type)?wood:iron,true);
                    Node(z==0?"EntrySocket":"ExitSocket",root.transform,new Vector3(x,h,z));
                }
                if(RouteSurface.IsStair(type)) for(int z=1;z<24;z++)
                {
                    float rise=RouteSurface.LocalHeight(type,z)-RouteSurface.LocalHeight(type,z-1);
                    ProBuilderBox("Editable stair riser",root.transform,new Vector3(0,RouteSurface.LocalHeight(type,z)-rise/2,z),new Vector3(6.6f,Mathf.Max(.01f,rise),.06f),concrete,true);
                }
                // Keep mesh assets as prefab subassets rather than references to temporary editor objects.
                string path=Authoring+"/"+id+".prefab";
                int meshIndex=0;
                foreach(var pb in root.GetComponentsInChildren<ProBuilderMesh>())
                {
                    var mesh=pb.GetComponent<MeshFilter>().sharedMesh;
                    string meshPath=Authoring+"/"+id+"_"+(meshIndex++)+".asset";
                    var prior=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(prior==null) AssetDatabase.CreateAsset(mesh,meshPath);
                    else
                    {
                        EditorUtility.CopySerialized(mesh,prior);
                        var serialized=new SerializedObject(pb); serialized.FindProperty("m_Mesh").objectReferenceValue=prior; serialized.ApplyModifiedPropertiesWithoutUndo();
                        pb.GetComponent<MeshFilter>().sharedMesh=prior;
                    }
                    pb.preserveMeshAssetOnDestroy=true;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        private static bool AddBakedAuthoringRoute(GameObject root,string id)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Authoring+"/"+id+".prefab"); if(source==null) return false;
            var previous=root.transform.Find("RouteStructure"); if(previous!=null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source); instance.name="RouteStructure";
            PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            instance.transform.SetParent(root.transform,false); int index=0;
            foreach(var pb in instance.GetComponentsInChildren<ProBuilderMesh>())
            {
                pb.ToMesh(); pb.Refresh(); var filter=pb.GetComponent<MeshFilter>();
                var copy=UnityEngine.Object.Instantiate(filter.sharedMesh); copy.name=id+"_Authoring_"+index;
                string path=Art+"/Meshes/"+copy.name+".asset"; index++;
                var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(prior==null) AssetDatabase.CreateAsset(copy,path);
                else { EditorUtility.CopySerialized(copy,prior); UnityEngine.Object.DestroyImmediate(copy); copy=prior; EditorUtility.SetDirty(prior); }
                pb.preserveMeshAssetOnDestroy=true; UnityEngine.Object.DestroyImmediate(pb); filter.sharedMesh=copy;
            }
            return true;
        }
        [MenuItem("Mongscape/Bake Edited ProBuilder Routes Into Game")]
        public static void BakeEditedRoutes()
        {
            Prepare(); BuildEditableRoutes();
            foreach(string id in MuhanokContentBuilder.ChunkIds)
            {
                var root=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/"+id+".prefab");
                AddBakedAuthoringRoute(root,id); Save(root);
            }
            AssetDatabase.SaveAssets(); ValidateTools();
            Debug.Log("PROBUILDER_RUNTIME_BAKE_PASS: 13 saved editable route structures baked into actual game prefabs");
        }
        [MenuItem("Mongscape/Validate Blender and ProBuilder Assets")]
        public static void ValidateTools()
        {
            foreach(string name in new[]{"Monkey","Police"})
            {
                var avatar=AssetDatabase.LoadAllAssetsAtPath(Characters+"/"+name+".fbx").OfType<Avatar>().First();
                if(!avatar.isHuman||!avatar.isValid) throw new InvalidOperationException("Avatar failed: "+name);
            }
            foreach(string id in MuhanokContentBuilder.ChunkIds)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Authoring+"/"+id+".prefab");
                if(prefab==null||prefab.GetComponentsInChildren<ProBuilderMesh>().Length<7) throw new InvalidOperationException("ProBuilder authoring failed: "+id);
                foreach(var pb in prefab.GetComponentsInChildren<ProBuilderMesh>())
                {
                    var mesh=pb.GetComponent<MeshFilter>().sharedMesh;
                    if(mesh==null||mesh.vertexCount<4||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) throw new InvalidOperationException("Unpersisted ProBuilder mesh: "+id);
                }
                var runtime=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+id+".prefab");
                var structure=runtime.transform.Find("RouteStructure");
                if(structure==null||structure.GetComponentsInChildren<ProBuilderMesh>().Length!=0) throw new InvalidOperationException("Route missing or editor component leaked into runtime: "+id);
                var sourceFloor=prefab.GetComponentInChildren<ProBuilderMesh>();
                var bakedFloor=structure.GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
                if(bakedFloor.Length!=sourceFloor.positions.Count) throw new InvalidOperationException("Edited floor topology not baked: "+id);
                for(int vertex=0;vertex<bakedFloor.Length;vertex++)
                    if(Vector3.Distance(bakedFloor[vertex],sourceFloor.positions[vertex])>.0001f) throw new InvalidOperationException("Edited floor position not baked: "+id);
            }
            Debug.Log("BLENDER_PROBUILDER_VALIDATION_PASS: 2 valid humanoid avatars / 13 editable route prefabs / all baked floor vertices match source / no runtime ProBuilder component");
        }
        public static void RefreshHandsAndValidate()
        {
            Prepare(); IndexModels(); Hands(); ValidateTools(); AssetDatabase.SaveAssets();
        }
        public static void BuildValidated()
        {
            ValidateTools(); MuhanokMixamoImport.ValidateDownloaded(); MuhanokBuildTools.BuildWindowsDev();
        }
    }
}
