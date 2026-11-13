using System.Collections.Generic;
using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        private static void AddPrisonGates(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>();
            if(type<4||type>6){chunk.Gates=new DescendingGate[0];return;}
            var gates=new List<DescendingGate>();
            foreach(var obstacle in chunk.Obstacles)
            {
                Clear(obstacle.transform); obstacle.Kind=ObstacleKind.Barrier;
                var previous=obstacle.GetComponent<DescendingGate>(); if(previous!=null) Object.DestroyImmediate(previous);
                var gate=obstacle.gameObject.AddComponent<DescendingGate>();
                gate.Panel=Node("SlidingDoorPanel",obstacle.transform,new Vector3(0,5,0));
                Shape("SteelDoor",gate.Panel,Vector3.zero,new Vector3(1.80f,3.30f,.23f),pale);
                for(int y=0;y<10;y++) Shape("ShutterRib",gate.Panel,new Vector3(0,-1.48f+y*.33f,-.145f),new Vector3(1.78f,.055f,.055f),iron);
                Shape("WarningBoard",gate.Panel,new Vector3(0,.5f,-.18f),new Vector3(1.78f,.72f,.06f),cream);
                for(int x=-2;x<=2;x++)
                {
                    var stripe=Shape("RedDoorStripe",gate.Panel,new Vector3(x*.35f,.5f,-.218f),new Vector3(.18f,.69f,.025f),red);
                    stripe.transform.localRotation=Quaternion.Euler(0,0,-24);
                }
                Shape("FloorSeal",gate.Panel,new Vector3(0,-1.60f,0),new Vector3(1.88f,.10f,.36f),black);
                for(int side=-1;side<=1;side+=2)
                {
                    Shape("VerticalTrack",obstacle.transform,new Vector3(side*.99f,2.3f,0),new Vector3(.16f,4.6f,.32f),iron);
                    Shape("SignalHousing",obstacle.transform,new Vector3(side*.95f,4.20f,-.30f),new Vector3(.24f,.42f,.24f),black);
                    var warning=Mat("GateWarningRed",new Color(1,.035f,.015f),0,.45f);Emit(warning,Color.red*3);
                    Shape("RedBeacon",obstacle.transform,new Vector3(side*.95f,4.2f,-.44f),new Vector3(.16f,.27f,.08f),warning,true);
                    var light=Node("GateRedBounce",obstacle.transform,new Vector3(side*.9f,3.8f,-.6f)).gameObject.AddComponent<Light>();
                    light.type=LightType.Point;light.color=Color.red;light.intensity=1.8f;light.range=3.2f;light.shadows=LightShadows.None;
                }
                Shape("DoorMotor",obstacle.transform,new Vector3(0,4.52f,0),new Vector3(2.15f,.35f,.47f),iron);
                gate.ResetGate(); gates.Add(gate);
            }
            chunk.Gates=gates.ToArray();
        }
    }
}
