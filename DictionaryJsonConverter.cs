using MDD4All.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;

namespace MDD4All.DME.Proxies
{
    // JSON only allows strings as property names, so dictionaries with complex keys need
    // their own format. Lives in this assembly because Newtonsoft instantiates the converter
    // itself - it has to be the copy loaded in the data model's own AssemblyLoadContext.
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
