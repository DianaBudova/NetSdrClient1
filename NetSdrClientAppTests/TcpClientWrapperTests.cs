using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        private TcpListener? _server;
        private int _port;
        private CancellationTokenSource? _serverCts;

        [SetUp]
        public void SetUp()
        {
            // Start listener on ephemeral port
            _server = new TcpListener(IPAddress.Loopback, 0);
            _server.Start();
            _port = ((IPEndPoint)_server.LocalEndpoint).Port;
            _serverCts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Cancel any running server work
                if (_serverCts != null)
                {
                    try { _serverCts.Cancel(); } catch { /* ignore */ }
                    _serverCts.Dispose();
                    _serverCts = null;
                }

                if (_server != null)
                {
                    try
                    {
                        // Stop listener if running
                        _server.Stop();
                    }
                    catch { /* ignore */ }

                    try
                    {
                        // If TcpListener exposes Dispose on the target framework, call it to satisfy analyzers.
                        // Use a cast to IDisposable to be safe across frameworks.
                        (_server as IDisposable)?.Dispose();
                    }
                    catch { /* ignore */ }

                    try
                    {
                        // Also dispose underlying socket to ensure native resources are released.
                        _server.Server.Dispose();
                    }
                    catch { /* ignore */ }

                    _server = null;
                }
            }
            catch
            {
                // ensure teardown never throws
            }
        }

        /// <summary>
        /// Connect -> server accepts -> Client reports Connected == true
        /// and Disconnect sets Connected == false.
        /// </summary>
        [Test]
        public async Task ConnectAndDisconnect_ReportsConnectedState()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);

            // Accept task on server side
            var acceptTcs = new TaskCompletionSource<TcpClient>();
            var acceptTask = Task.Run(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_serverCts!.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var tcp = await _server!.AcceptTcpClientAsync().ConfigureAwait(false);
                    acceptTcs.TrySetResult(tcp);
                }
                catch (Exception ex)
                {
                    acceptTcs.TrySetException(ex);
                }
            });

            client.Connect();

            // Wait for server accept
            var serverClient = await acceptTcs.Task.ConfigureAwait(false);
            Assert.That(client.Connected, Is.True, "client should be connected after Connect() and server accept");

            // Now disconnect and assert not connected
            client.Disconnect();
            Assert.That(client.Connected, Is.False, "client should not be connected after Disconnect()");
            serverClient.Close();
        }

        /// <summary>
        /// When server sends data, client invokes MessageReceived with correct payload.
        /// </summary>
        [Test]
        public async Task StartListening_MessageReceived_EventRaisedWithPayload()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);

            // Setup server accept and then write a message
            var serverSideTask = Task.Run(async () =>
            {
                var serverTcp = await _server!.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = serverTcp.GetStream();
                // Wait a moment to ensure client's listening loop is running
                await Task.Delay(50).ConfigureAwait(false);
                var message = Encoding.UTF8.GetBytes("hello-from-server");
                await stream.WriteAsync(message, 0, message.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                // Give the client a bit of time to process before closing
                await Task.Delay(50).ConfigureAwait(false);
                serverTcp.Close();
            });

            var receivedTcs = new TaskCompletionSource<byte[]>();
            client.MessageReceived += (s, data) =>
            {
                receivedTcs.TrySetResult(data);
            };

            client.Connect();

            var completed = await Task.WhenAny(receivedTcs.Task, Task.Delay(2000)).ConfigureAwait(false);
            Assert.That(receivedTcs.Task.IsCompleted, Is.True, "MessageReceived event should be triggered");

            var payload = receivedTcs.Task.Result;
            var text = Encoding.UTF8.GetString(payload);
            Assert.That(text, Is.EqualTo("hello-from-server"));

            // cleanup
            client.Disconnect();
            await serverSideTask.ConfigureAwait(false);
        }

        /// <summary>
        /// SendMessageAsync should throw InvalidOperationException if not connected.
        /// </summary>
        [Test]
        public void SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);
            // Using fluent/constraint-style assertion to check exception
            Assert.ThrowsAsync<InvalidOperationException>(async () => await client.SendMessageAsync("hi"));
        }

        /// <summary>
        /// When connected, SendMessageAsync sends bytes to the server.
        /// </summary>
        [Test]
        public async Task SendMessageAsync_WhenConnected_ServerReceivesData()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);

            // Server will accept and read what client sends
            var serverReadTcs = new TaskCompletionSource<byte[]>();
            var serverTask = Task.Run(async () =>
            {
                var serverTcp = await _server!.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = serverTcp.GetStream();
                var buffer = new byte[1024];
                var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                var received = new byte[read];
                Array.Copy(buffer, received, read);
                serverReadTcs.TrySetResult(received);
                serverTcp.Close();
            });

            client.Connect();

            // send a message
            var payload = Encoding.UTF8.GetBytes("client-to-server");
            await client.SendMessageAsync(payload).ConfigureAwait(false);

            var completed = await Task.WhenAny(serverReadTcs.Task, Task.Delay(2000)).ConfigureAwait(false);
            Assert.That(serverReadTcs.Task.IsCompleted, Is.True, "Server should receive the data sent by client");

            var received = serverReadTcs.Task.Result;
            var text = Encoding.UTF8.GetString(received);
            Assert.That(text, Is.EqualTo("client-to-server"));

            // cleanup
            client.Disconnect();
            await serverTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Connecting twice should not crash and second Connect should be no-op (Connected remains true).
        /// </summary>
        [Test]
        public async Task Connect_WhenAlreadyConnected_IsNoOp()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);

            var serverAcceptTask = Task.Run(async () =>
            {
                var serverTcp = await _server!.AcceptTcpClientAsync().ConfigureAwait(false);
                // keep connection open shortly
                await Task.Delay(200).ConfigureAwait(false);
                serverTcp.Close();
            });

            client.Connect();

            // Wait briefly to ensure connect completed
            await Task.Delay(100).ConfigureAwait(false);
            Assert.That(client.Connected, Is.True, "client should be connected after Connect");

            // calling Connect again should not throw and client stays connected
            Assert.DoesNotThrow(() => client.Connect());
            Assert.That(client.Connected, Is.True, "client should remain connected after second Connect call");

            client.Disconnect();
            await serverAcceptTask.ConfigureAwait(false);
        }

        /// <summary>
        /// When server closes connection, client eventually reports Connected == false after disconnect flow.
        /// </summary>
        [Test]
        public async Task ServerClosesConnection_ClientDetectsDisconnect()
        {
            var client = new TcpClientWrapper("127.0.0.1", _port);

            var serverTask = Task.Run(async () =>
            {
                var serverTcp = await _server!.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = serverTcp.GetStream();
                // send one message and then close from server side
                var msg = Encoding.UTF8.GetBytes("bye");
                await stream.WriteAsync(msg, 0, msg.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                // close server connection
                serverTcp.Close();
            });

            var receivedTcs = new TaskCompletionSource<bool>();
            client.MessageReceived += (s, data) =>
            {
                receivedTcs.TrySetResult(true);
            };

            client.Connect();
            // wait for message
            var r = await Task.WhenAny(receivedTcs.Task, Task.Delay(2000)).ConfigureAwait(false);
            Assert.That(receivedTcs.Task.IsCompleted, Is.True, "Should receive initial message from server");

            // wait a little to allow client to notice connection closed and exit listening loop
            await Task.Delay(200).ConfigureAwait(false);

            // The wrapper's Connected property will be false if underlying client was closed via Disconnect
            // In our wrapper the server-side close won't automatically set wrapper fields to null, but the stream ReadAsync will end.
            // At minimum ensure no exception thrown on calling Disconnect after server closed socket
            Assert.DoesNotThrow(() => client.Disconnect());
            Assert.That(client.Connected, Is.False);

            await serverTask.ConfigureAwait(false);
        }
    }
}
