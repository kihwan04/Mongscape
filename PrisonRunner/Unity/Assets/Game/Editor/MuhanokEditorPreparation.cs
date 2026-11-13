using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Muhanok.Editor
{
    [InitializeOnLoad]
    public static class MuhanokEditorPreparation
    {
        private static double next;
        static MuhanokEditorPreparation() { EditorApplication.update+=Tick; }
        private static void Tick()
        {
            if(UnityEngine.Application.isBatchMode||EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
            next=EditorApplication.timeSinceStartup+.5;
            string request=Path.Combine(UnityEngine.Application.dataPath,"../Library/MuhanokPrepare.request");
            if(!File.Exists(request)) return;
            string stalePlay=Path.Combine(UnityEngine.Application.dataPath,"../Library/MuhanokQuickPlay.request");
            if(File.Exists(stalePlay)) File.Delete(stalePlay);
            if(EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying=false; return; }
            File.Delete(request);
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.Log("MUHANOK_PREPARATION_CANCELLED"); return; }
            Debug.Log("MUHANOK_EDITOR_PREPARED: stopped Play and checked scene saves for requested upgrade");
            EditorApplication.Exit(0);
        }
    }
}
