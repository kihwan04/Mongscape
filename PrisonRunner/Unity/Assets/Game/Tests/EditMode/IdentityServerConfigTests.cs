using System;
using Muhanok.Infrastructure;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class IdentityServerConfigTests
    {
        [TestCase("https://faces.example.com/", "https://faces.example.com")]
        [TestCase("https://faces.example.com:8443", "https://faces.example.com:8443")]
        [TestCase("http://127.0.0.1:8765", "http://127.0.0.1:8765")]
        [TestCase("http://localhost:8766/", "http://localhost:8766")]
        public void SupportsHttpsServerAndLoopbackDocker(string input,string expected)
        { Assert.AreEqual(expected,LocalProfileClient.IdentityServerConfig.Normalize(input)); }

        [TestCase("http://faces.example.com")]
        [TestCase("http://192.168.1.10:8765")]
        [TestCase("https://user:password@faces.example.com")]
        [TestCase("https://faces.example.com/api")]
        [TestCase("https://faces.example.com/?token=example")]
        [TestCase("https://faces.example.com/#fragment")]
        [TestCase("file:///tmp/example")]
        [TestCase("not a URL")]
        public void RejectsInsecureOrAmbiguousRemoteAddress(string input)
        { Assert.Throws<FormatException>(()=>LocalProfileClient.IdentityServerConfig.Normalize(input)); }

        [Test]
        public void EnvironmentOverridesTheSidecarAndInvalidAddressFailsClosed()
        {
            string previous=Environment.GetEnvironmentVariable("MONGSCAPE_IDENTITY_URL");
            try
            {
                Environment.SetEnvironmentVariable("MONGSCAPE_IDENTITY_URL","https://faces.example.com");
                Assert.AreEqual("https://faces.example.com",LocalProfileClient.IdentityServerConfig.Load());
                Environment.SetEnvironmentVariable("MONGSCAPE_IDENTITY_URL","http://faces.example.com");
                Assert.Throws<FormatException>(()=>LocalProfileClient.IdentityServerConfig.Load());
            }
            finally{Environment.SetEnvironmentVariable("MONGSCAPE_IDENTITY_URL",previous);}
        }
    }
}
