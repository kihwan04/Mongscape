using Muhanok.Domain;

namespace Muhanok.Application
{
    public interface IGameInput { bool TryRead(out MotionCommand command); void Clear(); }
    public sealed class InputRouter
    {
        private readonly RunSession session;
        private readonly IGameInput keyboard, pose;
        public InputRouter(RunSession run, IGameInput keys, IGameInput camera) { session = run; keyboard = keys; pose = camera; }
        public void Tick()
        {
            if (session.Flow.State != GameFlowState.Running || session.Paused) { keyboard.Clear(); pose.Clear(); return; }
            // Both sources remain available; no stale commands survive title/retry.
            for (int i = 0; i < 8 && keyboard.TryRead(out var command); i++) session.Runner.Command(command);
            for (int i = 0; i < 8 && pose.TryRead(out var command); i++) session.Runner.Command(command);
        }
    }
}
