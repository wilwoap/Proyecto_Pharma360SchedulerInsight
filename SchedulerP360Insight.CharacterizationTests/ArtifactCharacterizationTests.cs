using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class ArtifactCharacterizationTests
    {
        [TestMethod]
        public void ReportAssets_MatchTheApprovedGoldenHashes()
        {
            string repositoryRoot = TestSupport.FindRepositoryRoot();
            string manifestPath = Path.Combine(
                repositoryRoot,
                "SchedulerP360Insight.CharacterizationTests",
                "Fixtures",
                "report-assets.sha256");
            List<string> entries = File.ReadAllLines(manifestPath)
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line) &&
                    !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .ToList();

            Assert.AreEqual(8, entries.Count);
            Assert.AreEqual(
                5,
                entries.Count(line =>
                    line.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase)));

            foreach (string entry in entries)
            {
                string[] fields = entry.Split(
                    new[] { "  " },
                    2,
                    StringSplitOptions.None);
                Assert.AreEqual(2, fields.Length, "Entrada inválida: " + entry);

                string relativePath = fields[1]
                    .Replace('/', Path.DirectorySeparatorChar);
                string assetPath = Path.Combine(repositoryRoot, relativePath);
                Assert.IsTrue(File.Exists(assetPath), relativePath);

                string actualHash;
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = File.OpenRead(assetPath))
                {
                    actualHash = ToLowerHex(algorithm.ComputeHash(stream));
                }

                Assert.AreEqual(fields[0], actualHash, relativePath);
            }
        }

        [TestMethod]
        public void SyntheticPdfFixture_IsNonEmptyAndHasPdfSignature()
        {
            string pdfPath = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "minimal-report.pdf");
            byte[] content = File.ReadAllBytes(pdfPath);

            Assert.IsTrue(content.Length > 32);
            Assert.AreEqual(
                "%PDF-",
                Encoding.ASCII.GetString(content, 0, 5));
        }

        [TestMethod]
        public void Log4Net_IsReferencedOnlyByCrystalAtTheFrozenAbi()
        {
            Assembly application = typeof(Scheduling.ReportJobFactory).Assembly;
            Assert.IsFalse(
                application.GetReferencedAssemblies()
                    .Any(reference => reference.Name == "log4net"),
                "El host no debe usar log4net directamente.");

            string crystalSharedPath = Path.Combine(
                AppContext.BaseDirectory,
                "CrystalDecisions.Shared.dll");
            Assembly crystalShared = Assembly.ReflectionOnlyLoadFrom(crystalSharedPath);
            AssemblyName log4NetReference = crystalShared.GetReferencedAssemblies()
                .Single(reference => reference.Name == "log4net");

            Assert.AreEqual(new Version(2, 0, 12, 0), log4NetReference.Version);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
