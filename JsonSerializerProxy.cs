using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace MDD4All.DME.Proxies
{
    // Runs Newtonsoft inside the data model's own AssemblyLoadContext, which is the only place a
    // "$type" in a file resolves to the type the caller means. DynamicInvoker reaches this class by
    // name from the outside - nothing calls it directly, so a rename here breaks no build.
    public class JsonSerializerProxy
    {
        // Assembled on every call instead of being handed out from a field. A single shared instance
        // whose converter list is swapped per call would have one call reconfigure the next. That
        // does no harm today only because a fresh proxy is built per invocation, and relying on that
        // is asking for trouble the moment this class is used any other way.
        private JsonSerializerSettings CommonSettings()
        {
            JsonSerializerSettings result = new JsonSerializerSettings
            {
                // Wherever the declared type does not say enough, the real one is written into the
                // file as an extra property named $type:
                //
                //     "Pet": { "$type": "PetShop.Dog, PetShop", "Name": "Rex" }
                //
                // A property declared as Animal holding a Dog would otherwise come back as an
                // Animal, so a data model built on inheritance loses everything below the base
                // class. "Auto" means this is written only where it is needed, not everywhere.
                //
                // The root object is the exception and needs help - see Serialize.
                TypeNameHandling = TypeNameHandling.Auto,

                // A property cleared to null has to appear in the file as null. Otherwise reading
                // leaves whatever the constructor put there and the cleared value reappears.
                NullValueHandling = NullValueHandling.Include,

                // Collections are replaced, not appended to. A list the constructor filled with
                // three items would otherwise hold five after loading a file that has two.
                ObjectCreationHandling = ObjectCreationHandling.Replace,

                // Data files are read and diffed by hand, so they are written across several lines
                // rather than as one.
                Formatting = Formatting.Indented
            };

            return result;
        }

        // Converters are Json.NET's list of exceptions. Walking the graph it asks each of them
        // CanConvert(type) for every value it meets, and the first one to say yes writes that value
        // in its place. The settings above are unaffected by that: a converter replaces the handling
        // of a single value, not of the run, so what it passes back through serializer.Serialize
        // still gets its $type and its indentation like everything else.
        //
        // The one registered here exists because JSON allows only strings as property names, so a
        // dictionary keyed by an object has nowhere to put its keys. It goes in whatever the flag
        // says - without it Json.NET would name the properties after ToString() of the key, and
        // nothing can read that back.
        //
        // Constructing it here is also what makes the flag possible at all. The other way to attach
        // a converter is an attribute on the model, which is out of reach for a foreign DLL and
        // could not carry a setting either.
        private JsonSerializerSettings SettingsForWriting(bool writeComplexDictionaryKeys)
        {
            JsonSerializerSettings result = CommonSettings();

            result.Converters = new List<JsonConverter> { new DictionaryJsonConverter(writeComplexDictionaryKeys) };

            return result;
        }

        // The converter reads both forms, so unlike writing there is nothing to choose here.
        private JsonSerializerSettings SettingsForReading()
        {
            JsonSerializerSettings result = CommonSettings();

            result.Converters = new List<JsonConverter> { new DictionaryJsonConverter() };

            return result;
        }

        public string Serialize(object objectInstance, bool includeTypeInformation, bool writeComplexDictionaryKeys)
        {
            string result = string.Empty;

            if (objectInstance != null)
            {
                JsonSerializerSettings settings = SettingsForWriting(writeComplexDictionaryKeys);

                if (includeTypeInformation)
                {
                    // The exception mentioned at TypeNameHandling above. Inside the graph every
                    // value has a declared type to be measured against, but the root has none -
                    // Json.NET knows what it was handed and sees no reason to record it. Declaring
                    // the root as "object" creates that gap on purpose, and Auto fills it in.
                    //
                    // This one line is what lets a file name its own data model, which is what
                    // opening a file without picking a model first relies on.
                    result = JsonConvert.SerializeObject(objectInstance, typeof(object), settings);
                }
                else
                {
                    result = JsonConvert.SerializeObject(objectInstance, settings);
                }
            }

            return result;
        }

        public object Deserialize(string json, Type targetType)
        {
            object result = null;

            if (!string.IsNullOrEmpty(json) && targetType != null)
            {
                result = JsonConvert.DeserializeObject(json, targetType, SettingsForReading());
            }

            return result;
        }
    }
}
