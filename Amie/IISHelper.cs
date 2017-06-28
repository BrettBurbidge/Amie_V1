using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Amie
{
	internal class IISHelper
	{
		/// <summary>
		/// Creates the application pool if it does not exists.  If it exists we update the settings to make sure it is setup properly.
		/// </summary>
		/// <param name="applicationPoolName"></param>
		public static void CreateApplicationPool(string applicationPoolName)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				//We want to make sure that if the current app pool exists it has the correct settings.
				ApplicationPool newPool = serverManager.ApplicationPools[applicationPoolName];
				if (newPool == null)
					newPool = serverManager.ApplicationPools.Add(applicationPoolName);

				SetApplicationPoolDefaults(newPool);
				serverManager.CommitChanges();
				//newPool.Recycle();
				return;
			}
		}

		private static void SetApplicationPoolDefaults(ApplicationPool appPool)
		{
			appPool.ManagedRuntimeVersion = "v4.0";
			appPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
		}

		public static void CreateSite(string siteName, string path)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				var sites = serverManager.Sites;
				if (sites[siteName] == null)
				{
					sites.Add(siteName, "http", "*:80:", path);
					serverManager.CommitChanges();
				}
			}
		}

		public static void CreateApplication(string siteName, string applicationName, string path)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				//var site = GetSite(siteName);
				//http://stackoverflow.com/a/10386994/393159
				var site = serverManager.Sites.Where(p => p.Name.ToLower() == siteName.ToLower()).FirstOrDefault();
				var applications = site.Applications;
				if (applications["/" + applicationName] == null)
				{
					//Null ref exception:
					//http://stackoverflow.com/questions/4518186/using-servermanager-to-create-application-within-application

					applications.Add("/" + applicationName, path);
					serverManager.CommitChanges();
				}
			}
		}

		///Old STuff
		//public static void CreateApplication(string siteName, string applicationName, string path)
		//{
		//    using (ServerManager serverManager = new ServerManager())
		//    {
		//        var site = GetSite(siteName);
		//        var applications = site.Applications;
		//        if (applications["/" + applicationName] == null)
		//        {
		//            //Null ref exception: http://stackoverflow.com/questions/4518186/using-servermanager-to-create-application-within-application
		//            //You are referencing the wrong dll?

		//            applications.Add("/" + applicationName, path);
		//            serverManager.CommitChanges();
		//        }
		//    }
		//}

		public static Site GetSite(string siteName)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				var site = serverManager.Sites.FirstOrDefault(x => x.Name == siteName);
				return site;
			}
		}

		public static Site GetFirstSite()
		{
			using (ServerManager serverManager = new ServerManager())
			{
				return serverManager.Sites.FirstOrDefault();
			}
		}

		public static Application GetApplication(string siteName, string applicationName)
		{
			var site = GetSite(siteName);
			var applications = site.Applications;
			return site.Applications["/" + applicationName];
		}

		//public static void CreateVirtualDirectory(string siteName, string applicationName, string virtualDirectoryName, string path)
		//{
		//    using (ServerManager serverManager = new ServerManager())
		//    {
		//        var application = GetApplication(serverManager, siteName, applicationName);
		//        application.VirtualDirectories.Add("/" + virtualDirectoryName, path);
		//        serverManager.CommitChanges();
		//    }
		//}

		public static void SetApplicationApplicationPool(string siteName, string applicationName, string applicationPoolName)
		{

			using (ServerManager serverManager = new ServerManager())
			{
				ApplicationPool appPool = serverManager.ApplicationPools[applicationName];
				if (appPool == null) return;
				//does not work to query site outside in this case.  check this out http://stackoverflow.com/a/10386994/393159
				//var site = GetSite(siteName);

				var site = serverManager.Sites.FirstOrDefault(x => x.Name == siteName);
				if (site != null)
				{
					var app = site.Applications["/" + applicationName];
					if (app != null)
						app.ApplicationPoolName = appPool.Name;
				}
				serverManager.CommitChanges();
			}
		}

		/// <summary>
		/// Attempts to get the web site object by the application name.  If the application is not installed this function will grab the first website in the list and return that as the website.
		/// </summary>
		/// <param name="applicationName"></param>
		/// <returns></returns>
		internal static Site GetSiteByApplicationName(string applicationName)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				foreach (var site in serverManager.Sites)
				{
					var applications = site.Applications;
					if (applications["/" + applicationName] != null)
					{
						return site;
					}
				}
			}
			return GetFirstSite();
		}

		internal static bool ApplicationExists(string applicationName)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				foreach (var site in serverManager.Sites)
				{
					var applications = site.Applications;
					if (applications["/" + applicationName] != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		internal static void ChangeApplicationPath(string siteName, string applicationName, string newPath)
		{
			using (ServerManager serverManager = new ServerManager())
			{
				var sites = serverManager.Sites;
				var site = sites[siteName];
				if (site == null) throw new Exception(string.Format("The site {0} could not be found", siteName));

				var application = site.Applications["/" + applicationName];
				if (application == null) throw new Exception(string.Format("The application {0} on site {1} could not be found", applicationName, siteName));

				application.VirtualDirectories[0].PhysicalPath = newPath;
				serverManager.CommitChanges();
			}
		}

		internal static bool IISIsInstalled()
		{
			ServiceController sc = new ServiceController("World Wide Web Publishing Service");
			return sc != null;
		}

		internal static string IISStatus()
		{
			ServiceController sc = new ServiceController("World Wide Web Publishing Service");
			if (sc == null) return "Not installed";

			return sc.Status.ToString();
		}
	}
}
