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

            bool isSimpleKey = keyType == typeof(string) ||
                               keyType.IsPrimitive ||
                               keyType == typeof(Guid);

            if (isSimpleKey)
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
            else
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
