using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;

namespace SchedulerP360Insight.Data
{
    public enum DataFailureKind
    {
        Cancelled,
        Timeout,
        Transient,
        Permanent,
        Unknown
    }

    public sealed class DataAccessException : Exception
    {
        public DataAccessException(
            string operation,
            DataFailureKind failureKind,
            int? sqlErrorNumber,
            Exception innerException)
            : base(CreateMessage(operation, failureKind, sqlErrorNumber), innerException)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                throw new ArgumentException(
                    "La operacion de datos es obligatoria.",
                    nameof(operation));
            }

            Operation = operation;
            FailureKind = failureKind;
            SqlErrorNumber = sqlErrorNumber;
        }

        public string Operation { get; }

        public DataFailureKind FailureKind { get; }

        public int? SqlErrorNumber { get; }

        public static DataAccessException Create(
            string operation,
            Exception error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            DataAccessException existing = error as DataAccessException;
            if (existing != null)
            {
                return existing;
            }

            return new DataAccessException(
                operation,
                SqlFailureClassifier.Classify(error),
                SqlFailureClassifier.GetSqlErrorNumber(error),
                error);
        }

        public IReadOnlyDictionary<string, string> CreateTelemetryFields()
        {
            Dictionary<string, string> fields =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["failure_kind"] = FailureKind
                        .ToString()
                        .ToLowerInvariant()
                };
            if (SqlErrorNumber.HasValue)
            {
                fields["sql_code"] = SqlErrorNumber.Value.ToString(
                    CultureInfo.InvariantCulture);
            }

            return fields;
        }

        private static string CreateMessage(
            string operation,
            DataFailureKind failureKind,
            int? sqlErrorNumber)
        {
            string safeOperation = string.IsNullOrWhiteSpace(operation)
                ? "unknown"
                : operation;
            return "Fallo una operacion de datos. operation=" +
                safeOperation + ", failure_kind=" +
                failureKind.ToString().ToLowerInvariant() +
                (sqlErrorNumber.HasValue
                    ? ", sql_code=" + sqlErrorNumber.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty) +
                ".";
        }
    }

    public static class SqlFailureClassifier
    {
        private static readonly HashSet<int> TransientSqlNumbers =
            new HashSet<int>
            {
                1205,
                40197,
                40501,
                40613,
                49918,
                49919,
                49920
            };

        private static readonly HashSet<int> PermanentSqlNumbers =
            new HashSet<int>
            {
                102,
                207,
                208,
                229,
                547,
                2601,
                2627,
                18456
            };

        public static DataFailureKind Classify(Exception error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            DataAccessException dataError = error as DataAccessException;
            if (dataError != null)
            {
                return dataError.FailureKind;
            }

            if (error is OperationCanceledException)
            {
                return DataFailureKind.Cancelled;
            }

            if (error is TimeoutException)
            {
                return DataFailureKind.Timeout;
            }

            SqlException sqlError = error as SqlException;
            return sqlError == null
                ? DataFailureKind.Unknown
                : ClassifySqlNumber(sqlError.Number);
        }

        public static DataFailureKind ClassifySqlNumber(int sqlErrorNumber)
        {
            if (sqlErrorNumber == -2)
            {
                return DataFailureKind.Timeout;
            }

            if (TransientSqlNumbers.Contains(sqlErrorNumber))
            {
                return DataFailureKind.Transient;
            }

            return PermanentSqlNumbers.Contains(sqlErrorNumber)
                ? DataFailureKind.Permanent
                : DataFailureKind.Unknown;
        }

        public static int? GetSqlErrorNumber(Exception error)
        {
            DataAccessException dataError = error as DataAccessException;
            if (dataError != null)
            {
                return dataError.SqlErrorNumber;
            }

            SqlException sqlError = error as SqlException;
            return sqlError == null ? (int?)null : sqlError.Number;
        }
    }
}
