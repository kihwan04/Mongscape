using Muhanok.Domain;
using Muhanok.Presentation.Map;
using UnityEngine;

namespace Muhanok.Editor
{
    public static partial class MuhanokArtUpgrade
    {
        // The prison stair has no closing door any more; only its open shutter tracks stay as scenery.
        private static void AddClosingPrisonEntry(GameObject root,int type)
        {
            var chunk=root.GetComponent<MuhanokMapChunk>();
            foreach(var old in root.GetComponentsInChildren<ClosingSlideGate>(true))Object.DestroyImmediate(old);
            chunk.SlideGates=new ClosingSlideGate[0];if(type!=7)return;
            var env=root.transform.Find("EnvironmentRoot");
            for(int side=-1;side<=1;side+=2)
                Shape("EntryShutterTrack",env,new Vector3(side*3.45f,2.2f,19),new Vector3(.18f,4.4f,.36f),iron);
            Label("CELL BLOCK",env,new Vector3(0,4.17f,23),.12f,cream.color);
        }
    }
}
