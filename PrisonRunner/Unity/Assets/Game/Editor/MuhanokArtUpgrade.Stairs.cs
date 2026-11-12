using System.Collections.Generic;
using Muhanok.Bootstrap;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        // Regenerates the three stair chunks (prison 7, mine 13, zoo 14) and registers the new ones with the scene's chunk list.
        [MenuItem("Mongscape/Build Stair Chunks")]
        public static void BuildStairChunks()
        {
            Prepare(); IndexModels(); BuildEditableRoutes();
            foreach(int type in new[]{7,RouteSurface.MineStair,RouteSurface.ZooStair})
            {
                EnsureStairBlockout(type);ConfigureStairImport(type);
                UpgradeChunk(type);
                // Keep the shadow budget that the later material pass applies to every chunk.
                string path=Root+"/Prefabs/"+MuhanokContentBuilder.ChunkIds[type]+".prefab";
                var chunk=PrefabUtility.LoadPrefabContents(path);MuhanokVisualPolish.ConfigureShadows(chunk);
                PrefabUtility.SaveAsPrefabAsset(chunk,path);PrefabUtility.UnloadPrefabContents(chunk);
            }
            RegisterChunkPrefabs();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("MUHANOK_STAIR_CHUNKS_PASS: prison, mine and zoo stair chunks without hazards, registered in the scene");
        }
        // ---- Blender stair meshes (chunk-local, entry at the origin, walkable surface already on the route curve) ----
        private static bool HasStairMesh(int index)=>index==RouteSurface.MineStair||index==RouteSurface.ZooStair;
        private static string StairModelPath(int index)=>"Assets/External/Staging/Blender/"+(index==RouteSurface.MineStair?"MineStair":"ZooStair")+".fbx";
        private static void ConfigureStairImport(int index)
        {
            if(!HasStairMesh(index))return;
            var importer=AssetImporter.GetAtPath(StairModelPath(index)) as ModelImporter; if(importer==null)return;
            if(!importer.importAnimation||importer.importCameras||importer.importLights||importer.importBlendShapes) return;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
            importer.SaveAndReimport();
        }
        // The mesh is added after the flat environment is baked and bent: it is authored on the route curve already.
        private static void AddStairMesh(Transform env,int index)
        {
            if(!HasStairMesh(index))return;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(StairModelPath(index));
            if(asset==null) throw new System.InvalidOperationException("Stair model missing: "+StairModelPath(index));
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(asset); visual.name="StairMesh"; visual.transform.SetParent(env,false);
            PrefabUtility.UnpackPrefabInstance(visual,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            visual.transform.localPosition=Vector3.zero; visual.transform.localRotation=Quaternion.identity; visual.transform.localScale=Vector3.one;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var source=renderer.sharedMaterials; var mapped=new Material[source.Length];
                for(int i=0;i<source.Length;i++) mapped[i]=StairMaterial(source[i]!=null?source[i].name:"");
                renderer.sharedMaterials=mapped;
            }
            foreach(var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var bounds=BoundsOf(visual); var local=new Bounds(env.InverseTransformPoint(bounds.center),bounds.size);
            Debug.Log("STAIR_MESH "+MuhanokContentBuilder.ChunkIds[index]+" bounds min "+local.min+" max "+local.max);
        }
        private static Material StairMaterial(string name)
        {
            if(name.Contains("Rock")) return rock; if(name.Contains("Timber")||name.Contains("Wood")) return wood; if(name.Contains("Iron")) return iron;
            if(name.Contains("Glass")) return glow; if(name.Contains("Stone")) return rockLight;
            if(name.Contains("Foliage")) return Mat("ZooFoliage",new Color(.17f,.40f,.16f),0,.1f);
            if(name.Contains("Earth")) return Mat("ZooEarth",new Color(.20f,.33f,.17f),0,.08f);
            return pale;
        }
        // Practical lights at the lantern positions of the mine stair mesh (flat heights: the route bend lifts them with the stairs).
        private static void MineStairLights(Transform env)
        {
            int n=0;
            foreach(float z in new[]{2f,8f,14f,20f})
            {
                var lamp=Node("StairLantern_"+(n++),env,new Vector3(n%2==0?2.3f:-2.3f,3.75f,z)).gameObject.AddComponent<Light>();
                lamp.type=LightType.Point; lamp.color=new Color(1,.52f,.16f); lamp.intensity=3.5f; lamp.range=7f; lamp.shadows=LightShadows.None;
            }
        }
        // A few existing zoo trees on the grass embankment, set flat-relative so the route bend lands them on the terrain.
        private static void ZooStairProps(Transform env)
        {
            for(int side=-1;side<=1;side+=2)
                foreach(float z in new[]{3f,9.5f,15f,21f})
                {
                    float x=side*(7.0f+Next()*1.2f), h=RouteSurface.LocalHeight(RouteSurface.ZooStair,z);
                    float t=Mathf.Min(1f,(Mathf.Abs(x)-5f)/4f), ground=(h-.05f)*(1-t)+Mathf.Max(-.5f,h*.3f-.5f)*t;
                    Prop("nature-kit",z>10&&z<16?"tree_palmDetailedTall":"tree_detailed_dark",env,new Vector3(x,ground-h-.1f,z),4.0f+Next()*1.3f,Next()*360);
                }
        }
        // The empty chunk contract the art pass fills in: entry/exit, lane markers, six inert obstacle sockets, an environment root.
        private static void EnsureStairBlockout(int index)
        {
            string id=MuhanokContentBuilder.ChunkIds[index], path=Root+"/Prefabs/"+id+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
            var root=new GameObject(id); var chunk=root.AddComponent<MuhanokMapChunk>(); chunk.AssetId=id;
            chunk.Entry=Node("Entry",root.transform,Vector3.zero); chunk.Exit=Node("Exit",root.transform,Vector3.forward*24);
            for(int lane=-1;lane<=1;lane++) Node(lane<0?"Lane_Left":lane>0?"Lane_Right":"Lane_Center",root.transform,new Vector3(lane*2.2f,0,0));
            var sockets=Node("ObstacleSockets",root.transform,Vector3.zero); chunk.Obstacles=new ObstacleController[6];
            for(int row=0;row<2;row++) for(int lane=-1;lane<=1;lane++)
            {
                var socket=Node("Socket_"+row+"_"+lane,sockets,new Vector3(lane*2.2f,0,7+row*12));
                var contract=socket.gameObject.AddComponent<MuhanokObstacleSocket>(); contract.Row=row; contract.Lane=lane;
                var inert=new GameObject("NoHazard"); inert.transform.SetParent(socket,false);
                chunk.Obstacles[row*3+lane+1]=inert.AddComponent<ObstacleController>(); inert.SetActive(false);
            }
            Node("EnvironmentRoot",root.transform,Vector3.zero);
            PrefabUtility.SaveAsPrefabAsset(root,path); Object.DestroyImmediate(root);
        }
        private static void RegisterChunkPrefabs()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/GameScene.unity");
            var game=Object.FindFirstObjectByType<GameBootstrap>();
            var list=new List<MuhanokMapChunk>(game.ChunkPrefabs);
            while(list.Count<MuhanokContentBuilder.ChunkIds.Length)
                list.Add(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+MuhanokContentBuilder.ChunkIds[list.Count]+".prefab").GetComponent<MuhanokMapChunk>());
            game.ChunkPrefabs=list.ToArray(); EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
