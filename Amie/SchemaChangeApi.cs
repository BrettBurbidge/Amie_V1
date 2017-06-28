using Amie.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace Amie
{
	internal class SchemaChangeApi
	{
		private PetaPoco.Database _database;

		private const string PROVIDER_NAME = "System.Data.SqlClient";
		private const string INITIAL_DATABASE_SCHEMA_FILENAME = "databasebaseline.sql";
		private const string INITIAL_DATABASE_DEFAULTS_FILENAME = "databasedefaults.sql";
		private const string NOTE_COMMENT_PREFIX = "--@";
		private const string SCRIPT_COMMENT_PREFIX = "--$";

		#region "Events"

		public event EventHandler<EventArgs> BeforeInitialSchema;

		protected virtual void OnBeforeInitialSchema(EventArgs e)
		{
			EventHandler<EventArgs> handler = BeforeInitialSchema;
			if (handler != null) handler(this, e);
		}

		public event EventHandler<EventArgs> AfterInitialSchema;

		protected virtual void OnAfterInitialSchema(EventArgs e)
		{
			EventHandler<EventArgs> handler = AfterInitialSchema;
			if (handler != null) handler(this, e);
		}

		public event EventHandler<EventArgs> BeforeDatabaseDefaults;

		protected virtual void OnBeforeDatabaseDefaults(EventArgs e)
		{
			EventHandler<EventArgs> handler = BeforeDatabaseDefaults;
			if (handler != null) handler(this, e);
		}

		public event EventHandler<EventArgs> AfterDatabaseDefaults;

		protected virtual void OnAfterDatabaseDefaults(EventArgs e)
		{
			EventHandler<EventArgs> handler = AfterDatabaseDefaults;
			if (handler != null) handler(this, e);
		}

		public event EventHandler<ScriptRunEventArgs> BeforeScriptRun;

		protected virtual void OnBeforeScriptRun(ScriptRunEventArgs e)
		{
			EventHandler<ScriptRunEventArgs> handler = BeforeScriptRun;
			if (handler != null) handler(this, e);
		}

		public event EventHandler<ScriptRunEventArgs> AfterScriptRun;

		protected virtual void OnAfterScriptRun(ScriptRunEventArgs e)
		{
			EventHandler<ScriptRunEventArgs> handler = AfterScriptRun;
			if (handler != null) handler(this, e);
		}

		public class ScriptRunEventArgs : EventArgs
		{
			public string ScriptName { get; set; }
			public double ScriptVersion { get; set; }
		}

		#endregion "Events"

		private SqlConnectionStringBuilder ConnectionString;
		private System.Reflection.Assembly ExecutingAssembly;

		internal SchemaChangeApi(SqlConnectionStringBuilder connectionString, System.Reflection.Assembly executingAssembly)
		{
			this.ExecutingAssembly = executingAssembly;
			this.ConnectionString = connectionString;
			_database = new PetaPoco.Database(connectionString.ConnectionString, PROVIDER_NAME);
		}

		/// <summary>
		/// This will install a new version of the database or update an older version.  The database must exist in order for this to work.
		/// If it does not exist the InstallResult.Success will be set to false and there will be a message.
		/// </summary>
		/// <returns></returns>
		internal InstallResult PerformDatabaseUpdate()
		{
			InstallResult result = new InstallResult();

			result = RunDatabaseTests();
			if (!result.Success)
				return result;

			Logger.Info("Current Database Version: {0}", DatabaseVersionInstalled.ToString());
			Logger.Info("Update Script Version: {0}", DatabaseUpdateVersion.ToString());

			if (DatabaseIsNew)
			{
				result = BuildNewDatabase();
				if (!result.Success)
					return result;
			}

			//Yes we want to run the function above and below.
			//The code (above) creates the initial database which could be 100 versions old.
			//This code (below) will update the database to the lastest version.

			if (!DatabaseIsCurrent)
			{
				Logger.Info("Database exists it's out of date and will be updated.");
				Logger.Info("The following scripts will be ran on the database");
				var scripts = GetScriptsToRun();
				foreach (var item in scripts)
				{
					Logger.Info("{0}.", item.ScriptName);
				}

				foreach (var item in scripts)
				{
					Logger.Info("Running {0} item.", item.ScriptName);
					var script = RunSingleScript(item.ScriptName);
					if (script.Status == Models.SchemaChange.Status_Failed)
					{
						string message = string.Format("The script {0} failed. With message {1}. The script has stopped. \r\n\r\nFix the errors and run it again.", script.ScriptName, script.ScriptErrors);
						Logger.Info(message);
						result.Success = false;
						result.Message = message;
						return result;
					}
					else
						Logger.Info("{0}:{1}.", script.Comment, script.Status);
				}
			}
			else
			{
				Logger.Info("The database is current, nothing to do here.");
			}

			Logger.Info("Database update finished!");
			return result;
		}

		internal InstallResult RunDatabaseTests()
		{
			InstallResult result = new InstallResult();

			Logger.Info("Connection string check...");
			if (string.IsNullOrEmpty(ConnectionString.ConnectionString))
			{
				result.Success = false;
				result.Message = "Database connection string missing.";
				return result;
			}

			if (!TestDatabaseConnection().Success)
			{
				result.Success = false;
				result.Message = "Connection to the database failed.";
				return result;
			}

			Logger.Info("Connection string looks good.");
			return result;
		}

		private InstallResult TestDatabaseConnection()
		{
			InstallResult result = new InstallResult();
			var con = new SqlConnection(ConnectionString.ConnectionString);
			try
			{
				con.Open();
				result.Success = true;
				return result;
			}
			catch (Exception ex)
			{
				result.Message = ex.ToString();
				result.Success = false;
				return result;
			}
			finally
			{
				con.Close();
			}
		}

		/// <summary>
		/// In order to get this database version the connection string information in AppInfo has to be correct.  Also the SchemaChange table has to exist in then database.
		/// If either of these missing this will return 0 as the database version.
		/// </summary>
		internal double DatabaseVersionInstalled
		{
			get
			{
				string sql = "SELECT TOP 1 * from SchemaChange ORDER BY DatabaseVersion Desc";
				try
				{
					var p = _database.SingleOrDefault<SchemaChange>(sql);
					return p == null ? 0 : p.DatabaseVersion;
				}
				catch (Exception)
				{
					return 0;
				}
			}
		}

		private List<Models.SchemaChange> GetHistory()
		{
			string sql = "ORDER BY DatabaseVersion";
			var p = _database.Fetch<Models.SchemaChange>(sql);
			return p;
		}

		private List<Models.SchemaChange> GetScriptsToRun()
		{
			double databaseVersion = DatabaseVersionInstalled;

			List<Models.SchemaChange> scriptsToRun = new List<SchemaChange>();

			foreach (var script in UpdateScripts)
			{
				if (script.Version > databaseVersion)
					scriptsToRun.Add(BuildSchemaFromFile(script));
			}

			return scriptsToRun;
		}

		private InstallResult BuildNewDatabase()
		{
			InstallResult result = new InstallResult();
			result = BuildInitialSchema();
			if (!result.Success) return result;

			result = BuildDatabaseDefaults();

			return result;
		}

		/// <summary>
		/// Looks for the file named DatabaseBaseline.sql and runs it on a blank database.
		/// </summary>
		private InstallResult BuildInitialSchema()
		{
			OnBeforeInitialSchema(EventArgs.Empty);

			InstallResult result = new InstallResult();
			Logger.Info("Creating initial database schema from {0}.", INITIAL_DATABASE_SCHEMA_FILENAME);

			var script = UpdateScript.FromScriptName(INITIAL_DATABASE_SCHEMA_FILENAME, ExecutingAssembly);

			if (script.FileContents == null)
			{
				string message = string.Format("The initial schema creation for this database has failed. The {0} file could not be found at {1}.", INITIAL_DATABASE_SCHEMA_FILENAME, script.FullNamespaceName);
				Logger.Error(message);
				result.Success = false;
				result.SetMessage(message);
				return result;
			}

			var dbCreateResuls = RunSingleScript(script);
			if (dbCreateResuls.Status == SchemaChange.Status_Failed)
			{
				string message = string.Format("The initial schema creation for this database has failed. There was a problem while running the update file {0}. Here is the error: {1} ", script.Name, dbCreateResuls.ScriptErrors);
				Logger.Error(message);
				result.Success = false;
				result.SetMessage(message);
			}

			Logger.Info("Initial database schema created!");

			OnAfterInitialSchema(EventArgs.Empty);
			return result;
		}

		/// <summary>
		/// Looks for a file named DatabaseDefaults.sql.  If it exists it runs it.  The purpose of this file is to create default data in the database.  This will only be ran one time on initial creation of the database.
		/// </summary>
		private InstallResult BuildDatabaseDefaults()
		{
			OnBeforeDatabaseDefaults(EventArgs.Empty);
			InstallResult result = new InstallResult();

			Logger.Info("Creating initial database defaults from {0}.", INITIAL_DATABASE_DEFAULTS_FILENAME);

			var script = UpdateScript.FromScriptName(INITIAL_DATABASE_DEFAULTS_FILENAME, ExecutingAssembly);

			if (script.FileContents == null)
			{
				string message = string.Format("Initial database defaults file {0} could not be found.  Database schema creation was successful but the defaults were not loaded into the database.  This will cause a problem with login and other initial date the database is expecting.  Try to to run it again in SQL manager if possible.", script.FullNamespaceName);
				Logger.Error(message);
				result.Success = false;
				result.SetMessage(message);
				return result;
			}

			var dbCreateResuls = RunSingleScript(script);
			if (dbCreateResuls.Status == SchemaChange.Status_Failed)
			{
				string message = string.Format("Building the initial database defaults for this database has failed. There was a problem while running the update file {0}. Here is the error: {1}. This will cause a problem with login and other initial date the database is expecting.  Try to to run it again in SQL manager if possible.", script.Name, dbCreateResuls.ScriptErrors);
				Logger.Error(message);
				result.Success = false;
				result.SetMessage(message);
			}

			Logger.Info("Initial database defaults created!");

			OnAfterDatabaseDefaults(EventArgs.Empty);
			return result;
		}

		private List<UpdateScript> _updateScripts;

		private List<UpdateScript> UpdateScripts
		{
			get
			{
				if (_updateScripts == null)
				{
					_updateScripts = UpdateScript.FromAssembly(ExecutingAssembly);
				}
				return _updateScripts;
			}
		}

		private bool DatabaseIsCurrent
		{
			get
			{
				return DatabaseVersionInstalled == DatabaseUpdateVersion;
			}
		}

		/// <summary>
		/// Returns true if the InstalledDatabaseVersion is 0
		/// </summary>
		private bool DatabaseIsNew
		{
			get
			{
				return DatabaseVersionInstalled == 0;
			}
		}

		/// <summary>
		/// The Max version of the update files.
		/// </summary>
		private double DatabaseUpdateVersion
		{
			get
			{
				return UpdateScripts.ToArray().Max(x => x.Version);
			}
		}

		private Models.SchemaChange BuildSchemaFromFile(UpdateScript updateScript)
		{
			List<string> scriptLines = ExtractSqlFromFile(updateScript);

			Models.SchemaChange script = new Models.SchemaChange();
			script.DatabaseVersion = updateScript.Name.ToLower() == INITIAL_DATABASE_SCHEMA_FILENAME ? .5 : updateScript.Version;
			script.LogThisChange = updateScript.Name.ToLower() == INITIAL_DATABASE_DEFAULTS_FILENAME ? false : true;
			script.DateApplied = DateTime.Now;
			script.ScriptName = updateScript.Name;
			script.Status = Models.SchemaChange.Status_NotRan;
			script.Notes = ExtractComments(scriptLines, NOTE_COMMENT_PREFIX);
			script.ScriptContent = BuildScriptContent(scriptLines);
			script.ScriptLines = scriptLines;
			return script;
		}

		private string BuildScriptContent(List<string> fileContents)
		{
			StringBuilder scripts = new StringBuilder();

			foreach (string line in fileContents)
			{
				scripts.AppendLine(line);
			}
			return scripts.ToString();
		}

		private SchemaChange RunSingleScript(UpdateScript script)
		{
			var schemaScript = BuildSchemaFromFile(script);

			if (schemaScript == null)
			{
				schemaScript = new SchemaChange();
				schemaScript.Status = Models.SchemaChange.Status_Failed;
				schemaScript.ScriptErrors = string.Format("The script name {0} was not found. The script did not run.", script.Name);
				return schemaScript;
			}

			//Fire the Before Event if we are not creating inital schema or database defaults.
			if (!script.Equals(INITIAL_DATABASE_DEFAULTS_FILENAME) && !script.Equals(INITIAL_DATABASE_SCHEMA_FILENAME))
			{
				ScriptRunEventArgs scriptEventArgs = new ScriptRunEventArgs()
				{
					ScriptName = script.Name,
					ScriptVersion = script.Version
				};
				OnBeforeScriptRun(scriptEventArgs);
			}

			using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
			{
				connection.Open();

				foreach (var item in schemaScript.ScriptLines)
				{
					schemaScript.Comment = ExtractSingleComment(item, SCRIPT_COMMENT_PREFIX);
					using (SqlTransaction transaction = connection.BeginTransaction())
					{
						try
						{
							SqlCommand sqlCommand = new SqlCommand(item, connection);
							sqlCommand.Connection = connection;
							sqlCommand.Transaction = transaction;
							sqlCommand.ExecuteNonQuery();
							transaction.Commit();
						}
						catch (Exception ex)
						{
							schemaScript.ScriptErrors = string.Format("Failed Running script {0} with the comment {1}. SQL Syntax is {2}  Error: {3}", schemaScript.ScriptName, schemaScript.Comment, item, ex.Message);

							//Get out becuase something broke!
							schemaScript.Status = Models.SchemaChange.Status_Failed;
							return schemaScript;
						}
					}
				}

				schemaScript.Status = Models.SchemaChange.Status_Success;
				if (schemaScript.LogThisChange) AddSchemaChange(schemaScript);

				//Fire the Before Event if we are not creating inital schema or database defaults.
				if (!script.Equals(INITIAL_DATABASE_DEFAULTS_FILENAME) && !script.Equals(INITIAL_DATABASE_SCHEMA_FILENAME))
				{
					ScriptRunEventArgs scriptEventArgs = new ScriptRunEventArgs()
					{
						ScriptName = script.Name,
						ScriptVersion = script.Version
					};
					OnAfterScriptRun(scriptEventArgs);
				}

				return schemaScript;
			}
		}

		private SchemaChange RunSingleScript(string scriptName)
		{
			var updateScript = UpdateScript.FromScriptName(scriptName, ExecutingAssembly);
			return RunSingleScript(updateScript);
		}

		private string ExtractComments(List<string> fileContents, string delimiter)
		{
			StringBuilder comments = new StringBuilder();

			foreach (string line in fileContents)
			{
				comments.AppendLine(ExtractSingleComment(line, delimiter));
			}

			return comments.ToString();
		}

		private string ExtractSingleComment(string line, string delimiter)
		{
			StringBuilder comments = new StringBuilder();

			string[] lines = line.Split(new[] { "\r\n" }, StringSplitOptions.None);

			foreach (var singleLine in lines)
			{
				int commentLocation = singleLine.IndexOf(delimiter);
				//Grab the comment from the start of the comment (Delimiter) to the end of the line.
				if (commentLocation > -1)
				{
					int commentStart = commentLocation + 3;
					int commentEnd = singleLine.Length - commentStart;
					string comment = singleLine.Substring(commentStart, commentEnd);
					comments.AppendLine(comment);
				}
			}
			return comments.ToString();
		}

		private List<string> ExtractSqlFromFile(UpdateScript script)
		{
			System.IO.StreamReader sqlFile = script.FileContents;
			string sql = sqlFile.ReadToEnd();
			sqlFile.Close();

			string[] sqls = sql.Split(new string[] { "GO" }, StringSplitOptions.None);
			List<string> sqlList = new List<string>();

			foreach (var item in sqls)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					if (!item.Contains("/*"))
						sqlList.Add(item);
				}
			}
			return sqlList;
		}

		private static string FixDirectoryPath(string path)
		{
			if (path.Substring(path.Length - 1, 1) != Path.DirectorySeparatorChar.ToString())
			{
				path = path + Path.DirectorySeparatorChar;
			}
			return path;
		}

		private void AddSchemaChange(Models.SchemaChange changeScript)
		{
			if (changeScript.Status == Models.SchemaChange.Status_Success)
			{
				_database.Save(changeScript);
			}
		}

		//private static List<string> FindConnectionString(string startPath)
		//{
		//	_possibleConnectionString = new List<string>();
		//	DirectoryInfo di = new DirectoryInfo(startPath);
		//	SearchForConfigFiles(di.Parent.FullName);
		//	return _possibleConnectionString;
		//}

		////Search for connection string
		//private static List<string> _possibleConnectionString;
		//private static void SearchForConfigFiles(string startDir)
		//{
		//	startDir = FixDirectoryPath(startDir);
		//	string[] files = null;
		//	files = Directory.GetFileSystemEntries(startDir);
		//	foreach (string f in files)
		//	{
		//		//Sub directories
		//		if ((Directory.Exists(f)))
		//		{
		//			SearchForConfigFiles(f);
		//			//Files in directory
		//		}
		//		else
		//		{
		//			FileInfo file = new FileInfo(f);
		//			if (file.Extension.ToLower() == ".config")
		//			{
		//				_possibleConnectionString.Add(ExtractConnectionString(file.FullName));
		//			}
		//		}
		//	}
		//}

		//private static string ExtractConnectionString(string filePath)
		//{
		//	XmlUtil xml = new XmlUtil(filePath);
		//	return xml.GetConnectionString();
		//}
	}
}