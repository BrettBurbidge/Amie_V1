using System;
using System.IO;
using System.Reflection;
using System.Web;
using System.IO.Compression;
using System.Diagnostics;

namespace Amie
{
    public class Updater
	{
        #region Server Side Update Code
                
        private static string AppInfoFilePath = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "AppInfo.json";

        public static InstallResult DepolyDatabaseUpdateOnly(string connectionString, System.Reflection.Assembly executingAssembly)
        {
            InstallResult result = new InstallResult();

            try
            {
                ShowAppStartInfo();
                result = PerformDatabaseUpdate(connectionString, executingAssembly);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.SetMessage("Database update failed. {0}", ex.ToString());
            }
            return result;
        }

        public static InstallResult DeployUpdate(string folderNameToUpdate, string connectionString)
        {
            InstallResult result = new InstallResult();
            try
            {
                if (File.Exists(AppInfoFilePath))
                {
                    result = PerformSingleUpdateFromAppInfoFile(AppInfoFilePath, folderNameToUpdate, connectionString);
                }
                else
                {
                    result.Success = false;
                    result.Message = string.Format("The AppInfoFile was not found at {0} so Amie does not know what to do.",AppInfoFilePath);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.SetMessage("Software update failed. {0}", ex.ToString());
            }
            return result;
        }

        public static InstallResult DeployFullUpdate(System.Reflection.Assembly executingAssembly)
		{
			InstallResult result = new InstallResult();
			try
			{
				ShowAppStartInfo();

				if (File.Exists(AppInfoFilePath))
				{
					result = PerformUpdateFromAppInfoFile(AppInfoFilePath, executingAssembly);
				}
                else
                {
                    result.Success = false;
                    result.Message = string.Format("The AppInfoFile was not found at {0} so Amie does not know what to do.", AppInfoFilePath);
                }
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.SetMessage("Software update failed. {0}", ex.ToString());
			}
			return result;
		}

		private static InstallResult PerformDatabaseUpdate(string connectionString, Assembly executingAssembly)
		{
			DBUpdater update = new DBUpdater(executingAssembly);
			var result = update.UpdateFromConnectionString(connectionString);
			return result;
		}

        private static InstallResult PerformSingleUpdateFromAppInfoFile(string appInfoFilePath, string folderNameToUpdate, string connectionString)
        {
            var appInfo = Amie.AppInfo.LoadFromFile(appInfoFilePath);
            var result = new InstallResult();

            //get the [folder] to update
            var folder = appInfo.AppFolderFromName(folderNameToUpdate);

            if (folder == null)
            {
                result.Success = false;
                result.SetMessage("Update failed: the app to udpate with the name {0} was not found in the AppInfo.json file.  An AppFolder configuration is required to update this product.",folderNameToUpdate);
                return result;
            }

            Amiedater a = new Amiedater(folder);

            if (folder.Type.Name == AppInfo.AppFolderType.Service.Name)
            {
               result = a.PerformServiceUpdate(connectionString);
            } else if (folder.Type.Name == AppInfo.AppFolderType.Web.Name)
            {
               result = a.PerformWebUpdate(connectionString);
            } else
            {
                //Must be a resource, such as a command line program or something.  We don't know where the old one is installed but we know where the new one is so at least we can update the connection string....
                result = a.PerformResouceUpdate(connectionString);
            }

            return result;
        }

		private static InstallResult PerformUpdateFromAppInfoFile(string appInfoFilePath, System.Reflection.Assembly executingAssembly)
		{
			var appInfo = Amie.AppInfo.LoadFromFile(appInfoFilePath);
			var result = new InstallResult();

            string connectionString = GetConnectionString(appInfo);

            if (string.IsNullOrEmpty(connectionString))
            {
                result.Success = false;
                result.Message = "Update failed the connection string was not found.";
                return result;
            }

            DBUpdater dbUpdate = new DBUpdater(executingAssembly);
            result = dbUpdate.UpdateFromConnectionString(connectionString);

			if (!result.Success)
			{
				return result;
			}

			foreach (var item in appInfo.AppFolders)
			{
				Amiedater a = new Amiedater(item);

				if (item.Type.Name == AppInfo.AppFolderType.Web.Name)
				{
					result = a.PerformWebUpdate();
				}
				else if (item.Type.Name == AppInfo.AppFolderType.Service.Name)
				{
					result = a.PerformServiceUpdate();
				}
				else
				{
					//Must be a resource, such as a command line program or something.  We don't know where the old one is installed but we know where the new one is so at least we can update the connection string....
					result = a.PerformResouceUpdate(connectionString);
				}
			}

			return result;
		}

        /// <summary>
        /// Returns the first connection string found.  Check the web first, then the service.
        /// </summary>
        /// <param name="appInfo"></param>
        /// <returns></returns>
        private static string GetConnectionString(AppInfo appInfo)
        {
            string connectionString = "";
           foreach (var item in appInfo.AppFolders)
            {
                Amiedater a = new Amiedater(item);

                if (item.Type.Name == AppInfo.AppFolderType.Web.Name)
                {
                    connectionString = a.GetConnectionString();
                    if (connectionString != "") return connectionString;
                }
                else if (item.Type.Name == AppInfo.AppFolderType.Service.Name)
                {
                    connectionString = a.GetConnectionString();
                    if (connectionString != "") return connectionString;
                }
            }
            return connectionString;
        }

		private static void ShowAppStartInfo()
		{
			string usage = "This application is normally ran by developers or in the background by an installer application.  Below is the usage if you are still interested. ";
			usage += "\r\n\r\n" + "Usage: YourInstaller.exe <database connection string>";
			usage += "\r\n\r\n" + "If the <database connection string> is supplied then only the database will be updated.";
			usage += "\r\n\r\n" + "If there is a file called 'AppInfo.json' in the same directory as YourInstaller.exe Amie will use this file to update.";
			usage += "\r\n\r\n" + "If there is no <database connection string> supplied and no 'AppInfo.json' file found this program will exit without action.";

			usage += "\r\n\r\n" + "To learn more about how to configure the 'AppInfo.json' file please see this website www.?.com (someday!) or talk to Brett Burbidge";

			Logger.Info(usage);
		}

        #endregion

        #region Update Trigger code

        public static InstallResult SendUpdateTrigger(HttpPostedFileBase file)
        {
            InstallResult result = new InstallResult();

            try
            {
                //Check to make sure its a zip file.
                if (!file.FileName.EndsWith(".zip"))
                {
                    result.Success = false;
                    result.Message = "The update file must be a zip file.";
                    return result;
                }

                //Read the bytes out of the files
                byte[] fileBytes;
                using (var reader = new System.IO.BinaryReader(file.InputStream))
                {
                    fileBytes = reader.ReadBytes(file.ContentLength);
                }

                //Create the update setings
                UpdateSettings settings = new UpdateSettings();
                settings.Key = AppSettings.PrivateKey;
                settings.UpdateFile = fileBytes;

                //Send the command to the server.
                Amie.Client.AsyncClient client = new Amie.Client.AsyncClient();
                client.StartClient(AppSettings.TCPClientAddress, AppSettings.TCPClientPort);
                byte[] bytesToSend = UpdateSettings.Serialize(settings);
                client.SendBytes(bytesToSend, true);

                //TODO: Need to buffer server response and send it back to the calling function....
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.ToString();
                return result;
            }
        }

        internal static void ExtractUpdateAndRun(UpdateSettings updateSettings)
        {
            Amie.Logger.Info("Checking package integrity...");
            if (updateSettings.Key != AppSettings.PrivateKey)
            {
                Amie.Logger.Error("Package integrity check failed. Update not processed.");
                return;
            }

            Amie.Logger.Info("Check for update file...");
            if (updateSettings.UpdateFile == null)
            {
                Amie.Logger.Error("Update file not found in package.  Update not processed.");
                return;
            }

            MemoryStream stream = new MemoryStream(updateSettings.UpdateFile);
            ZipArchive archive = new ZipArchive(stream);
            AppInfo appInfo = GetAppInfo(archive);

            if (appInfo == null)
            {
                Amie.Logger.Error("The update package is not structured correctly. Is there a file named AppInfo.json in the package? Update not processed.");
                return;
            }


            if (!Directory.Exists(AppSettings.BaseUpdatePath))
                Directory.CreateDirectory(AppSettings.BaseUpdatePath);

            string defaultSavedUpdateDirectory = AppSettings.BaseUpdatePath + Path.DirectorySeparatorChar + appInfo.ProductName + "_" + appInfo.Version;

            string savedUpdateDirectory = GetSavedUpdateDirectory(defaultSavedUpdateDirectory);
            string savedUpdateFileName = savedUpdateDirectory + ".zip";

            Amie.Logger.Info("Saving update package to {0}.", savedUpdateFileName);
            File.WriteAllBytes(savedUpdateFileName, updateSettings.UpdateFile);

            Amie.Logger.Info("Creating the unzip directory {0}.", savedUpdateDirectory);
            Directory.CreateDirectory(savedUpdateDirectory);

            Amie.Logger.Info("Unzipping update package.");
            ZipFile.ExtractToDirectory(savedUpdateFileName, savedUpdateDirectory);

            //If anything fails below here we must delete the update directory so that the user can try again.  The ExtractToDirectory will not work if there are matching files in the folder.
            try
            {
                Amie.Logger.Info("Executing update executable {0}.", appInfo.UpdateExecutableName);
                string updateExeLocation = GetUpdateExecutableLocation(appInfo, savedUpdateDirectory);
                if (updateExeLocation != "")
                {
                    var result = RunUpdateCommand(updateExeLocation);
                    if (!result.Success)
                    {
                        Amie.Logger.Error(result.Message);
                        RecursiveDelete(savedUpdateDirectory);
                    }
                }
                else
                {
                    Amie.Logger.Error("The update executable {0} could not be found at {1}. Update not processed.", appInfo.UpdateExecutableName, updateExeLocation);
                    RecursiveDelete(savedUpdateDirectory);
                }
            }
            catch (Exception)
            {
                RecursiveDelete(savedUpdateDirectory);
                throw;
            }

            return;
        }

        private static string GetSavedUpdateDirectory(string defaultPath)
        {
            if (!Directory.Exists(defaultPath)) return defaultPath;

            defaultPath = defaultPath + DateTime.Now.ToString("-yyyy-MM-dd-HH_mm_ss");

            return GetSavedUpdateDirectory(defaultPath);
        }

        private static AppInfo GetAppInfo(ZipArchive archive)
        {
            Stream unzippedJsonFile;
            AppInfo appInfo = null;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.Contains("AppInfo.json"))
                {
                    MemoryStream memoryStream = new MemoryStream();
                    unzippedJsonFile = entry.Open();
                    unzippedJsonFile.CopyTo(memoryStream);
                    appInfo = Amie.AppInfo.LoadFromStream(memoryStream);
                    return appInfo;
                }
            }
            return appInfo;
       }

