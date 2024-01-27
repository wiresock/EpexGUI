using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WireSockUI.Extensions;
using WireSockUI.Native;

namespace WireSockUI.Config
{
    /// <summary>
    ///     WireGuard Profile, including WireSock extensions
    /// </summary>
    internal class Profile
    {
        private string _address;

        // WireSock Extensions
        private string _allowedIPs;
        private string _dns;
        private string _endpoint;
        private string _listenport;
        private string _mtu;
        private string _persistentKeepAlive;

        private string _presharedKey;

        // Interface values
        private string _privateKey;

        // Peer values
        private string _publicKey;
        private string _socks5Proxy;

        /// <summary>
        ///     Create an empty profile from scratch
        /// </summary>
        public Profile()
        {
        }

        /// <summary>
        ///     Load a profile from specified filepath
        /// </summary>
        /// <param name="profilePath">Full filepath to a profile</param>
        public Profile(string profilePath)
        {
            if (!File.Exists(profilePath))
                throw new FileNotFoundException($"Profile {Path.GetFileName(profilePath)} does not exist.");

            var sections = IniFile.GetSectionNames(profilePath);

            if (!sections.Contains("Interface"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)} does not contain an \"Interface\" section.");

            var section = IniFile.GetSection(profilePath, "Interface");

            // Validate minimum required fields
            if (!section.ContainsKey("PrivateKey"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)}, section \"Interface\" does not have a \"PrivateKey\" defined.");

