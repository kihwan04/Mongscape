using UnityEngine;
namespace Muhanok.Presentation.Map
{
    public sealed class FallenTreeHazard : MonoBehaviour, IRunnerAnimated
    {
        public Transform Pivot;
        public ObstacleController[] Hazards;
        public float Progress {get;private set;}
        public void Preview(float runnerZ)
        {
            Progress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(32,14,transform.position.z-runnerZ));
            Pivot.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-4,90,Progress));
        }
    }
}
