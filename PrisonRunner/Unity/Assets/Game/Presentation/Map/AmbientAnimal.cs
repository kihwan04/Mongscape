using UnityEngine;

namespace Muhanok.Presentation.Map
{
    // A subtle ambient idle for static authored animals; not a skeletal locomotion rig.
    public sealed class AmbientAnimal : MonoBehaviour
    {
        private Vector3 size;
        private Quaternion facing;
        private void Awake(){size=transform.localScale;facing=transform.localRotation;}
        private void Update()
        {
            float phase=Time.time*1.4f+transform.position.z*.3f;
            transform.localScale=new Vector3(size.x,size.y*(1+Mathf.Sin(phase)*.008f),size.z);
            transform.localRotation=facing*Quaternion.Euler(0,Mathf.Sin(phase*.35f)*1.5f,0);
        }
    }
}
