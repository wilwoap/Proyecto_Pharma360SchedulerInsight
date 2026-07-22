using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class EnvironmentConfigurationTests
    {
        [TestMethod]
        public void RequiredVariable_ReturnsInheritedValue()
        {
            string requestedName = null;

            string value = AppConfig.GetRequiredEnvironmentVariable(
                AppConfig.ConnectionStringEnvironmentVariable,
                name =>
                {
                    requestedName = name;
                    return "synthetic-connection";
                });

            Assert.AreEqual(AppConfig.ConnectionStringEnvironmentVariable, requestedName);
            Assert.AreEqual("synthetic-connection", value);
        }

        [TestMethod]
        public void RequiredVariable_MissingValueFailsWithoutPrintingASecret()
        {
            InvalidOperationException error =
                TestSupport.Throws<InvalidOperationException>(
                    () => AppConfig.GetRequiredEnvironmentVariable(
                        AppConfig.ConnectionStringEnvironmentVariable,
                        _ => null));

            StringAssert.Contains(
                error.Message,
                AppConfig.ConnectionStringEnvironmentVariable);
            Assert.IsFalse(error.Message.Contains("Password="));
        }

        [TestMethod]
        public void RequiredVariable_RejectsBlankName()
        {
            TestSupport.Throws<ArgumentException>(
                () => AppConfig.GetRequiredEnvironmentVariable(" ", _ => "value"));
        }
    }
}
