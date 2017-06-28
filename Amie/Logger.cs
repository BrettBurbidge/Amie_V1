using System;
using System.IO;
using System.Reflection;

namespace Amie
{
    internal static class Logger
	{
        
        
        internal static void Info(string message, params string[] args)
		{
			WriteEntry(string.Format(message, args), "Info");
		}

        internal static void Error(string message, params string[] args)
		{
			WriteEntry(string.Format(message, args), "Error");
		}

        static void WriteEntry(string message, string severity)
        {
            try
            {
                string theMessage = string.Format("\r\n{0}-{1}: {2}", severity, DateTime.Now.ToString(), message);
                Console.WriteLine(theMessage);

                string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string logfilePath = Path.Combine(assemblyFolder, "Amie_Service_Log.txt");
                System.IO.File.AppendAllText(logfilePath, theMessage);
            }
            catch (Exception ex)
            {
                AddEventLogEntry(string.Format("Problem logging this message.  Might be a file permissions problem. The message is {0}.  The exception is {1}.", message, ex.Message), severity);
            }

        }

        static void AddEventLogEntry(string message, string severity)
        {
            string logSource = Amie.AppSettings.ProductName;
            string logName = "Application";
            System.Diagnostics.EventLog eventLogTraffic = new System.Diagnostics.EventLog();

            if (!System.Diagnostics.EventLog.SourceExists(logSource))
            {
                System.Diagnostics.EventLog.CreateEventSource(logSource, logName);
            }
            eventLogTraffic.Source = logSource;
            eventLogTraffic.Log = logName;

            if (severity.ToLower().Equals("info"))
                eventLogTraffic.WriteEntry(message, System.Diagnostics.EventLogEntryType.Information);
            else
                eventLogTraffic.WriteEntry(message, System.Diagnostics.EventLogEntryType.Error);
        }

    }
}