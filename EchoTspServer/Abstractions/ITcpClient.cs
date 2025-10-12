using System;

namespace EchoTspServer.Abstractions
{
    public interface ITcpClient : IDisposable
    {
        INetworkStream GetStream();
        void Close();
    }
}
