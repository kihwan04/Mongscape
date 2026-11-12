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
                EnsureStairBlockout(type);
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
