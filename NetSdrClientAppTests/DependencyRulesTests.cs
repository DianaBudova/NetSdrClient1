using NetArchTest.Rules;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    public class DependencyRulesTests
    {
        [Test]
        public void Ui_Should_Not_Depend_On_Infrastructure()
        {
            var result = Types
                .InAssembly(typeof(NetSdrClientApp.Program).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.UI")
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.Infrastructure")
                .GetResult();

            Assert.IsTrue(result.IsSuccessful, 
                "UI layer should not depend directly on Infrastructure layer");
        }

        [Test]
        public void Domain_Should_Not_Depend_On_UI()
        {
            var result = Types
                .InAssembly(typeof(NetSdrClientApp.Program).Assembly)
                .That()
                .ResideInNamespace("NetSdrClientApp.Domain")
                .ShouldNot()
                .HaveDependencyOn("NetSdrClientApp.UI")
                .GetResult();

            Assert.IsTrue(result.IsSuccessful, 
                "Domain layer should not depend on UI layer");
        }
    }
}
