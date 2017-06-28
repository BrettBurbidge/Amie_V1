using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amie
{
	public class InstallResult
	{
		/// <summary>
		/// Assumes success.  Success is set to true as a default;  
		/// </summary>
		public InstallResult()
		{
			Success = true;
		}
		public bool Success { get; set; }
		public string Message { get; set; }

		public void SetMessage(string message, params string[] args)
		{
			this.Message += string.Format(message, args) + "\r\n";
		}
	}
}
