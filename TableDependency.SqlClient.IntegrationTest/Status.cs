﻿using System.Configuration;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TableDependency.Enums;
using TableDependency.EventArgs;
using TableDependency.Mappers;
using TableDependency.SqlClient.IntegrationTest.Model;

namespace TableDependency.SqlClient.IntegrationTest
{
    [TestClass]
    public class Status
    {
        private static string _connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
        private const string TableName = "TestTable";
        private SqlTableDependency<TestTable> _tableDependency = null;
        
        [TestInitialize]
        public void TestInitialize()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText =
                        $"IF OBJECT_ID('{TableName}', 'U') IS NULL BEGIN CREATE TABLE [{TableName}]( " +
                        "[Id][int] IDENTITY(1, 1) NOT NULL, " +
                        "[First Name] [nvarchar](50) NOT NULL, " +
                        "[Second Name] [nvarchar](50) NOT NULL, " +
                        "[Born] [datetime] NULL); END";
                    sqlCommand.ExecuteNonQuery();

                    sqlCommand.CommandText = $"DELETE FROM [{TableName}]";
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        [TestMethod]
        public void StatusTest()
        {
            try
            {
                var mapper = new ModelToTableMapper<TestTable>();
                mapper.AddMapping(c => c.Name, "FIRST name");
                mapper.AddMapping(c => c.Surname, "Second Name");
                _tableDependency = new SqlTableDependency<TestTable>(_connectionString, TableName, mapper);
                _tableDependency.OnChanged += TableDependency_Changed;

                Assert.IsTrue(_tableDependency.Status == TableDependencyStatus.WaitingToStart);

                _tableDependency.Start();

                Thread.Sleep(5000);

                var t = new Task(ModifyTableContent);
                t.Start();
                t.Wait(20000);

                _tableDependency.Stop();
                Assert.IsTrue(_tableDependency.Status == TableDependencyStatus.StoppedDueToCancellation);
            }
            finally
            {
                _tableDependency?.Dispose();
            }
        }

        private void TableDependency_Changed(object sender, RecordChangedEventArgs<TestTable> e)
        {
            Assert.IsTrue(_tableDependency.Status == TableDependencyStatus.ListenerForNotification);
        }

        private static void ModifyTableContent()
        {
            using (var sqlConnection = new SqlConnection(_connectionString))
            {
                sqlConnection.Open();
                using (var sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText = $"INSERT INTO [{TableName}] ([First Name], [Second Name]) VALUES ('aaa', 'aaa')";
                    sqlCommand.ExecuteNonQuery();
                    Thread.Sleep(500);

                    sqlCommand.CommandText = $"UPDATE [{TableName}] SET [First Name] = 'bbb', [Second Name] = 'bbbb'";
                    sqlCommand.ExecuteNonQuery();
                    Thread.Sleep(500);

                    sqlCommand.CommandText = $"DELETE FROM [{TableName}]";
                    sqlCommand.ExecuteNonQuery();
                    Thread.Sleep(500);
                }
            }
        }
    }
}