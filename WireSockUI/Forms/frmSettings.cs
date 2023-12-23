using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;
using WireSockUI.Native;
using WireSockUI.Properties;

namespace WireSockUI.Forms
{
    public partial class FrmSettings : Form
    {
        public FrmSettings()
        {
            InitializeComponent();

            Icon = Resources.ico;

            chkAutorun.Checked = Settings.Default.AutoRun;
            chkAutoMinimize.Checked = Settings.Default.AutoMinimize;
            chkAutoConnect.Checked = Settings.Default.AutoConnect;
            chkAutoUpdate.Checked = Settings.Default.AutoUpdate;
            chkUseAdapter.Checked = Settings.Default.UseAdapter;
            //chkNotify.Checked = Settings.Default.EnableNotifications;
            chkDisableAutoAdmin.Checked = Settings.Default.DisableAutoAdmin;
            ddlLogLevel.SelectedItem = Settings.Default.LogLevel;

            if (!IsCurrentProcessElevated())
            {
                chkUseAdapter.Enabled = false;
                chkUseAdapter.Checked = false;
            }

            chkAutorun.Checked = IsCurrentProcessElevated() ? IsAutoRunEnabled() : IsAutoRunForNonAdminEnabled();
            Settings.Default.AutoRun = chkAutorun.Checked;
        }

        private static bool IsCurrentProcessElevated()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void OnProfilesFolderClick(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", Global.MainFolder);
        }

        private static string GetAppName()
        {
            return Assembly.GetExecutingAssembly().GetName().Name;
        }

        private static bool IsAutoRunEnabled()
        {
            using (var ts = new TaskService())
            {
                return ts.FindTask(GetAppName()) != null;
            }
        }

        private static bool IsAutoRunForNonAdminEnabled()
        {
            try
            {
                var startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupFolderPath, GetAppName() + ".lnk");

                return File.Exists(shortcutPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error checking auto-run status: " + ex.Message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private static void EnableAutoRun()
        {
            try
            {
                using (var ts = new TaskService())
                {
                    var td = ts.NewTask();
                    td.RegistrationInfo.Description = "Auto start for " + GetAppName();

                    td.Principal.RunLevel = TaskRunLevel.Highest; // Run with the highest privileges

                    td.Triggers.Add(new LogonTrigger()); // Trigger on logon

                    var appPath = Assembly.GetExecutingAssembly().Location;
                    td.Actions.Add(new ExecAction(appPath)); // Path to the executable

                    // Set power and idle options
                    td.Settings.DisallowStartIfOnBatteries =
                        false; // Allow the task to start if the computer is running on batteries
                    td.Settings.StopIfGoingOnBatteries =
                        false; // Do not stop the task if the computer switches to battery power
                    td.Settings.WakeToRun = true; // Allow the task to wake the computer if needed
                    td.Settings.IdleSettings.StopOnIdleEnd =
                        false; // Do not stop the task when the computer ceases to be idle

                    ts.RootFolder.RegisterTaskDefinition(GetAppName(), td);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error enabling autorun: " + ex.Message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void DisableAutoRun()
        {
            try
            {
                using (var ts = new TaskService())
                {
                    ts.RootFolder.DeleteTask(GetAppName(), false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error disabling autorun: " + ex.Message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void EnableAutoRunForNonAdmin()
        {
            try
            {
                var appPath = Assembly.GetExecutingAssembly().Location;
                var startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupFolderPath, GetAppName() + ".lnk");

                using (var link = new ShellLink())
                {
                    link.TargetPath = appPath;
                    // Optionally set other properties like arguments, working directory, etc.
                    // link.Arguments = "<YourArguments>";
                    // link.WorkingDirectory = "<YourWorkingDirectory>";

                    link.Save(shortcutPath);
                }

                MessageBox.Show("Auto-run enabled successfully.", "Success", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error enabling auto-run: " + ex.Message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void DisableAutoRunForNonAdmin()
        {
            try
            {
                var startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupFolderPath, GetAppName() + ".lnk");

                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    MessageBox.Show("Auto-run disabled successfully.", "Success", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Auto-run shortcut not found.", "Info", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error disabling auto-run: " + ex.Message, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (Settings.Default.AutoRun != chkAutorun.Checked)
            {
                if (!chkAutorun.Checked)
                {
                    if (IsCurrentProcessElevated())
                        DisableAutoRun();
                    else
                        DisableAutoRunForNonAdmin();
                }
                else
                {
                    if (IsCurrentProcessElevated())
                        EnableAutoRun();
                    else
                        EnableAutoRunForNonAdmin();
                }

                Settings.Default.AutoRun = chkAutorun.Checked;
            }

            Settings.Default.AutoConnect = chkAutoConnect.Checked;
            Settings.Default.AutoMinimize = chkAutoMinimize.Checked;
            Settings.Default.AutoUpdate = chkAutoUpdate.Checked;
            Settings.Default.UseAdapter = chkUseAdapter.Checked;
            //Settings.Default.EnableNotifications = chkNotify.Checked;
            Settings.Default.DisableAutoAdmin = chkDisableAutoAdmin.Checked;
            Settings.Default.LogLevel = ddlLogLevel.SelectedItem as string;

            Settings.Default.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}