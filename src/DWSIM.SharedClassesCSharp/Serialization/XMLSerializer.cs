using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace XMLSerializer
{
    public static class XMLSerializer
    {
        public static List<XElement> Serialize(object obj)
        {
            return Serialize(obj, fields: false);
        }

        public static List<XElement> Serialize(object obj, bool fields)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var elements = new List<XElement>
            {
                new("Type", obj.GetType().ToString()),
            };

            foreach (var member in GetMembers(obj.GetType(), fields, requireWritable: false))
            {
                if (member.GetCustomAttribute<XmlIgnoreAttribute>() != null)
                {
                    continue;
                }

                var memberType = GetMemberType(member);
                if (!IsSimpleType(memberType))
                {
                    continue;
                }

                var value = GetValue(member, obj);
                if (value != null)
                {
                    elements.Add(new XElement(member.Name, FormatValue(value, memberType)));
                }
            }

            return elements;
        }

        public static void Deserialize(object obj, IEnumerable<XElement> data)
        {
            Deserialize(obj, data, fields: false);
        }

        public static void Deserialize(object obj, IEnumerable<XElement> data, bool fields)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (data == null)
            {
                return;
            }

            var members = GetMembers(obj.GetType(), fields, requireWritable: true)
                .Where(m => m.GetCustomAttribute<XmlIgnoreAttribute>() == null)
                .Where(m => IsSimpleType(GetMemberType(m)))
                .ToDictionary(m => m.Name, StringComparer.Ordinal);

            foreach (var element in data)
            {
                if (!members.TryGetValue(element.Name.LocalName, out var member))
                {
                    continue;
                }

                SetValue(member, obj, ConvertValue(element.Value, GetMemberType(member)));
            }
        }

        private static IEnumerable<MemberInfo> GetMembers(Type type, bool fields, bool requireWritable)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            if (fields)
            {
                return type.GetFields(flags)
                    .Where(field => !requireWritable || !field.IsInitOnly);
            }

            return type.GetProperties(flags)
                .Where(property => property.CanRead)
                .Where(property => !requireWritable || property.CanWrite)
                .Where(property => property.GetIndexParameters().Length == 0);
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => throw new NotSupportedException($"Unsupported member type: {member.MemberType}"),
            };
        }

        private static object GetValue(MemberInfo member, object obj)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(obj),
                PropertyInfo property => property.GetValue(obj),
                _ => throw new NotSupportedException($"Unsupported member type: {member.MemberType}"),
            };
        }

        private static void SetValue(MemberInfo member, object obj, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(obj, value);
                    break;
                case PropertyInfo property:
                    property.SetValue(obj, value);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported member type: {member.MemberType}");
            }
        }

        private static bool IsSimpleType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(ArrayList)
                || underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(Guid);
        }

        private static string FormatValue(object value, Type declaredType)
        {
            var underlying = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            if (underlying == typeof(ArrayList))
            {
                return string.Join(",", ((ArrayList)value)
                    .Cast<object>()
                    .Select(FormatArrayItem));
            }

            if (underlying == typeof(double))
            {
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }

            if (underlying == typeof(float))
            {
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            }

            if (underlying == typeof(DateTime))
            {
                return ((DateTime)value).ToString("O", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string FormatArrayItem(object value)
        {
            return value switch
            {
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty,
            };
        }

        private static object ConvertValue(string value, Type targetType)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null && string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var underlying = nullableType ?? targetType;

            if (underlying == typeof(ArrayList))
            {
                var values = new ArrayList();
                if (string.IsNullOrEmpty(value))
                {
                    return values;
                }

                foreach (var item in value.Split(','))
                {
                    if (double.TryParse(item, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                    {
                        values.Add(number);
                    }
                    else
                    {
                        values.Add(item);
                    }
                }

                return values;
            }

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