            if (!section.ContainsKey("Address"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)}, section \"Interface\" does not have a \"Address\" defined.");

            PrivateKey = section.Get("PrivateKey");
            Address = section.Get("Address");
            Dns = section.Get("DNS");
            Mtu = section.Get("MTU");

            if (!sections.Contains("Peer"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)} does not contain an \"Peer\" section.");

            section = IniFile.GetSection(profilePath, "Peer");

            // Validate minimum required fields
            if (!section.ContainsKey("PublicKey"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)}, section \"Peer\" does not have a \"PublicKey\" defined.");

            if (!section.ContainsKey("Endpoint"))
                throw new ArgumentException(
                    $"Profile {Path.GetFileName(profilePath)}, section \"Peer\" does not have a \"Endpoint\" defined.");

            PeerKey = section.Get("PublicKey");
            PresharedKey = section.Get("PresharedKey");
            AllowedIPs = section.Get("AllowedIPs");
            Endpoint = section.Get("Endpoint");
            PersistentKeepAlive = section.Get("PersistentKeepAlive");

            AllowedApps = section.Get("AllowedApps");
            DisallowedApps = section.Get("DisallowedApps");
            DisallowedIPs = section.Get("DisallowedIPs");
            Socks5Proxy = section.Get("Socks5Proxy");
            Socks5ProxyUsername = section.Get("Socks5Username");
            Socks5ProxyPassword = section.Get("Socks5ProxyPassword");
        }

        /// <summary>
        ///     Local interface private key
        /// </summary>
        public string PrivateKey
        {
            get => _privateKey;
            set
            {
                ValidateKey("Interface", "PrivateKey", value);
                _privateKey = value;
            }
        }

        /// <summary>
        ///     Local interface public key, derived from private key
        /// </summary>
        public string PublicKey
        {
            get
            {
                if (!string.IsNullOrEmpty(_privateKey))
                    // Determine public key from private key data
                    return
                        Convert.ToBase64String(
                            Curve25519.GetPublicKey(
                                Convert.FromBase64String(PrivateKey)));

                return null;
            }
        }

        /// <summary>
        ///     List of interface IP addresses
        /// </summary>
        public string Address
        {
            get => _address;
            set
            {
                ValidateAddresses("Interface", "Address", value, IpHelper.IsValidSubnetOrSingleIpAddress);
                _address = value;
            }
        }

        /// <summary>
        ///     List of interface DNS servers
        /// </summary>
        public string Dns
        {
            get => _dns;
            set
            {
                ValidateAddresses("Interface", "DNS", value, IpHelper.IsValidIpAddress);
                _dns = value;
            }
        }

        /// <summary>
        ///     Interface Maximum Transmissible Unit size
        /// </summary>
        public string Mtu
        {
            get => _mtu;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!int.TryParse(value, out var mtu))
                        throw new FormatException("\"MTU\" in \"Interface\", is not a numerical value.");

                    if (mtu < 576 || mtu > 65535)
                        throw new FormatException("\"MTU\" in \"Interface\", invalid value. Expected 576...65535.");

                    _mtu = value;
                }
                else
                {
                    _mtu = null;
                }
            }
        }

        /// <summary>
        ///     Interface ListenPort
        /// </summary>
        public string ListenPort
        {
            get => _listenport;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!int.TryParse(value, out var listenPort))
                        throw new FormatException("\"ListenPort\" in \"Interface\", is not a numerical value.");

                    if (listenPort < 1 || listenPort > 65535)
                        throw new FormatException(
                            "\"ListenPort\" in \"Interface\", invalid value. Expected 1...65535.");

                    _listenport = value;
                }
                else
                {
                    _listenport = null;
                }
            }
        }

        /// <summary>
        ///     Peer public key
        /// </summary>
        public string PeerKey
        {
            get => _publicKey;
            set
            {
                ValidateKey("Peer", "PublicKey", value);
                _publicKey = value;
            }
        }

        /// <summary>
        ///     Peer preshared key (optional)
        /// </summary>
        public string PresharedKey
        {
            get => _presharedKey;
            set
            {
                ValidateKey("Peer", "PresharedKey", value);
                _presharedKey = value;
            }
        }

        /// <summary>
        ///     Peer allowed IP list
        /// </summary>
        public string AllowedIPs
        {
            get => _allowedIPs;
            set
            {
                ValidateAddresses("Peer", "AllowedIPs", value, IpHelper.IsValidIpNetwork);
                _allowedIPs = value;
            }
        }

        /// <summary>
        ///     Peer endpoint address (DNS or IP)
        /// </summary>
        public string Endpoint
        {
            get => _endpoint;
            set
            {
                if (!IpHelper.IsValidAddress(value))
                    throw new FormatException("\"Endpoint\" in \"Peer\", is not a valid IPv4, IPv6 or domain address.");

                _endpoint = value;
            }
        }


        /// <summary>
        ///     Persistent keep alive interval
        /// </summary>
        public string PersistentKeepAlive
        {
            get => _persistentKeepAlive;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!int.TryParse(value, out var mtu))
                        throw new FormatException("\"PersistentKeepalive\" in \"Peer\", is not a numerical value.");

                    if (mtu < 0 || mtu > 65535)
                        throw new FormatException(
                            "\"PersistentKeepalive\" in \"Peer\", invalid value. Expected 0...65535.");

                    _persistentKeepAlive = value;
                }
                else
                {
                    _persistentKeepAlive = null;
                }
            }
        }

        /// <summary>
        ///     Peer allowed applications list
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string AllowedApps { get; set; }

        /// <summary>
        ///     Peer disallowed applications list
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string DisallowedApps { get; set; }

        /// <summary>
        ///     Peer disallowed IP addresses
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string DisallowedIPs { get; set; }

        /// <summary>
        ///     Peer SOCKS5 proxy
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string Socks5Proxy
        {
            get => _socks5Proxy;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (!IpHelper.IsValidAddress(value))
                        throw new FormatException(
                            "\"Endpoint\" in \"Peer\", is not a valid IPv4, IPv6 or domain address.");

                    _socks5Proxy = value;
                }
                else
                {
                    _socks5Proxy = null;
                }
            }
        }

        /// <summary>
        ///     Peer SOCKS5 proxy username
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string Socks5ProxyUsername { get; set; }

        /// <summary>
        ///     Peer SOCKS5 proxy password
        /// </summary>
        /// <remarks>WireSock specific extension</remarks>
        public string Socks5ProxyPassword { get; set; }

        internal static void ValidateKey(string section, string key, string keyValue)
        {
            byte[] keyBinary;

            if (string.IsNullOrWhiteSpace(keyValue)) return;

            try
            {
                keyBinary = Convert.FromBase64String(keyValue);
            }
            catch (FormatException)
            {
                throw new FormatException($"\"{key}\" in \"{section}\", invalid base64 encoded value.");
            }

            // 256-bit keys only
            if (keyBinary.Length != 32)
                throw new FormatException(
                    $"\"{key}\" in \"{section}\", invalid key length, only 256-bit keys are supported.");
        }

        internal static void ValidateAddresses(string section, string key, string keyValue,
            Func<string, bool> validator)
        {
            if (string.IsNullOrWhiteSpace(keyValue)) return;

            foreach (var value in keyValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (!validator(value.Trim()))
                    throw new FormatException($"\"{key}\" in \"{section}\", invalid address \"{value}\".");
        }

        public static IEnumerable<string> GetProfiles()
        {
            var files = Directory.GetFiles(Global.ConfigsFolder);

            foreach (var file in files)
            {
                if (!file.EndsWith(".conf")) continue;

                yield return Path.GetFileNameWithoutExtension(file);
            }
        }

        /// <summary>
        ///     Retrieve the full path to a given <paramref name="profileName" />
        /// </summary>
        /// <param name="profileName">Profile name</param>
        /// <returns>Full path to the profile</returns>
        /// <remarks>The profile might not exist, this merely returns the path it should be at.</remarks>
        public static string GetProfilePath(string profileName)
        {
            return Path.Combine(Global.ConfigsFolder, profileName + ".conf");
        }

        /// <summary>
        ///     Load an existing named profile
        /// </summary>
        /// <param name="profileName">Profile name (i.e. filename without extension)</param>
        public static Profile LoadProfile(string profileName)
        {
            var filename = GetProfilePath(profileName);
            return new Profile(filename);
        }
    }
}