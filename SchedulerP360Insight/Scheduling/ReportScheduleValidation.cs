using Quartz;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SchedulerP360Insight.Scheduling
{
    public static class ReportScheduleRejectionReasons
    {
        public const string MissingDefinition = "missing_definition";
        public const string InvalidReportId = "invalid_report_id";
        public const string DuplicateReportId = "duplicate_report_id";
        public const string MissingReportUid = "missing_report_uid";
        public const string MissingReportName = "missing_report_name";
        public const string UnknownReportType = "unknown_report_type";
        public const string UnknownReportUid = "unknown_report_uid";
        public const string UnsupportedReportUid = "unsupported_report_uid";
        public const string InvalidCron = "invalid_cron";
        public const string BuildFailure = "build_failure";
    }

    public sealed class ReportScheduleRejection
    {
        public ReportScheduleRejection(
            ReportScheduleDefinition definition,
            string reason)
        {
            Definition = definition;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public ReportScheduleDefinition Definition { get; }

        public string Reason { get; }
    }

    public sealed class ReportScheduleValidationResult
    {
        public ReportScheduleValidationResult(
            IList<ReportScheduleDefinition> accepted,
            IList<ReportScheduleRejection> rejected)
        {
            Accepted = new ReadOnlyCollection<ReportScheduleDefinition>(
                accepted ?? throw new ArgumentNullException(nameof(accepted)));
            Rejected = new ReadOnlyCollection<ReportScheduleRejection>(
                rejected ?? throw new ArgumentNullException(nameof(rejected)));
        }

        public IReadOnlyList<ReportScheduleDefinition> Accepted { get; }

        public IReadOnlyList<ReportScheduleRejection> Rejected { get; }
    }

    public sealed class ReportScheduleValidator
    {
        private readonly ReportJobFactory jobFactory;

        public ReportScheduleValidator(ReportJobFactory jobFactory)
        {
            this.jobFactory = jobFactory ??
                throw new ArgumentNullException(nameof(jobFactory));
        }

        public ReportScheduleValidationResult ValidateAll(
            IReadOnlyList<ReportScheduleDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            HashSet<int> duplicateIds = new HashSet<int>(
                definitions
                    .Where(item => item != null && item.ReportId > 0)
                    .GroupBy(item => item.ReportId)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key));
            List<ReportScheduleDefinition> accepted =
                new List<ReportScheduleDefinition>();
            List<ReportScheduleRejection> rejected =
                new List<ReportScheduleRejection>();

            foreach (ReportScheduleDefinition definition in definitions)
            {
                string reason = Validate(definition, duplicateIds);
                if (reason == null)
                {
                    accepted.Add(definition);
                }
                else
                {
                    rejected.Add(new ReportScheduleRejection(
                        definition,
                        reason));
                }
            }

            return new ReportScheduleValidationResult(accepted, rejected);
        }

        private string Validate(
            ReportScheduleDefinition definition,
            ISet<int> duplicateIds)
        {
            if (definition == null)
            {
                return ReportScheduleRejectionReasons.MissingDefinition;
            }

            if (definition.ReportId <= 0)
            {
                return ReportScheduleRejectionReasons.InvalidReportId;
            }

            if (duplicateIds.Contains(definition.ReportId))
            {
                return ReportScheduleRejectionReasons.DuplicateReportId;
            }

            if (string.IsNullOrWhiteSpace(definition.ReportUID))
            {
                return ReportScheduleRejectionReasons.MissingReportUid;
            }

            if (string.IsNullOrWhiteSpace(definition.ReportName))
            {
                return ReportScheduleRejectionReasons.MissingReportName;
            }

            try
            {
                jobFactory.ResolveJobType(definition.ReportType);
            }
            catch (ArgumentException)
            {
                return ReportScheduleRejectionReasons.UnknownReportType;
            }

            if (!jobFactory.IsKnownReportUid(definition.ReportUID))
            {
                return ReportScheduleRejectionReasons.UnknownReportUid;
            }

            if (!jobFactory.SupportsReportUid(
                definition.ReportType,
                definition.ReportUID))
            {
                return ReportScheduleRejectionReasons.UnsupportedReportUid;
            }

            if (string.IsNullOrWhiteSpace(definition.ReportSchedule) ||
                !CronExpression.IsValidExpression(
                    definition.ReportSchedule))
            {
                return ReportScheduleRejectionReasons.InvalidCron;
            }

            return null;
        }
    }
}
