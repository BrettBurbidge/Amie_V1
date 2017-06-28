using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

namespace Amie
{
    [Serializable]
	public class AppInfo
	{
		public AppInfo()
		{
			AppFolders = new List<AppFolder>();
		}

		public string ProductName { get; set; }
		public string Version { get; set; }
		public DateTime DateCreated { get; set; }
        /// <summary>
        /// The name of the executable to run in this package.  For exmaple TrafficUpdate.exe
        /// </summary>
        public string UpdateExecutableName { get; set; }

		public static AppInfo LoadFromFile(string appInfoFilePath)
		{
			var fileOutput = File.ReadAllText(appInfoFilePath);
			AppInfo deserializedProduct = JsonConvert.DeserializeObject<AppInfo>(fileOutput);
			return deserializedProduct;
		}

        public static AppInfo LoadFromStream(MemoryStream stream)
        {
            Encoding enc = Encoding.ASCII;
            
            string fileData = enc.GetString(stream.ToArray());
            AppInfo deserializedProduct = JsonConvert.DeserializeObject<AppInfo>(fileData);
            return deserializedProduct;
        }

		/// <summary>
		/// The list of folders in this app.  Folders can be Web, Service or Resources that are installed
		/// </summary>
		public List<AppFolder> AppFolders { get; set; }

        public AppFolder AppFolderFromName(string folderName)
        {
            foreach (AppFolder folder in AppFolders)
            {
                if (folder.Name == folderName)
                    return folder;
            }
            return null;
        }

		/// <summary>
		/// Defines the folder information for this application.
		/// </summary>
		public class AppFolder
		{
			public AppFolder(string name, AppFolderType type)
			{
				this.Name = name;
				this.Type = type;
			}

			/// <summary>
			/// The base update path to the Application directory
			/// </summary>
			[JsonIgnore]
			public string BaseUpdatePath
			{
				get
				{
					return Environment.CurrentDirectory + Path.DirectorySeparatorChar + "Application";
				}
			}

			/// <summary>
			/// The name of the folder.  We are expecting this to be located within the Application folder.
			/// </summary>
			public string Name { get; set; }

			/// <summary>
			/// Type helps the installer to know how to handle this type of folder it can be Web, Service or Resource
			/// </summary>
			[JsonIgnore]
			public AppFolderType Type { get; set; }

			/// <summary>
			/// The string name of the Type property.  When set, sets the Type property.
			/// </summary>
			public string TypeName
			{
				get
				{
					return Type.ToString();
				}
				set
				{
					Type = AppFolderType.SetType(value);
				}
			}

			/// <summary>
			/// The connenection string name in the app.config file.
			/// </summary>
			public string ConnectionStringName { get; set; }

			/// <summary>
			/// The name of the dll that will be in the folder.  We get the version information and config file name from this property.
			/// </summary>
			public string AssemblyName { get; set; }

			/// <summary>
			/// Path to the new files in this package
			/// </summary>
			[JsonIgnore]
			internal string UpdatePath
			{
				get
				{
					return BaseUpdatePath + Path.DirectorySeparatorChar + Name;
				}
			}

			/// <summary>
			/// The path to the update assebly.
			/// </summary>
			[JsonIgnore]
			internal string UpdateAssemblyPath
			{
				get
				{
					if (Type.Name == AppFolderType.Web.Name)
						return BaseUpdatePath + Path.DirectorySeparatorChar + Name + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + AssemblyName;
					else
						return BaseUpdatePath + Path.DirectorySeparatorChar + Name + Path.DirectorySeparatorChar + AssemblyName;
				}
			}

			/// <summary>
			/// Path to the config file for this folder based on type.
			/// </summary>
			[JsonIgnore]
			internal string UpdateConfigPath
			{
				get
				{
					if (Type.Name == AppFolderType.Web.Name)
						return UpdatePath + Path.DirectorySeparatorChar + "web.config";
					else
						return UpdatePath + Path.DirectorySeparatorChar + this.AssemblyName + ".config";
				}
			}

			[JsonIgnore]
			internal Version UpdateVersion
			{
				get
				{
					string path = UpdatePath;

					if (Type.Name == AppFolderType.Web.Name)
					{
						path = UpdatePath + Path.DirectorySeparatorChar + "bin";
					}
					return GetAssemblyVersion(UpdateAssemblyPath);
				}
			}

			[JsonIgnore]
			internal string InstalledPath
			{
				get
				{
					if (Type.Name == AppFolderType.Web.Name)
					{
						if (this.WebApplication == null) return "";
						return this.WebApplication.VirtualDirectories[0].PhysicalPath;
					}
					else if (Type.Name == AppFolderType.Service.Name)
					{
						if (!IsInstalled) return "";

						try
						{
							ManagementObject wmiService;
							wmiService = new ManagementObject("Win32_Service.Name='" + this.ServiceName + "'");
							wmiService.Get();

							// string name = wmiService["Name"] as string;
							string pathName = wmiService["PathName"] as string;

							FileInfo file = new FileInfo(CleanPath(pathName));
							return file.FullName;
							//return pathName;
						}
						catch (Exception)
						{
							return "";
						}
					}
					else
					{
						//if this is a resource we have no idea where it is located......
						return "";
					}
				}
			}

