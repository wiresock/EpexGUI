using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WireSockUI.Config;
using WireSockUI.Native;
using WireSockUI.Properties;

namespace WireSockUI.Forms
{
    public partial class FrmEdit : Form
    {
        private static readonly Regex ProfileMatch =
            new Regex(@"^\s*((?<comment>[;#].*)|(?<section>\[\w+\])|((?<key>[a-zA-Z0-9]+)[ \t]*=[ \t]*(?<value>.*?)))$",
                RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex MultiValueMatch =
            new Regex(@"[^, ]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private volatile bool _highlighting = false;

        public FrmEdit()
        {
            Initialize();

            Text = Resources.EditProfileTitleNew;
            txtEditor.Text = Resources.template_conf;

            ApplySyntaxHighlighting();
        }

        public FrmEdit(string config)
        {
            Initialize();

            Text = string.Format(Resources.EditProfileTitle, config);

            txtProfileName.Text = config.ToLowerInvariant();
            txtEditor.Text = File.ReadAllText(Path.Combine(Global.ConfigsFolder, config + ".conf"));

            ApplySyntaxHighlighting();
        }

        public string ReturnValue { get; private set; }

        private void ApplySyntaxHighlighting()
        {
            if (_highlighting) return;
            _highlighting = true;

            var hasErrors = false;

            // Saving the original settings
            var originalIndex = txtEditor.SelectionStart;
            var originalLength = txtEditor.SelectionLength;
            var originalColor = Color.Black;

            lblName.Focus();

            // removes any previous highlighting
            txtEditor.SelectionStart = 0;
            txtEditor.SelectionLength = txtEditor.Text.Length;
            txtEditor.SelectionColor = originalColor;
            txtEditor.SelectionFont = new Font(txtEditor.SelectionFont, FontStyle.Regular);

            foreach (Match m in ProfileMatch.Matches(txtEditor.Text))
            {
                if (m.Groups["comment"].Success)
                {
                    txtEditor.SelectionStart = m.Groups["comment"].Index;
                    txtEditor.SelectionLength = m.Groups["comment"].Length;
                    txtEditor.SelectionFont = new Font(txtEditor.SelectionFont, FontStyle.Italic);

                    switch (m.Groups["comment"].Value[0])
                    {
                        case '#':
                            txtEditor.SelectionColor = Color.LightSlateGray;
                            break;
                        case ';':
                            txtEditor.SelectionColor = Color.SaddleBrown;
                            break;
                    }

                    continue;
                }

                if (m.Groups["section"].Success)
                {
                    txtEditor.SelectionStart = m.Groups["section"].Index;
                    txtEditor.SelectionLength = m.Groups["section"].Length;
                    txtEditor.SelectionColor = Color.DarkBlue;
                    txtEditor.SelectionFont = new Font(txtEditor.SelectionFont, FontStyle.Bold);

                    switch (m.Groups["section"].Value.ToLowerInvariant())
                    {
                        case "[interface]":
                        case "[peer]":
                            break;
                        // Unrecognized sections
                        default:
                            txtEditor.UnderlineSelection();
                            hasErrors = true;
                            break;
                    }

                    continue;
                }

                if (m.Groups["key"].Success)
                {
                    txtEditor.SelectionStart = m.Groups["key"].Index;
                    txtEditor.SelectionLength = m.Groups["key"].Length;
                    txtEditor.SelectionColor = Color.Navy;

                    var key = m.Groups["key"].Value.ToLowerInvariant();
                    var value = string.Empty;

                    if (m.Groups["value"].Success)
                    {
                        txtEditor.SelectionStart = m.Groups["value"].Index;
                        txtEditor.SelectionLength = m.Groups["value"].Length;
                        txtEditor.SelectionColor = Color.DarkGreen;

                        value = m.Groups["value"].Value;
                    }

                    switch (key)
                    {
                        // base64 256-bit keys
                        case "privatekey":
                        case "publickey":
                        case "presharedkey":
                            {
                                if (!string.IsNullOrEmpty(value))
                                    try
                                    {
                                        if (key == "privatekey")
                                            txtPublicKey.Text = string.Empty;

                                        var binaryKey = Convert.FromBase64String(value);

                                        if (binaryKey.Length != 32)
                                            throw new FormatException();

                                        // Convert Peer.PrivateKey into PublicKey for display
                                        if (key == "privatekey")
                                            txtPublicKey.Text = Convert.ToBase64String(Curve25519.GetPublicKey(binaryKey));
                                    }
                                    catch (FormatException)
                                    {
                                        txtEditor.UnderlineSelection();
                                        hasErrors = true;
                                    }
                            }
                            break;
                        // IPv4/IPv6 CIDR notation values
                        case "address":
                        case "allowedips":
                        case "disallowedips":
                            {
                                foreach (Match e in MultiValueMatch.Matches(value))
                                    if (!string.IsNullOrWhiteSpace(e.Value) && !IpHelper.IsValidCidr(e.Value))
                                    {
                                        txtEditor.SelectionStart = m.Groups["value"].Index + e.Index;
                                        txtEditor.SelectionLength = e.Length;
                                        txtEditor.UnderlineSelection();
                                        hasErrors = true;
                                    }
                            }
                            break;
                        // IPv4/IPv6 values
                        case "dns":
                            {
                                foreach (Match e in MultiValueMatch.Matches(value))
                                    if (!string.IsNullOrWhiteSpace(e.Value) && !IpHelper.IsValidIpAddress(e.Value))
                                    {
                                        txtEditor.SelectionStart = m.Groups["value"].Index + e.Index;
                                        txtEditor.SelectionLength = e.Length;
                                        txtEditor.UnderlineSelection();
                                        hasErrors = true;
                                    }
                            }

                            break;
                        // IPv4, IPv6 or DNS value
                        case "endpoint":
                        case "socks5proxy":
                            if (!IpHelper.IsValidAddress(value))
                            {
                                txtEditor.UnderlineSelection();
                                hasErrors = true;
                            }

                            break;
                        // Numerical values
                        case "mtu":
                        case "persistentkeepalive":
                            {
                                if (!int.TryParse(m.Groups["value"].Value, out var intValue))
                                {
                                    txtEditor.UnderlineSelection();
                                    hasErrors = true;
                                }
                                else
                                {
                                    if (intValue < 0 || intValue > 65535)
                                    {
                                        txtEditor.UnderlineSelection();
                                        hasErrors = true;
                                    }
                                }
                            }
                            break;
                        // Comma-delimited string values
                        case "allowedapps":
                        case "disallowedapps":
                            {
                                foreach (Match e in MultiValueMatch.Matches(value))
                                    if (!string.IsNullOrWhiteSpace(e.Value) &&
                                        !Regex.IsMatch(e.Value, @"^[a-z0-9_-]+$", RegexOptions.IgnoreCase))
                                    {
                                        txtEditor.SelectionStart = m.Groups["value"].Index + e.Index;
                                        txtEditor.SelectionLength = e.Length;
                                        txtEditor.UnderlineSelection();
                                        hasErrors = true;
                                    }
                            }
                            break;
                        // String values
                        case "socks5proxyusername":
                        case "socks5proxypassword":
                            break;
                        // Unrecognized keys
                        default:
                            txtEditor.SelectionStart = m.Groups["key"].Index;
                            txtEditor.SelectionLength = m.Groups["key"].Length;
                            txtEditor.UnderlineSelection();
                            hasErrors = true;
                            break;
                    }
                }
            }

            // restoring the original settings
            txtEditor.SelectionStart = originalIndex;
            txtEditor.SelectionLength = originalLength;
            txtEditor.SelectionColor = originalColor;

            txtEditor.Focus();

            btnSave.Enabled = !hasErrors;

            _highlighting = false;
        }

        private void Initialize()
        {
            InitializeComponent();

            Icon = Resources.ico;
            txtProfileName.SetCueBanner(Resources.EditProfileCue);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            var tmpProfile = Path.GetTempFileName();
            File.WriteAllText(tmpProfile, txtEditor.Text);

            try
            {
                var profile = new Profile(tmpProfile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Resources.EditProfileError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.Delete(tmpProfile);

                DialogResult = DialogResult.None;
                return;
            }

            if (string.IsNullOrWhiteSpace(txtProfileName.Text) ||
                txtProfileName.Text.IndexOfAny(Path.GetInvalidFileNameChars()) > 0)
            {
                MessageBox.Show(Resources.EditProfileNameError, Resources.EditProfileError, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                File.Delete(tmpProfile);

                DialogResult = DialogResult.None;
                return;
            }

            var profilePath = Path.Combine(Global.ConfigsFolder, txtProfileName.Text + ".conf");

            File.Delete(profilePath);
            File.Move(tmpProfile, profilePath);

            ReturnValue = txtProfileName.Text;

            Close();
        }

        private void OnProcessClick(object sender, EventArgs e)
        {
            using (var taskManager = new TaskManager())
            {
                if (taskManager.ShowDialog() != DialogResult.OK) return;
                txtEditor.SelectionLength = 0;
                txtEditor.SelectedText = taskManager.ReturnValue;
            }
        }

        private void OnProfileChanged(object sender, EventArgs e)
        {
            ApplySyntaxHighlighting();
        }
    }
}