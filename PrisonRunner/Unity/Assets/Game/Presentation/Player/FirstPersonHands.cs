using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation
{
    public sealed class FirstPersonHands : MonoBehaviour
    {
        public Transform Left, Right, Visual;
        public Transform CartRim;
        public AudioSource CartSound;
        private RunSession session;
        private Vector3 leftStart, rightStart;
        private float phase;
        private float lean,jumpPose,crouchPose,stumblePose;
        public void Configure(RunSession run)
        { session = run; leftStart = Left.localPosition; rightStart = Right.localPosition; }
        private void LateUpdate()
        {
            if (session == null) return;
            bool visible = !session.Runner.Riding&&session.Flow.State == GameFlowState.Running;
            Visual.gameObject.SetActive(visible); if (!visible) {if(CartSound!=null)CartSound.Stop();phase = 0; return; }
            phase += Time.deltaTime*10;
            float z=session.Runner.Z;
            int sequence=Mathf.Max(0,Mathf.FloorToInt(z/24));float local=z-sequence*24;
            float progress=RouteSurface.CartProgress(z);
            float ride=RouteSurface.IsCart(z)?Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.055f,progress))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.955f,1,progress))):0;
            if(CartRim!=null) { CartRim.gameObject.SetActive(ride>0); CartRim.localPosition=new Vector3(0,Mathf.Lerp(-2.4f,-1.36f,ride),.07f); }
            if(CartSound!=null)
            {
                bool rolling=ride>0&&session.Flow.State==GameFlowState.Running&&!session.Paused;
                if(rolling&&!CartSound.isPlaying)CartSound.Play();
                if(!rolling&&CartSound.isPlaying)CartSound.Stop();
            }
            float amount = session.Runner.Crouching ? .01f : Mathf.Lerp(.035f,.007f,ride);
            float blend=1-Mathf.Exp(-12*Time.deltaTime);
            lean=Mathf.Lerp(lean,session.Runner.X*.025f,blend);
            jumpPose=Mathf.Lerp(jumpPose,session.Runner.Y>.05f?1:0,blend);
            crouchPose=Mathf.Lerp(crouchPose,session.Runner.Crouching?1:0,blend);
            stumblePose=Mathf.Lerp(stumblePose,session.Runner.Stumbling?1:0,blend);
            float stride=Mathf.Sin(phase)*amount;
            var lift=new Vector3(-lean,jumpPose*.11f-crouchPose*.14f,-jumpPose*.05f+stumblePose*.10f);
            Left.localPosition=Vector3.Lerp(leftStart,new Vector3(-.48f,-.24f,.95f),ride)+lift+new Vector3(0,stride,Mathf.Cos(phase)*.022f);
            Right.localPosition=Vector3.Lerp(rightStart,new Vector3(.48f,-.24f,.95f),ride)+lift+new Vector3(0,-stride,-Mathf.Cos(phase)*.022f);
            Left.localRotation=Quaternion.Euler(-12+stride*160-jumpPose*18+stumblePose*25,-12,-10+lean*100);
            Right.localRotation=Quaternion.Euler(-12-stride*160-jumpPose*18+stumblePose*25,12,10+lean*100);
        }
    }
}
