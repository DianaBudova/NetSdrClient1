using NUnit.Framework;
using EchoServer.Wrappers;
using EchoServer.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

namespace EchoServerTests
{
    [TestFixture]
    public class TcpListenerWrapperTests
    {
        private TcpListenerWrapper _listenerWrapper = null!;
        private int _port;

        [SetUp]
        public void Setup()
        {
            _listenerWrapper = new TcpListenerWrapper(IPAddress.Loopback, 0);
            _listenerWrapper.Start();

            var localEndPoint = _listenerWrapper.InnerListener.LocalEndpoint as IPEndPoint;
            _port = localEndPoint!.Port;
        }

        [TearDown]
        public void TearDown()
        {
            _listenerWrapper.Stop();
        }

        [Test]
        public async Task AcceptTcpClientAsync_ShouldReturnClient_WhenClientConnects()
        {
            // Arrange
            var acceptTask = _listenerWrapper.AcceptTcpClientAsync();

            // Act
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _port);

            ITcpClient wrapperClient = await acceptTask;

            // Assert
            Assert.That(wrapperClient, Is.Not.Null);
            Assert.That(wrapperClient, Is.InstanceOf<ITcpClient>());

            var stream = wrapperClient.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes("Hello");
            await stream.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);

            Assert.That(buffer.Length, Is.EqualTo(5));
        }

        [Test]
        public void Start_ShouldStartListener_WithoutException()
        {
            Assert.That(() => _listenerWrapper.Start(), Throws.Nothing);
        }

        [Test]
        public void Stop_ShouldStopListener_WithoutException()
        {
            Assert.That(() => _listenerWrapper.Stop(), Throws.Nothing);
        }
    }
}
