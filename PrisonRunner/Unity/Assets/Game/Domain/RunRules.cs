using System;
using PrisonRunner.Core;

namespace Muhanok.Domain
{
    public enum GameFlowState { Boot, TitleIdle, IntroTimeline, FirstPersonTakeover, Running, Caught, Result, Retry, LoadingIntro }
    public enum MotionCommand { Center, Left, Right, Jump, Crouch, HighKnee }
    public enum ObstacleKind { Crate, Pipe, Barrier, Cart, Laser, Stair }
    public enum CaptureReason { Collision, BrokenRail, BananaPursuit }

    public sealed class GameFlowController
    {
        public GameFlowState State { get; private set; } = GameFlowState.Boot;
        public event Action<GameFlowState> Changed;
        public bool Transition(GameFlowState next)
        {
            bool valid = (State == GameFlowState.Boot && next == GameFlowState.TitleIdle)
                || (State == GameFlowState.TitleIdle && next == GameFlowState.IntroTimeline)
                || (State == GameFlowState.TitleIdle && next == GameFlowState.LoadingIntro)
                || (State == GameFlowState.LoadingIntro && next == GameFlowState.Running)
                || (State == GameFlowState.IntroTimeline && next == GameFlowState.FirstPersonTakeover)
                || (State == GameFlowState.FirstPersonTakeover && next == GameFlowState.Running)
                || (State == GameFlowState.Running && next == GameFlowState.Caught)
                || (State == GameFlowState.Caught && next == GameFlowState.Result)
                || (State == GameFlowState.Result && next == GameFlowState.Retry)
                // Leaving a run from the pause menu abandons it without a result.
                || (State == GameFlowState.Running && next == GameFlowState.Retry)
                || (State == GameFlowState.Retry && next == GameFlowState.TitleIdle);
            if (!valid) return false;
            State = next;
            Changed?.Invoke(State);
            return true;
        }
    }

    [Serializable]
    public sealed class RunConfig
    {
        public float LaneSpacing = 2.2f, LaneSeconds = .20f, BaseSpeed = 9f, MaxSpeed = 17f;
        public float JumpHeight = 1.8f, Gravity = 24f, CrouchSeconds = .8f, HighKneeSeconds = .9f;
        public float LightHit = .18f, HeavyHit = .35f, Recovery = .018f, StumbleSeconds = .8f;
        public int CleanBonus = 25; // Unused since score v2; kept for saved settings.
        public float SlipSeconds = 2.4f, SlipSpeedMultiplier = .5f, CartSpeed = 9f;
        public float BananaCatchSeconds=1.2f;
        public float SpeedRampDistance = 120f;
        // Jump/slide pressed shortly before landing runs on landing. Zero keeps the strict legacy rule.
        public float InputBufferSeconds = 0f;
        // Interval mode: walking sections are sprints, the mine cart ride is the rest.
        public bool IntervalMode = false;
        public float SprintSpeed = 6f, SprintSpeedStep = .5f, MaxSprintSpeed = 9.5f, RestSpeed = 5f;
        public float SprintSeconds = 40f, RestSeconds = 20f, SpeedChangeRate = 2.5f;
        public bool RandomizeLayout = false;
        public bool ExtendedRoutes = true;
        // Difficulty curve: nothing rises before the grace distance, everything reaches its max at the full distance.
        public float DifficultyGraceDistance = 150f, DifficultyFullDistance = 2000f;
        public float MaxRestSpeed = 7f;
        public float ObstacleChanceStart = .25f, ObstacleChanceMax = .9f;
        public float BlockerChanceStart = .25f, BlockerChanceMax = .72f;
        public float DoubleBlockerStart = .3f, DoubleBlockerMax = .85f;
        public float BananaChanceStart = .3f, BananaChanceMax = .82f;
    }

    // Per-chunk hazard odds: single-lane rows, a lane-blocker chunk, a blocker on both open lanes, a banana per lane.
    public readonly struct HazardOdds
    {
        public readonly float Row, Blocker, DoubleBlocker, Banana;
        public HazardOdds(float row, float blocker, float doubleBlocker, float banana) { Row = row; Blocker = blocker; DoubleBlocker = doubleBlocker; Banana = banana; }
    }

    public sealed class ChaseSystem
    {
        public float Pressure { get; private set; }
        public bool IsCaught => Pressure >= 1f;
        public void Hit(float amount) { Pressure = Math.Min(1f, Math.Max(0f, Pressure + amount)); }
        public void Recover(float amount) { if (!IsCaught) Hit(-Math.Max(0f, amount)); }
        public void Reset() { Pressure = 0f; }
    }

