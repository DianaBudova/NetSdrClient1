using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public class UdpClientWrapper : IUdpClient, IDisposable, IAsyncDisposable
    {
        private readonly IPEndPoint _localEndPoint;
        private CancellationTokenSource? _cts;
        private UdpClient? _udpClient;
        private bool _disposed;
        private readonly object _sync = new();
        public event EventHandler<byte[]>? MessageReceived;
        public UdpClientWrapper(int port)
        {
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
        }
        public async Task StartListeningAsync()
        {
            ThrowIfDisposed();
            lock (_sync)
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _udpClient?.Dispose();
                _udpClient = new UdpClient(_localEndPoint);
            }
            Console.WriteLine("Start listening for UDP messages...");
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient!.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                    MessageReceived?.Invoke(this, result.Buffer);
                    Console.WriteLine($"Received from {result.RemoteEndPoint}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Receive loop canceled.");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("UdpClient was disposed during receive.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
        }
        public void StopListening()
        {
            try
            {
                StopInternal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StopListening: {ex.Message}");
            }
        }
        public void Exit()
        {
            try
            {
                StopInternal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Exit: {ex.Message}");
            }
        }
        public override int GetHashCode()
        {
            var payload = $"{nameof(UdpClientWrapper)}|{_localEndPoint.Address}|{_localEndPoint.Port}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToInt32(hash, 0);
        }
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not UdpClientWrapper other) return false;
            return _localEndPoint.Address.Equals(other._localEndPoint.Address)
                   && _localEndPoint.Port == other._localEndPoint.Port;
        }
        protected virtual void StopInternal()
        {
            lock (_sync)
            {
                try
                {
                    _cts?.Cancel();
                    try { _udpClient?.Close(); } catch { /* swallow */ }
                    Console.WriteLine("Stopped listening for UDP messages.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while stopping: {ex.Message}");
                }
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try
                {
                    StopInternal();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while stopping: {ex.Message}");
                }

                lock (_sync)
                {
                    try
                    {
                        _udpClient?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while disposing _udpClient: {ex.Message}");
                    }
                    try
                    {
                        _cts?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while disposing _cts: {ex.Message}");
                    }
                    _udpClient = null;
                    _cts = null;
                }
            }
            _disposed = true;
        }
        ~UdpClientWrapper()
        {
            Dispose(disposing: false);
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                await DisposeAsyncCore().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DisposeAsyncCore: {ex.Message}");
            }

            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual ValueTask DisposeAsyncCore()
        {
            return ValueTask.CompletedTask;
        }
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(UdpClientWrapper));
        }
    }
}
