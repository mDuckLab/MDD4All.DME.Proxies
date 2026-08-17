using MDD4All.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace MDD4All.DME.Proxies
{
    // Writes the object graph as XML, minus every dictionary in it.
    //
    // XmlSerializer cannot serialize a dictionary at all. Not "not the complex ones" like JSON -
    // any of them, down to Dictionary<string, string>, because the type implements IDictionary:
    //
    //     NotSupportedException: Cannot serialize member Person.ContactDetails of type
    //     Dictionary<string, string>, because it implements IDictionary.
    //
    // It throws while the serializer is being built, before a single character is written, so one
    // dictionary anywhere in the graph takes the whole file down. And unlike Json.NET there is no
    // converter to hook in: the only way to teach XmlSerializer a type is IXmlSerializable, which
    // the type has to implement itself. Data models are foreign DLLs, so that is out of reach.
    //
    // The XML here exists to be looked at, not to be a second storage format - JSON is the complete
    // one and the only one that survives a round trip. Rather than let the view fail on any model
    // containing a dictionary, the dictionaries are left out and the rest is shown.
    //
    // XmlAttributeOverrides is what makes that possible without touching the model: the same effect
    // as writing [XmlIgnore] onto a property, but assembled here at runtime and handed to the
    // serializer from outside. It is the counterpart to registering DictionaryJsonConverter in the
    // settings instead of putting an attribute on the model.
    //
    // Two things to know. Whoever reads such a file gets a graph with empty dictionaries and no
    // hint that anything was left out, so the caller has to say so on screen. And a serializer
    // built with overrides is not cached by .NET the way a plain one is - every call generates a
    // temporary assembly that stays in the process, which is harmless when looking at a file and a
    // leak in a loop.
    public class XmlSerializerProxy
    {
        public string Serialize(object obj)
        {
            Type rootType = obj.GetType();

            XmlAttributeOverrides overrides = new XmlAttributeOverrides();

            IgnoreDictionaries(rootType, new List<Type>(), overrides);

            XmlSerializer serializer = new XmlSerializer(rootType, overrides);

            StringWriter stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, obj);

            return stringWriter.ToString();
        }

        // Every type in the graph has to be visited, not just the root: an override is registered
        // against the type that declares the property, so a dictionary on Person is only found by
        // descending into Person. Each type is entered once, as types can reference each other.
        private void IgnoreDictionaries(Type type, List<Type> visited, XmlAttributeOverrides overrides)
        {
            if (!visited.Contains(type))
            {
                visited.Add(type);

                foreach (PropertyInfo property in type.GetProperties())
                {
                    TypeAnalyzer analyst = TypeAnalyzer.CreateAnalyst(property.PropertyType);

                    if (analyst.TypeCategory == TypeCategory.IDictionary)
                    {
                        overrides.Add(type, property.Name, new XmlAttributes { XmlIgnore = true });
                    }
                    else
                    {
                        // Not descended into for a dictionary - it is not written, so whatever is
                        // inside it never reaches the serializer either.
                        foreach (Type nested in TypesInside(property.PropertyType))
                        {
                            IgnoreDictionaries(nested, visited, overrides);
                        }
                    }
                }
            }
        }

        // A list's elements, an array's elements and a plain object can each hold a dictionary
        // further down.
        private List<Type> TypesInside(Type type)
        {
            List<Type> result = new List<Type>();

            if (type.IsArray)
            {
                Type elementType = type.GetElementType();

                if (elementType != null)
                {
                    result.Add(elementType);
                }
            }
            else if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    result.Add(argument);
                }
            }
            else if (type.IsClass && type != typeof(string))
            {
                result.Add(type);
            }

            return result;
        }
    }
}
