using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;

namespace Amie
{
    [Serializable]
internal class UpdateSettings
    {
        internal string Key { get; set; }

        internal byte[] UpdateFile { get; set; }

        internal AppInfo AppInfo { get; set; }
        
        internal static byte[] Serialize(UpdateSettings settings)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, settings);
                return stream.ToArray();
            }
        }

        internal static UpdateSettings Deserialize(byte[] updateSettings)
        {

            using (MemoryStream stream = new MemoryStream(updateSettings))
            {
                var formatter = new BinaryFormatter();
                return (UpdateSettings)formatter.Deserialize(stream);
            }

        }

    }
}
