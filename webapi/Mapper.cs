using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace webapi
{
    public class Mapper
    {
        public static void RedactExcept(object target, IEnumerable<string> allowedPropertyNames)
        {
            var type = target.GetType();

            foreach (var prop in type.GetProperties())
            {
                if (allowedPropertyNames.Contains(prop.Name) || !prop.CanWrite) continue;

                prop.SetValue(target, GetDefault(prop.PropertyType));
            }

            foreach (var field in type.GetFields())
            {
                if (allowedPropertyNames.Contains(field.Name) || !field.IsPublic) continue;

                field.SetValue(target, GetDefault(field.FieldType));
            }
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        public static void MapExplicit(object from, object to, IEnumerable<string> propertyNames)
        {
            // Copy top level values only

            var fromType = from.GetType();
            var toType = to.GetType();

            // Copy properties
            foreach (var fromProp in fromType.GetProperties())
            {
                if (!fromProp.CanRead) continue;

                var fromPropName = fromProp.Name;
                if (!propertyNames.Contains(fromPropName)) continue;

                var toProp = toType.GetProperty(fromPropName);
                if (toProp == null || !toProp.CanWrite) continue;

                toProp.SetValue(to, fromProp.GetValue(from));
            }

            // Copy fields
            foreach (var fromField in fromType.GetFields())
            {
                var fromFieldName = fromField.Name;
                if (!propertyNames.Contains(fromFieldName)) continue;

                var toField = toType.GetField(fromFieldName);
                if (toField == null || !toField.IsPublic) continue;

                toField.SetValue(to, fromField.GetValue(from));
            }
        }


        public static void MapExcept(object from, object to, params string[] exceptions)
        {
            var fromType = from.GetType();
            var toType = to.GetType();

            var props = toType.GetProperties().Select(f => f.Name);
            MapExplicit(from, to, props.Except(exceptions));

            var fields = toType.GetFields().Where(f => f.IsPublic).Select(f => f.Name);
            MapExplicit(from, to, fields.Except(exceptions));
        }
    }
}
