using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static class MuhanokArtMaintenance
    {
        public static void CleanRouteMeshes()
        {
            var paths=new List<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Content"})) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Add("Assets/Game/Scenes/GameScene.unity");
            var used=new HashSet<string>(AssetDatabase.GetDependencies(paths.ToArray(),true)); int removed=0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/Game/Content/ArtV2/Meshes"}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid); if(!path.Contains("_Route_")) continue;
                    if(!used.Contains(path)) { AssetDatabase.DeleteAsset(path); removed++; continue; }
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); mesh.name=Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(mesh);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets(); Debug.Log("MUHANOK_ROUTE_MESH_CLEANUP: unused generated copies "+removed);
        }
    }
}