        private static string GetUpdateExecutableLocation(AppInfo appInfo, string savedUpdateDirectory)
        {
            //Recursively look through the file system for the executable name and return the full path.
            var allFiles = Directory.GetFiles(savedUpdateDirectory, appInfo.UpdateExecutableName, SearchOption.AllDirectories);
            //There should only be one..... SHOULD
            if (allFiles.Length > 0)
                return allFiles[0];
            else
                return "";
        }

        public static void RecursiveDelete(string dirToDelete)
        {
            string[] files = null;

            if (dirToDelete.Substring(dirToDelete.Length - 1, 1) != Path.DirectorySeparatorChar.ToString())
            {
                dirToDelete = dirToDelete + Path.DirectorySeparatorChar;
            }

            files = Directory.GetFileSystemEntries(dirToDelete);
            foreach (string f in files)
            {
                //Sub directories
                if ((Directory.Exists(f)))
                {
                    RecursiveDelete(f);
                    Directory.Delete(f);
                    //Files in directory
                }
                else
                {
                    File.Delete(f);
                }
            }
        }

        private static InstallResult RunUpdateCommand(string updateExeLocation)
        {
            var result = new InstallResult();

            Process p = new Process();

            FileInfo fileInfo = new FileInfo(updateExeLocation);

            try
            {
                
                p.StartInfo.FileName = fileInfo.FullName;
                p.StartInfo.Arguments = "fullupdate";
                //Makes the working directory of TrafficUpdate the actual working directory instead of where this service is running.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.WorkingDirectory = fileInfo.DirectoryName;
                p.Start();

                p.WaitForExit(int.MaxValue);

                result.Message = "Update ran successfully.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.SetMessage("Update failed. {0}.", ex.ToString());
            }

            return result;
        }

        #endregion
    }
}