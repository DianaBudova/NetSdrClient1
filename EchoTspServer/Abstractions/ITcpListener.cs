using System.Threading.Tasks;

namespace EchoTspServer.Abstractions
{
    public interface ITcpListener
    {
        void Start();
        Task<ITcpClient> AcceptTcpClientAsync();
        void Stop();
    }
}
