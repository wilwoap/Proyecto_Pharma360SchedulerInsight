using Dapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using System.Data.SqlClient;

namespace SchedulerP360Insight.CharacterizationTests
{
    [TestClass]
    public sealed class DapperCompatibilityTests
    {
        [TestMethod]
        public void QuerySingle_MapsAdoNetReaderAndRequestsConnectionClose()
        {
            DataTable result = new DataTable();
            result.Columns.Add("ReportId", typeof(int));
            result.Columns.Add("ReportName", typeof(string));
            result.Rows.Add(42, "Reporte de compatibilidad");

            using (FakeDbConnection connection = new FakeDbConnection(result))
            {
                DependencyRow row = connection.QuerySingle<DependencyRow>(
                    "SELECT ReportId, ReportName FROM CompatibilityProbe");

                Assert.AreEqual(42, row.ReportId);
                Assert.AreEqual("Reporte de compatibilidad", row.ReportName);
                Assert.AreEqual(
                    "SELECT ReportId, ReportName FROM CompatibilityProbe",
                    connection.LastCommandText);
                Assert.AreEqual(1, connection.OpenCount);
                Assert.IsTrue(
                    (connection.LastCommandBehavior & CommandBehavior.CloseConnection) != 0,
                    "Dapper debe pedir al proveedor ADO.NET que cierre una conexion abierta por la consulta.");
            }
        }

        private sealed class DependencyRow
        {
            public int ReportId { get; set; }

            public string ReportName { get; set; }
        }

        private sealed class FakeDbConnection : IDbConnection
        {
            private readonly DataTable _result;

            public FakeDbConnection(DataTable result)
            {
                _result = result ?? throw new ArgumentNullException(nameof(result));
            }

            public string ConnectionString { get; set; }

            public int ConnectionTimeout => 0;

            public string Database => "Characterization";

            public ConnectionState State { get; private set; } = ConnectionState.Closed;

            public int OpenCount { get; private set; }

            public string LastCommandText { get; private set; }

            public CommandBehavior LastCommandBehavior { get; private set; }

            public IDbTransaction BeginTransaction()
            {
                throw new NotSupportedException();
            }

            public IDbTransaction BeginTransaction(IsolationLevel il)
            {
                throw new NotSupportedException();
            }

            public void ChangeDatabase(string databaseName)
            {
                throw new NotSupportedException();
            }

            public void Close()
            {
                State = ConnectionState.Closed;
            }

            public IDbCommand CreateCommand()
            {
                return new FakeDbCommand(
                    this,
                    _result,
                    commandText => LastCommandText = commandText,
                    behavior => LastCommandBehavior = behavior);
            }

            public void Open()
            {
                OpenCount++;
                State = ConnectionState.Open;
            }

            public void Dispose()
            {
                Close();
            }
        }

        private sealed class FakeDbCommand : IDbCommand
        {
            private readonly DataTable _result;
            private readonly Action<string> _captureCommandText;
            private readonly Action<CommandBehavior> _captureCommandBehavior;
            private readonly SqlCommand _parameterOwner = new SqlCommand();
            private string _commandText;

            public FakeDbCommand(
                IDbConnection connection,
                DataTable result,
                Action<string> captureCommandText,
                Action<CommandBehavior> captureCommandBehavior)
            {
                Connection = connection;
                _result = result;
                _captureCommandText = captureCommandText;
                _captureCommandBehavior = captureCommandBehavior;
            }

            public string CommandText
            {
                get => _commandText;
                set
                {
                    _commandText = value;
                    _captureCommandText(value);
                }
            }

            public int CommandTimeout { get; set; }

            public CommandType CommandType { get; set; }

            public IDbConnection Connection { get; set; }

            public IDataParameterCollection Parameters => _parameterOwner.Parameters;

            public IDbTransaction Transaction { get; set; }

            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel()
            {
            }

            public IDbDataParameter CreateParameter()
            {
                return new SqlParameter();
            }

            public int ExecuteNonQuery()
            {
                throw new NotSupportedException();
            }

            public IDataReader ExecuteReader()
            {
                _captureCommandBehavior(CommandBehavior.Default);
                return _result.CreateDataReader();
            }

            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                _captureCommandBehavior(behavior);
                return _result.CreateDataReader();
            }

            public object ExecuteScalar()
            {
                throw new NotSupportedException();
            }

            public void Prepare()
            {
            }

            public void Dispose()
            {
                _parameterOwner.Dispose();
            }
        }
    }
}
