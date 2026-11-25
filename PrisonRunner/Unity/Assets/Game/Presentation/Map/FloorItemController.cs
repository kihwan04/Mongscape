using Muhanok.Domain;
using UnityEngine;
namespace Muhanok.Presentation.Map
{
    public enum FloorItemKind { BananaPeel, Puddle }
    public sealed class FloorItemController : MonoBehaviour
    {
        public FloorItemKind Kind;
        public bool Passed {get;private set;}
        public void ResetItem(){Passed=false;}
        public void Evaluate(RunSession session,float previousZ)
        {
            if(Passed||!gameObject.activeInHierarchy||session.Flow.State!=GameFlowState.Running||session.Paused)return;
            float z=transform.position.z;
            if(previousZ>z||session.Runner.Z<z)return;
            Passed=true;
            RouteSurface.Sample(session.Runner.Z,out float offset,out _);
            if(Mathf.Abs(session.Runner.X+offset-transform.position.x)<.9f&&session.Runner.Y<.45f)
                session.Slip(Kind==FloorItemKind.BananaPeel?SlipSource.Banana:SlipSource.Floor);
        }
    }
}
