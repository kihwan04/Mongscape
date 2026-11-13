using Unity.Cinemachine;
using UnityEngine;
using Muhanok.Domain;

namespace Muhanok.Presentation
{
    public sealed class FirstPersonCameraRig : MonoBehaviour
    {
        public CinemachineCamera Menu, IntroA, Chase, FirstPerson;
        private CinemachineBrain brain;
        private Camera output;
        private CinemachineCamera active;
        public void Configure(Camera camera)
        {
            output=camera;
            brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null) brain = camera.gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .25f);
            foreach(var cameraRig in new[]{Menu,IntroA,Chase,FirstPerson})cameraRig.Lens.FarClipPlane=100;
            Select(Menu);
        }
        public void Select(CinemachineCamera next)
        {
            if (active == next) return;
            Menu.Priority = IntroA.Priority = Chase.Priority = FirstPerson.Priority = 0;
            next.Priority = 20; active = next;
        }
        public void IntroPose(float time, Vector3 runner)
        {
            if (time < 4.5f)
            {
                Select(IntroA);
                float push = Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.2f,3.4f,time));
                Vector3 position = Vector3.Lerp(new Vector3(1.05f,1.30f,5.9f),new Vector3(.80f,1.95f,4.4f),push);
                Set(IntroA, position, new Vector3(0, Mathf.Lerp(1.28f,1.83f,push), 2f));
                IntroA.Lens.FieldOfView = Mathf.Lerp(44,43,push);
            }
            else
            {
                Select(Chase);
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6f, 8f, time));
                Vector3 head = runner + Vector3.up * 1.65f;
                Vector3 start = runner + new Vector3(1.7f, 2.5f, -4.5f);
                Chase.transform.position = Vector3.Lerp(start, head, t);
                Quaternion follow = Quaternion.LookRotation(head + Vector3.forward * 5 - start);
                Chase.transform.rotation = Quaternion.Slerp(follow, Quaternion.identity, t);
                Chase.Lens.FieldOfView = Mathf.Lerp(65f, 80f, t);
                FirstPerson.transform.SetPositionAndRotation(head, Quaternion.identity);
            }
        }
        public void Takeover(Vector3 head)
        {
            FirstPerson.transform.SetPositionAndRotation(head, Quaternion.identity);
            // Chase has exactly the same pose and FOV at t=8; switching does not pop.
            Chase.transform.SetPositionAndRotation(head, Quaternion.identity); Chase.Lens.FieldOfView = 80f;
            Select(FirstPerson);
        }
        public void Follow(Vector3 head, bool crouch, float dt,float bank=0,float pitch=0,float bob=0)
        {
            if(active!=FirstPerson&&output!=null)FirstPerson.transform.SetPositionAndRotation(output.transform.position,output.transform.rotation);
            Select(FirstPerson);
            Vector3 position = head;
            if (crouch) position.y -= .8f;
            position.y += bob;
            FirstPerson.transform.position = Vector3.Lerp(FirstPerson.transform.position, position, 1 - Mathf.Exp(-24f * dt));
            RouteSurface.Sample(head.z,out float x,out float y); RouteSurface.Sample(head.z+.5f,out float nextX,out float nextY);
            FirstPerson.transform.rotation = Quaternion.Slerp(FirstPerson.transform.rotation,Quaternion.LookRotation(new Vector3(nextX-x,nextY-y,.5f))*Quaternion.AngleAxis(-pitch,Vector3.right)*Quaternion.AngleAxis(bank,Vector3.forward),1-Mathf.Exp(-8*dt));
        }
        public void FollowCart(Vector3 head,float dt,float bank)
        {
            if(active!=Chase&&output!=null)Chase.transform.SetPositionAndRotation(output.transform.position,output.transform.rotation);
            Select(Chase);
            RouteSurface.Sample(head.z,out float x,out float y);
            RouteSurface.Sample(head.z+7,out float nx,out float ny);
            if(RouteSurface.ActivePlan?.ForkWindow(head.z)??false){float lane=head.x-x;x+=lane;nx+=lane;}
            var forward=new Vector3(nx-x,ny-y,7).normalized;
            float local=RouteSurface.ActivePlan?.LapDistance(head.z)??0;
            float junction=(RouteSurface.ActivePlan?.Extended??false)?Mathf.SmoothStep(0,1,Mathf.InverseLerp(80,96,local))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(144,160,local))):0;
            var position=new Vector3(x,y,head.z)-forward*Mathf.Lerp(4.4f,5.2f,junction)+Vector3.up*Mathf.Lerp(3.0f,3.5f,junction);
            var target=new Vector3(nx,ny+.75f,head.z+7);
            float blend=1-Mathf.Exp(-12*dt);
            Chase.transform.position=Vector3.Lerp(Chase.transform.position,position,blend);
            Chase.transform.rotation=Quaternion.Slerp(Chase.transform.rotation,Quaternion.LookRotation(target-position)*Quaternion.AngleAxis(bank*.35f*(1-junction),Vector3.forward),blend);
            Chase.Lens.FieldOfView=Mathf.Lerp(62,65,junction);
        }
        public void ResetCamera() { Select(Menu); }
        public void FollowCapture(Vector3 player,float dt,float prone=0)
        {
            Select(Chase);
            RouteSurface.Sample(player.z,out float center,out _);
            var position=player+new Vector3(player.x>center? -2.7f:2.7f,Mathf.Lerp(2.5f,1.85f,prone),Mathf.Lerp(-.2f,1.8f,prone));
            float blend=1-Mathf.Exp(-9*dt);
            Chase.transform.position=Vector3.Lerp(Chase.transform.position,position,blend);
            Chase.transform.rotation=Quaternion.Slerp(Chase.transform.rotation,Quaternion.LookRotation(player+new Vector3(0,Mathf.Lerp(1.1f,.55f,prone),-.25f)-position),blend);
            Chase.Lens.FieldOfView=57;
        }
        private static void Set(CinemachineCamera cam, Vector3 pos, Vector3 target) { cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(target - pos); }
    }
}
