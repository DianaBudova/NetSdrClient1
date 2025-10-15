using NUnit.Framework;
using EchoServer.Wrappers;
using EchoServer.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServerTests
{
    [TestFixture]
    public class NetworkStreamWrapperTests
    {
        private TcpListener _listener = null!;
        private TcpClient _client = null!;
        private NetworkStreamWrapper _clientStreamWrapper = null!;
        private int _port;

        [SetUp]
        public async Task Setup()
        {
            // Створюємо локальний TCP сервер на довільному порту
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            // Підключаємо клієнт
            _client = new TcpClient();
            var acceptTask = _listener.AcceptTcpClientAsync();
            await _client.ConnectAsync(IPAddress.Loopback, _port);

            var serverClient = await acceptTask;

            // Створюємо NetworkStreamWrapper для клієнта
            _clientStreamWrapper = new NetworkStreamWrapper(_client.GetStream());

            // Зберігаємо серверний стрім для читання у тестах
            _serverStream = serverClient.GetStream();
        }

        private NetworkStream _serverStream = null!;

        [TearDown]
        public void TearDown()
        {
            _clientStreamWrapper.Dispose();
            _serverStream.Dispose();
            _client.Dispose();
            _listener.Stop();
        }

        [Test]
        public async Task WriteAsync_ShouldSendDataToServer()
        {
            // Arrange
            byte[] buffer = Encoding.UTF8.GetBytes("Hello");

            // Act
            await _clientStreamWrapper.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None);

            // Read на стороні сервера
            byte[] serverBuffer = new byte[buffer.Length];
            int read = await _serverStream.ReadAsync(serverBuffer, 0, serverBuffer.Length, CancellationToken.None);

            // Assert
            Assert.That(read, Is.EqualTo(buffer.Length));
            Assert.That(Encoding.UTF8.GetString(serverBuffer), Is.EqualTo("Hello"));
        }

        [Test]
        public async Task ReadAsync_ShouldReceiveDataFromServer()
        {
            // Arrange
            byte[] bufferToServer = Encoding.UTF8.GetBytes("World");
            await _serverStream.WriteAsync(bufferToServer, 0, bufferToServer.Length);

            byte[] clientBuffer = new byte[bufferToServer.Length];

            // Act
            int read = await _clientStreamWrapper.ReadAsync(clientBuffer, 0, clientBuffer.Length, CancellationToken.None);

            // Assert
            Assert.That(read, Is.EqualTo(bufferToServer.Length));
            Assert.That(Encoding.UTF8.GetString(clientBuffer), Is.EqualTo("World"));
        }

        [Test]
        public void Dispose_ShouldNotThrow()
        {
            Assert.That(() => _clientStreamWrapper.Dispose(), Throws.Nothing);
        }
    }
}
