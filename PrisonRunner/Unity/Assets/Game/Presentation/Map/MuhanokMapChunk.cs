using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Map
{
    public sealed class MuhanokMapChunk : MonoBehaviour
    {
        public RailForkView ForkView {get;set;}
        public const float Length = 24f;
        public string AssetId;
        public int RouteType;
        public Transform Entry, Exit;
        public ObstacleController[] Obstacles;
        public ObstacleController[] ExerciseObstacles = new ObstacleController[0];
        public CoinController[] Coins;
        public DescendingGate[] Gates;
        public FloorItemController[] FloorItems;
        public CartRailGap[] RailGaps;
        public ClosingSlideGate[] SlideGates;
        public int Sequence { get; private set; }
        public void PlaceOnRoute(int sequence)
        {
            Sequence = sequence;
            var plan = RouteSurface.ActivePlan;
            var entry = Entry.localPosition; var exit = Exit.localPosition;
            entry.x = exit.x = 0;
            if (plan != null && plan.IsForkTrackChunk(sequence))
            {
                float origin = plan.CycleOffset(sequence);
                entry.x = plan.SelectedForkOffset(sequence * Length) - origin;
                exit.x = plan.SelectedForkOffset((sequence + 1) * Length - .001f) - origin;
            }
            Entry.localPosition = entry; Exit.localPosition = exit;
            RouteSurface.Sample(sequence * Length, out float x, out _);
            transform.position += new Vector3(x, RouteSurface.EntryHeight(RouteType), sequence * Length) - Entry.position;
        }
        public void UpdateCartRoute()
        {
            if(Coins==null || (RouteType!=1 && RouteType!=2))return;
            foreach(var coin in Coins)
            {
                var p=coin.transform.localPosition;
                RouteSurface.Sample(Entry.position.z+p.z, out float x, out _);
                p.x=x-transform.position.x;
                coin.transform.localPosition=p;
            }
        }
        // Runtime additions placed by the decorator; not part of the authored prefab.
        public readonly System.Collections.Generic.List<ObstacleController> DynamicObstacles = new System.Collections.Generic.List<ObstacleController>();
        public readonly System.Collections.Generic.List<FloorItemController> DynamicFloorItems = new System.Collections.Generic.List<FloorItemController>();
        public readonly System.Collections.Generic.List<ItemPickup> Pickups = new System.Collections.Generic.List<ItemPickup>();
        public readonly System.Collections.Generic.List<IRunnerAnimated> Animated = new System.Collections.Generic.List<IRunnerAnimated>();
        // This run's hazard plan for the chunk; kept with the pooled chunk so the decorator reads the same result.
        public readonly ObstacleLayout Layout = new ObstacleLayout();
        private System.Collections.Generic.List<ObstacleSlot> slots;
        private System.Collections.Generic.List<LanePlacement> floorSlots;
        // Authored hazards as Domain slots: lane obstacles, then the full exercise rows. Measured once per pooled chunk.
        private void DescribeSlots()
        {
            if(slots!=null)return;
            slots=new System.Collections.Generic.List<ObstacleSlot>(12);floorSlots=new System.Collections.Generic.List<LanePlacement>(2);
            for(int i=0;i<Obstacles.Length;i++)
                slots.Add(new ObstacleSlot(Obstacles[i].Kind,i%3-1,LocalZ(Obstacles[i].transform),false,RouteType==7));
            if(ExerciseObstacles!=null)for(int i=0;i<ExerciseObstacles.Length;i++)
                slots.Add(new ObstacleSlot(ExerciseObstacles[i].Kind,i%3-1,LocalZ(ExerciseObstacles[i].transform),true));
            if(FloorItems!=null)foreach(var item in FloorItems)
            {
                var p=transform.InverseTransformPoint(item.transform.position);
                floorSlots.Add(new LanePlacement(Mathf.Clamp(Mathf.RoundToInt((p.x-RouteSurface.LocalX(RouteType,p.z))/2.2f),-1,1),p.z));
            }
        }
        private float LocalZ(Transform item)=>transform.InverseTransformPoint(item.position).z;
        public void Prepare(int sequence, float chance, System.Random random)
        {
            DescribeSlots();
            var plan=RouteSurface.ActivePlan;
            bool fork=plan!=null&&(plan.IsForkChunk(sequence)||plan.IsForkTrackChunk(sequence));
            // The first cart chunk after walking is where the cart is boarded.
            bool boarding=sequence<=0||plan!=null&&!ObstacleLayout.IsCart(plan.TypeAt(sequence-1));
            Layout.Plan(random,RouteType,sequence,slots,floorSlots,chance,plan!=null&&plan.Extended,fork,boarding);
            if(Gates!=null) foreach(var gate in Gates) gate.ResetGate();
            if(SlideGates!=null)foreach(var gate in SlideGates)gate.ResetGate();
            if(FloorItems!=null)for(int i=0;i<FloorItems.Length;i++){FloorItems[i].ResetItem();FloorItems[i].gameObject.SetActive(Layout.FloorItems[i]);}
            // Fork tracks hide the bridge for the three-way junction; their rail pieces go with it.
            bool junction=plan!=null&&plan.IsForkTrackChunk(sequence);
            if(RailGaps!=null)for(int i=0;i<RailGaps.Length;i++)
            {
                RailGaps[i].Configure(i<Layout.RailGaps.Length?Layout.RailGaps[i]:0);
                if(RailGaps[i].Pieces!=null)RailGaps[i].Pieces.gameObject.SetActive(!junction);
            }
            for (int i = 0; i < Obstacles.Length; i++)
            {
                Obstacles[i].ResetPass();Obstacles[i].FullExerciseRow=slots[i].FullRow;Obstacles[i].gameObject.SetActive(Layout.Slots[i]);
            }
            if(ExerciseObstacles!=null)for(int i=0;i<ExerciseObstacles.Length;i++)
            {
                var obstacle=ExerciseObstacles[i];
                obstacle.ResetPass();obstacle.FullExerciseRow=true;obstacle.gameObject.SetActive(Layout.Slots[Obstacles.Length+i]);
            }
            if (Coins != null) foreach(var coin in Coins) { coin.ResetCoin(); coin.gameObject.SetActive(sequence>0); }
            if(RouteType==1||RouteType==2)
            {
                UpdateCartRoute();
                return;
            }
            if (Coins != null) for(int i=0;i<Coins.Length;i++)
            {
                int row = i/3, lane = (sequence+row)%3;
                if (Obstacles[row*3+lane].gameObject.activeSelf) lane=(lane+1)%3;
                var position=Coins[i].transform.localPosition; position.x=(lane-1)*2.2f+RouteSurface.LocalX(RouteType,position.z); Coins[i].transform.localPosition=position;
            }
        }
        public void AnimateGates(float runnerZ)
        {
            if(Gates!=null) foreach(var gate in Gates) if(gate.gameObject.activeInHierarchy) gate.Preview(runnerZ);
            if(SlideGates!=null)foreach(var gate in SlideGates)if(gate.gameObject.activeInHierarchy)gate.Preview(runnerZ);
            foreach(var item in Animated)item.Preview(runnerZ);
        }
    }
}
