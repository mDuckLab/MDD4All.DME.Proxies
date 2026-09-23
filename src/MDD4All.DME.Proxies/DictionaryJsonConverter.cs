using MDD4All.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;

namespace MDD4All.DME.Proxies
{
    // JSON allows only strings as property names, so a dictionary keyed by an object has nowhere
    // to put its keys. Left to Json.NET, a Dictionary<Address, Person> comes out like this:
    //
    //     "Residents": { "Hauptstrasse 12, ": { "Name": "Meier" } }
    //
    // The key has been through ToString() and is gone. Nothing can turn that text back into an
    // Address, so the file is unreadable the moment it is opened again. This converter writes a
    // list of pairs instead, where the key stays an object of its own.
    //
    // It sits in this assembly because JsonSerializerProxy constructs it, and that runs inside the
    // data model's own AssemblyLoadContext.
    public class DictionaryJsonConverter : JsonConverter
    {
        private readonly bool _writeComplexKeys;

        // Only writing is a choice. Reading has to understand every form that was ever written,
        // so the flag is not consulted there.
        public DictionaryJsonConverter(bool writeComplexKeys = true)
        {
            _writeComplexKeys = writeComplexKeys;
        }

        // Json.NET asks this for every type it meets and hands over the ones that say yes. Only
        // dictionaries whose key is an object are taken - a key that survives being turned into
        // text is Json.NET's own business, and it does that better than a hand-written branch
        // here would. A DateTime key, for one, needs its round-trip format and not ToString().
        public override bool CanConvert(Type objectType)
        {
            bool result = false;

            TypeAnalyzer analyst = TypeAnalyzer.CreateAnalyst(objectType);

            if (analyst.TypeCategory == TypeCategory.IDictionary)
            {
                result = !TypeAnalyzer.IsSimpleDataType(analyst.UnderlyingTypes[0]);
            }

            return result;
        }


        // Json.NET has stepped aside for this value, so exactly one complete JSON value has to be
        // written here - no more and no less, or everything after it in the file is shifted. That
        // is why the dead ends below write null instead of simply returning.
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IDictionary dictionary = value as IDictionary;

            if (dictionary == null)
            {
                // The property holds nothing, and NullValueHandling.Include means that has to be
                // said out loud rather than skipped.
                writer.WriteNull();
            }
            else if (_writeComplexKeys)
            {
                // [ { "Key": {...}, "Value": {...} } ] - a complex key can't be a property name.
                writer.WriteStartArray();

                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("Key");
                    serializer.Serialize(writer, entry.Key);

                    writer.WritePropertyName("Value");
                    serializer.Serialize(writer, entry.Value);

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                // Dropped, and that has to happen here. Leaving it to Newtonsoft would name the
                // properties after ToString() of the key, which nothing can turn back into an object.
                writer.WriteNull();
            }
        }

        // Which form is in the file is decided by looking at it, not by asking the type. The same
        // property is an object where the keys were simple, an array where they were not, and null
        // where a save dropped it. All three have to keep working no matter how the setting stands
        // today, because files written earlier do not change.
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object result = null;

            // An explicit null has to stay null - building an empty dictionary here would make
            // a cleared property come back as {} after reloading.
            if (reader.TokenType != JsonToken.Null)
            {
                TypeAnalyzer analyst = TypeAnalyzer.CreateAnalyst(objectType);

                Type keyType = analyst.UnderlyingTypes[0];
                Type valueType = analyst.UnderlyingTypes[1];

                IDictionary dictionary = (IDictionary)Activator.CreateInstance(objectType);

                if (reader.TokenType == JsonToken.StartArray)
                {
                    // The Key/Value form written above.
                    JArray temporaryArray = JArray.Load(reader);

                    foreach (JToken item in temporaryArray)
                    {
                        JToken keyToken = item["Key"];
                        JToken valueToken = item["Value"];

                        if (keyToken != null)
                        {
                            object key = keyToken.ToObject(keyType, serializer);

                            object value = null;

                            if (valueToken != null)
                            {
                                value = valueToken.ToObject(valueType, serializer);
                            }

                            if (key != null)
                            {
                                dictionary.Add(key, value);
                            }
                        }
                    }
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    // Only files written before the plain form was abandoned still look like this,
                    // and their keys went through ToString(). Populate is left to fail on them with
                    // a message naming the key it cannot convert, which beats a silent empty result.
                    serializer.Populate(reader, dictionary);
                }

                result = dictionary;
            }

            return result;
        }
    }
}
