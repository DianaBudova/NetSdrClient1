using NUnit.Framework;
using NetSdrClientApp.Networking;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class UdpClientWrapperTests
    {
        private UdpClientWrapper _udpWrapper = null!;
        private int _port;

        [SetUp]
        public void Setup()
        {
            using var udpTemp = new UdpClient(0);
            _port = ((IPEndPoint)udpTemp.Client.LocalEndPoint!).Port;

            _udpWrapper = new UdpClientWrapper(_port);
        }

        [TearDown]
        public void TearDown()
        {
            _udpWrapper.Dispose();
        }

        [Test]
        public void Equals_ShouldReturnTrue_ForSameEndPoint()
        {
            var other = new UdpClientWrapper(_port);
            Assert.That(_udpWrapper.Equals(other), Is.True);
        }

        [Test]
        public void GetHashCode_ShouldReturnInt()
        {
            int hash = _udpWrapper.GetHashCode();
            Assert.That(hash, Is.TypeOf<int>());
        }

        [Test]
        public async Task StartListeningAsync_ShouldReceiveMessage()
        {
            // Arrange
            byte[] receivedData = null!;
            var tcs = new TaskCompletionSource<bool>();

            _udpWrapper.MessageReceived += (s, data) =>
            {
                receivedData = data;
                tcs.TrySetResult(true);
            };

            var listeningTask = _udpWrapper.StartListeningAsync();

            using var udpClient = new UdpClient();
            byte[] sendData = Encoding.UTF8.GetBytes("TestMessage");
            await udpClient.SendAsync(sendData, sendData.Length, new IPEndPoint(IPAddress.Loopback, _port));

            // Wait until message is received or timeout
            var cts = new CancellationTokenSource(2000);
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }

            Assert.That(receivedData, Is.Not.Null);
            Assert.That(Encoding.UTF8.GetString(receivedData), Is.EqualTo("TestMessage"));

            // Stop listening
            _udpWrapper.StopListening();
            await Task.Delay(100);
        }

        [Test]
        public void StopListening_ShouldNotThrow()
        {
            Assert.That(() => _udpWrapper.StopListening(), Throws.Nothing);
        }

        [Test]
        public void Exit_ShouldNotThrow()
        {
            Assert.That(() => _udpWrapper.Exit(), Throws.Nothing);
        }

        [Test]
        public void Dispose_ShouldNotThrow()
        {
            Assert.That(() => _udpWrapper.Dispose(), Throws.Nothing);
        }

        [Test]
        public async Task DisposeAsync_ShouldNotThrow()
        {
            await using var wrapperAsync = new UdpClientWrapper(_port);
            Assert.That(async () => await wrapperAsync.DisposeAsync(), Throws.Nothing);
        }

        [Test]
        public void ThrowIfDisposed_ShouldThrowAfterDispose()
        {
            _udpWrapper.Dispose();
            Assert.That(() => _udpWrapper.GetHashCode(), Throws.Nothing);
            Assert.That(() => _udpWrapper.StartListeningAsync(), Throws.TypeOf<ObjectDisposedException>());
        }
    }
}
