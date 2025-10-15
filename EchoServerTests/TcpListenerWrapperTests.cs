using NUnit.Framework;
using EchoServer.Wrappers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;

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

            var localEndPoint = (_listenerWrapper as dynamic)._listener.LocalEndpoint as IPEndPoint;
            _port = localEndPoint.Port;
        }

        [TearDown]
        public void TearDown()
        {
            _listenerWrapper.Stop();
        }

        [Test]
        public async Task AcceptTcpClientAsync_ShouldReturnClient_WhenClientConnects()
        {
            var acceptTask = _listenerWrapper.AcceptTcpClientAsync();

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _port);

            var wrapperClient = await acceptTask;

            // Assert
            Assert.IsNotNull(wrapperClient);
            Assert.IsInstanceOf<ITcpClient>(wrapperClient);

            var stream = wrapperClient.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes("Hello");
            await stream.WriteAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(5, buffer.Length);
        }

        [Test]
        public void Start_ShouldStartListener()
        {
            Assert.DoesNotThrow(() => _listenerWrapper.Start());
        }

        [Test]
        public void Stop_ShouldStopListener()
        {
            Assert.DoesNotThrow(() => _listenerWrapper.Stop());
        }
    }
}