			[JsonIgnore]
			internal string InstalledConfigPath
			{
				get
				{
					if (Type.Name == AppFolderType.Web.Name)
					{
						if (this.WebApplication == null) return "";
						return this.WebApplication.VirtualDirectories[0].PhysicalPath + Path.DirectorySeparatorChar + "web.config";
					}
					else
					{
						if (this.InstalledPath == "") return "";
						string path = Path.GetDirectoryName(this.InstalledPath) + Path.DirectorySeparatorChar + this.AssemblyName + ".config";
						return path;
					}
				}
			}

			[JsonIgnore]
			internal Version InstalledVersion
			{
				get
				{
					string path = InstalledPath;

					if (path == "") return new Version();

					if (Type == AppFolderType.Web)
					{
						path = InstalledPath + Path.DirectorySeparatorChar + "bin";
					}

					string assemblyPath = path + Path.DirectorySeparatorChar + this.AssemblyName;
					return GetAssemblyVersion(assemblyPath);
				}
			}

			[JsonIgnore]
			internal bool NeedsUpdate
			{
				get
				{
					return InstalledVersion < UpdateVersion;
				}
			}

			private SqlConnectionStringBuilder _conStr;

			[JsonIgnore]
			internal SqlConnectionStringBuilder ConnectionString
			{
				get
				{
					if (_conStr == null)
					{
						ExeConfigurationFileMap configFile = new ExeConfigurationFileMap();
						Configuration config;
						//Check the installed path first so that the user does not have to change the connection string!!
						if (InstalledPath != "")
						{
							configFile.ExeConfigFilename = this.InstalledConfigPath;
							config = ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
						}
						else
						{
							configFile.ExeConfigFilename = this.UpdateConfigPath;
							config = ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
						}

						if (!File.Exists(configFile.ExeConfigFilename)) return null;
						_conStr = new SqlConnectionStringBuilder(config.ConnectionStrings.ConnectionStrings[this.ConnectionStringName].ConnectionString);
					}
					return _conStr;
				}
				set
				{
					_conStr = value;
				}
			}

			[JsonIgnore]
			internal bool UpdateRequired
			{
				get
				{
					return this.NeedsUpdate;
				}
			}

			//Web specific settings
			/// <summary>
			/// Required if the Type is Web, this is the web application name
			/// </summary>
			public string WebApplicationName { get; set; }

			/// <summary>
			/// Required if the Type is Web, this is the web application pool name.
			/// </summary>
			public string WebApplicationPoolName { get; set; }

			/// <summary>
			/// The name of the web site.  Most of the time this will be Default Web Site
			/// </summary>
			[JsonIgnore]
			internal Microsoft.Web.Administration.Site WebSite
			{
				get
				{
					return IISHelper.GetSiteByApplicationName(this.WebApplicationName);
				}
			}

			/// <summary>
			/// The web application that matches the WebApplicationName.  If this is null the service does not exist.
			/// </summary>
			[JsonIgnore]
			internal Microsoft.Web.Administration.Application WebApplication
			{
				get
				{
					return IISHelper.GetApplication(WebSite.Name, this.WebApplicationName);
				}
			}

			/// <summary>
			/// The application exists on the server
			/// </summary>
			[JsonIgnore]
			internal bool IsInstalled
			{
				get
				{
					if (Type.Name == AppFolderType.Web.Name)
						return IISHelper.ApplicationExists(this.WebApplicationName);
					else if (Type.Name == AppFolderType.Service.Name)
						return this.Service != null;
					else
						return Directory.Exists(InstalledPath);
				}
			}

			//Service specific settings
			public string ServiceName { get; set; }

			[JsonIgnore]
			internal ServiceController Service
			{
				get
				{
					return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == this.ServiceName);
				}
			}

			private string CleanPath(string path)
			{
				string invalid = new string(Path.GetInvalidPathChars());
				foreach (char c in invalid)
				{
					path = path.Replace(c.ToString(), "");
				}
				return path;
			}

			private Version GetAssemblyVersion(string path)
			{
				try
				{
					Assembly assembly = Assembly.LoadFrom(CleanPath(path));
					return assembly.GetName().Version;
				}
				catch (Exception)
				{
					return new Version();
				}
			}
		}

		/// <summary>
		/// Used as a type of string enum.  The Type of folder dictates what will happen when this is installed.  Web, Service or Resource.
		/// </summary>
		public sealed class AppFolderType
		{
			public readonly string Name;

			public static readonly AppFolderType Web = new AppFolderType("Web");
			public static readonly AppFolderType Service = new AppFolderType("Service");
			public static readonly AppFolderType Resource = new AppFolderType("Resource");

            public AppFolderType() { }

			public static AppFolderType SetType(string type)
			{
				//A little dangerous becuase it does not fit into the 3 above...
				return new AppFolderType(type);
			}

			private AppFolderType(string name)
			{
				this.Name = name;
			}

			public override string ToString()
			{
				return Name;
			}
		}
	}
}