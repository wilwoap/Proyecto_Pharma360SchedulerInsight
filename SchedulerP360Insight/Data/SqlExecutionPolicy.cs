using SchedulerP360Insight.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace SchedulerP360Insight.Data
{
    public sealed class SqlExecutionPolicy
    {
        public static readonly TimeSpan DefaultConnectionTimeout =
            TimeSpan.FromSeconds(15);
        public static readonly TimeSpan DefaultCommandTimeout =
            TimeSpan.FromSeconds(30);

        private readonly string boundedConnectionString;

        public SqlExecutionPolicy(
            string connectionString,
            TimeSpan? connectionTimeout = null,
            TimeSpan? commandTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "La cadena de conexion es obligatoria.",
                    nameof(connectionString));
            }

            ConnectionTimeoutSeconds = ToWholeSeconds(
                connectionTimeout ?? DefaultConnectionTimeout,
                nameof(connectionTimeout),
                1,
                120);
            CommandTimeoutSeconds = ToWholeSeconds(
                commandTimeout ?? DefaultCommandTimeout,
                nameof(commandTimeout),
                1,
                300);

            try
            {
                SqlConnectionStringBuilder builder =
                    new SqlConnectionStringBuilder(connectionString)
                    {
                        ConnectTimeout = ConnectionTimeoutSeconds
                    };
                boundedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException error)
            {
                throw new InvalidOperationException(
                    "La cadena de conexion SQL tiene un formato no valido.",
                    error);
            }
        }

        public SqlExecutionPolicy(SchedulerOptions options)
            : this(
                (options ?? throw new ArgumentNullException(nameof(options)))
                    .ConnectionString,
                options.SqlConnectionTimeout,
                options.SqlCommandTimeout)
        {
        }

        public int ConnectionTimeoutSeconds { get; }

        public int CommandTimeoutSeconds { get; }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(boundedConnectionString);
        }

        public SqlCommand CreateCommand(
            string commandText,
            SqlConnection connection,
            CommandType commandType = CommandType.Text)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                throw new ArgumentException(
                    "El texto o nombre del comando SQL es obligatorio.",
                    nameof(commandText));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            return new SqlCommand(commandText, connection)
            {
                CommandType = commandType,
                CommandTimeout = CommandTimeoutSeconds
            };
        }

        public override string ToString()
        {
            return "SqlExecutionPolicy { ConnectionString=[REDACTED], " +
                "ConnectionTimeoutSeconds=" +
                ConnectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture) +
                ", CommandTimeoutSeconds=" +
                CommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture) +
                " }";
        }

        private static int ToWholeSeconds(
            TimeSpan value,
            string parameterName,
            int minimum,
            int maximum)
        {
            double seconds = value.TotalSeconds;
            if (seconds < minimum ||
                seconds > maximum ||
                seconds != Math.Truncate(seconds))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "El timeout debe ser un numero entero de segundos entre " +
                    minimum + " y " + maximum + ".");
            }

            return Convert.ToInt32(seconds, CultureInfo.InvariantCulture);
        }
    }
}
