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

        // Newtonsoft calls this for every type it encounters and routes the matching ones here.
        public override bool CanConvert(Type objectType)
        {
            return typeof(IDictionary).IsAssignableFrom(objectType);
        }


        // Json.NET has stepped aside for this value, so exactly one complete JSON value has to be
        // written here - no more and no less, or everything after it in the file is shifted. That
        // is why the dead ends below write null instead of simply returning.
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            IDictionary dictionary = value as IDictionary;

            if (dictionary == null)
            {
                writer.WriteNull();
                return;
            }

            Type[] genericArguments = value.GetType().GetGenericArguments();

            // A non-generic dictionary doesn't reveal its key type, so no format can be picked.
            if (genericArguments.Length < 2)
            {
                writer.WriteNull();
                return;
            }

            Type keyType = genericArguments[0];

            if (TypeAnalyzer.IsSimpleDataType(keyType))
            {
                // { "Key1": "Value1" } - the key doubles as the JSON property name.
                writer.WriteStartObject();

                foreach (DictionaryEntry entry in dictionary)
                {
                    string propertyName = string.Empty;

                    if (entry.Key != null)
                    {
                        propertyName = entry.Key.ToString();
                    }

                    writer.WritePropertyName(propertyName);
                    serializer.Serialize(writer, entry.Value);
                }

                writer.WriteEndObject();
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
            // An explicit null has to stay null - building an empty dictionary here would make
            // a cleared property come back as {} after reloading.
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            Type[] genericArguments = objectType.GetGenericArguments();

            if (genericArguments.Length < 2)
            {
                return null;
            }

            Type keyType = genericArguments[0];
            Type valueType = genericArguments[1];

            IDictionary dictionary = (IDictionary)Activator.CreateInstance(objectType);

            if (reader.TokenType == JsonToken.StartArray)
            {
                // The complex-key format written above.
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
                // Simple keys already match what Newtonsoft expects.
                serializer.Populate(reader, dictionary);
            }

            return dictionary;
        }
    }
}
