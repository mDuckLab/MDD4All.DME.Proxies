using Newtonsoft.Json;
using System;

namespace MDD4All.DME.Proxies
{
    public class JsonSerializerProxy
    {

        private JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            // Includes the full C# type name in the JSON (as $type). 
            // This is vital for deserializing inherited classes correctly.
            TypeNameHandling = TypeNameHandling.Auto,
            // Forces the reader to look for metadata (like $type or $id) at the beginning.
            //MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
            //// Ensures that the same object isn't saved twice; instead, it uses references ($id/$ref).
            //PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            //// Prevents the serializer from crashing if objects point to each other in a circle.
            //ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // Explicitly writes 'null' into the JSON file instead of skipping the property.
            // Also needed on the way back in: without an explicit "Property": null in the
            // file, deserialization leaves whatever the target type's constructor already
            // set, so a deleted/nulled property would silently reappear after reload.
            NullValueHandling = NullValueHandling.Include,
            // Ensures a "fresh start" by replacing existing collections and objects instead of 
            // appending new data to them. This prevents data pollution and duplicate entries.
            // Example: If a list currently has 3 items and you load a file containing 2 items, 
            // 'Replace' ensures the list has exactly 2 items. Without this, the list would 
            // incorrectly grow to 5 items due to default 'Append' behavior.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            //// Allows the use of private or internal constructors when creating objects from JSON.
            //ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            //// Uses a simplified assembly name in the $type metadata for better compatibility.
            //TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            // Formats the resulting JSON string with indentation and line breaks for human readability.
            Formatting = Formatting.Indented,
            // Adds a custom converter to handle Dictionary structures correctly during conversion.
            // This converter handles the transformation of IDictionary objects.
            // It solves the problem that standard JSON only allows strings as keys, 
            // whereas C# dictionaries can use complex objects as keys.
            //Converters = new List<JsonConverter> { new DictionaryJsonConverter() }
        };

        public string Serialize(object objectInstance, bool includeTypeInformation)
        {
            string result = string.Empty;

            if (objectInstance != null)
            {
                if (includeTypeInformation)
                {
                    // Declaring the root as "object" makes TypeNameHandling.Auto write the actual
                    // type as $type - without it the root's type is implied and never recorded.
                    result = JsonConvert.SerializeObject(objectInstance, typeof(object), SerializerSettings);
                }
                else
                {
                    result = JsonConvert.SerializeObject(objectInstance, SerializerSettings);
                }
            }

            return result;
        }

        // Runs in the data model's own AssemblyLoadContext, so a $type in the file resolves to the
        // same type identity the caller expects - resolving it outside would yield a different one.
        public object Deserialize(string json, Type targetType)
        {
            object result = null;

            if (!string.IsNullOrEmpty(json) && targetType != null)
            {
                result = JsonConvert.DeserializeObject(json, targetType, SerializerSettings);
            }

            return result;
        }
    }
}
