using NetArchTest.Rules;
using Xunit;

namespace NetSdrClientAppTests
{
    public class DependencyRulesTests
    {
        [Fact]
        public void Client_ShouldNotDependOnServer()
        {
            var result = Types.InAssembly(typeof(NetSdrClientApp.NetSdrClient).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp", true)
                .ShouldNot()
                .HaveDependencyOn("EchoTspServer")
                .GetResult();

            Assert.True(result.IsSuccessful, "NetSdrClientApp не повинен залежати напряму від EchoTspServer");
        }

        [Fact]
        public void Server_ShouldNotDependOnClient()
        {
            var result = Types.InAssembly(typeof(EchoServer).Assembly)
                .That()
                .ResideInNamespace("EchoTspServer", true)
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp")
                .GetResult();

            Assert.True(result.IsSuccessful, "EchoTspServer не повинен залежати від NetSdrClientApp");
        }

        [Fact]
        public void ClientMessages_ShouldNotDependOnServer()
        {
            var result = Types.InAssembly(typeof(NetSdrClientApp.Messages.NetSdrMessageHelper).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Messages", true)
                .ShouldNot()
                .HaveDependencyOn("EchoTspServer")
                .GetResult();

            Assert.True(result.IsSuccessful, "NetSdrClientApp.Messages не повинен залежати від EchoTspServer");
        }

        [Fact]
        public void ClientNetworking_ShouldNotDependOnServer()
        {
            var result = Types.InAssembly(typeof(NetSdrClientApp.Networking.TcpClientWrapper).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Networking", true)
                .ShouldNot()
                .HaveDependencyOn("EchoTspServer")
                .GetResult();

            Assert.True(result.IsSuccessful, "NetSdrClientApp.Networking не повинен залежати від EchoTspServer");
        }
    }
}
