using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amie.Conversions
{
	public interface iConversion
	{
		/// <summary>
		/// The version of the database script
		/// </summary>
		double ScriptVersion { get; }

		/// <summary>
		/// If true the conversion will run after the sql change is performed in the database.  If false the conversion will run before the sql script change.
		/// </summary>
		bool RunAfterSQLChange { get; }

        /// <summary>
        /// A place holder for update notes.  These are written in the class that is running the update to inform our future selves what we did.
        /// </summary>
        string UpdateNotes { get; }
		
		/// <summary>
		/// Function that will perform the conversion
		/// </summary>
		void PerformConversion(string scriptName, double scriptVersion, string connectonString);
	}
}
