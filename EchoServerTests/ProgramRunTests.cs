using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EchoServer;
using EchoServer.Wrappers;
using NUnit.Framework;

namespace EchoServerTests
{
    [TestFixture]
    public class ProgramRunTests
    {
        // --- Fake implementations used in tests ---

        // Fake console that returns a sequence of keys.
        private class FakeConsoleWrapper : IConsoleWrapper
        {
            private readonly Queue<ConsoleKeyInfo> _keys;

            public FakeConsoleWrapper(IEnumerable<ConsoleKeyInfo> keys)
            {
                _keys = new Queue<ConsoleKeyInfo>(keys);
            }

            public ConsoleKeyInfo ReadKey(bool intercept)
            {
                // If no key available, block briefly to emulate waiting for input.
                if (_keys.Count == 0)
                {
                    // Sleep a small amount so RunAsync doesn't busy-loop in tests.
                    Thread.Sleep(10);
                    return new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false);
                }

                return _keys.Dequeue();
            }

            public void WriteLine(string message) { /* noop for tests */ }
        }

        // Fake logger that stores messages
        private class FakeLogger : ILogger
        {
            public readonly List<string> Messages = new List<string>();
            public void Log(string message) => Messages.Add(message);
        }

        // Fake TcpListenerWrapper: override StartAsync/Stop to just set flags.
        private class FakeTcpListenerWrapper : TcpListenerWrapper
        {
            public bool Started { get; private set; } = false;
            public bool Stopped { get; private set; } = false;

            public FakeTcpListenerWrapper(IPAddress addr, int port) : base(addr, port) { }

            // Assumes base has virtual StartAsync/Stop; if not virtual - tests must adapt.
            public override Task StartAsync()
            {
                Started = true;
                return Task.CompletedTask;
            }

            public override void Stop()
            {
                Stopped = true;
            }
        }

        // Fake factory for TcpListenerWrapper
        private class FakeTcpListenerFactory : ITcpListenerWrapperFactory
        {
            private readonly FakeTcpListenerWrapper _instance;
            public FakeTcpListenerFactory(FakeTcpListenerWrapper instance) => _instance = instance;
            public TcpListenerWrapper Create(IPAddress address, int port) => _instance;
        }

        // Fake UdpTimedSender: track Start/Stop and disposed
        private class FakeUdpTimedSender : UdpTimedSender
        {
            public bool Started { get; private set; } = false;
            public bool Stopped { get; private set; } = false;
            public bool Disposed { get; private set; } = false;

            public FakeUdpTimedSender(string host, int port) : base(host, port) { }

            public override void StartSending(int intervalMilliseconds)
            {
                Started = true;
            }

            public override void StopSending()
            {
                Stopped = true;
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        // Fake factory for UdpTimedSender
        private class FakeUdpTimedSenderFactory : IUdpTimedSenderFactory
        {
            private readonly FakeUdpTimedSender _instance;
            public FakeUdpTimedSenderFactory(FakeUdpTimedSender instance) => _instance = instance;
            public UdpTimedSender Create(string host, int port) => _instance;
        }

        // ---------- Tests ----------

        [Test]
        public async Task RunAsync_WhenConsoleReturnsQImmediately_StartsAndStopsSenderAndServer()
        {
            // Arrange
            var fakeLogger = new FakeLogger();
            var fakeConsole = new FakeConsoleWrapper(new[] { new ConsoleKeyInfo('\0', ConsoleKey.Q, false, false, false) });

            var fakeListener = new FakeTcpListenerWrapper(IPAddress.Loopback, 0);
            var listenerFactory = new FakeTcpListenerFactory(fakeListener);

            var fakeSender = new FakeUdpTimedSender("127.0.0.1", 0);
            var senderFactory = new FakeUdpTimedSenderFactory(fakeSender);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            var runTask = Program.RunAsync(
                console: fakeConsole,
                logger: fakeLogger,
                listenerFactory: listenerFactory,
                senderFactory: senderFactory,
                serverPort: 5000,
                host: "127.0.0.1",
                udpPort: 60000,
                intervalMilliseconds: 1000,
                cancellationToken: cts.Token);

            // Wait for completion (should finish quickly because console immediately returns Q)
            await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);

            // Assert
            Assert.That(runTask.IsCompleted, Is.True, "RunAsync should complete when console returned Q immediately");
            Assert.That(fakeSender.Started, Is.True, "Sender should be started");
            Assert.That(fakeSender.Stopped, Is.True, "Sender should be stopped");
            Assert.That(fakeListener.Started, Is.True, "Server listener should be started");
            Assert.That(fakeListener.Stopped, Is.True, "Server listener should be stopped");
            Assert.That(fakeLogger.Messages, Has.Some.Contains("Press 'q' to stop the server and sender..."));
            Assert.That(fakeLogger.Messages, Has.Some.Contains("Sender and server stopped."));
        }

        [Test]
        public async Task RunAsync_WhenConsoleReturnsNonQThenQ_LoopsUntilQAndThenStops()
        {
            // Arrange: first a NoName (no input), then a Q
            var fakeConsole = new FakeConsoleWrapper(new[]
            {
                new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false),
                new ConsoleKeyInfo('\0', ConsoleKey.Q, false, false, false)
            });

            var fakeLogger = new FakeLogger();
            var fakeListener = new FakeTcpListenerWrapper(IPAddress.Loopback, 0);
            var listenerFactory = new FakeTcpListenerFactory(fakeListener);
            var fakeSender = new FakeUdpTimedSender("127.0.0.1", 0);
            var senderFactory = new FakeUdpTimedSenderFactory(fakeSender);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            var runTask = Program.RunAsync(
                console: fakeConsole,
                logger: fakeLogger,
                listenerFactory: listenerFactory,
                senderFactory: senderFactory,
                serverPort: 5000,
                host: "127.0.0.1",
                udpPort: 60000,
                intervalMilliseconds: 100,
                cancellationToken: cts.Token);

            // Wait for completion
            await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);

            // Assert
            Assert.That(runTask.IsCompleted, Is.True, "RunAsync should eventually complete after console returns Q");
            Assert.That(fakeSender.Started, Is.True, "Sender should be started");
            Assert.That(fakeSender.Stopped, Is.True, "Sender should be stopped");
            Assert.That(fakeListener.Started, Is.True);
            Assert.That(fakeListener.Stopped, Is.True);
            Assert.That(fakeLogger.Messages, Has.Some.Contains("Press 'q' to stop the server and sender..."));
            Assert.That(fakeLogger.Messages, Has.Some.Contains("Sender and server stopped."));
        }
    }
}
