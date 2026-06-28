using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace XMLSerializer
{
    public static class XMLSerializer
    {
        public static List<XElement> Serialize(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return obj.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && IsSimpleType(p.PropertyType))
                .Select(p => new XElement(p.Name, Convert.ToString(p.GetValue(obj), CultureInfo.InvariantCulture)))
                .ToList();
        }

        public static void Deserialize(object obj, IEnumerable<XElement> data)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (data == null)
            {
                return;
            }

            var properties = obj.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanWrite && IsSimpleType(p.PropertyType))
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var element in data)
            {
                if (!properties.TryGetValue(element.Name.LocalName, out var property))
                {
                    continue;
                }

                property.SetValue(obj, ConvertValue(element.Value, property.PropertyType));
            }
        }

        private static bool IsSimpleType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(Guid);
        }

        private static object ConvertValue(string value, Type targetType)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(string))
            {
                return value;
            }

            if (underlying.IsEnum)
            {
                return Enum.Parse(underlying, value);
            }

            if (underlying == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (underlying == typeof(DateTime))
            {
                return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }
    }
}
