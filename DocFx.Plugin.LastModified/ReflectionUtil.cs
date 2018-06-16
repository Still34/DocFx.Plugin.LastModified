using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocFx.Plugin.LastModified
{
    public static class ReflectionUtil
    {
        public static string GetReflectedProperties<T>(T input)
        {
            if (input == null) return null;
            var type = input.GetType();
            var props = type.GetProperties();
            var sb = new StringBuilder();
            foreach (var propertyInfo in props.OrderBy(x => x.Name))
            {
                var propValue = propertyInfo.GetValue(input, null) ?? "Empty";
                if (propValue is IEnumerable<object> list)
                    sb.AppendLine($"{propertyInfo.Name}: {string.Join(", ", list)}");
                if (propValue is KeyValuePair<object, object> keyValuePair)
                    sb.AppendLine($"{keyValuePair.Key}: {keyValuePair.Value}");
                else sb.AppendLine($"{propertyInfo.Name}: {propValue}");
            }

            return sb.ToString();
        }
    }
}