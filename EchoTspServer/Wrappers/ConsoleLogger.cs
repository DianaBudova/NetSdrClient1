using EchoTspServer.Abstractions;
using System;

namespace EchoTspServer.Wrappers
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
