using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WireSockUI.Native
{
    internal class WindowsApplicationContext : ApplicationContext
    {
        private WindowsApplicationContext(string name, string appUserModelId)
        {
            Name = name;
            AppUserModelId = appUserModelId;
        }

        /// <summary>
        /// </summary>
        public string Name { get; }

        public string AppUserModelId { get; }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string appId);

        public static WindowsApplicationContext FromCurrentProcess(
            string customName = null,
            string appUserModelId = null)
        {
            var mainModule = Process.GetCurrentProcess().MainModule;

            if (mainModule?.FileName == null) throw new InvalidOperationException("No valid process module found.");

            var appName = customName ?? Path.GetFileNameWithoutExtension(mainModule.FileName);
            var aumid = appUserModelId ?? appName; //TODO: Add seeded bits to avoid collisions?

            SetCurrentProcessExplicitAppUserModelID(aumid);

            using (var shortcut = new ShellLink
                   {
                       TargetPath = mainModule.FileName,
                       Arguments = string.Empty,
                       AppUserModelId = aumid
                   })
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var startMenuPath = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs");
                var shortcutFile = Path.Combine(startMenuPath, $"{appName}.lnk");

                shortcut.Save(shortcutFile);
            }

            return new WindowsApplicationContext(appName, aumid);
        }
    }
}