    public sealed class ScoreSystem
    {
        public float Distance { get; private set; }
        public int Coins { get; private set; }
        // Distance plus exercise (RunScore); coins and clean passes do not score.
        public int Score => RunScore.From(Distance, workout);
        // Obstacles and rail gaps cleared; kept for the record only.
        public int CleanPasses { get; private set; }
        private readonly WorkoutEstimate workout;
        public ScoreSystem(WorkoutEstimate runWorkout = null) { workout = runWorkout; }
        public void Advance(float metres) { Distance += Math.Max(0f, metres); }
        public void CleanPass() { CleanPasses++; }
        // The double-coin item adds extra coins per pickup.
        public int CoinMultiplier { get; set; } = 1;
        public void CollectCoin() { Coins += Math.Max(1, CoinMultiplier); }
        public void Reset() { Distance = 0f; Coins = 0; CleanPasses = 0; CoinMultiplier = 1; }
    }

    public sealed class DifficultyDirector
    {
        public float Speed(float distance, RunConfig config, bool riding = false)
            => Math.Min(config.MaxSpeed, (riding ? config.CartSpeed : config.BaseSpeed)
                + Math.Max(0f, distance) / Math.Max(1f, config.SpeedRampDistance));
        public float ObstacleChance(float distance) => Math.Min(.9f, .55f + distance / 2000f);
        // 0 until the grace distance, then a smoothstep that reaches 1 at the full distance.
        public float Progress(float distance, RunConfig config)
        {
            if (distance >= config.DifficultyFullDistance) return 1f;
            if (distance <= config.DifficultyGraceDistance) return 0f;
            float t = (distance - config.DifficultyGraceDistance) / (config.DifficultyFullDistance - config.DifficultyGraceDistance);
            return t * t * (3f - 2f * t);
        }
        public float Ramp(float start, float max, float distance, RunConfig config) => start + (max - start) * Progress(distance, config);
        public HazardOdds Odds(float distance, RunConfig config) => new HazardOdds(
            Ramp(config.ObstacleChanceStart, config.ObstacleChanceMax, distance, config),
            Ramp(config.BlockerChanceStart, config.BlockerChanceMax, distance, config),
            Ramp(config.DoubleBlockerStart, config.DoubleBlockerMax, distance, config),
            Ramp(config.BananaChanceStart, config.BananaChanceMax, distance, config));
    }

    public static class SafePatternValidator
    {
        // A complete row always reserves an empty lane. Rows are separated by 12m.
        public static bool IsSafe(int occupiedLaneMask) => (occupiedLaneMask & 7) != 7;
        public static bool Avoided(ObstacleKind kind, float height, bool crouch, bool highKnee)
        {
            switch (kind)
            {
                case ObstacleKind.Crate: return height > .9f;
                case ObstacleKind.Pipe: return crouch;
                case ObstacleKind.Stair: return highKnee;
                case ObstacleKind.Cart: return height > .9f;
                default: return false;
            }
        }
        public static bool IsActionRowSafe(int mask,ObstacleKind kind)=>IsSafe(mask)||kind==ObstacleKind.Crate||kind==ObstacleKind.Pipe||kind==ObstacleKind.Stair;
    }

