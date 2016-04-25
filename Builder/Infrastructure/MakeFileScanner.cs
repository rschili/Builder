using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using log4net;
using RSCoreLib;

namespace Builder
    {
    public class MakeFileScanner
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(MakeFileScanner));
        
        public static PartType GuessPartTypeFromMakeFile (string filePath)
            {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return PartType.Unknown;

            foreach(var line in File.ReadLines(filePath))
                {
                if(string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.Contains(".cpp ") || line.Contains(".vcxproj"))
                    return PartType.Cpp;

                if (line.Contains(".cs ") || line.Contains(".csproj"))
                    return PartType.CSharp;
                }

            return PartType.Unknown;
            }
        
        }
    }
