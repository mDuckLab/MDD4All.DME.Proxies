using System.IO;
using System.Xml.Serialization;

namespace MDD4All.DME.Proxies
{
    public class XmlSerializerProxy
    {
        public string Serialize(object obj)
        {
            XmlSerializer serializer = new XmlSerializer(obj.GetType());

            StringWriter stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, obj);

            return stringWriter.ToString();
        }
    }

}
