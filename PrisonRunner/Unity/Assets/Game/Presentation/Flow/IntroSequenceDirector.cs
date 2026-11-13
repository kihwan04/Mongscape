using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Events;

namespace Muhanok.Presentation
{
    public sealed class IntroSequenceDirector : MonoBehaviour
    {
        private PlayableDirector director;
        private RunSession session;
        private RunnerView monkey, police;
        private FirstPersonCameraRig rig;
        private Transform cart, pickaxe;
        private GameObject idea;
        private AudioSource cartAudio;
        private Transform toolParent;
        private Vector3 toolLocalPosition, droppedPosition;
        private Quaternion toolLocalRotation;
        private bool dropped;
        public float Elapsed { get; private set; }
        public void Configure(RunSession run, RunnerView hero, RunnerView guard, FirstPersonCameraRig cameraRig,
            Transform minecart, Transform tool, GameObject bulb, AudioSource sound, TimelineAsset timeline)
        {
            session = run; monkey = hero; police = guard; rig = cameraRig; cart = minecart; pickaxe = tool; idea = bulb; cartAudio = sound;
            toolParent = tool.parent; toolLocalPosition = tool.localPosition; toolLocalRotation = tool.localRotation;
            director = gameObject.AddComponent<PlayableDirector>(); director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual; director.extrapolationMode = DirectorWrapMode.Hold;
            director.playableAsset = timeline;
            var receiver = gameObject.AddComponent<SignalReceiver>();
            if (timeline != null) foreach (var track in timeline.GetOutputTracks())
            {
                if (track is IntroTimelineTrack) director.SetGenericBinding(track, this);
                if (track is SignalTrack)
                {
                    director.SetGenericBinding(track, receiver);
                    foreach (var marker in track.GetMarkers()) if (marker is SignalEmitter signal && signal.asset != null)
                    {
                        var reaction = new UnityEvent(); reaction.AddListener(CompleteIntro); receiver.AddReaction(signal.asset, reaction);
                    }
                }
            }
            ResetSequence();
        }
        public void ResetSequence()
        {
            Elapsed = 0; director.Stop(); director.time = 0;
            monkey.Pose(new Vector3(0, 0, 2), true); monkey.Play("AN_Monkey_MinePickaxe_Loop");
            police.Pose(new Vector3(1.8f, 0, -4), true);
            cart.gameObject.SetActive(true); cart.localPosition = new Vector3(2.2f, 0, -18); pickaxe.gameObject.SetActive(true); idea.SetActive(false);
            dropped = false; pickaxe.SetParent(toolParent, false); pickaxe.localPosition = toolLocalPosition; pickaxe.localRotation = toolLocalRotation;
            if (cartAudio != null) cartAudio.Stop(); rig.ResetCamera();
        }
        public void Begin()
        {
            director.time=0;director.Play();director.Evaluate();Sample(0);
            if(cartAudio!=null){cartAudio.volume=0;cartAudio.Play();}
        }
        public void PrepareGameplay()
        {
            Elapsed=0;director.Stop();
            session.Runner.SetStartZ(2);
            monkey.Pose(new Vector3(0,0,2),false);police.Pose(new Vector3(0,0,-12),true);
            cart.gameObject.SetActive(false);pickaxe.gameObject.SetActive(false);idea.SetActive(false);
            if(cartAudio!=null)cartAudio.Stop();
            rig.Takeover(monkey.Head);
        }
        public void Tick(float dt)
        {
            var state = session.Flow.State;
            if (state != GameFlowState.IntroTimeline && state != GameFlowState.FirstPersonTakeover) return;
            Elapsed = Mathf.Min(8f, Elapsed + dt);
            director.time = Elapsed; director.Evaluate();
            // Missing timeline remains demonstrable with the same sequence as a fallback.
            if (director.playableAsset == null) Sample(Elapsed);
            if (Elapsed >= 8f && session.Flow.State != GameFlowState.Running) CompleteIntro();
        }
        public void Sample(float t)
        {
            var state = session.Flow.State;
            if (state != GameFlowState.IntroTimeline && state != GameFlowState.FirstPersonTakeover) return;
            cart.localPosition = new Vector3(2.2f, 0, Mathf.Lerp(-18f, 5f, Mathf.Clamp01(t / 3f)));
            if (cartAudio != null) cartAudio.volume = Mathf.Lerp(.05f, .55f, Mathf.Clamp01(t / 3));
            idea.SetActive(t >= 3f && t < 4.4f);
            monkey.Play(t < 1.5f ? "AN_Monkey_MinePickaxe_Loop" : t < 2.3f ? "AN_Monkey_StopWork"
                : t < 3f ? "AN_Monkey_LookCart" : t < 4.4f ? "AN_Monkey_IdeaReact" : t < 5f ? "AN_Monkey_SprintStart" : "AN_Monkey_Run");
            if (t >= 4.4f)
            {
                if (!dropped) { dropped = true; droppedPosition = pickaxe.position; pickaxe.SetParent(transform, true); }
                float fall = Mathf.Clamp01((t - 4.4f) * 2);
                pickaxe.position = Vector3.Lerp(droppedPosition, new Vector3(.8f, .12f, 2.5f), fall);
                pickaxe.rotation = Quaternion.Euler(0, 25, Mathf.Lerp(0, 90, fall));
            }
            Vector3 hero = new Vector3(0, 0, 2 + Mathf.Max(0, t - 4.5f) * 2);
            monkey.Pose(hero, t < 7.65f);
            if (t > 4.5f)
            {
                police.Play(t < 5.3f ? "AN_Guard_Notice" : "AN_Guard_ChaseRun");
                police.Pose(new Vector3(-1.3f, 0, hero.z - Mathf.Lerp(10, 5, Mathf.InverseLerp(4.5f, 8, t))), true);
            }
            rig.IntroPose(t, hero);
            if (t >= 6f && state == GameFlowState.IntroTimeline) session.Flow.Transition(GameFlowState.FirstPersonTakeover);
        }
        public void CompleteIntro()
        {
            if (session.Flow.State != GameFlowState.FirstPersonTakeover) return;
            Sample(8); session.Runner.SetStartZ(9f); rig.Takeover(monkey.Head);
            director.Pause(); if (cartAudio != null) cartAudio.Stop(); session.Flow.Transition(GameFlowState.Running);
        }
    }
}
