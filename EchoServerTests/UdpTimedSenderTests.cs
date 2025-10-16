using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EchoServer;
using NUnit.Framework;

namespace EchoServerTests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private const int ReceiveTimeoutMs = 2000;
        private UdpClient? _listener;
        private int _port;
        private UdpTimedSender? _sender;

        [SetUp]
        public void SetUp()
        {
            _listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); // ephemeral port
            _port = ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _sender?.StopSending();
                _sender?.Dispose();
            }
            catch { }

            try
            {
                _listener?.Close();
                _listener?.Dispose();
            }
            catch { }
        }

        private static async Task<UdpReceiveResult?> ReceiveWithTimeoutAsync(UdpClient listener, int timeoutMs)
        {
            var receiveTask = listener.ReceiveAsync();
            var delayTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false);
            return completed == receiveTask ? receiveTask.Result : null;
        }

        [Test]
        public async Task StartSending_SendsUdpMessage_WithExpectedFormat()
        {
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(50);

                var received = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(received, Is.Not.Null, "No UDP message received within timeout.");

                var data = received!.Value.Buffer;
                Assert.Multiple(() =>
                {
                    Assert.That(data, Has.Length.GreaterThanOrEqualTo(4), "Received data too short.");
                    Assert.That(data[0], Is.EqualTo(0x04), "First header byte mismatch.");
                    Assert.That(data[1], Is.EqualTo(0x84), "Second header byte mismatch.");
                    ushort seq = BitConverter.ToUInt16(data, 2);
                    Assert.That(seq, Is.EqualTo((ushort)1), "Sequence number of first message should be 1.");
                });
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }

        [Test]
        public void StartSending_Throws_WhenAlreadyRunning()
        {
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(100);
                var ex = Assert.Throws<InvalidOperationException>(() => _sender!.StartSending(100));
                Assert.That(ex!.Message, Does.Contain("already running").IgnoreCase);
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }

        [Test]
        public async Task StopSending_StopsFurtherMessages()
        {
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(50);
                var first = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(first, Is.Not.Null, "Expected to receive at least one message after start.");

                _sender.StopSending();
                var second = await ReceiveWithTimeoutAsync(_listener!, 500);
                Assert.That(second, Is.Null, "No further messages expected after StopSending.");
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }

        [Test]
        public async Task Dispose_StopsAndDisposesResources_NoExceptions()
        {
            _sender = new UdpTimedSender("127.0.0.1", _port);

            _sender.StartSending(50);
            var received = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
            Assert.That(received, Is.Not.Null, "Expected message before dispose.");

            Assert.DoesNotThrow(() => _sender!.Dispose());

            var afterDispose = await ReceiveWithTimeoutAsync(_listener!, 300);
            Assert.That(afterDispose, Is.Null, "No messages expected after Dispose.");
            _sender = null;
        }

        [Test]
        public async Task Messages_Sequence_IncrementsAcrossSends()
        {
            _sender = new UdpTimedSender("127.0.0.1", _port);

            try
            {
                _sender.StartSending(50);

                var first = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(first, Is.Not.Null, "First message not received.");
                var firstSeq = BitConverter.ToUInt16(first!.Value.Buffer, 2);

                var second = await ReceiveWithTimeoutAsync(_listener!, ReceiveTimeoutMs);
                Assert.That(second, Is.Not.Null, "Second message not received.");
                var secondSeq = BitConverter.ToUInt16(second!.Value.Buffer, 2);

                Assert.That(secondSeq, Is.EqualTo(firstSeq + 1), "Sequence should increment by 1.");
            }
            finally
            {
                _sender?.StopSending();
                _sender?.Dispose();
                _sender = null;
            }
        }

        [Test]
        public async Task SendMessageCallback_ExceptionIsHandled_WritesErrorToConsole()
        {
            // Arrange: invalid host to force IPAddress.Parse or Send to fail inside the callback
            _sender = new UdpTimedSender("not-an-ip", _port);

            var originalOut = Console.Out;
            try
            {
                using var sw = new StringWriter();
                Console.SetOut(sw);

                // Act: start sender - timer uses dueTime=0, so callback runs immediately (or very soon)
                _sender.StartSending(50);

                // give callback some time to run and handle exception
                await Task.Delay(300).ConfigureAwait(false);

                // capture console output
                var output = sw.ToString();

                // Assert: catch block wrote error message to console
                Assert.That(output, Does.Contain("Error sending message").IgnoreCase,
                    "Expected catch block to write 'Error sending message' to Console output when exception occurs.");
            }
            finally
            {
                // cleanup
                try { _sender?.StopSending(); } catch { }
                try { _sender?.Dispose(); } catch { }
                _sender = null;
                Console.SetOut(originalOut);
            }
        }
    }
}