using System.Collections.Generic;
using Muhanok.Application;
using Muhanok.Domain;
using UnityEngine.InputSystem;

namespace Muhanok.Infrastructure
{
    public sealed class KeyboardGameInput : IGameInput
    {
        private readonly Queue<MotionCommand> buffer = new Queue<MotionCommand>(6);
        public static bool StartPressed => (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
            || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame))
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        // R recalibrates the camera pose. Ctrl+Shift+F12 toggles developer (exhibition) mode, where Ctrl+F10 clears today's ranking.
        public static bool RecalibratePressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        public static bool SettingsPressed => Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
        public static bool OverlayPressed => Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
        public static bool ClearRankingPressed => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed && Keyboard.current.f10Key.wasPressedThisFrame;
        // F8 toggles the player lock. F9 toggles demo invincibility and F7 ends the run at once (even while invincible); only development builds read those two.
        public static bool ForceCatchPressed => Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
        // F6 starts and stops the joint recording (developer mode only, see GameBootstrap.TogglePoseRecording).
        public static bool PoseRecordPressed => Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame;
        public static bool PlayerLockPressed => Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
        public static bool InvinciblePressed =>Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
        public static bool DeveloperTogglePressed => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed && Keyboard.current.shiftKey.isPressed && Keyboard.current.f12Key.wasPressedThisFrame;
        // Korean names need the IME while the setup text fields are open.
        public static void SetTextInput(bool on) { if (Keyboard.current != null) Keyboard.current.SetIMEEnabled(on); }
        public void Poll()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) buffer.Enqueue(MotionCommand.Left);
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) buffer.Enqueue(MotionCommand.Right);
            if (k.cKey.wasPressedThisFrame) buffer.Enqueue(MotionCommand.Center);
            if (k.spaceKey.wasPressedThisFrame) buffer.Enqueue(MotionCommand.Jump);
            if (k.sKey.isPressed || k.downArrowKey.isPressed) buffer.Enqueue(MotionCommand.Crouch);
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) buffer.Enqueue(MotionCommand.KneeStep);
        }
        public bool TryRead(out MotionCommand command) { command = MotionCommand.Center; if (buffer.Count == 0) return false; command = buffer.Dequeue(); return true; }
        public void Clear() { buffer.Clear(); }
    }
}
