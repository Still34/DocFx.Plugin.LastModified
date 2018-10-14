using System.Linq;

namespace DocFx.Plugin.LastModified
{
    public static class StringHelper
    {
        // Modified from Humanizr/Humanizer
        // The MIT License (c)
        // Copyright (c) .NET Foundation and Contributors
        public static string Truncate(this string value, int length)
        {
            var truncationString = "...";

            if (value == null) return null;
            if (value.Length == 0) return value;

            var alphaNumericalCharactersProcessed = 0;

            if (value.ToCharArray().Count(char.IsLetterOrDigit) <= length) return value;

            for (var i = 0; i < value.Length - truncationString.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i])) alphaNumericalCharactersProcessed++;

                if (alphaNumericalCharactersProcessed + truncationString.Length == length)
                    return value.Substring(0, i + 1) + truncationString;
            }

            return value;
        }
    }
}