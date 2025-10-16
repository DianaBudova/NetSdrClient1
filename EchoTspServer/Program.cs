using EchoServer.Wrappers;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    // --- Interfaces for testability ---
    public interface IConsoleWrapper
    {
        ConsoleKeyInfo ReadKey(bool intercept);
        void WriteLine(string message);
    }

    public interface ILogger
    {
        void Log(string message);
    }

    // Factories return the concrete types used by the existing code,
    // keeping compatibility with the rest of the app.
    public interface ITcpListenerWrapperFactory
    {
        TcpListenerWrapper Create(IPAddress address, int port);
    }

    public interface IUdpTimedSenderFactory
    {
        UdpTimedSender Create(string host, int port);
    }

    // --- Real/default implementations (used by production Main) ---
    public class RealConsoleWrapper : IConsoleWrapper
    {
        public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
        public void WriteLine(string message) => Console.WriteLine(message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine(message);
    }

    public class RealTcpListenerWrapperFactory : ITcpListenerWrapperFactory
    {
        public TcpListenerWrapper Create(IPAddress address, int port) => new TcpListenerWrapper(address, port);
    }

    public class RealUdpTimedSenderFactory : IUdpTimedSenderFactory
    {
        public UdpTimedSender Create(string host, int port) => new UdpTimedSender(host, port);
    }

    // --- Program: refactored single-file entrypoint + RunAsync for testing ---
    public static class Program
    {
        // Testable entrypoint: accepts dependencies and a cancellation token.
        // You can call RunAsync from tests with fake implementations.
        public static async Task RunAsync(
            IConsoleWrapper console,
            ILogger logger,
            ITcpListenerWrapperFactory listenerFactory,
            IUdpTimedSenderFactory senderFactory,
            int serverPort,
            string host,
            int udpPort,
            int intervalMilliseconds,
            CancellationToken cancellationToken)
        {
            var listener = listenerFactory.Create(IPAddress.Any, serverPort);
            var server = new EchoServer(listener, logger);

            // start server in background
            var serverTask = Task.Run(() => server.StartAsync(), cancellationToken);

            using (var sender = senderFactory.Create(host, udpPort))
            {
                logger.Log("Press 'q' to stop the server and sender...");
                sender.StartSending(intervalMilliseconds);

                // loop until 'q' pressed or cancellation requested
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Blocking call replaced by console wrapper for testability
                    var keyInfo = console.ReadKey(intercept: true);
                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                    // small yield to avoid busy-loop if console wrapper returns frequently
                    await Task.Yield();
                }

                // cleanup
                sender.StopSending();
                server.Stop();
                logger.Log("Sender and server stopped.");
            }

            // try waiting for server to stop gracefully (if StartAsync returns)
            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* ignore */ }
            catch (Exception ex)
            {
                // Do not throw from main run path — log and continue
                logger.Log($"Server task finished with error: {ex.Message}");
            }
        }

        // Keep Main thin: construct real implementations and call RunAsync.
        public static Task Main(string[] args)
        {
            int serverPort = 5000;
            var logger = new ConsoleLogger();
            var console = new RealConsoleWrapper();
            var listenerFactory = new RealTcpListenerWrapperFactory();
            var senderFactory = new RealUdpTimedSenderFactory();

            string host = "127.0.0.1";
            int udpPort = 60000;
            int intervalMilliseconds = 5000;

            // Create a cancellation token that can be used to stop RunAsync from tests or other code.
            var cts = new CancellationTokenSource();

            // Note: we return the task — app will run until RunAsync completes.
            return RunAsync(
                console,
                logger,
                listenerFactory,
                senderFactory,
                serverPort,
                host,
                udpPort,
                intervalMilliseconds,
                cts.Token);
        }
    }
}
