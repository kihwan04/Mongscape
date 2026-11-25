using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    public sealed class CoinController : MonoBehaviour
    {
        // The magnet pulls coins from this far ahead (and slightly behind) the runner, in any lane.
        public const float MagnetReach = 8f, MagnetBehind = 2f, MagnetPickup = .8f;
        private bool collected, pulled, hasHome;
        private Vector3 home;
        public bool Pulled => pulled;
        // Stair coins sit at knee height and only count while the runner is knee-stepping.
        public bool RequiresKneeStep { get; set; }
        // Pooled coins return to their authored spot; chunk preparation then only re-picks the lane (x).
        public void ResetCoin()
        {
            if (!hasHome) { home = transform.localPosition; hasHome = true; }
            else transform.localPosition = home;
            collected = pulled = false; transform.localRotation = Quaternion.identity;
        }
        public void Evaluate(RunSession run,float previousZ)
        {
            if(collected||!gameObject.activeSelf||run.Flow.State!=GameFlowState.Running||run.Paused) return;
            if(!ObstacleLayout.CoinReachable(RequiresKneeStep,run.Runner.KneeStepping)) return;
            RouteSurface.Sample(run.Runner.Z,out float x,out float y);
            var player=new Vector3(run.Runner.WorldLaneOffset+x,run.Runner.Y+y+.85f,run.Runner.Z);
            if(run.Items.IsActive(ItemKind.Magnet))
            {
                float ahead=transform.position.z-run.Runner.Z;
                if(pulled||ahead<=MagnetReach&&ahead>=-MagnetBehind)
                {
                    if(!hasHome){home=transform.localPosition;hasHome=true;}
                    pulled=true;
                    transform.position=Vector3.MoveTowards(transform.position,player,(14f+run.CurrentSpeed)*Time.deltaTime);
                    if((transform.position-player).sqrMagnitude<=MagnetPickup*MagnetPickup){Collect(run);return;}
                }
            }
            if(previousZ>transform.position.z||run.Runner.Z<transform.position.z) return;
            if(Mathf.Abs(player.x-transform.position.x)>.8f||Mathf.Abs(player.y-transform.position.y)>.9f) return;
            Collect(run);
        }
        private void Collect(RunSession run) { collected=true; run.CollectCoin(); gameObject.SetActive(false); }
        private void Update() { transform.Rotate(0,Time.deltaTime*90,0,Space.Self); }
    }
}
