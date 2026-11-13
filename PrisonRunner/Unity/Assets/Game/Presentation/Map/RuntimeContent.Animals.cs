using UnityEngine;
using Muhanok.Domain;

namespace Muhanok.Presentation.Map
{
    public sealed partial class RuntimeContent
    {
        private static Material giraffeSkin;
        private static readonly Color Sand=new Color(.96f,.72f,.28f),Spot=new Color(.42f,.22f,.075f),Grey=new Color(.49f,.54f,.58f),Dark=new Color(.065f,.055f,.04f);
        private static Transform Node(string name,Transform parent,Vector3 position)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        private static Transform Shape(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 size,Color color,Quaternion? rotation=null)
        {var t=Part(type,parent,p,size,color,rotation);t.name=name;return t;}
        private static void Limb(Transform parent,Vector3 a,Vector3 b,float width,Color color,string name)
        {
            var delta=b-a;Shape(name,PrimitiveType.Capsule,parent,(a+b)*.5f,new Vector3(width,delta.magnitude*.5f,width),color,Quaternion.FromToRotation(Vector3.up,delta));
        }
        private static Material GiraffeSkin()
        {
            if(giraffeSkin!=null)return giraffeSkin;
            var pixels=new Color32[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                int cellX=x/32,cellY=y/16,hash=(cellX*73+cellY*151+19)&255;
                float dx=(x%32)/32f-.5f+(hash%5-2)*.025f,dy=(y%16)/16f-.5f+((hash/7)%5-2)*.025f;
                bool spot=Mathf.Abs(dx)<.38f && Mathf.Abs(dy)<.36f && Mathf.Abs(dx)+Mathf.Abs(dy)*.7f<.53f;
                pixels[y*128+x]=spot?Color.Lerp(Spot,Sand,(hash%3)*.07f):Sand;
            }
            var texture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Authored giraffe hide",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};
            texture.SetPixels32(pixels);texture.Apply(true,true);
            giraffeSkin=new Material(Material(Color.white)){name="Giraffe continuous spotted hide"};
            giraffeSkin.SetTexture("_BaseMap",texture);return giraffeSkin;
        }
        private ZooHazard BuildGiraffe()
        {
            var node=Node("Zoo_Giraffe",root,Vector3.zero);var animal=node.gameObject.AddComponent<ZooHazard>();animal.Giraffe=true;
            var body=Node("Body",node,new Vector3(5.65f,0,0));
            var torso=Shape("SpottedTorso",PrimitiveType.Sphere,body,new Vector3(.35f,2.55f,.15f),new Vector3(1.75f,1.45f,2.5f),Sand);
            torso.GetComponent<Renderer>().sharedMaterial=GiraffeSkin();
            Shape("Shoulder",PrimitiveType.Sphere,body,new Vector3(-.25f,2.75f,0),new Vector3(1.05f,1.15f,1.1f),Sand).GetComponent<Renderer>().sharedMaterial=GiraffeSkin();
            for(int i=0;i<4;i++)
            {
                float x=i<2?-.2f:.95f,z=i%2==0?-.75f:.75f;
                Limb(body,new Vector3(x,2.2f,z),new Vector3(x-.12f,1.15f,z+.1f),.25f,Sand,"UpperLeg");
                Limb(body,new Vector3(x-.12f,1.15f,z+.1f),new Vector3(x-.26f,.16f,z+.14f),.19f,Sand,"LowerLeg");
                Shape("Hoof",PrimitiveType.Sphere,body,new Vector3(x-.26f,.13f,z+.14f),new Vector3(.3f,.24f,.34f),Spot);
            }
            Limb(body,new Vector3(.95f,2.65f,1.0f),new Vector3(1.2f,1.8f,1.4f),.09f,Sand,"Tail");
            Shape("TailTuft",PrimitiveType.Sphere,body,new Vector3(1.2f,1.7f,1.4f),new Vector3(.17f,.4f,.2f),Spot);
            animal.Attachment=Node("NeckRoot",body,new Vector3(-.25f,2.85f,0));
            var head=Node("Head",node,Vector3.zero);animal.Head=head;
            Shape("Head",PrimitiveType.Sphere,head,new Vector3(0,.02f,.05f),new Vector3(.6f,.72f,1.1f),Sand);
            Shape("Muzzle",PrimitiveType.Sphere,head,new Vector3(0,-.1f,.59f),new Vector3(.58f,.43f,.55f),new Color(.69f,.43f,.2f));
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Ear",PrimitiveType.Sphere,head,new Vector3(side*.42f,.35f,-.2f),new Vector3(.48f,.15f,.24f),Sand,Quaternion.Euler(0,side*15,side*20));
                Shape("Eye",PrimitiveType.Sphere,head,new Vector3(side*.27f,.17f,.25f),new Vector3(.115f,.13f,.11f),Dark);
                Limb(head,new Vector3(side*.18f,.3f,-.12f),new Vector3(side*.19f,.76f,-.15f),.07f,Sand,"Horn");
                Shape("HornTip",PrimitiveType.Sphere,head,new Vector3(side*.19f,.77f,-.15f),Vector3.one*.15f,Spot);
                Shape("Nostril",PrimitiveType.Sphere,head,new Vector3(side*.16f,-.02f,.83f),new Vector3(.09f,.07f,.045f),Dark);
            }
            CombineParts(body.gameObject);CombineParts(head.gameObject);
            var reach=Node("ConnectedNeck",node,Vector3.zero);animal.Reach=reach.gameObject.AddComponent<AnimalReachMesh>();animal.Moving=reach;
            animal.Reach.Configure(GiraffeSkin(),.28f,.23f);animal.Hazards=Hazards(node,ObstacleKind.Crate,3);return animal;
        }
        private ZooHazard BuildElephant()
        {
            var node=Node("Zoo_Elephant",root,Vector3.zero);var animal=node.gameObject.AddComponent<ZooHazard>();
            var body=Node("Body",node,new Vector3(6.25f,0,.15f));
            Shape("Torso",PrimitiveType.Sphere,body,new Vector3(.45f,2.3f,.1f),new Vector3(3.5f,2.8f,2.8f),Grey);
            for(int i=0;i<4;i++)
            {
                float x=i<2?-.45f:1.45f,z=i%2==0?-.85f:.85f;
                Limb(body,new Vector3(x,1.9f,z),new Vector3(x,.42f,z),.67f,Grey,"Leg");
                Shape("Foot",PrimitiveType.Sphere,body,new Vector3(x,.25f,z),new Vector3(.8f,.5f,.8f),Grey*.88f);
            }
            Limb(body,new Vector3(1.9f,2.35f,.1f),new Vector3(2.2f,1.4f,.2f),.13f,Grey,"Tail");
            var head=Node("Head",body,new Vector3(-1.05f,3.0f,-.15f));animal.Head=head;
            Shape("Forehead",PrimitiveType.Sphere,head,new Vector3(-.15f,.05f,0),new Vector3(1.9f,2.15f,1.9f),Grey);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Ear",PrimitiveType.Sphere,head,new Vector3(.15f,.1f,side*1.0f),new Vector3(1.05f,1.9f,.25f),Grey*.86f,Quaternion.Euler(side*12,0,-10));
                Shape("Eye",PrimitiveType.Sphere,head,new Vector3(-.71f,.33f,side*.66f),new Vector3(.12f,.15f,.13f),Dark);
                Limb(head,new Vector3(-.65f,-.32f,side*.54f),new Vector3(-1.12f,-.55f,side*.62f),.15f,new Color(.94f,.89f,.7f),"Tusk");
            }
            animal.Attachment=Node("MuzzleRoot",head,new Vector3(-.85f,.12f,0));
            CombineParts(body.gameObject);
            var reach=Node("ConnectedTrunk",node,Vector3.zero);animal.Reach=reach.gameObject.AddComponent<AnimalReachMesh>();animal.Reach.Configure(Material(Grey),.36f,.14f);
            var tip=Node("RoundedTrunkTip",node,Vector3.zero);animal.Moving=tip;
            Shape("TrunkTip",PrimitiveType.Sphere,tip,Vector3.zero,new Vector3(.3f,.38f,.3f),Grey);
            for(int side=-1;side<=1;side+=2)Shape("Nostril",PrimitiveType.Sphere,tip,new Vector3(side*.065f,-.13f,-.075f),new Vector3(.065f,.045f,.06f),Dark);
            CombineParts(tip.gameObject);animal.Hazards=Hazards(node,ObstacleKind.Crate,3);return animal;
        }
        private FallenTreeHazard BuildTree()
        {
            var node=Node("Zoo_FallingTree",root,Vector3.zero);var tree=node.gameObject.AddComponent<FallenTreeHazard>();
            var pivot=Node("RootHinge",node,new Vector3(3.8f,.53f,0));tree.Pivot=pivot;
            var bark=new Color(.28f,.14f,.06f);var wood=new Color(.69f,.43f,.19f);var leaves=new Color(.19f,.38f,.12f);
            Shape("WholeTrunk",PrimitiveType.Cylinder,pivot,new Vector3(0,3.9f,0),new Vector3(.62f,3.9f,.62f),bark);
            for(int i=0;i<7;i++)
            {
                float angle=i*Mathf.PI*2/7;
                Shape("BarkRidge",PrimitiveType.Cube,pivot,new Vector3(Mathf.Cos(angle)*.285f,3.9f,Mathf.Sin(angle)*.285f),new Vector3(.07f,7.7f,.08f),bark*.8f,Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0));
            }
            Shape("ExposedRoot",PrimitiveType.Sphere,pivot,new Vector3(0,.04f,0),new Vector3(.85f,.22f,.78f),wood);
            for(int i=0;i<3;i++)
            {
                float side=i-1;Limb(pivot,new Vector3(0,7.35f,0),new Vector3(side*.55f,8.25f,i%2==0?.5f:-.5f),.2f,bark,"CrownBranch");
                Shape("LeafCrown",PrimitiveType.Sphere,pivot,new Vector3(side*.4f,8.35f,i%2==0?.35f:-.35f),new Vector3(1.1f,1.05f,1.2f),leaves*(.9f+i*.06f));
            }
            CombineParts(pivot.gameObject);tree.Hazards=Hazards(node,ObstacleKind.Crate,3);return tree;
        }
    }
}
