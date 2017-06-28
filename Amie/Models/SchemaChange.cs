using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amie.Models
{
	public partial class SchemaChange
	{
		[PetaPoco.ResultColumn]
		public string ScriptContent { get; set; }

		[PetaPoco.ResultColumn]
		public List<string> ScriptLines { get; set; }

		[PetaPoco.ResultColumn]
		public string ScriptErrors { get; set; }

		[PetaPoco.ResultColumn]
		public string Status { get; set; }

		/// <summary>
		/// Comment in the script, if it has one.
		/// </summary>
		[PetaPoco.ResultColumn]
		public string Comment { get; set; }

		/// <summary>
		/// Set this to false if the database change being ran should end up in the schemaChange table.
		/// This will only be false if the script ran is Inserting data, if the script is changing schema then this should be true.
		/// </summary>
		[PetaPoco.ResultColumn]
		public bool LogThisChange { get; set; }

		public static string Status_Failed = "Failed";
		public static string Status_Success = "Success";
		public static string Status_NotRan = "Not Ran";

	}
}
