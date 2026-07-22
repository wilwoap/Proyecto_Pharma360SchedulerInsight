using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchedulerP360Insight.Observability;
using SchedulerP360Insight.Services;
using SchedulerP360Insight.Scheduling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class ReportPipelineHardeningTests
    {
        [TestMethod]
        public void FileNamePolicy_PreservesSafeLegacyShape()
        {
            ReportRenderRequest request = CreateRequest(
                reportName: "Reporte mensual",
                referenceEventId: "EV-7");

            string result = ReportFileNamePolicy.CreatePdfFileName(
                request,
                includeReferenceEvent: true);

            Assert.AreEqual(
                "Reporte mensual_42(202607220915)[EV-7].pdf",
                result);
        }

        [TestMethod]
        public void FileNamePolicy_RemovesTraversalAndReservedNames()
        {
            ReportRenderRequest request = CreateRequest(
                reportName: "Ventas/Region\\Norte",
                referenceEventId: "../EV|7");

            string result = ReportFileNamePolicy.CreatePdfFileName(
                request,
                includeReferenceEvent: true);

            Assert.AreEqual(result, Path.GetFileName(result));
            Assert.IsFalse(result.Contains("/"));
            Assert.IsFalse(result.Contains("\\"));
            Assert.IsTrue(result.EndsWith(".pdf"));
            Assert.AreEqual(
                "_CON.pdf",
                ReportFileNamePolicy.NormalizePdfFileName("CON.pdf"));
            Assert.AreEqual(
                "_CON.archive.pdf",
                ReportFileNamePolicy.NormalizePdfFileName(
                    "CON.archive.pdf"));
            Assert.AreEqual(
                180,
                ReportFileNamePolicy.CreatePdfFileName(
                    CreateRequest(
                        reportName: new string('R', 200),
                        referenceEventId: new string('E', 200)),
                    includeReferenceEvent: true).Length);
            TestSupport.Throws<UnauthorizedAccessException>(
                () => ReportFileNamePolicy.NormalizePdfFileName(
                    "../external.pdf"));
        }

        [TestMethod]
        public async Task ArtifactStore_PromotesGoldenPdfWithoutChangingBytes()
        {
            string root = CreateTemporaryRoot();
            try
            {
                string source = Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "minimal-report.pdf");
                byte[] expected = File.ReadAllBytes(source);
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();

                ReportRenderResult result = await store.CreatePdfAsync(
                    ReportRendererKinds.DevExpress,
                    root,
                    "golden.pdf",
                    (temporaryPath, _) =>
                    {
                        File.Copy(source, temporaryPath);
                        return Task.CompletedTask;
                    },
                    CancellationToken.None);

                CollectionAssert.AreEqual(
                    expected,
                    File.ReadAllBytes(result.ArtifactPath));
                Assert.AreEqual(expected.LongLength, result.ArtifactLength);
                Assert.AreEqual(
                    0,
                    FindTemporaryFiles(root).Count);

                File.Delete(result.ArtifactPath);
                Assert.IsFalse(File.Exists(result.ArtifactPath));
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_RepeatedNameNeverOverwritesExistingPdf()
        {
            string root = CreateTemporaryRoot();
            try
            {
                byte[] firstBytes = MinimalPdf("first");
                byte[] secondBytes = MinimalPdf("second");
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();

                ReportRenderResult first = await CreatePdfAsync(
                    store,
                    root,
                    "repeated.pdf",
                    firstBytes);
                ReportRenderResult second = await CreatePdfAsync(
                    store,
                    root,
                    "repeated.pdf",
                    secondBytes);

                Assert.AreNotEqual(first.ArtifactPath, second.ArtifactPath);
                CollectionAssert.AreEqual(
                    firstBytes,
                    File.ReadAllBytes(first.ArtifactPath));
                CollectionAssert.AreEqual(
                    secondBytes,
                    File.ReadAllBytes(second.ArtifactPath));
                StringAssert.EndsWith(second.ArtifactPath, "repeated-1.pdf");
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_CollisionSuffixRespectsFileNameLimit()
        {
            string root = CreateTemporaryRoot();
            try
            {
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();
                string requestedName = new string('R', 176) + ".pdf";

                ReportRenderResult first = await CreatePdfAsync(
                    store,
                    root,
                    requestedName,
                    MinimalPdf("first-long"));
                ReportRenderResult second = await CreatePdfAsync(
                    store,
                    root,
                    requestedName,
                    MinimalPdf("second-long"));

                Assert.AreEqual(
                    180,
                    Path.GetFileName(first.ArtifactPath).Length);
                Assert.AreEqual(
                    180,
                    Path.GetFileName(second.ArtifactPath).Length);
                StringAssert.EndsWith(second.ArtifactPath, "-1.pdf");
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_ConcurrentPromotionCreatesDistinctFiles()
        {
            string root = CreateTemporaryRoot();
            try
            {
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();
                byte[] content = MinimalPdf("concurrent");

                ReportRenderResult[] results = await Task.WhenAll(
                    CreatePdfAsync(store, root, "same.pdf", content),
                    CreatePdfAsync(store, root, "same.pdf", content));

                Assert.AreEqual(
                    2,
                    results.Select(item => item.ArtifactPath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count());
                Assert.IsTrue(results.All(item => File.Exists(
                    item.ArtifactPath)));
                Assert.AreEqual(0, FindTemporaryFiles(root).Count);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_RenderFailureDeletesTemporaryFile()
        {
            string root = CreateTemporaryRoot();
            try
            {
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();

                await ThrowsAsync<IOException>(() => store.CreatePdfAsync(
                    ReportRendererKinds.DevExpress,
                    root,
                    "failed.pdf",
                    (temporaryPath, _) =>
                    {
                        File.WriteAllBytes(
                            temporaryPath,
                            MinimalPdf("partial"));
                        throw new IOException("Synthetic disk failure.");
                    },
                    CancellationToken.None));

                Assert.AreEqual(0, Directory.GetFiles(root).Length);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_InvalidPdfIsClassifiedAndRemoved()
        {
            string root = CreateTemporaryRoot();
            try
            {
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();

                ReportRenderException error =
                    await ThrowsAsync<ReportRenderException>(
                        () => store.CreatePdfAsync(
                            ReportRendererKinds.DevExpress,
                            root,
                            "invalid.pdf",
                            (temporaryPath, _) =>
                            {
                                File.WriteAllText(
                                    temporaryPath,
                                    "not a pdf",
                                    Encoding.UTF8);
                                return Task.CompletedTask;
                            },
                            CancellationToken.None));

                Assert.AreEqual("artifact.invalid_pdf", error.FailureCode);
                Assert.IsTrue(error.Permanent);
                Assert.AreEqual(0, Directory.GetFiles(root).Length);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task ArtifactStore_CancellationAfterRenderDeletesTemporaryFile()
        {
            string root = CreateTemporaryRoot();
            try
            {
                AtomicReportArtifactStore store =
                    new AtomicReportArtifactStore();
                using (CancellationTokenSource cancellation =
                    new CancellationTokenSource())
                {
                    await ThrowsAsync<OperationCanceledException>(
                        () => store.CreatePdfAsync(
                            ReportRendererKinds.DevExpress,
                            root,
                            "cancelled.pdf",
                            (temporaryPath, _) =>
                            {
                                File.WriteAllBytes(
                                    temporaryPath,
                                    MinimalPdf("cancelled"));
                                cancellation.Cancel();
                                return Task.CompletedTask;
                            },
                            cancellation.Token));
                }

                Assert.AreEqual(0, Directory.GetFiles(root).Length);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public void TemporaryCleanup_DeletesOnlyOwnedStaleFiles()
        {
            string root = CreateTemporaryRoot();
            try
            {
                DateTime now = new DateTime(
                    2026,
                    7,
                    22,
                    9,
                    30,
                    0,
                    DateTimeKind.Utc);
                string stale = Path.Combine(
                    root,
                    AtomicReportArtifactStore.TemporaryFilePrefix +
                    "stale" +
                    AtomicReportArtifactStore.TemporaryFileSuffix);
                string recent = Path.Combine(
                    root,
                    AtomicReportArtifactStore.TemporaryFilePrefix +
                    "recent" +
                    AtomicReportArtifactStore.TemporaryFileSuffix);
                string unrelated = Path.Combine(root, "keep.tmp");
                string final = Path.Combine(root, "keep.pdf");
                File.WriteAllText(stale, "stale");
                File.WriteAllText(recent, "recent");
                File.WriteAllText(unrelated, "unrelated");
                File.WriteAllBytes(final, MinimalPdf("final"));
                File.SetLastWriteTimeUtc(stale, now.AddDays(-2));
                File.SetLastWriteTimeUtc(recent, now.AddMinutes(-5));

                TemporaryFileCleanupResult result =
                    new AtomicReportArtifactStore()
                        .CleanupStaleTemporaryFiles(
                            root,
                            now,
                            TimeSpan.FromHours(24));

                Assert.AreEqual(2, result.Examined);
                Assert.AreEqual(1, result.Deleted);
                Assert.AreEqual(0, result.Failed);
                Assert.IsFalse(File.Exists(stale));
                Assert.IsTrue(File.Exists(recent));
                Assert.IsTrue(File.Exists(unrelated));
                Assert.IsTrue(File.Exists(final));
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public void SourcePolicy_AcceptsRptAndRejectsTraversalOrWrongExtension()
        {
            string root = CreateTemporaryRoot();
            try
            {
                string report = Path.Combine(root, "synthetic.rpt");
                File.WriteAllText(report, "synthetic");
                File.WriteAllText(
                    Path.Combine(root, "synthetic.txt"),
                    "synthetic");

                Assert.AreEqual(
                    report,
                    ReportPathPolicy.ResolveExistingFileUnderRoot(
                        root,
                        "synthetic.rpt",
                        ".rpt"));
                TestSupport.Throws<UnauthorizedAccessException>(
                    () => ReportPathPolicy.ResolveExistingFileUnderRoot(
                        root,
                        Path.Combine("..", "external.rpt"),
                        ".rpt"));
                ReportRenderException wrongExtension =
                    TestSupport.Throws<ReportRenderException>(
                        () => ReportPathPolicy.ResolveExistingFileUnderRoot(
                            root,
                            "synthetic.txt",
                            ".rpt"));
                Assert.AreEqual(
                    "artifact.source_extension_invalid",
                    wrongExtension.FailureCode);
            }
            finally
            {
                DeleteTemporaryRoot(root);
            }
        }

        [TestMethod]
        public async Task HtmlRenderer_ReturnsTypedNoArtifactResult()
        {
            HtmlReportRenderer renderer = new HtmlReportRenderer();

            ReportRenderResult result = await renderer.RenderAsync(
                CreateRequest(reportUid: "RVIS"),
                CancellationToken.None);

            Assert.AreEqual(ReportRendererKinds.Html, result.RendererKind);
            Assert.AreEqual("none", result.ArtifactKind);
            Assert.IsFalse(result.HasArtifact);
        }

        [TestMethod]
        public async Task HtmlRenderer_UnknownUidFailsPermanently()
        {
            HtmlReportRenderer renderer = new HtmlReportRenderer();

            ReportRenderException error =
                await ThrowsAsync<ReportRenderException>(
                    () => renderer.RenderAsync(
                        CreateRequest(reportUid: "UNKNOWN"),
                        CancellationToken.None));

            Assert.AreEqual("renderer.unknown_uid", error.FailureCode);
            Assert.IsTrue(error.Permanent);
        }

        [TestMethod]
        public void UidCatalog_EnforcesRendererCompatibility()
        {
            Assert.IsTrue(ReportUidCatalog.SupportsCrystal("PVM"));
            Assert.IsFalse(ReportUidCatalog.SupportsCrystal("RVIS"));
            Assert.IsTrue(ReportUidCatalog.SupportsDevExpress("AURX"));
            Assert.IsFalse(ReportUidCatalog.SupportsDevExpress("PVM"));
            Assert.IsTrue(ReportUidCatalog.SupportsHtml("RVIS"));
            Assert.IsFalse(ReportUidCatalog.IsKnown("UNKNOWN"));
        }

        [TestMethod]
        public void Validator_RejectsUnknownUidBeforeScheduling()
        {
            ReportScheduleDefinition definition = TestSupport.CreateReport(
                reportType: "html",
                reportUid: "UNKNOWN");
            ReportScheduleValidationResult result =
                new ReportScheduleValidator(new ReportJobFactory())
                    .ValidateAll(new[] { definition });

            Assert.AreEqual(0, result.Accepted.Count);
            Assert.AreEqual(1, result.Rejected.Count);
            Assert.AreEqual(
                ReportScheduleRejectionReasons.UnknownReportUid,
                result.Rejected[0].Reason);
        }

        [TestMethod]
        public void Validator_RejectsUidUnsupportedBySelectedRenderer()
        {
            ReportScheduleDefinition definition = TestSupport.CreateReport(
                reportType: "crystal reports",
                reportUid: "RVIS");
            ReportScheduleValidationResult result =
                new ReportScheduleValidator(new ReportJobFactory())
                    .ValidateAll(new[] { definition });

            Assert.AreEqual(0, result.Accepted.Count);
            Assert.AreEqual(
                ReportScheduleRejectionReasons.UnsupportedReportUid,
                result.Rejected.Single().Reason);
        }

        [TestMethod]
        public void DeliveryClassifier_PreservesTypedRenderFailureCode()
        {
            NotificationFailureDecision decision =
                NotificationFailureClassifier.Classify(
                    new ReportRenderException(
                        "artifact.invalid_pdf",
                        permanent: true,
                        message: "Synthetic invalid PDF."));

            Assert.IsTrue(decision.Permanent);
            Assert.AreEqual("artifact.invalid_pdf", decision.ErrorCode);
            TestSupport.Throws<ArgumentException>(() =>
                new ReportRenderException(
                    "artifact.invalid\ncode",
                    permanent: true,
                    message: "Synthetic invalid code."));
            TestSupport.Throws<ArgumentException>(() =>
                new ReportRenderException(
                    new string('a', 65),
                    permanent: true,
                    message: "Synthetic oversized code."));
        }

        [TestMethod]
        public void Diagnostics_ExposeOnlyBoundedArtifactAndProcessFields()
        {
            ReportRenderResult result = ReportRenderResult.Pdf(
                ReportRendererKinds.DevExpress,
                @"C:\synthetic\report.pdf",
                1234);
            IReadOnlyDictionary<string, string> fields =
                ReportRenderDiagnostics.CreateFields(
                    ReportRendererKinds.DevExpress,
                    result,
                    new ReportProcessSnapshot
                    {
                        WorkingSetBytes = 1000,
                        HandleCount = 10
                    },
                    new ReportProcessSnapshot
                    {
                        WorkingSetBytes = 1250,
                        HandleCount = 12
                    });

            Assert.AreEqual("pdf", fields["artifact_kind"]);
            Assert.AreEqual("1234", fields["artifact_bytes"]);
            Assert.AreEqual("250", fields["working_set_delta_bytes"]);
            Assert.AreEqual("2", fields["handle_delta"]);
            Assert.AreEqual(
                ReportRendererKinds.DevExpress,
                fields["renderer_kind"]);
            Assert.IsFalse(fields.Values.Any(value =>
                value.Contains("synthetic")));

            Dictionary<string, string> candidateFields =
                fields.ToDictionary(
                    item => item.Key,
                    item => item.Value);
            candidateFields["artifact_path"] =
                @"C:\synthetic\report.pdf";
            IReadOnlyDictionary<string, string> filtered =
                EventFieldPolicy.Filter(candidateFields);
            Assert.AreEqual(fields.Count, filtered.Count);
            Assert.IsFalse(filtered.ContainsKey("artifact_path"));
        }

        private static ReportRenderRequest CreateRequest(
            string reportUid = "AURX",
            string reportName = "Reporte mensual",
            string referenceEventId = "EV-7")
        {
            return new ReportRenderRequest(
                reportUid,
                reportName,
                42,
                referenceEventId,
                new DateTime(2026, 7, 22, 9, 15, 0),
                outputRoot: @"C:\P360\Output",
                sourceRoot: @"C:\P360\Source",
                sourceFileName: "report.rpt");
        }

        private static Task<ReportRenderResult> CreatePdfAsync(
            AtomicReportArtifactStore store,
            string root,
            string name,
            byte[] content)
        {
            return store.CreatePdfAsync(
                ReportRendererKinds.DevExpress,
                root,
                name,
                (temporaryPath, _) =>
                {
                    File.WriteAllBytes(temporaryPath, content);
                    return Task.CompletedTask;
                },
                CancellationToken.None);
        }

        private static byte[] MinimalPdf(string marker)
        {
            return Encoding.ASCII.GetBytes(
                "%PDF-1.4\n% " + marker +
                "\n1 0 obj<</Type/Catalog>>endobj\n%%EOF\n");
        }

        private static List<string> FindTemporaryFiles(string root)
        {
            return Directory.GetFiles(
                    root,
                    AtomicReportArtifactStore.TemporaryFilePrefix +
                    "*" +
                    AtomicReportArtifactStore.TemporaryFileSuffix,
                    SearchOption.TopDirectoryOnly)
                .ToList();
        }

        private static string CreateTemporaryRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "P360PR11_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTemporaryRoot(string root)
        {
            string canonicalTemp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string canonicalRoot = Path.GetFullPath(root);
            if (!canonicalRoot.StartsWith(
                canonicalTemp,
                StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(canonicalRoot).StartsWith(
                    "P360PR11_",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "La limpieza de prueba intento salir del directorio temporal.");
            }

            if (Directory.Exists(canonicalRoot))
            {
                Directory.Delete(canonicalRoot, recursive: true);
            }
        }

        private static async Task<TException> ThrowsAsync<TException>(
            Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException error)
            {
                return error;
            }
            catch (Exception error)
            {
                Assert.Fail(
                    "Se esperaba " + typeof(TException).Name +
                    " pero se recibio " + error.GetType().Name + ".");
            }

            Assert.Fail(
                "Se esperaba una excepcion " + typeof(TException).Name + ".");
            return null;
        }
    }
}
