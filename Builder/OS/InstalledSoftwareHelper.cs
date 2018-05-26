using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RSCoreLib.OS
    {
    public class InstalledSoftware
        {
        public string DisplayName;
        public string InstallationPath;
        public int? VersionMajor;
        public string DisplayVersion;
        }

    public static class InstalledSoftwareHelper
        {
        
        public static IEnumerable<InstalledSoftware> GetInstalledSoftware()
            {
            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var firstEnumerable = EnumerateUninstallKey(registry_key);

            if(!Environment.Is64BitProcess)
                {
                return firstEnumerable;
                }

            string registry_key32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
            return Enumerable.Concat(firstEnumerable, EnumerateUninstallKey(registry_key32));
            }

        private static IEnumerable<InstalledSoftware> EnumerateUninstallKey (string registry_key)
            {
            using (var key = Registry.LocalMachine.OpenSubKey(registry_key))
                {
                foreach (string subkey_name in key.GetSubKeyNames())
                    {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                        {
                        var name = subkey.GetValue("DisplayName")?.ToString() ?? null;
                        var path = subkey.GetValue("InstallLocation")?.ToString() ?? null;

                        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(path))
                            continue;

                        var versionMajorO = subkey.GetValue("VersionMajor");
                        int? versionMajor = null;
                        if (versionMajorO != null)
                            {
                            if (versionMajorO is int)
                                {
                                versionMajor = (int)versionMajorO;
                                }
                            else
                                {
                                versionMajor = Convert.ToInt32(versionMajorO);
                                }
                            }

                        yield return new InstalledSoftware()
                            {
                            DisplayName = name,
                            InstallationPath = path,
                            VersionMajor = versionMajor,
                            DisplayVersion = subkey.GetValue("DisplayVersion")?.ToString() ?? null
                            };
                        }
                    }
                }
            }

        }
    }
