using System;
using System.IO;

namespace RSCoreLib
    {
    public static class PathHelper
        {
        public static string EnsureTrailingDirectorySeparator(string path)
            {
            if (string.IsNullOrEmpty(path))
                return path;

            path = FromSlash(path);
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                path = path + Path.DirectorySeparatorChar;

            return path;
            }

        public static bool PointsToSameDirectory (string a, string b)
            {
            var strA = PathHelper.EnsureTrailingDirectorySeparator(a);
            var strB = PathHelper.EnsureTrailingDirectorySeparator(b);
            return string.Equals(strA, strB, StringComparison.OrdinalIgnoreCase);
            }

        public static string GetApplicationDir ()
            {
            return AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            }


        public static bool IsDirectory (string path)
            {
            try
                {
                FileAttributes attr = File.GetAttributes(path);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    return true;
                }
            catch (Exception e)
                {
                Log.Error("Exception trying to check if {0} is a directory. Message: {1}", path, e.Message);
                }

            return false;
            }

        public static bool IsSeparator(char c)
            {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
            }

        public static string FromSlash(string path)
            {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
        
        }
    }