    public sealed class RunnerController
    {
        private readonly RunConfig config;
        private float laneFrom, laneTime, verticalSpeed, crouchLeft, kneeLeft, stumbleLeft, slowLeft, jumpBuffer, crouchBuffer;
        public int Lane { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public bool Crouching => crouchLeft > 0f;
        public bool HighKnee => kneeLeft > 0f;
        public bool Stumbling => stumbleLeft > 0f;
        public bool Riding => RouteSurface.IsCart(Z);
        public float CartLean=>Riding&&!(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)?X/config.LaneSpacing:0;
        public float WorldLaneOffset=>Riding?(RouteSurface.ActivePlan?.CartLaneOffset(Z,X,CartLean*.16f)??CartLean*.16f):X;
        public bool Slowing => slowLeft > 0;
        public float SlowRemaining => slowLeft;
        public float SpeedMultiplier => slowLeft>0?config.SlipSpeedMultiplier:1;
        public RunnerController(RunConfig tuning) { config = tuning; Reset(); }
        public void Command(MotionCommand command)
        {
            if(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)return;
            int next = Lane;
            if (command == MotionCommand.Left) next = LaneRules.Move(Lane, -1);
            if (command == MotionCommand.Right) next = LaneRules.Move(Lane, 1);
            if(Riding && !(RouteSurface.ActivePlan?.ForkWindow(Z)??false))
            {
                if(command==MotionCommand.Left)next=-1;
                if(command==MotionCommand.Right)next=1;
                if(command==MotionCommand.Center)next=0;
            }
            else if(Riding&&command==MotionCommand.Center)next=0;
            if (next != Lane) { laneFrom = X; laneTime = 0f; Lane = next; }
            if (command == MotionCommand.Jump && !Riding)
            {
                if (Y <= .001f) StartJump();
                else jumpBuffer = config.InputBufferSeconds;
            }
            if (command == MotionCommand.Crouch)
            {
                if (Y <= .001f && verticalSpeed <= 0f) crouchLeft = config.CrouchSeconds;
                else crouchBuffer = config.InputBufferSeconds;
            }
            if (command == MotionCommand.HighKnee && !Riding) kneeLeft = config.HighKneeSeconds;
        }
        public void Stumble() { stumbleLeft = config.StumbleSeconds; }
        public void Slip() { slowLeft = config.SlipSeconds; }
        public void SetStartZ(float z) { Z = z; }
        public void Tick(float dt, float speed)
        {
            if (dt <= 0f) return;
            if(RouteSurface.ActivePlan?.RailChoiceLocked(Z)??false)
            {
                Lane=RouteSurface.ActivePlan.ChoiceAt(RouteSurface.SequenceAt(Z));laneFrom=X=Lane*config.LaneSpacing;laneTime=1;
            }
            laneTime += dt;
            float t = Math.Min(1f, laneTime / Math.Max(.01f, config.LaneSeconds));
            t = t * t * (3f - 2f * t);
            X = laneFrom + (Lane * config.LaneSpacing - laneFrom) * t;
            // Analytic gravity keeps the arc stable even at low frame rates.
            Y = Math.Max(0f, Y + verticalSpeed * dt - config.Gravity * dt * dt * .5f);
            verticalSpeed = Y <= 0f ? Math.Max(0f, verticalSpeed - config.Gravity * dt) : verticalSpeed - config.Gravity * dt;
            if(Riding){Y=0;verticalSpeed=0;kneeLeft=0;jumpBuffer=0;}
            if (Y <= .001f && verticalSpeed <= 0f)
            {
                if (jumpBuffer > 0f) { jumpBuffer = 0f; crouchBuffer = 0f; StartJump(); }
                else if (crouchBuffer > 0f) { crouchBuffer = 0f; crouchLeft = config.CrouchSeconds; }
            }
            jumpBuffer = Math.Max(0f, jumpBuffer - dt);
            crouchBuffer = Math.Max(0f, crouchBuffer - dt);
            bool wasRiding=Riding;
            Z += speed * SpeedMultiplier * dt;
            if(wasRiding!=Riding){laneFrom=X;Lane=0;laneTime=0;}
            if(wasRiding&&!Riding&&(RouteSurface.ActivePlan?.Extended??false)){X=laneFrom=0;laneTime=1;}
            crouchLeft = Math.Max(0f, crouchLeft - dt);
            kneeLeft = Math.Max(0f, kneeLeft - dt);
            stumbleLeft = Math.Max(0f, stumbleLeft - dt);
            slowLeft = Math.Max(0f, slowLeft - dt);
        }
        private void StartJump()
        {
            crouchLeft = 0f;
            verticalSpeed = (float)Math.Sqrt(2f * config.Gravity * config.JumpHeight);
        }
        public void Reset() { Lane = 0; X = Y = 0f; Z = 2f; laneFrom = verticalSpeed = crouchLeft = kneeLeft = stumbleLeft = slowLeft = jumpBuffer = crouchBuffer = 0f; laneTime = 1f; }
    }

