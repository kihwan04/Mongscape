using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class CameraRequiredStyleTests
    {
        [Test]
        public void WithoutCameraOnlyKeyboardIsAllowed()
        {
            Assert.IsTrue(PlayStyleInfo.Allowed(PlayStyle.Keyboard, false));
            Assert.IsFalse(PlayStyleInfo.Allowed(PlayStyle.FullBody, false));
            Assert.IsFalse(PlayStyleInfo.Allowed(PlayStyle.UpperBody, false));
            Assert.AreEqual(PlayStyle.Keyboard, PlayStyleInfo.Playable(PlayStyle.FullBody, false));
            Assert.AreEqual(PlayStyle.Keyboard, PlayStyleInfo.Playable(PlayStyle.UpperBody, false));
        }

        [Test]
        public void CameraAllowsEveryStyle()
        {
            foreach (PlayStyle style in new[] { PlayStyle.FullBody, PlayStyle.UpperBody, PlayStyle.Keyboard })
            {
                Assert.IsTrue(PlayStyleInfo.Allowed(style, true));
                Assert.AreEqual(style, PlayStyleInfo.Playable(style, true));
            }
        }
    }
}
