using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Muhanok.Editor
{
    public static class MuhanokQuickPlay
    {
        private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";
        private static double nextRequestCheck;
        private const string CaptureTitleKey = "Mongscape.CaptureTitle";
        private static double captureAfter;
        private static DateTime captureStarted;
        private static bool captureRequested;

        [InitializeOnLoadMethod]
        private static void WatchLaunchRequest()
        {
            if (UnityEngine.Application.isBatchMode) return;
            EditorApplication.update -= CheckLaunchRequest;
            EditorApplication.update += CheckLaunchRequest;
            EditorApplication.update -= CaptureTitleWhenReady;
            EditorApplication.update += CaptureTitleWhenReady;
            EditorApplication.playModeStateChanged -= FocusGame;
            EditorApplication.playModeStateChanged += FocusGame;
        }

        private static void CheckLaunchRequest()
        {
            if (EditorApplication.timeSinceStartup < nextRequestCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextRequestCheck = EditorApplication.timeSinceStartup + .5;
            string request = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MuhanokQuickPlay.request"));
            if (!File.Exists(request)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            File.Delete(request);
            PlayGameScene();
        }

        [MenuItem("Mongscape/Play GameScene", false, 0)]
        public static void PlayGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.playModeStateChanged -= FocusGame;
            EditorApplication.playModeStateChanged += FocusGame;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Mongscape/Play GameScene", true)]
        private static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        [MenuItem("Mongscape/Capture Title Preview")]
        public static void PlayAndCaptureTitle()
        {
            SessionState.SetBool(CaptureTitleKey, true);
            captureRequested = false;
            PlayGameScene();
        }

        private static void CaptureTitleWhenReady()
        {
            if (!SessionState.GetBool(CaptureTitleKey, false)) return;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating
                || UnityEditor.ShaderUtil.anythingCompiling || UnityEngine.Time.frameCount < 10)
            {
                captureAfter = EditorApplication.timeSinceStartup + 2;
                return;
            }
            if (EditorApplication.timeSinceStartup < captureAfter) return;
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Builds/QA/VisualReview/Mongscape/Title_LatestEditor.png"));
            if (!captureRequested)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                captureStarted = DateTime.UtcNow;
                UnityEngine.ScreenCapture.CaptureScreenshot(path);
                captureRequested = true;
                return;
            }
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < captureStarted) return;
            SessionState.SetBool(CaptureTitleKey, false);
            UnityEngine.Debug.Log("MONGSCAPE_TITLE_PREVIEW_PASS: " + path);
        }

        private static void FocusGame(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= FocusGame;
            Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.delayCall+=MuhanokGameViewFit.Fit;
            UnityEngine.Debug.Log("MUHANOK_EDITOR_READY: Click the Game view to start. A/D, Space, S, W.");
        }
    }
}
