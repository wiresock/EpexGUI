using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WireSockUI.Native
{
    /// <summary>
    /// Provides methods to read and write INI files using Windows native APIs.
    /// </summary>
    internal static class IniFile
    {
        // External function declarations
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileSectionNames([In] [Out] char[] lpszReturnBuffer, int nSize,
            string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileSection(string lpAppName, [In] [Out] char[] lpszReturnBuffer,
            int nSize, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault,
            [In] [Out] char[] lpReturnedString, int nSize, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long WritePrivateProfileString(string name, string key, string val, string filePath);

        /// <summary>
        /// Retrieves all section names in the specified INI file.
        /// </summary>
        /// <param name="fileName">Path to the INI file.</param>
        /// <returns>An IEnumerable of section names.</returns>
        public static IEnumerable<string> GetSectionNames(string fileName)
        {
            const int bufferSize = 32 * 1024;

            // MSDN specifies maximum of 32 KB
            var buffer = new char[bufferSize];

            var length = GetPrivateProfileSectionNames(buffer, bufferSize, fileName);

            if (length == bufferSize - 2) yield break;
            var value = new string(buffer, 0, (int)length);

            foreach (var entry in value.Trim('\0').Split('\0')) yield return entry;
        }

        /// <summary>
        /// Retrieves all keys and their values in a section from the specified INI file.
        /// </summary>
        /// <param name="fileName">Path to the INI file.</param>
        /// <param name="sectionName">The name of the section to look up.</param>
        /// <returns>A dictionary containing keys and their values in the specified section.</returns>
        public static Dictionary<string, string> GetSection(string fileName, string sectionName)
        {
            const int bufferSize = 32 * 1024;

            // MSDN specifies maximum of 32 KB
            var buffer = new char[bufferSize];

            var length = GetPrivateProfileSection(sectionName, buffer, bufferSize, fileName);

            var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (length == bufferSize - 2) return section;
            var value = new string(buffer, 0, (int)length);

            foreach (var entry in value.Trim('\0').Split('\0'))
                section.Add(entry.Substring(0, entry.IndexOf('=')), entry.Substring(entry.IndexOf('=') + 1));

            return section;
        }

        /// <summary>
        /// Reads a string value associated with the specified key in the given section of the INI file.
        /// </summary>
        /// <param name="fileName">The path to the INI file.</param>
        /// <param name="sectionName">The name of the section to look up.</param>
        /// <param name="key">The key of the value to read.</param>
        /// <returns>A string containing the value associated with the specified key, or an empty string if the key is not found.</returns>
        public static string ReadString(string fileName, string sectionName, string key)
        {
            const int bufferSize = 2 * 1024;

            var buffer = new char[bufferSize];

            var length = GetPrivateProfileString(sectionName, key, null, buffer, bufferSize, fileName);

            return length != bufferSize - 2 ? new string(buffer, 0, (int)length) : string.Empty;
        }
    }
}