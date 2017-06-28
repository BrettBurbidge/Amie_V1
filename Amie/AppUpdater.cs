using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace Amie
{
	internal class Amiedater
	{
		private AppInfo.AppFolder FolderToUpdate;

		internal Amiedater(AppInfo.AppFolder folder)
		{
			FolderToUpdate = folder;
		}

        internal InstallResult PerformWebUpdate(string connectionString = "")
        {
            var result = new InstallResult();

            try
            {
                //update existing web
                Logger.Info("Updating web app {0}", FolderToUpdate.WebApplicationName);

                Logger.Info("Creating application pool: {0}", FolderToUpdate.WebApplicationPoolName);
                IISHelper.CreateApplicationPool(FolderToUpdate.WebApplicationPoolName);

                Logger.Info("Creating application {0} in website {1}", FolderToUpdate.WebApplicationName, FolderToUpdate.WebSite.Name);
                IISHelper.ChangeApplicationPath(FolderToUpdate.WebSite.Name, FolderToUpdate.WebApplicationName, FolderToUpdate.UpdatePath);

                Logger.Info("Setting Application Pool {0} in application {1}", FolderToUpdate.WebApplicationPoolName, FolderToUpdate.WebApplicationName);
                IISHelper.SetApplicationApplicationPool(FolderToUpdate.WebSite.Name, FolderToUpdate.WebApplicationName, FolderToUpdate.WebApplicationPoolName);

                //install new web 
                if (FolderToUpdate.WebApplication == null)
                {
                    if (connectionString == "")
                    {
                        Logger.Error("The connection string was not passed into the arguments when the update started. The install was successfull but the connection string was not updated.");
                    }
                    else
                    {
                       result = SetConnectionString(FolderToUpdate.InstalledConfigPath, connectionString);
                    }
                }
                else
                {
                    //In an update we don't copy anything or move anything except settings. Wherever the installer(human) puts the folder is where it will live. We just keep moving the path of the web and path of the service to the new spot.
                    result = CopyConfigSettings(FolderToUpdate.InstalledConfigPath, FolderToUpdate.UpdateConfigPath);
                }

                Logger.Info(FolderToUpdate.Name + " Update Finished!");
            }
            catch (Exception ex)
            {
                Logger.Info(FolderToUpdate.Name + " Web Update Failed.  Here is the error {0}.", ex.Message);
                result.Success = false;
                result.SetMessage("Web update failed. Here is the error {0}.", ex.Message);
            }
            return result;
        }

		internal InstallResult PerformServiceUpdate(string connectionString = "")
		{
            var result = new InstallResult();
            Logger.Info("Updating service {0} at {1}", FolderToUpdate.ServiceName, FolderToUpdate.UpdatePath);

            //Install new service
            if (FolderToUpdate.Service == null)
            {
               result = InstallNewService();

                if (result.Success)
                {
                    if (connectionString == "")
                    {
                        Logger.Error("The connection string was not passed into the arguments when the update started. The new service was installed but the connection string to the database was not set.");
                    }

                    result = SetConnectionString(FolderToUpdate.InstalledConfigPath, connectionString);

                    StartService();
                }

                Logger.Info("Servied install finished!");
                return result;                
            }

            //Update existing service
            if (StopService())
            {
                CopyConfigSettings(FolderToUpdate.InstalledConfigPath, FolderToUpdate.UpdateConfigPath);

                result = UninstallOldService();

                if (result.Success)
                {
                    result = InstallNewService();
                }

                //If this fails, don't error, just keep moving on, nothing to see here.
                StartService();

                Logger.Info(FolderToUpdate.ServiceName + " Service update finshed!");
            }
            else
            {
                Logger.Error("The service {0} could not be stopped.", FolderToUpdate.ServiceName);
                result.Success = false;
                result.SetMessage("Installation failed. The service {0} could not be stopped.", FolderToUpdate.ServiceName);
            }

            return result;
		}

        internal InstallResult PerformResouceUpdate(string connectionString)
        {
            var result = new InstallResult();
            try
            {
                SetConnectionString(FolderToUpdate.UpdateConfigPath, connectionString);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.SetMessage("Updating the configuration file of the Resource config file {0} failed.  Here is the error {1}.", FolderToUpdate.UpdatePath, ex.Message);
            }
            
            return result;
        }

        internal string GetConnectionString()
        {
            if (string.IsNullOrEmpty(FolderToUpdate.InstalledConfigPath)) return ""; //empty strings here throw an exception.

            ExeConfigurationFileMap installedConfigFile = new ExeConfigurationFileMap();
            installedConfigFile.ExeConfigFilename = FolderToUpdate.InstalledConfigPath;
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(installedConfigFile, ConfigurationUserLevel.None);
            return config.ConnectionStrings.ConnectionStrings[FolderToUpdate.ConnectionStringName].ConnectionString;
        }
        
		internal InstallResult CopyConfigSettings(string source, string destination)
		{
			var result = new InstallResult();

			if (!File.Exists(source))
			{
				result.Success = false;
				result.SetMessage("The source configuration file {0} could not be found.", source);
				return result;
			}

			if (!File.Exists(destination))
			{
				result.Success = false;
				result.SetMessage("The destination configuration file {0} could not be found.", destination);
				return result;
			}

			ExeConfigurationFileMap installedConfigFile = new ExeConfigurationFileMap();
			installedConfigFile.ExeConfigFilename = source; //_info.WebConfigPathInstalled;
			Configuration configInstalled = ConfigurationManager.OpenMappedExeConfiguration(installedConfigFile, ConfigurationUserLevel.None);

			ExeConfigurationFileMap updateConfigFile = new ExeConfigurationFileMap();
			updateConfigFile.ExeConfigFilename = destination; //_info.WebConfigPathUpdate;
			Configuration configUpdate = ConfigurationManager.OpenMappedExeConfiguration(updateConfigFile, ConfigurationUserLevel.None);

			foreach (KeyValueConfigurationElement appSetting in configInstalled.AppSettings.Settings)
			{
				if (configUpdate.AppSettings.Settings[appSetting.Key] != null)
				{
					configUpdate.AppSettings.Settings[appSetting.Key].Value = appSetting.Value;
				}
			}

			configUpdate.ConnectionStrings.ConnectionStrings[FolderToUpdate.ConnectionStringName].ConnectionString = FolderToUpdate.ConnectionString.ConnectionString;

			configUpdate.Save(ConfigurationSaveMode.Modified);
			Logger.Info("Copy Config Settings: Done");
			return result;
		}

		private InstallResult SetConnectionString(string configFilePath, string connectionString)
		{
            var result = new InstallResult();
            try
            {
                ExeConfigurationFileMap updateConfigFile = new ExeConfigurationFileMap();
                updateConfigFile.ExeConfigFilename = configFilePath;
                Configuration configUpdate = ConfigurationManager.OpenMappedExeConfiguration(updateConfigFile, ConfigurationUserLevel.None);
                configUpdate.ConnectionStrings.ConnectionStrings[FolderToUpdate.ConnectionStringName].ConnectionString = connectionString;
                configUpdate.Save(ConfigurationSaveMode.Modified);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.SetMessage("Setting the connection string to {0} failed.  The error is {1}.", configFilePath, ex.Message);
            }
            return result;
		}

		private bool StopService()
		{
			//Service does not exist
			if (FolderToUpdate.Service == null) return true;

			if (FolderToUpdate.Service.Status == System.ServiceProcess.ServiceControllerStatus.Stopped) return true;

			FolderToUpdate.Service.Stop();
			return true;
		}

		private bool StartService()
		{
			//Service does not exist
			if (FolderToUpdate.Service == null) return true;

			if (FolderToUpdate.Service.Status == System.ServiceProcess.ServiceControllerStatus.Running) return true;

			try
			{
				FolderToUpdate.Service.Start();
			}
			catch (Exception ex)
			{
				Logger.Info("The service was installed but not started becuase of an error: {0}.", ex.ToString());
			}

			return true;
		}

		private InstallResult InstallNewService()
		{
			Logger.Info("Installing Service at {0}", FolderToUpdate.UpdateAssemblyPath);
			return CallInstallUtil(new string[] { "/LogFile=InstallTrafficService.log", FolderToUpdate.UpdateAssemblyPath });
		}

		private InstallResult UninstallOldService()
		{
            var result = new InstallResult();
            //Another way to do it if InstallUtil.exe is not installed on the system, but I could never get it to work 2/21/2015.  Can be used to call install as well without the /u
            //System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { Assebly.GetExecutingAsseembly().Location });

            //Attempt to uninstall from installed location.  If this does not work then use the update location to try uninstall. If this is already installed it should not matter.
            try
            {
                //Old Service does not exist
                if (FolderToUpdate.Service == null) return result;

                Logger.Info("Uninstalling Service at {0}", FolderToUpdate.InstalledPath);
                result = CallInstallUtil(new string[] { "/u", "/LogFile=UninstallTrafficService.log", FolderToUpdate.InstalledPath });
            }
            catch (Exception)
            {
                try
                {
                    Logger.Info("Uninstalling the service from the installed location {0} failed. Is possible the old files were removed. So we are trying at the update location {1}.", FolderToUpdate.InstalledPath, FolderToUpdate.UpdatePath);
                    result = CallInstallUtil(new string[] { "/u", "/LogFile=UninstallTrafficService.log", FolderToUpdate.UpdatePath });
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.SetMessage("Uninstalling the old service failed. Installation was stopped.  Here is the error {0}.", ex.Message);
                }
            }
            return result;
		}

		private static string InstallUtilPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

		private InstallResult CallInstallUtil(string[] installUtilArguments)
		{
            var result = new InstallResult();

			Process proc = new Process();
			proc.StartInfo.FileName = Path.Combine(InstallUtilPath, "installutil.exe");
			proc.StartInfo.Arguments = String.Join(" ", installUtilArguments);
			proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.UseShellExecute = false;

			proc.Start();
			string outputResult = proc.StandardOutput.ReadToEnd();
			proc.WaitForExit();

			//  ---check result---
			if (proc.ExitCode != 0)
			{
                result.SetMessage("Error with the service.  Here is the error {0}", outputResult);
				Logger.Error("Error with the service.  Here is the error {0}", outputResult);
                //installResult.Success = false;
                //installResult.Message = outputResult;
                result.Success = false; 
			}

			return result;
		}
	}
}