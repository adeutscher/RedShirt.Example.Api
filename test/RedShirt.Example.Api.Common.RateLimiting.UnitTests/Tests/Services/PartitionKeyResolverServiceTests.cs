using Microsoft.AspNetCore.Http;
using Moq;
using RedShirt.Example.Api.Common.RateLimiting.Services;
using System.Net;
using System.Security.Claims;

namespace RedShirt.Example.Api.Common.RateLimiting.UnitTests.Tests.Services;

public class PartitionKeyResolverServiceTests
{
    public class ResolvePartitionKey
    {
        [Fact]
        public void IgnoresWhitespaceSubClaim_AndFallsBackToIp()
        {
            var connection = new Mock<ConnectionInfo>();
            connection.SetupGet(c => c.RemoteIpAddress).Returns(IPAddress.Loopback);

            var context = new Mock<HttpContext>();
            context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "   ")
            ], "test")));
            context.SetupGet(c => c.Connection).Returns(connection.Object);

            var key = new PartitionKeyResolverService().ResolvePartitionKey(context.Object);

            Assert.Equal("ip:127.0.0.1", key);
        }

        [Fact]
        public void PrefersSubClaim_OverNameIdentifier()
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", "subject-1"),
                    new Claim(ClaimTypes.NameIdentifier, "name-id-1")
                ], "test"))
            };

            var key = new PartitionKeyResolverService().ResolvePartitionKey(context);

            Assert.Equal("user:subject-1", key);
        }

        [Fact]
        public void ReturnsAnonymous_WhenNoClaimsAndNoIp()
        {
            var connection = new Mock<ConnectionInfo>();
            connection.SetupGet(c => c.RemoteIpAddress).Returns((IPAddress?) null);

            var context = new Mock<HttpContext>();
            context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
            context.SetupGet(c => c.Connection).Returns(connection.Object);

            var key = new PartitionKeyResolverService().ResolvePartitionKey(context.Object);

            Assert.Equal("anonymous", key);
        }

        [Fact]
        public void UsesNameIdentifier_WhenSubClaimMissing()
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "name-id-2")
                ], "test"))
            };

            var key = new PartitionKeyResolverService().ResolvePartitionKey(context);

            Assert.Equal("user:name-id-2", key);
        }

        [Fact]
        public void UsesRemoteIp_WhenNoUserClaims()
        {
            var connection = new Mock<ConnectionInfo>();
            connection.SetupGet(c => c.RemoteIpAddress).Returns(IPAddress.Parse("203.0.113.10"));

            var context = new Mock<HttpContext>();
            context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
            context.SetupGet(c => c.Connection).Returns(connection.Object);

            var key = new PartitionKeyResolverService().ResolvePartitionKey(context.Object);

            Assert.Equal("ip:203.0.113.10", key);
        }
    }
}