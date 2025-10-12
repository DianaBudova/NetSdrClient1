using NUnit.Framework;
using Moq;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Timers;
using EchoServer;
using EchoServer.Abstractions;

namespace EchoServerTests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<ITcpListener> _mockListener;
        private Mock<ILogger> _mockLogger;
        private EchoServer.EchoServer _server;

        [SetUp]
        public void Setup()
        {
            _mockListener = new Mock<ITcpListener>();
            _mockLogger = new Mock<ILogger>();
            _server = new EchoServer.EchoServer(_mockListener.Object, _mockLogger.Object);
        }

        [Test]
        public async Task StartAsync_ShouldStartListenerAndAcceptClients()
        {
            // Arrange
            var mockClient = new Mock<ITcpClient>();
            var mockStream = new Mock<INetworkStream>();

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            _mockListener.SetupSequence(l => l.AcceptTcpClientAsync())
                         .ReturnsAsync(mockClient.Object)
                         .ThrowsAsync(new OperationCanceledException());

            // Act
            await _server.StartAsync();

            // Assert
            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockListener.Verify(l => l.AcceptTcpClientAsync(), Times.Exactly(2));
            _mockLogger.Verify(log => log.Log("Server started."), Times.Once);
        }

        //[Test]
        //public async Task HandleClientAsync_ShouldEchoReceivedData()
        //{
        //    // Arrange
        //    var mockClient = new Mock<ITcpClient>();
        //    var mockStream = new Mock<INetworkStream>();
        //    var message = "Hello, World!";
        //    var messageBytes = Encoding.UTF8.GetBytes(message);
        //    var buffer = new byte[1024];

        //    mockStream.SetupSequence(s => s.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
        //        .Returns((byte[] b, int o, int s, CancellationToken t) =>
        //        {
        //            messageBytes.CopyTo(b, o);
        //            return Task.FromResult(messageBytes.Length);
        //        })
        //        .ReturnsAsync(0);

        //    mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

        //    // Act
        //    await _server.HandleClientAsync(mockClient.Object, CancellationToken.None);

        //    // Assert
        //    mockStream.Verify(s => s.WriteAsync(
        //        It.Is<byte[]>(b => Encoding.UTF8.GetString(b, 0, message.Length) == message),
        //        0,
        //        messageBytes.Length,
        //        It.IsAny<CancellationToken>()), Times.Once);

        //    mockClient.Verify(c => c.Close(), Times.Once);  
        //    _mockLogger.Verify(log => log.Log($"Echoed {messageBytes.Length} bytes to the client."), Times.Once);
        //}

        [Test]
        public void Stop_ShouldStopListenerAndCancelToken()
        {
            // Act
            _server.Stop();

            // Assert
            _mockListener.Verify(l => l.Stop(), Times.Once);
            _mockLogger.Verify(log => log.Log("Server stopped."), Times.Once);
        }
    }
}
