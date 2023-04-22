using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using WireSockUI.Native;
using WireSockUI.Properties;

namespace WireSockUI.Forms
{
    public partial class TaskManager : Form
    {
        public TaskManager()
        {
            InitializeComponent();

            // Safely set the icon
            if (Resources.ico != null) Icon = Resources.ico;

            // Safely set the refresh button image
            var refreshIcon = WindowsIcons.GetWindowsIcon(WindowsIcons.Icons.Refresh, 16);
            if (refreshIcon != null) btnRefresh.Image = refreshIcon.ToBitmap();

            // Ensure the process list rows fill the entire width, but no scrollbar appears
            if (lstProcesses != null && lstProcesses.Columns.Count > 0)
                lstProcesses.Columns[0].Width = lstProcesses.Size.Width - 18;

            // Safely set the cue banner text
            if (txtSearch != null && Resources.ProcessesSearchCue != null)
                txtSearch.SetCueBanner(Resources.ProcessesSearchCue);

            UpdateProcesses();
        }

        public string ReturnValue { get; private set; }

        public void UpdateProcesses()
        {
            lstProcesses.Items.Clear();
            lstProcesses.SmallImageList.Images.Clear();

            var currentUser = WindowsIdentity.GetCurrent().Name;

            // Get unique processes for the current user
            var processes = ProcessList.GetProcessList()
                .Where(p => p.User == currentUser)
                .Distinct(ProcessEntry.Comparer);

            // Add a default icon to the list view's image list
            const string defaultIconKey = "DefaultIcon";
            var defaultIcon = Resources.ico; // Replace with the appropriate resource for the default icon
            if (defaultIcon != null) lstProcesses.SmallImageList.Images.Add(defaultIconKey, (Icon)defaultIcon.Clone());

            // Add process items to the list view
            foreach (var process in processes)
            {
                var displayName = Path.GetFileNameWithoutExtension(process.ImageName);
                var iconKey = process.ProcessId.ToString();

                // If the process's image file exists, extract its associated icon and add it to the list view's image list
                if (File.Exists(process.ImageName))
                {
                    if (process.ImageName != null)
                        using (var icon = Icon.ExtractAssociatedIcon(process.ImageName))
                        {
                            if (icon != null)
                                lstProcesses.SmallImageList.Images.Add(iconKey, (Icon)icon.Clone());
                            else
                                iconKey = defaultIconKey;
                        }
                    else
                        iconKey = defaultIconKey;
                }
                else
                {
                    iconKey = defaultIconKey;
                }

                // Create a new list view item for the process and add it to the list view
                var listViewItem = new ListViewItem(displayName, iconKey);
                lstProcesses.Items.Add(listViewItem);
            }
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            UpdateProcesses();
            txtSearch.Text = string.Empty;
        }

        private void OnFindProcessChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                foreach (ListViewItem item in lstProcesses.Items)
                    if (item.Text.StartsWith(txtSearch.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        item.EnsureVisible();
                        item.Selected = true;
                        return;
                    }
            }
            else
            {
                foreach (ListViewItem item in lstProcesses.SelectedItems) item.Selected = false;
            }
        }

        private void OnProcessSelected(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            ReturnValue = lstProcesses.SelectedItems[0].Text;
            Close();
        }
    }
}