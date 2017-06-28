using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Amie
{
	internal class XmlUtil
	{
		private string m_filePath;

		public XmlUtil(string filePath)
		{
			m_filePath = filePath;
		}

		/// Returns the Web.config as XmlDocument
		private XmlDocument GetWebConfig()
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.Load(m_filePath);
			return xmlDoc;
		}

		private void UpdateAuthentication(string mode)
		{
			XmlDocument xmlDoc = GetWebConfig();
			XmlNode xmlNode = xmlDoc.SelectSingleNode("//authentication/@mode");

			if (xmlNode == null)
			{
				throw new Exception("Authentication setting not found!");
			}
			xmlNode.Value = mode;
			SaveWebConfig(xmlDoc);
		}

		public string GetConnectionString()
		{
			XmlDocument xmlDoc = GetWebConfig();
			XmlNode xmlNode = xmlDoc.SelectSingleNode("//connectionStrings/add/@connectionString");

			if (xmlNode == null)
			{
				return "";
			}
			else
			{
				return xmlNode.Value;
			}
		}

		public WebsiteUtil.AuthMode GetAuthMode()
		{
			XmlDocument xmlDoc = GetWebConfig();
			XmlNode xmlNode = xmlDoc.SelectSingleNode("//authentication/@mode");

			if (xmlNode == null)
			{
				return WebsiteUtil.AuthMode.Windows;
			}
			else
			{
				if (xmlNode.Value == "Forms")
					return WebsiteUtil.AuthMode.Forms;
				else if (xmlNode.Value == "Windows")
					return WebsiteUtil.AuthMode.Windows;
				else
					return WebsiteUtil.AuthMode.Windows;
			}
		}

		/// Saves the changes to the Web.config file
		private void SaveWebConfig(XmlDocument xmlDoc)
		{
			try
			{
				XmlTextWriter writer = new XmlTextWriter(m_filePath, null);
				writer.Formatting = Formatting.Indented;
				xmlDoc.WriteTo(writer);
				writer.Flush();
				writer.Close();
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
			}
		}
	}
}
