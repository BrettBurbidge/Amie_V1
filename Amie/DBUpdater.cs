using System;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;

namespace Amie
{
	internal class DBUpdater
	{
		private Assembly ExecutingAssembly;

		public DBUpdater(Assembly executingAssembly)
		{
			this.ExecutingAssembly = executingAssembly;
		}

        private string ConnectionString { get; set; }

		public InstallResult UpdateFromConnectionString(string connectionString)
		{
			SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
			SchemaChangeApi schema = new SchemaChangeApi(connectionStringBuilder, ExecutingAssembly);

            ConnectionString = connectionStringBuilder.ConnectionString;

			schema.BeforeScriptRun += schema_BeforeScriptRun;
			schema.AfterScriptRun += schema_AfterScriptRun;

			var installResult = schema.PerformDatabaseUpdate();
			return installResult;
		}

		private void schema_AfterScriptRun(object sender, SchemaChangeApi.ScriptRunEventArgs e)
		{
			RunConversion(true, e);
		}

		private void RunConversion(bool isAfterSqlChange, SchemaChangeApi.ScriptRunEventArgs e)
		{
			Type iConversionType = typeof(Conversions.iConversion);

			//Get the list of iConversion classes to run.
			var q = from t in ExecutingAssembly.GetTypes()
					where iConversionType.IsAssignableFrom(t)
					select t;
			var list = q.ToList();

			if (list == null || list.Count() == 0) return;
	
			foreach (var item in list)
			{
				object reportObject = System.Activator.CreateInstance(item);
				Conversions.iConversion conversion = (Conversions.iConversion)reportObject;
				//Make sure the versions match
				if (conversion.ScriptVersion == e.ScriptVersion)
				{
					if (isAfterSqlChange == conversion.RunAfterSQLChange)
					{
						Logger.Info("Running conversion script {0} version {1}",e.ScriptName, e.ScriptVersion.ToString());
						conversion.PerformConversion(e.ScriptName, e.ScriptVersion, ConnectionString);
					}
				}
			}
		}

		private void schema_BeforeScriptRun(object sender, SchemaChangeApi.ScriptRunEventArgs e)
		{
			RunConversion(false, e);
		}
	}
}