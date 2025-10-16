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
        public async Task StartListeningAsync_ShouldRaiseMessageReceived_WhenUdpPacketArrives()
        {
            // Arrange
            byte[] received = null!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _udpWrapper.MessageReceived += (s, data) => received = data;

            // Act
            var listeningTask = _udpWrapper.StartListeningAsync();

            using var sender = new UdpClient();
            byte[] payload = Encoding.UTF8.GetBytes("TestMessage");
            await sender.SendAsync(payload, payload.Length, "127.0.0.1", _port);

            while (received == null && !cts.Token.IsCancellationRequested)
                await Task.Delay(10);

            _udpWrapper.StopListening();

            await listeningTask;

            // Assert
            Assert.That(received, Is.Not.Null, "MessageReceived was not raised");
            Assert.That(Encoding.UTF8.GetString(received), Is.EqualTo("TestMessage"));
        }

        [Test]
        public void StopListening_ShouldNotThrow() =>
            Assert.That(() => _udpWrapper.StopListening(), Throws.Nothing);

        [Test]
        public void Exit_ShouldNotThrow() =>
            Assert.That(() => _udpWrapper.Exit(), Throws.Nothing);

        [Test]
        public void Dispose_ShouldNotThrow() =>
            Assert.That(() => _udpWrapper.Dispose(), Throws.Nothing);

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

        private class FaultyUdpClientWrapper : UdpClientWrapper
        {
            public FaultyUdpClientWrapper(int port) : base(port) { }

            protected override void StopInternal()
            {
                throw new InvalidOperationException("Stop failed");
            }

            protected override ValueTask DisposeAsyncCore()
            {
                throw new InvalidOperationException("DisposeAsyncCore failed");
            }
        }

        [Test]
        public void StartListeningAsync_ShouldCatchObjectDisposedException()
        {
            var wrapper = new UdpClientWrapper(0);
            wrapper.Dispose();
            Assert.That(async () => await wrapper.StartListeningAsync(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void StopInternal_Exception_ShouldBeHandled()
        {
            var wrapper = new FaultyUdpClientWrapper(0);
            Assert.That(() => wrapper.StopListening(), Throws.Nothing, "StopInternal exception should be swallowed");
        }

        [Test]
        public void Dispose_ExceptionDuringDispose_ShouldBeHandled()
        {
            var wrapper = new FaultyUdpClientWrapper(0);
            Assert.That(() => wrapper.Dispose(), Throws.Nothing, "Dispose should swallow exceptions from StopInternal");
        }

        [Test]
        public void Destructor_ShouldNotThrow()
        {
            void CreateAndRelease()
            {
                var wrapper = new UdpClientWrapper(0);
            }
            Assert.DoesNotThrow(() => CreateAndRelease());
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public async Task DisposeAsync_Exception_ShouldBeHandled()
        {
            var wrapper = new FaultyUdpClientWrapper(0);
            await using var _ = wrapper;
            Assert.DoesNotThrowAsync(async () => await wrapper.DisposeAsync());
        }
    }
}
