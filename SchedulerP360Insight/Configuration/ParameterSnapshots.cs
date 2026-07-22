using SchedulerP360Insight.Data;
using SchedulerP360Insight.Modulos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;

namespace SchedulerP360Insight.Configuration
{
    public static class LaboratoryParameterNames
    {
        private static readonly ReadOnlyCollection<string> Names =
            Array.AsReadOnly(new[]
            {
                "MAIL_SSL",
                "LABORATORIO_URL_LOGO",
                "LABORATORIO_IMPLEMENTACION",
                "MAIL_ADMINISTRADOR_LABORATORIO",
                "MAIL_SMTP",
                "MAIL_USER",
                "MAIL_PASS",
                "MAIL_PORT",
                "EMPRESA_PAIS",
                "EMPRESA_CIUDAD",
                "EMPRESA_DIRECCION",
                "EMPRESA_SITIO_WEB",
                "EMPRESA_EMAIL_CONTACTO",
                "EMPRESA_TELEFONO_CONTACTO"
            });

        public static IReadOnlyCollection<string> All => Names;
    }

    public interface ISystemParameterSource
    {
        IReadOnlyDictionary<string, string> Load(
            IReadOnlyCollection<string> parameterNames);
    }

    public interface IParameterSnapshotProvider
    {
        LaboratoryConstants GetSnapshot();
    }

    public sealed class SqlSystemParameterSource : ISystemParameterSource
    {
        public const string OperationName = "system-parameters.load";

        private readonly SqlExecutionPolicy policy;

        public SqlSystemParameterSource(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "La cadena de conexión es obligatoria.",
                    nameof(connectionString));
            }

            policy = new SqlExecutionPolicy(connectionString);
        }

        public SqlSystemParameterSource(SchedulerOptions options)
        {
            policy = new SqlExecutionPolicy(
                options ?? throw new ArgumentNullException(nameof(options)));
        }

        public IReadOnlyDictionary<string, string> Load(
            IReadOnlyCollection<string> parameterNames)
        {
            if (parameterNames == null || parameterNames.Count == 0)
            {
                throw new ArgumentException(
                    "Debe solicitarse al menos un parámetro.",
                    nameof(parameterNames));
            }

            string[] names = parameterNames.ToArray();
            StringBuilder placeholders = new StringBuilder();
            for (int index = 0; index < names.Length; index++)
            {
                if (index > 0)
                {
                    placeholders.Append(", ");
                }

                placeholders.Append("@parameter");
                placeholders.Append(index);
            }

            string query =
                "SELECT parametro, VALOR FROM T_PARAMETROS " +
                "WHERE parametro IN (" + placeholders + ")";
            Dictionary<string, string> values =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (SqlConnection connection = policy.CreateConnection())
                using (SqlCommand command = policy.CreateCommand(query, connection))
                {
                for (int index = 0; index < names.Length; index++)
                {
                    command.Parameters.Add(
                        "@parameter" + index,
                        SqlDbType.VarChar,
                        128).Value = names[index];
                }

                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    int nameOrdinal = reader.GetOrdinal("parametro");
                    int valueOrdinal = reader.GetOrdinal("VALOR");
                    while (reader.Read())
                    {
                        string name = reader.GetString(nameOrdinal);
                        string value = reader.IsDBNull(valueOrdinal)
                            ? null
                            : reader.GetString(valueOrdinal);
                        if (values.ContainsKey(name))
                        {
                            throw new InvalidOperationException(
                                "El parámetro '" + name +
                                "' está duplicado en T_PARAMETROS.");
                        }

                        values.Add(name, value);
                    }
                }
                }
            }
            catch (SqlException error)
            {
                throw DataAccessException.Create(OperationName, error);
            }

            return new ReadOnlyDictionary<string, string>(values);
        }
    }

    public sealed class LegacySystemParameterSource : ISystemParameterSource
    {
        private readonly Func<string, string> readParameter;

        public LegacySystemParameterSource(Func<string, string> readParameter)
        {
            this.readParameter = readParameter ??
                throw new ArgumentNullException(nameof(readParameter));
        }

        public IReadOnlyDictionary<string, string> Load(
            IReadOnlyCollection<string> parameterNames)
        {
            if (parameterNames == null)
            {
                throw new ArgumentNullException(nameof(parameterNames));
            }

            Dictionary<string, string> values =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in parameterNames)
            {
                values.Add(name, readParameter(name));
            }

            return new ReadOnlyDictionary<string, string>(values);
        }
    }

    public sealed class StartupParameterSnapshotProvider :
        IParameterSnapshotProvider
    {
        private readonly object sync = new object();
        private readonly ISystemParameterSource source;
        private readonly string googleMapsApiKey;
        private LaboratoryConstants snapshot;

        public StartupParameterSnapshotProvider(
            ISystemParameterSource source,
            string googleMapsApiKey)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.googleMapsApiKey = googleMapsApiKey;
        }

        public LaboratoryConstants GetSnapshot()
        {
            LaboratoryConstants current = Volatile.Read(ref snapshot);
            if (current != null)
            {
                return current;
            }

            lock (sync)
            {
                if (snapshot == null)
                {
                    IReadOnlyDictionary<string, string> values =
                        source.Load(LaboratoryParameterNames.All);
                    LaboratoryConstants loaded =
                        LaboratoryConstants.FromParameters(
                            values,
                            googleMapsApiKey);
                    Volatile.Write(ref snapshot, loaded);
                }

                return snapshot;
            }
        }
    }
}
