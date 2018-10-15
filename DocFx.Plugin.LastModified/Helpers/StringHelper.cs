using System.Linq;

namespace DocFx.Plugin.LastModified.Helpers
{
    public static class StringHelper
    {
        /// <summary>
        ///     Truncates the string to match the specified <paramref name="length"/>.
        /// </summary>
        /// <remarks>
        ///     <para>This method is retrieved and modified from Humanizr/Humanizer.</para>
        ///     <para>MIT License (c)</para>
        ///     <para>Copyright (c) .NET Foundation and Contributors</para>
        /// </remarks>
        /// <param name="value">The string to truncate.</param>
        /// <param name="length">The target maximum length of the string.</param>
        /// <returns>
        ///     A truncated string based on the <paramref name="length" /> specified.
        /// </returns>
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