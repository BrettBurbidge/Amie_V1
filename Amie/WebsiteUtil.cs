using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace Amie
{
	internal class WebsiteUtil
	{
		private readonly string _server;

		public WebsiteUtil(string server)
		{
			_server = server;
		}

		#region " Virtual Directories "

		/// <summary>
		/// Creates a virtual directory. If the virtual directory already exists it deletes it and recreates it.
		/// </summary>
		/// <param name="siteID">The site ID where the virtual directory should exist</param>
		/// <param name="virDirName">The virtual directory name, I.E. CEDC or SP</param>
		/// <param name="path">The root of the virtual directory, where the files exist.</param>
		/// <param name="appPoolName">The application pool that this virtual directory runs on</param>
		/// <param name="authMode">The Authentication Mode, Windows or Forms</param>
		/// <returns></returns>
		/// <remarks></remarks>
		public bool CreateVirtualDir(string siteID, string virDirName, string path, string appPoolName, AuthMode authMode)
		{
			System.DirectoryServices.DirectoryEntry IISSchema =
			  new System.DirectoryServices.DirectoryEntry("IIS://" + _server + "/Schema/AppIsolated");
			bool CanCreate = !(IISSchema.Properties["Syntax"].Value.ToString().ToUpper() == "BOOLEAN");
			IISSchema.Dispose();

			if (CanCreate)
			{
				bool PathCreated = false;
				try
				{
					System.DirectoryServices.DirectoryEntry IISAdmin =
					  new System.DirectoryServices.DirectoryEntry(string.Format("IIS://{0}/W3SVC/{1}/Root", _server, siteID));
					//make sure folder exists
					if (!System.IO.Directory.Exists(path))
					{
						System.IO.Directory.CreateDirectory(path);
						PathCreated = true;
					}

					//If the virtual directory already exists then delete it
					foreach (System.DirectoryServices.DirectoryEntry VD in IISAdmin.Children)
					{
						if (VD.Name == virDirName)
						{
							IISAdmin.Invoke("Delete", new string[]
              {
                VD.SchemaClassName,
                virDirName
              });
							IISAdmin.CommitChanges();
							break; // TODO: might not be correct. Was : Exit For
						}
					}

					//Create and setup new virtual directory
					System.DirectoryServices.DirectoryEntry VDir = IISAdmin.Children.Add(virDirName, "IIsWebVirtualDir");

					VDir.Properties["Path"][0] = path;
					VDir.Properties["AppFriendlyName"][0] = virDirName;
					VDir.Properties["EnableDirBrowsing"][0] = false;
					VDir.Properties["AccessRead"][0] = true;
					VDir.Properties["AccessExecute"][0] = true;
					VDir.Properties["AccessWrite"][0] = false;
					VDir.Properties["AccessScript"][0] = true;
					VDir.Properties["AuthNTLM"][0] = true;
					VDir.Properties["EnableDefaultDoc"][0] = true;
					VDir.Properties["DefaultDoc"][0] = "default.htm,default.aspx,default.asp";
					VDir.Properties["AspEnableParentPaths"][0] = true;
					VDir.Properties["AuthFlags"][0] = GetAuthFlags(authMode);

					VDir.CommitChanges();

					//the following are acceptable params
					//INPROC = 0
					//OUTPROC = 1
					//POOLED = 2
					VDir.Invoke("AppCreate", 1);
					AssignVirtualDirectoryToAppPool(siteID, virDirName, appPoolName);
				}
				catch (Exception Ex)
				{
					if (PathCreated)
					{
						System.IO.Directory.Delete(path);
					}
					throw Ex;
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		public int GetAuthFlags(AuthMode mode)
		{
			//~ AuthFlags 4 = Anonymous Off, Impersonation On, Forms Off, Windows On
			if (mode == AuthMode.Windows)
			{
				return 4;
			}
			else
			{
				//~ AuthFlag 1 = Anonymous On, Impersonation On, Forms On, Windows Off
				return 1;
			}
		}

		public string GetVirtualDirectoryProperties(string siteid, string virtualDirectoryName)
		{
			// Dim virDir As DirectoryEntry = GetVirtualDirectoryEntry(siteid, virtualDirectoryName)
			string str = "";
			//For i As Integer = 0 To virDir.Properties.PropertyNames.Count - 1
			//    str &= "Name: " & virDir.Properties.PropertyNames(i).ToString & " Value: "
			//    If virDir.Properties.Item(virDir.Properties.PropertyNames(i).ToString).Value IsNot Nothing Then
			//        str &= virDir.Properties.Item(virDir.Properties.PropertyNames(i).ToString).Value.ToString & vbCrLf
			//    Else
			//        str &= "Nothing" & vbCrLf
			//    End If
			//Next
			str = "Not implemented";
			return str;
		}

		private System.DirectoryServices.DirectoryEntry GetVirtualDirectoryEntry(string siteId, string virtualDirectoryName)
		{
			System.DirectoryServices.DirectoryEntry virDir =
			  new System.DirectoryServices.DirectoryEntry(string.Format("IIS://{0}/W3SVC/{1}/Root/{2}", _server, siteId,
				virtualDirectoryName));
			return virDir;
		}

		public bool AssignVirtualDirectoryToAppPool(string siteId, string virDir, string appPoolName)
		{
			try
			{
				DirectoryEntry objVdir = GetVirtualDirectoryEntry(siteId, virDir);
				string strClassName = objVdir.SchemaClassName.ToString();
				if (strClassName.EndsWith("VirtualDir"))
				{
					object[] objParam =
          {
            0,
            appPoolName,
            true
          };
					objVdir.Properties["AppIsolated"][0] = "2";
					objVdir.CommitChanges();
					objVdir.Invoke("AppCreate3", objParam);
					return true;
				}
				else
				{
					return false;
				}
			}
			catch (Exception)
			{
				return false;
			}
		}

		public List<IISEntry> VirtualDirList(string siteID)
		{

			List<IISEntry> wsList = new List<IISEntry>();
			IISEntry entry = null;
			System.DirectoryServices.DirectoryEntry iisRoot = GetWebSiteDirectoryEntry(siteID);

			foreach (System.DirectoryServices.DirectoryEntry e in iisRoot.Children)
			{
				if (e.SchemaClassName.ToUpper() == "IISWEBVIRTUALDIR")
				{
					entry = new IISEntry(e.Name, e.Name);
					// entry.AppPoolID = e.Properties("AppPoolId")(0).ToString
					wsList.Add(entry);
				}
			}
			return wsList;

		}

		public string GetVirtualDirectoryAppPool(string siteID, string virDirName)
		{
			DirectoryEntry objVdir = GetVirtualDirectoryEntry(siteID, virDirName);
			string dirClassName = null;
			try
			{
				dirClassName = objVdir.SchemaClassName.ToString();
			}
			catch (Exception)
			{
				//means that the virtual directory does not exist.
				return "";
			}
			if (dirClassName.EndsWith("VirtualDir"))
			{
				return objVdir.Properties["AppPoolId"][0].ToString();
			}
			else
			{
				return "";
			}
		}

		public enum AuthMode
		{
			Windows,
			Forms
		}

		#endregion

		#region " Web Sites "

		private DirectoryEntry GetWebSiteDirectoryEntry(string siteID)
		{
			System.DirectoryServices.DirectoryEntry webSiteDirectory =
			  new System.DirectoryServices.DirectoryEntry(string.Format("IIS://{0}/W3SVC/{1}/Root", _server, siteID));
			return webSiteDirectory;
		}

		public string GetWebSiteFilePath(string siteID)
		{
			string path = string.Empty;
			System.DirectoryServices.DirectoryEntry webSiteRoot = GetWebSiteDirectoryEntry(siteID);

			if (webSiteRoot.Properties["Path"].Count > 0)
			{
				path = webSiteRoot.Properties["Path"][0].ToString();
			}

			if (path == string.Empty)
			{
				path = "PATH NOT FOUND";
			}

			return path;
		}

		public enum WebsiteStatusValue
		{
			Start = 2,
			Stop = 4,
			Pause = 6
		}

		public bool SetWebsiteStatus(string siteID, WebsiteStatusValue state)
		{
			// Try
			ConnectionOptions connectionOptions = new ConnectionOptions();

			//If m_username <> "" Then
			//    connectionOptions.Username = m_username
			//    connectionOptions.Password = m_password
			//Else
			connectionOptions.Impersonation = ImpersonationLevel.Impersonate;
			//End If

			ManagementScope managementScope = new ManagementScope("\\\\" + _server + "\\root\\microsoftiisv2",
			  connectionOptions);

			managementScope.Connect();
			if (managementScope.IsConnected == false)
			{
				//  MessageBox.Show("Could not connect to WMI namespace " + managementScope.Path, "Connect Failed")
				return false;
			}
			else
			{
				SelectQuery selectQuery = new SelectQuery(("Select * From IIsWebServer Where Name = 'W3SVC/" + (siteID + "'")));
				ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, selectQuery);
				foreach (ManagementObject objMgmt in managementObjectSearcher.Get())
				{
					objMgmt.InvokeMethod(state.ToString(), new object[(0)]);
				}
			}
			return true;
		}

		public string GetWebsiteStatus(int siteID)
		{
			string result = "unknown";
			DirectoryEntry root =
			  new System.DirectoryServices.DirectoryEntry(string.Format("IIS://{0}/W3SVC/{1}", _server, siteID));
			PropertyValueCollection pvc = default(PropertyValueCollection);

			pvc = root.Properties["ServerState"];
			if ((pvc.Value != null))
			{
				switch (Convert.ToInt32(pvc.Value))
				{
					case (int)WebsiteStatusValue.Start:
						result = "Running";
						break;
					case (int)WebsiteStatusValue.Stop:
						result = "Stopped";
						break;
					case (int)WebsiteStatusValue.Pause:
						result = "Paused";
						break;
					default:
						result = "Unknown";
						break;
				}

			}
			return result;
		}

		#endregion

		#region " Application Pools "

		private System.DirectoryServices.DirectoryEntry GetAppPoolDirectoryEntry()
		{
			System.DirectoryServices.DirectoryEntry appPoolRoot =
			  new System.DirectoryServices.DirectoryEntry(string.Format("IIS://{0}/W3SVC/AppPools", _server));
			return appPoolRoot;
		}

		private string GetAppPoolID(string appPoolName)
		{
			System.DirectoryServices.DirectoryEntry appPMetabase = GetAppPoolDirectoryEntry();
			//If the virtual directory already exists then delete it
			foreach (System.DirectoryServices.DirectoryEntry ap in appPMetabase.Children)
			{

				if (ap.Name == appPoolName)
				{
				}
			}
			return "0";
		}

		public void CreateAppPool(string appPoolName)
		{
			System.DirectoryServices.DirectoryEntry appPMetabase = GetAppPoolDirectoryEntry();
			bool appPoolExists = false;
			//If the virtual directory already exists then delete it
			foreach (System.DirectoryServices.DirectoryEntry ap in appPMetabase.Children)
			{
				if (ap.Name == appPoolName)
				{
					appPoolExists = true;
					break; // TODO: might not be correct. Was : Exit For
				}
			}
			if (!appPoolExists)
			{
				appPMetabase.Children.Add(appPoolName, "IIsApplicationPool");
				appPMetabase.CommitChanges();
				//appPMetabase.Invoke("AppCreate", 1)
			}

		}

		public List<IISEntry> GetAppPoolList()
		{
			List<IISEntry> wsList = new List<IISEntry>();
			IISEntry entry = null;
			System.DirectoryServices.DirectoryEntry iisRoot = GetAppPoolDirectoryEntry();

			foreach (System.DirectoryServices.DirectoryEntry e in iisRoot.Children)
			{
				entry = new IISEntry(e.Name, e.Name);
				wsList.Add(entry);
			}
			return wsList;
		}

		#endregion

		#region " IIS Entry Object Helper "

		public class IISEntry
		{
			private string m_ID;
			private string m_Name;

			public IISEntry(string id, string name)
			{
				m_ID = id;
				m_Name = name;
			}

			public string ID
			{
				get { return m_ID; }
				set { m_ID = value; }
			}

			public string Name
			{
				get { return m_Name; }
				set { m_Name = value; }
			}

			private string m_appPoolID;

			public string AppPoolID
			{
				get { return m_appPoolID; }
				set { m_appPoolID = value; }
			}
		}

		#endregion

	}
}
