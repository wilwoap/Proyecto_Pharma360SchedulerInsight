using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class HtmlTemplateCharacterizationTests
    {
        [TestMethod]
        [TestCategory("CharacterizationDebt")]
        public void LegacyTemplate_InsertsSpecialCharactersVerbatim()
        {
            const string specialValue =
                "<script>synthetic-only</script> & \"quoted\"";
            InfoColaNotificaciones notification =
                SyntheticFixtures.CreateNotification(
                    reportUid: "AURX",
                    recipientName: specialValue);

            string html = Utilitarios.ConstruirCuerpoEmailPlantillaHTML(
                notification,
                SyntheticFixtures.CreateLaboratory(),
                "HTMLBody_Plantilla_VM_01");

            Assert.IsFalse(string.IsNullOrWhiteSpace(html));
            StringAssert.Contains(html, specialValue);
        }

        [TestMethod]
        public void UnknownUid_KeepsCommonTemplateReplacementBehavior()
        {
            InfoColaNotificaciones notification =
                SyntheticFixtures.CreateNotification(
                    reportUid: "UNKNOWN",
                    recipientName: "Destinatario fixture");

            string html = Utilitarios.ConstruirCuerpoEmailPlantillaHTML(
                notification,
                SyntheticFixtures.CreateLaboratory(),
                "HTMLBody_Plantilla_VM_01");

            Assert.IsFalse(string.IsNullOrWhiteSpace(html));
            StringAssert.Contains(html, "Destinatario fixture");
            StringAssert.Contains(html, "Reporte sintético");
        }

        [TestMethod]
        [TestCategory("CharacterizationDebt")]
        public void MissingTemplateKey_ReturnsNullContentDespiteLegacyDocumentation()
        {
            string html = Utilitarios.ConstruirCuerpoEmailPlantillaHTML(
                SyntheticFixtures.CreateNotification(),
                SyntheticFixtures.CreateLaboratory(),
                "MISSING_SYNTHETIC_TEMPLATE");

            Assert.IsNull(html);
        }
    }
}
