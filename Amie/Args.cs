using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amie
{
    public class Args
    {
        private string[] _args;
        public Args(string[] args)
        {
            _args = args;
            ConfiguredArguments = new List<Argument>();
            ConfiguredArguments.Add(new Argument("help", "Shows the usage message.", ShowUsage));
        }

        public void Run()
        {
            BuildRuntimeArguments();

            foreach (Argument item in RuntimeArguments)
            {
                var argument = GetConfiguredArgument(item);
                if (argument != null)
                {
                    if (argument.TriggerFunction != null)
                    {
                        if (!argument.HasRequiredVariable)
                        {
                            argument.TriggerFunction(item.Value, this);
                        }
                        else
                        {
                            Argument requiredVariable = GetRuntimeArgument(argument.RequiredVariable);
                            if (requiredVariable == null)
                                throw new Exception(string.Format("A required variable {0} for the key {1} does not exist in the command line arguments.  Please use the required variable {0} like this /variablename:variablevalue.", argument.RequiredVariable, argument.Key));
                            else
                                argument.TriggerFunction(requiredVariable.Value, this);
                        }
                    }
                }
            }
        }

        private string FormatKey(string key)
        {
            key = key.Replace("-","");
            key = key.Replace("--","");
            key = key.Replace("/","");

            return key.ToLower();
        }

        public void ShowUsage(string textToAppend, Args args)
        {
            StringBuilder usage = new StringBuilder();
            usage.AppendLine("Options: \r\n\r\b" );
            foreach (var key in ConfiguredArguments)
            {
                usage.AppendLine(key.Key + " " + key.Desc);
            }

            usage.AppendLine(textToAppend);
            Console.WriteLine(usage.ToString());
        }

        /// <summary>
        /// These are the arguments passed in at run time. 
        /// </summary>
        private List<Argument> RuntimeArguments{ get; set; }

        private void BuildRuntimeArguments()
        {
            RuntimeArguments = new List<Argument>();
            Argument keyValue;

            foreach (string item in _args)
            {
                keyValue = new Argument();
                string[] parts = item.Split(':');
                if (parts.Length > 1)
                {
                    keyValue.Key = FormatKey(parts[0]);
                    keyValue.Value = parts[1];
                }
                else
                {
                    keyValue.Key = FormatKey(item);
                }
                RuntimeArguments.Add(keyValue);
            }
        }

        public void AddAction(string keyName, string desc, Action<string,Args> action)
        {
            string key = FormatKey(keyName);
            Argument keyValue = GetConfiguredArgument(key);
            if (keyValue != null)
                throw new Exception(string.Format("The key {0} already exists.", keyName));
            Argument k = new Argument(key, desc, action);
            ConfiguredArguments.Add(k);
        }

        public void AddActionWithRequiredVariable(string actionName, string variableName, string desc, Action<string, Args> action)
        {
            string key = FormatKey(actionName);
            Argument keyValue = GetConfiguredArgument(key);
            if (keyValue != null)
                throw new Exception(string.Format("The key {0} already exists.", actionName));


            Argument k = new Argument(key, variableName, desc, action);
            ConfiguredArguments.Add(k);
        }

        public void AddVariable(string keyName, string desc)
        {
            string key = FormatKey(keyName);
            Argument keyValue = GetConfiguredArgument(key);
            if (keyValue != null)
                throw new Exception(string.Format("The key {0} already exists.", keyName));
            Argument k = new Argument(key, desc);
            ConfiguredArguments.Add(k);
        }

        private Argument GetConfiguredArgument(Argument key)
        {
            foreach (var item in ConfiguredArguments)
            {
                if (item.Key == key.Key)
                    return item;
            }
            return null;
        }

        public Argument GetRuntimeArgument(string key)
        {
            foreach (var item in RuntimeArguments)
            {
                if (item.Key == key)
                    return item;
            }
            return null;
        }

        private Argument GetConfiguredArgument(string key)
        {
            foreach (var item in ConfiguredArguments)
            {
                if (item.Key == key)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// The arguments that are passed in when the class is built.  At compile time.
        /// </summary>
        public List<Argument> ConfiguredArguments {get;set;}
    }
   
    public class Argument
    {
        public Argument()
        {

        }

        public Argument(string key, string desc, Action<string, Args> trigger)
        {
            this.Key = key;
            this.Desc = desc;
            this.TriggerFunction = trigger;
        }

        public Argument(string key, string requiredVariable, string desc, Action<string, Args> trigger)
        {
            this.Key = key;
            this.HasRequiredVariable = true;
            this.RequiredVariable = requiredVariable;
            this.Desc = desc;
            this.TriggerFunction = trigger;
        }

        public Argument(string key, string desc)
        {
            this.Key = key;
            this.Desc = desc;
        }

        /// <summary>
        /// If true the action must be accompanied with a variable.  A variable is the /RequiredVariable=varablevalue.  For example this could be /connectionString="connection string info"
        /// </summary>
        public bool HasRequiredVariable { get; set; }

        /// <summary>
        /// Determins if this Key is required in the commandline arguments in order to run
        /// </summary>
        public string RequiredVariable { get; set;}

        /// <summary>
        /// The Method holder.  The Action to execute when this key is found.
        /// </summary>
        public Action<string,Args> TriggerFunction;

        /// <summary>
        /// Used together with a value. /Key=value,  /filePath=c:\test\one
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Used togther with a key /Key=value, /petType=dog
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Describes the usage of this key, key=value
        /// </summary>
        public string Desc { get; set; }
    }
}
