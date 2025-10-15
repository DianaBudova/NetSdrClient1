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
        private UdpClient _udpTemp = null!; // Disposable ресурс для визначення порту

        [SetUp]
        public void Setup()
        {
            _udpTemp = new UdpClient(0);
            _port = ((IPEndPoint)_udpTemp.Client.LocalEndPoint!).Port;

            _udpWrapper = new UdpClientWrapper(_port);
        }

        [TearDown]
        public void TearDown()
        {
            _udpWrapper.Dispose();
            _udpTemp.Dispose(); // <-- Dispose в TearDown
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
    }
}
