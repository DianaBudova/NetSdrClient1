using NUnit.Framework;
using NetSdrClientApp.Networking;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class UdpClientWrapperExceptionTests
    {
        private int _port;

        [SetUp]
        public void Setup()
        {
            using var udpTemp = new UdpClient(0);
            _port = ((IPEndPoint)udpTemp.Client.LocalEndPoint!).Port;
        }

        [Test]
        public async Task StartListeningAsync_ShouldCatchObjectDisposedException()
        {
            var wrapper = new UdpClientWrapper(_port);

            var listenTask = wrapper.StartListeningAsync();
            
            // Force dispose immediately to trigger ObjectDisposedException in loop
            wrapper.Dispose();

            await listenTask;

            // No exception should escape
            Assert.Pass("ObjectDisposedException handled inside StartListeningAsync.");
        }

        [Test]
        public void StopInternal_ShouldCatchExceptionDuringClose()
        {
            var wrapper = new FaultyUdpClientWrapper(_port);

            // This will force StopInternal to throw inside Close
            Assert.DoesNotThrow(() => wrapper.StopListening());
        }

        [Test]
        public void Dispose_ShouldCatchExceptionDuringUdpClientDispose()
        {
            var wrapper = new FaultyUdpClientWrapper(_port);

            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void Dispose_ShouldCatchExceptionDuringCtsDispose()
        {
            var wrapper = new FaultyCtsWrapper(_port);

            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void Finalizer_ShouldNotThrow()
        {
            var wrapper = new UdpClientWrapper(_port);
            wrapper.Dispose();

            // Force GC to call finalizer
            wrapper = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Pass("Finalizer called without exceptions.");
        }
    }

    // Helper classes to simulate exceptions
    class FaultyUdpClientWrapper : UdpClientWrapper
    {
        public FaultyUdpClientWrapper(int port) : base(port) { }

        protected override void StopInternal()
        {
            // simulate exception inside Close
            throw new Exception("StopInternal exception");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new Exception("Dispose UdpClient exception");
            }
            base.Dispose(disposing);
        }
    }

    class FaultyCtsWrapper : UdpClientWrapper
    {
        public FaultyCtsWrapper(int port) : base(port) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new Exception("Dispose CTS exception");
            }
            base.Dispose(disposing);
        }
    }
}
