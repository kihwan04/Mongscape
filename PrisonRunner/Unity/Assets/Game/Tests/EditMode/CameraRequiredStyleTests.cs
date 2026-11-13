using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class CameraRequiredStyleTests
    {
        [Test]
        public void WithoutCameraOnlyKeyboardIsAllowed()
        {
            Assert.IsTrue(PlayStyleInfo.Allowed(PlayStyle.Keyboard, false, false));
            Assert.IsFalse(PlayStyleInfo.Allowed(PlayStyle.FullBody, false, false));
            Assert.IsFalse(PlayStyleInfo.Allowed(PlayStyle.UpperBody, false, false));
            Assert.AreEqual(PlayStyle.Keyboard, PlayStyleInfo.Playable(PlayStyle.FullBody, false, false));
            Assert.AreEqual(PlayStyle.Keyboard, PlayStyleInfo.Playable(PlayStyle.UpperBody, false, false));
        }

        [Test]
        public void CameraOrDeveloperModeAllowsEveryStyle()
        {
            foreach (PlayStyle style in new[] { PlayStyle.FullBody, PlayStyle.UpperBody, PlayStyle.Keyboard })
            {
                Assert.IsTrue(PlayStyleInfo.Allowed(style, true, false));
                Assert.IsTrue(PlayStyleInfo.Allowed(style, false, true));
                Assert.AreEqual(style, PlayStyleInfo.Playable(style, false, true));
            }
        }
    }
}
