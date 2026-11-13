using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace Muhanok.Presentation
{
    public sealed class IntroTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        public ClipCaps clipCaps => ClipCaps.None;
        public override double duration => 8;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => ScriptPlayable<IntroTimelineBehaviour>.Create(graph);
    }
}
