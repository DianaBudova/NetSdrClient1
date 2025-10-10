using NetArchTest.Rules;
using NetSdrClientApp; // to access Program type

namespace NetSdrClientAppTests
{
    public class DependencyRulesTests
    {
        [Test]
        public void Networking_Should_Not_Depend_On_Messages()
        {
            // Ensure classes in Networking do not depend on Messages
            var result = Types
                .InAssembly(typeof(Program).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Networking")
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.Messages")
                .GetResult();

            Assert.IsTrue(result.IsSuccessful,
                "Networking layer should not depend on Messages");
        }

        [Test]
        public void Messages_Should_Not_Depend_On_Networking()
        {
            // Ensure classes in Messages do not depend on Networking
            var result = Types
                .InAssembly(typeof(Program).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Messages")
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.Networking")
                .GetResult();

            Assert.IsTrue(result.IsSuccessful,
                "Messages layer should not depend on Networking");
        }

        [Test]
        public void OtherNamespaces_Should_Not_Depend_On_Program()
        {
            // Ensure no other classes depend directly on Program.cs
            var result = Types
                .InAssembly(typeof(Program).Assembly)
                .That()
                .DoNotResideInNamespace("NetSdrClientApp") // exclude Program namespace
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.Program")
                .GetResult();

            Assert.IsTrue(result.IsSuccessful,
                "Other namespaces should not depend directly on Program");
        }
    }
}
