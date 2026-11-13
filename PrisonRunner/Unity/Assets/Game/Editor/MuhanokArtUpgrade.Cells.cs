using System.Linq;
using Muhanok.Presentation.Map;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void EnsureCellAnimation()
        {
            foreach(string name in new[]{"Monkey","Guard"})
            {
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animations/"+name+".controller");
                var layers=controller.layers; layers[0].iKPass=true;controller.layers=layers;
                if(name=="Monkey")
                {
                    var state=layers[0].stateMachine.states.FirstOrDefault(s=>s.state.name=="AN_Monkey_CellIdle").state;
                    if(state==null)state=layers[0].stateMachine.AddState("AN_Monkey_CellIdle");
                    state.motion=AssetDatabase.LoadAllAssetsAtPath(Characters+"/AN_Monkey_CellIdle.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
                }
                EditorUtility.SetDirty(controller);
            }
        }
        private static void AddCellActors(GameObject root,int type)
        {
            var previous=root.transform.Find("CellActors");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            if(type!=4&&type!=5)return;
            var group=Node("CellActors",root.transform,Vector3.zero);
            for(int side=-1;side<=1;side+=2)
            {
                float z=side<0?4:16;int number=100+type*20+(int)z+(side>0?10:0);
                var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/CHR_Monkey_Prisoner.prefab"));
                actor.name="CellMonkey_"+number;actor.transform.SetParent(group,false);
                actor.transform.localPosition=new Vector3(side*4.58f,.04f,z+1.9f);
                actor.transform.localRotation=Quaternion.Euler(0,side<0?90:-90,0);
                actor.GetComponent<Muhanok.Presentation.FootGrounding>().FollowRoute=false;
                var prisoner=actor.AddComponent<CellPrisoner>();prisoner.CellNumber=number;prisoner.Side=side;
                var fill=Node("CellFaceFill",group,new Vector3(side*4.15f,2.25f,z+1.6f)).gameObject.AddComponent<Light>();
                fill.type=LightType.Point;fill.color=new Color(1,.72f,.46f);fill.intensity=2.1f;fill.range=3.4f;fill.shadows=LightShadows.None;
            }
        }
    }
}
