using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Services;
using System;
using System.IO;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class PathPolicyTests
    {
        [TestMethod]
        public void CombineUnderRoot_AcceptsAChildFile()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "p360-characterization-root");

            string result = ReportPathPolicy.CombineUnderRoot(
                root,
                Path.Combine("reports", "synthetic.pdf"));

            StringAssert.StartsWith(
                result,
                Path.GetFullPath(root) + Path.DirectorySeparatorChar);
            StringAssert.EndsWith(result, "synthetic.pdf");
        }

        [TestMethod]
        public void CombineUnderRoot_RejectsTraversal()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "p360-characterization-root");

            TestSupport.Throws<UnauthorizedAccessException>(
                () => ReportPathPolicy.CombineUnderRoot(
                    root,
                    Path.Combine("..", "outside.pdf")));
        }

        [TestMethod]
        public void CombineUnderRoot_RejectsAbsoluteFileName()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "p360-characterization-root");
            string externalPath = Path.Combine(
                Path.GetPathRoot(root),
                "outside.pdf");

            TestSupport.Throws<UnauthorizedAccessException>(
                () => ReportPathPolicy.CombineUnderRoot(root, externalPath));
        }
    }
}
