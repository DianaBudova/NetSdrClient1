using NUnit.Framework;
using EchoServer.Wrappers;
using EchoServer.Abstractions;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServerTests
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        private TcpClient _tcpClient = null!;
        private TcpClientWrapper _wrapper = null!;

        [SetUp]
        public void Setup()
        {
            _tcpClient = new TcpClient();
            _wrapper = new TcpClientWrapper(_tcpClient);
        }

        [TearDown]
        public void TearDown()
        {
            _wrapper.Dispose();
        }

        [Test]
        public async Task GetStream_ShouldReturnNetworkStreamWrapper()
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();

            await _tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, port);

            using var serverClient = await acceptTask;

            var stream = _wrapper.GetStream();

            Assert.That(stream, Is.Not.Null);
            Assert.That(stream, Is.InstanceOf<INetworkStream>());
        }


        [Test]
        public void Close_ShouldNotThrowException()
        {
            Assert.That(() => _wrapper.Close(), Throws.Nothing);
        }

        [Test]
        public void Dispose_ShouldNotThrowException()
        {
            Assert.That(() => _wrapper.Dispose(), Throws.Nothing);
        }

        [Test]
        public async Task Stream_ShouldWriteAndReadData()
        {
            // Arrange
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();

            await _tcpClient.ConnectAsync(System.Net.IPAddress.Loopback, port);

            using var serverClient = await acceptTask;

            var clientStream = _wrapper.GetStream();
            var serverStream = new NetworkStreamWrapper(serverClient.GetStream());

            // Act
            byte[] buffer = Encoding.UTF8.GetBytes("Hello");
            await clientStream.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);

            byte[] serverBuffer = new byte[buffer.Length];
            int read = await serverStream.ReadAsync(serverBuffer, 0, serverBuffer.Length, CancellationToken.None);

            // Assert
            Assert.That(read, Is.EqualTo(buffer.Length));
            Assert.That(Encoding.UTF8.GetString(serverBuffer), Is.EqualTo("Hello"));
        }
    }
}
