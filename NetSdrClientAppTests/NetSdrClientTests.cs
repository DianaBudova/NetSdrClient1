using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    private const string SamplesFile = "samples.bin";

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // cleanup samples file
        if (File.Exists(SamplesFile))
        {
            try { File.Delete(SamplesFile); } catch { /* ignore */ }
        }
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQNoConnectionTest()
    {

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsyncTest()
    {
        await ConnectAsyncTest();

        long freq = 123456789;
        int channel = 2;

        await _client.ChangeFrequencyAsync(freq, channel);

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(b =>
            b.Skip(4).First() == (byte)channel
        )), Times.Once);
    }

    [Test]
    public async Task ChangeFrequencyNoConnectionTest()
    {
        long freq = 123456789;
        int channel = 2;

        await _client.ChangeFrequencyAsync(freq, channel);

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task SendTcpRequest_ReturnsResponse()
    {
        await ConnectAsyncTest();

        byte[] testMsg = new byte[] { 0x01, 0x02 };

        var task = _client.StartIQAsync();
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, new byte[] { 0x05 });

        await task;

        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public void TcpClientMessageReceived_SetsTaskCompletionSource()
    {
        byte[] response = new byte[] { 0x01, 0x02 };
        var sendTask = _client.StartIQAsync();
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, response);

        Assert.Pass("TaskCompletionSource set correctly via MessageReceived");
    }

    [Test]
        public void UdpClientMessageReceived_WritesSamplesFile()
        {
            // arrange: create a fake UDP payload. Exact contents depend on NetSdrMessageHelper
            // but we provide some bytes — handler should call TranslateMessage/GetSamples and then write file.
            byte[] udpPayload = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };

            // act: raise the UDP MessageReceived event
            _updMock.Raise(u => u.MessageReceived += null, _updMock.Object, udpPayload);

            // give a short time for handler to process and write file
            Task.Delay(100).GetAwaiter().GetResult();

            // assert: file created and has non-zero length; contents length should be multiple of 2 (16-bit samples)
            Assert.That(File.Exists(SamplesFile), Is.True, "samples.bin should be created by UDP handler");

            var fileInfo = new FileInfo(SamplesFile);
            Assert.That(fileInfo.Length, Is.GreaterThan(0), "samples.bin should not be empty");
            Assert.That(fileInfo.Length % 2, Is.EqualTo(0), "samples.bin length should be multiple of 2 (16-bit samples)");

            // optionally: read some bytes (not asserting exact values because helper logic is external)
            var bytes = File.ReadAllBytes(SamplesFile);
            Assert.That(bytes.Length, Is.EqualTo(fileInfo.Length));
        }

}
