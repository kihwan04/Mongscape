using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    public sealed class ObstacleController : MonoBehaviour
    {
        public ObstacleKind Kind;
        public float CollisionHalfWidth = .95f;
        public bool FullExerciseRow {get;set;}
        public bool Passed { get; private set; }
        public void ResetPass() { Passed = false; }
        public void Evaluate(RunSession session, float previousZ)
        {
            if (Passed || !gameObject.activeSelf || session.Flow.State!=GameFlowState.Running || session.Paused) return;
            var runner = session.Runner; float z = transform.position.z;
            if (previousZ > z || runner.Z < z) return;
            Passed = true;
            RouteSurface.Sample(runner.Z,out float offsetX,out _);
            bool sameLane = Mathf.Abs(runner.X+offsetX-transform.position.x) < (FullExerciseRow?1.101f:CollisionHalfWidth);
            bool clean = !sameLane || SafePatternValidator.Avoided(Kind, runner.Y, runner.Crouching, runner.KneeStepping);
            if (clean) { session.Score.CleanPass(); if(sameLane)session.RecordDodge(); }
            else session.Hit(Kind == ObstacleKind.Barrier || Kind == ObstacleKind.Laser || Kind == ObstacleKind.Cart);
        }
    }
}
