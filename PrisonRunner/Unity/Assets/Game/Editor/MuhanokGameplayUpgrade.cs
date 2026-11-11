using System;
using Muhanok.Content;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static class MuhanokGameplayUpgrade
    {
        private const string Prefabs = "Assets/Game/Content/Prefabs/";

        [MenuItem("Mongscape/Apply Map Exercises and Progressive Speed")]
        public static void Apply()
        {
            for(int type=0;type<MuhanokContentBuilder.ChunkIds.Length;type++)
            {
                string path=Prefabs+MuhanokContentBuilder.ChunkIds[type]+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root,type);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var settings=AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Game/Content/Data/GameSettings.asset");
            settings.Run.SpeedRampDistance=120f;settings.Run.CartSpeed=settings.Run.BaseSpeed;EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("MUHANOK_GAMEPLAY_UPGRADE_PASS: every walking map has jump/slide; stairs retain high-knee and closing slide gate; progressive walking/cart speed");
        }

        public static void Configure(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>();
            var previous=root.transform.Find("MapExercises");
            if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            if(type==1||type==2){chunk.ExerciseObstacles=new ObstacleController[0];return;}

            var group=new GameObject("MapExercises").transform;group.SetParent(root.transform,false);
            int rows=type==7?1:2;
            chunk.ExerciseObstacles=new ObstacleController[rows*3];
            for(int row=0;row<rows;row++)for(int lane=-1;lane<=1;lane++)
            {
                float z=type==7?3f:7f+row*12f;
                string id=row==0?"OBS_Crate_Low":type>=11?MuhanokArtUpgrade.ZooDuckId:"OBS_LowPipe";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+id+".prefab");
                if(source==null)throw new InvalidOperationException("Missing exercise visual: "+id);
                var socket=new GameObject("Exercise_"+row+"_"+lane).transform;socket.SetParent(group,false);
                socket.localPosition=new Vector3(lane*2.2f+RouteSurface.LocalX(type,z),RouteSurface.LocalHeight(type,z),z);
                var contract=socket.gameObject.AddComponent<MuhanokObstacleSocket>();contract.Row=row;contract.Lane=lane;
                var instance=UnityEngine.Object.Instantiate(source,socket,false);
                chunk.ExerciseObstacles[row*3+lane+1]=instance.GetComponent<ObstacleController>();
                instance.SetActive(false);
            }
            // Keep the original closed-to-floor prison doors as avoidable lane hazards, after the slide.
            if(type>=4&&type<=6)for(int lane=0;lane<3;lane++)
            {
                var socket=chunk.Obstacles[lane].transform.parent;
                socket.localPosition=new Vector3((lane-1)*2.2f,0,23f);
            }
            // Jump at the foot of the stairs, then high knees, then slide under the existing entrance.
            if(type==7)for(int lane=0;lane<3;lane++)
            {
                var socket=chunk.Obstacles[lane].transform.parent;
                socket.localPosition=new Vector3((lane-1)*2.2f,RouteSurface.LocalHeight(type,12f),12f);
            }
        }
    }
}