    public sealed class RunSession
    {
        public readonly GameFlowController Flow = new GameFlowController();
        public readonly ChaseSystem Chase = new ChaseSystem();
        // Score reads Workout for the exercise part, so it is built in the constructor.
        public readonly ScoreSystem Score;
        public readonly DifficultyDirector Difficulty = new DifficultyDirector();
        public readonly RunnerController Runner;
        public readonly RunConfig Config;
        public readonly WorkoutEstimate Workout=new WorkoutEstimate();
        public readonly PowerUpState Items=new PowerUpState();
        public bool Paused {get; set;}
        // Tutorial runs never end in a capture: a mistake is only counted and the tutorial restarts the step.
        public bool IsTutorial { get => tutorial; set { tutorial = value; TutorialMisses = 0; } }
        public int TutorialMisses {get;private set;}
        // The tutorial slows the runner in front of a lesson obstacle (1 = normal speed).
        public float SpeedScale {get;set;}=1f;
        private bool tutorial;
        public int BananaHits {get;private set;}
        public int ShieldSaves {get;private set;}
        public int DodgeStreak {get;private set;}
        public int BestDodgeStreak {get;private set;}
        public void RecordDodge() { DodgeStreak++;BestDodgeStreak=Math.Max(BestDodgeStreak,DodgeStreak); }
        public bool BananaPursuit=>bananaCatchLeft>0;
        public float TargetSpeed => Config.IntervalMode ? IntervalSpeed() : Difficulty.Speed(Score.Distance, Config, Runner.Riding);
        public float CurrentSpeed => Flow.State == GameFlowState.Running && !Paused ? MoveSpeed * Runner.SpeedMultiplier * SpeedScale : 0f;
        private float MoveSpeed => Config.IntervalMode ? smoothSpeed : TargetSpeed;
        // Interval rhythm comes from the route: walking chunks sprint, the cart ride rests.
        public bool Resting => Config.IntervalMode && Runner.Riding;
        public int IntervalCycle => RouteSurface.CycleAt(RouteSurface.SequenceAt(Runner.Z));
        public float PhaseRemainingSeconds => RouteSurface.SegmentRemaining(Runner.Z) / Math.Max(1f, TargetSpeed);
        public CaptureReason Reason {get;private set;}
        private float bananaCatchLeft, smoothSpeed;
        private float lastHit = -100f, clock;
        // Play time without pauses; shown to the player, not part of the score.
        public float Elapsed => clock;
        public RunSession(RunConfig config) { Config = config; Score = new ScoreSystem(Workout); Runner = new RunnerController(config); smoothSpeed = TargetSpeed; }
        private float IntervalSpeed()
        {
            // The extended route follows the difficulty curve; the authored interval route keeps its per-lap step.
            if (Config.ExtendedRoutes)
                return Runner.Riding ? Difficulty.Ramp(Config.RestSpeed, Config.MaxRestSpeed, Score.Distance, Config)
                    : Difficulty.Ramp(Config.SprintSpeed, Config.MaxSprintSpeed, Score.Distance, Config);
            if (Runner.Riding) return Config.RestSpeed;
            return Math.Min(Config.MaxSprintSpeed, Config.SprintSpeed + Config.SprintSpeedStep * IntervalCycle);
        }
        public void Tick(float dt)
        {
            if (Flow.State != GameFlowState.Running || Paused) return;
            clock += dt;
            float target = TargetSpeed;
            smoothSpeed = smoothSpeed < target ? Math.Min(target, smoothSpeed + Config.SpeedChangeRate * dt) : Math.Max(target, smoothSpeed - Config.SpeedChangeRate * 1.6f * dt);
            Items.Tick(dt); Score.CoinMultiplier = Items.IsActive(ItemKind.DoubleScore) ? 2 : 1;
            float z = Runner.Z;
            Runner.Tick(dt, MoveSpeed * SpeedScale);
            Score.Advance(Runner.Z - z);
            if(BananaPursuit)
            {
                bananaCatchLeft=Math.Max(0,bananaCatchLeft-dt);
                Chase.Hit(.55f*dt/Math.Max(.1f,Config.BananaCatchSeconds));
                if(bananaCatchLeft<=.0001f)Fail(CaptureReason.BananaPursuit);
            }
            else if (BananaHits==0&&clock - lastHit > 2f) Chase.Recover(Config.Recovery * dt);
        }
        // Reads the item state at pickup time, so a coin in the same frame as the item already counts double.
        public void CollectCoin() { Score.CoinMultiplier = Items.IsActive(ItemKind.DoubleScore) ? 2 : 1; Score.CollectCoin(); }
        public bool Hit(bool heavy)
        {DodgeStreak=0;return Fail(CaptureReason.Collision);}
        public bool Fail(CaptureReason reason)
        {
            if (Flow.State != GameFlowState.Running || Paused) return false;
            if (tutorial) { Reason = reason; lastHit = clock; TutorialMisses++; return true; }
            if (reason == CaptureReason.Collision && Items.ConsumeShield())
            {
                // The shield absorbs one solid hit: a stumble instead of a capture.
                ShieldSaves++; Runner.Stumble(); lastHit = clock; return false;
            }
            Reason=reason;
            lastHit = clock;
            Runner.Stumble(); Chase.Hit(1f);
            Flow.Transition(GameFlowState.Caught);
            return true;
        }
        public bool Slip(bool banana=false)
        {
            if(Flow.State!=GameFlowState.Running||Paused||Runner.Riding)return false;
            DodgeStreak=0;
            // The shield also absorbs one slip: no slowdown, no banana count, so no pursuit starts.
            if(Items.ConsumeShield()){ShieldSaves++;lastHit=clock;return false;}
            Runner.Slip();
            if(banana)
            {
                BananaHits++;
                if(BananaHits==1)Chase.Hit(.45f);
                if(BananaHits>=2&&!BananaPursuit)bananaCatchLeft=Math.Max(.1f,Config.BananaCatchSeconds);
            }
            return true;
        }
        public void Reset() { Runner.Reset(); Chase.Reset(); Score.Reset(); Workout.Reset(); Items.Reset(); Paused=false; SpeedScale=1f; clock = 0f; lastHit = -100f;BananaHits=0;ShieldSaves=0;DodgeStreak=BestDodgeStreak=0;bananaCatchLeft=0;Reason=CaptureReason.Collision; smoothSpeed = TargetSpeed; }
    }
}
