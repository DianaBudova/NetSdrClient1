using System;
using System.IO;
using EchoServer.Wrappers;
using NUnit.Framework;

namespace EchoServerTests
{
    [TestFixture]
    public class ConsoleLoggerTests
    {
        private ConsoleLogger _logger;
        private StringWriter _stringWriter;
        private TextWriter _originalOut;

        [SetUp]
        public void SetUp()
        {
            _logger = new ConsoleLogger();
            _originalOut = Console.Out;
            _stringWriter = new StringWriter();
            Console.SetOut(_stringWriter);
        }

        [TearDown]
        public void TearDown()
        {
            Console.SetOut(_originalOut);
            _stringWriter.Dispose();
        }

        [Test]
        public void Log_WritesMessageToConsole()
        {
            // Arrange
            string testMessage = "Hello, Logger!";

            // Act
            _logger.Log(testMessage);

            // Assert
            string output = _stringWriter.ToString();
            Assert.That(output, Does.Contain(testMessage), "Console output should contain the logged message.");
        }

        [Test]
        public void Log_EmptyMessage_WritesEmptyLine()
        {
            // Act
            _logger.Log(string.Empty);

            // Assert
            string output = _stringWriter.ToString();
            Assert.That(output, Does.Contain(Environment.NewLine), "Console output should contain a newline for empty message.");
        }
    }
}
