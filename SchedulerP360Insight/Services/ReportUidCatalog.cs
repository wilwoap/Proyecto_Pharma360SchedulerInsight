using System;
using System.Collections.Generic;

namespace SchedulerP360Insight.Services
{
    public static class ReportUidCatalog
    {
        private static readonly HashSet<string> CrystalUids =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PVM",
                "PVMM",
                "PVG",
                "PVGM"
            };

        private static readonly HashSet<string> DevExpressUids =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AURX",
                "AUMD",
                "RPED",
                "DPED",
                "XPED",
                "VPED"
            };

        private static readonly HashSet<string> HtmlUids =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "PVM",
                "PVMM",
                "PVG",
                "PVGM",
                "AURX",
                "AUMD",
                "RVIS",
                "AVIS",
                "RPED",
                "DPED",
                "XPED",
                "VPED",
                "STNP",
                "VTNP"
            };

        public static bool IsKnown(string reportUid)
        {
            return reportUid != null && HtmlUids.Contains(reportUid);
        }

        public static bool SupportsCrystal(string reportUid)
        {
            return reportUid != null && CrystalUids.Contains(reportUid);
        }

        public static bool SupportsDevExpress(string reportUid)
        {
            return reportUid != null && DevExpressUids.Contains(reportUid);
        }

        public static bool SupportsHtml(string reportUid)
        {
            return reportUid != null && HtmlUids.Contains(reportUid);
        }

        public static bool Supports(string reportType, string reportUid)
        {
            if (reportType == "crystal reports")
            {
                return SupportsCrystal(reportUid);
            }

            if (reportType == "devexpress reports")
            {
                return SupportsDevExpress(reportUid);
            }

            if (reportType == "html")
            {
                return SupportsHtml(reportUid);
            }

            return false;
        }
    }
}
