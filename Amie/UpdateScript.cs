using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Amie
{
	internal class UpdateScript
	{
		internal UpdateScript(string scriptName)
		{
			Name = scriptName;
		}

		private static string ScriptNamespace
		{
			get
			{
				return "Scripts";
			}
		}

		/// <summary>
		/// Load the scripts from the passed in assembly.  The scripts have to be in the "Scripts" folder in the root of the assebmly to be detected.
		/// </summary>
		/// <param name="executingAssembly"></param>
		/// <returns></returns>
		internal static List<UpdateScript> FromAssembly(Assembly executingAssembly)
		{
			List<UpdateScript> scripts = new List<UpdateScript>();
			string[] names = executingAssembly.GetManifestResourceNames();

			foreach (var name in names)
			{
				if (name.Contains(ScriptNamespace))
				{
					int trimAmount = name.IndexOf(ScriptNamespace) + ScriptNamespace.Length + 1;
					string scriptName = name.Substring(trimAmount, name.Length - trimAmount);
					var updateScript = new UpdateScript(scriptName) { FullNamespaceName = name, FileContents = GetStream(executingAssembly, name) };
					scripts.Add(updateScript);
				}
			}
			return scripts.OrderBy(x => x.Version).ToList();
		}

		private static StreamReader GetStream(Assembly executingAssembly, string fullName)
		{
			var resourceStream = executingAssembly.GetManifestResourceStream(fullName);

			if (resourceStream == null) return null;

			var stream = new StreamReader(resourceStream);

			return stream;
		}

		/// <summary>
		/// Load a single script from the executing assembly.  The script has to be in the "Scripts" folder in the root of the assebmly to be detected.
		/// </summary>
		/// <param name="scriptName">This is NOT case sensitive.  6-UpdateUsers.sql == 6-updateUsers.SQL</param>
		/// <param name="executingAssembly"></param>
		/// <returns></returns>
		internal static UpdateScript FromScriptName(string scriptName, Assembly executingAssembly)
		{
			var scripts = FromAssembly(executingAssembly);
			foreach (var item in scripts)
			{
				if (item.Name.ToLower() == scriptName.ToLower())
					return item;
			}
			return null;
		}

		private StreamReader _fileContents;

		/// <summary>
		/// The content of the file in a StreamReader. If Null the script was not found in the executing assembly.
		/// </summary>
		internal StreamReader FileContents
		{
			get
			{
				return _fileContents;
			}
			set
			{
				_fileContents = value;
			}
		}

		/// <summary>
		/// Fully qualified name of the script from the Assembly.  For example MyUpdateUtil.Scripts.1-create username table.sql.
		/// </summary>
		internal string FullNamespaceName { get; set; }

		/// <summary>
		/// The name of the script with the namespace stripped off.  This is the simple name of the script. If the fully qualified name is MyUpdateUtil.Scripts.1-create username table.sql this will return 1-create username table.sql.
		/// </summary>
		internal string Name { get; set; }

		/// <summary>
		/// The verison of the script.  The script name has to be numbered like this [version]-[name of script]. For example 1-append profile to user table.sql.  The version of this script would be 1.  This is a double so the script could be numbered like 1.5-extent username field size.sql
		/// If the script has a REM in the beggining of the name it will be skipped.
		/// </summary>
		internal double Version
		{
			get
			{
				int separatorIndex = Name.IndexOf("-");
				//The filename does not contain the required separator - get out.  If the script is REM'd out then set version to 0 and it will never be ran.
				if (separatorIndex == -1 || Name.StartsWith("REM")) return 0;

				string versionOnFile = Name.Substring(0, separatorIndex);
				double fileVersion = double.Parse(versionOnFile);
				return fileVersion;
			}
		}

		/// <summary>
		/// Compares the name of the script with the name passed in.  NOT case sensitive.  
		/// </summary>
		/// <param name="scriptName"></param>
		/// <returns></returns>
		internal bool Equals(string scriptName)
		{
			return this.Name.ToLower() == scriptName.ToLower();
		}
	}
}