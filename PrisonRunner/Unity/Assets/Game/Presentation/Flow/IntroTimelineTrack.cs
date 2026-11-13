using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Muhanok.Presentation
{
    [TrackClipType(typeof(IntroTimelineClip))]
    [TrackBindingType(typeof(IntroSequenceDirector))]
    public sealed class IntroTimelineTrack : TrackAsset { }
    public sealed class IntroTimelineBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (playerData is IntroSequenceDirector sequence) sequence.Sample((float)playable.GetTime());
        }
    }
}
