#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Bootstrap
{
    // Explicit build smoke only; absent from normal play unless this command-line flag is supplied.
    public sealed class RuntimeSmokeCheck : MonoBehaviour
    {
        private GameBootstrap game;
        private int step;
        private float start, hitClock;
        private int hits;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--muhanok-smoke") < 0) return;
            UnityEngine.Application.runInBackground = true;
            new GameObject("Build Smoke").AddComponent<RuntimeSmokeCheck>();
        }
        private void Start() { game = FindFirstObjectByType<GameBootstrap>(); start = Time.realtimeSinceStartup; }
        private void Update()
        {
            try
            {
                Check(Time.realtimeSinceStartup - start < 40, "Smoke timeout");
                Check(game != null && game.Session != null, "Bootstrap missing");
                if (step == 0)
                {
                    Check(game.Session.Flow.State == GameFlowState.TitleIdle, "Title missing");
                    game.BeginRun(""); step = 1;
                }
                else if (step == 1 && game.Session.Flow.State == GameFlowState.Running&&!game.IsRevealing)
                {
                    Check(Mathf.Abs(game.CameraRig.FirstPerson.Lens.FieldOfView - 80) < .001f, "First-person FOV");
                    Check(game.Intro.Elapsed==0,"A second timeline ran after loading");
                    Check(game.Session.Slip(),"Floor slip unavailable");Check(game.Session.Flow.State==GameFlowState.Running,"Slip killed the player");
                    int count = game.Map.CreatedCount;
                    for (int i = 0; i < 120; i++) game.Map.Tick(i * 24 + 2);
                    Check(count == game.Map.CreatedCount, "Pool growth");
                    game.Map.Reset(); step = 2;
                }
                else if (step == 2)
                {
                    hitClock += Time.deltaTime;
                    if (hitClock >= .7f && hits < 1) { hitClock = 0; game.Session.Hit(true); hits++;Check(game.Session.Flow.State==GameFlowState.Caught,"Single collision did not end run"); }
                    if (game.Session.Flow.State == GameFlowState.Result) { game.Retry(); step = 3; }
                }
                else if (step == 3)
                {
                    Check(game.Session.Flow.State == GameFlowState.TitleIdle && game.Session.Score.Score == 0 && game.Session.Chase.Pressure == 0, "Retry dirty state");
                    Debug.Log("MUHANOK_RUNTIME_SMOKE_PASS: loading intro/direct gameplay/floor slip/120 chunks/single fatal hit/result/retry");
                    UnityEngine.Application.Quit(0); enabled = false;
                }
            }
            catch (Exception error) { Debug.LogError("MUHANOK_RUNTIME_SMOKE_FAIL: " + error); UnityEngine.Application.Quit(1); enabled = false; }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
#endif
