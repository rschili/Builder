using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;
using Newtonsoft.Json.Linq;
using RSCoreLib.WPF;

namespace Builder
    {
    public class PartFile
        {
        public IDictionary<string, Product> Products { get; } = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, Part> Parts { get; } = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
        }

    public class Part
        {
        public string Name { get; set; }
        public string MakeFile { get; set; }
        public IList<SubPart> SubParts { get; } = new List<SubPart>();
        }

    public class SubPart
        {
        public string Name { get; set; }
        public string Repository { get; set; }
        public string PartFile { get; set; }
        }

    public class Product
        {
        public string Name { get; set; }
        public IList<SubPart> SubParts { get; } = new List<SubPart>();
        public IList<string> SubProducts { get; } = new List<string>();
        }

    public class PartFileScanner
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(PartFileScanner));
        
        public static IList<Part> DiscoverParts (CompiledBuildStrategy strat, string srcPath)
            {
            var defaultTarget = strat.DefaultTarget;
            if (defaultTarget == null || string.IsNullOrEmpty(defaultTarget.PartFile) || string.IsNullOrEmpty(defaultTarget.Repository))
                return null;

            LocalRepository rep;
            if(!strat.LocalRepositories.TryGetValue(defaultTarget.Repository, out rep) || rep == null)
                {
                log.Info($"Did not find repository {defaultTarget.Repository} path.");
                return null;
                }

            string path = Path.Combine(srcPath, rep.Directory);
            PartFile p = LoadPartFile(path, defaultTarget.PartFile);
            return null;
            }

        private static IEnumerable<SubPart> ReadSubParts(XElement node)
            {
            var subParts = node.Elements().Where(e => e.Name.LocalName.Equals("SubPart", StringComparison.OrdinalIgnoreCase));
            foreach (var subPart in subParts)
                {
                var sName = subPart.Attribute("PartName")?.Value;
                if (string.IsNullOrEmpty(sName))
                    continue;

                yield return new SubPart()
                    {
                    Name = sName,
                    Repository = subPart.Attribute("Repository")?.Value,
                    PartFile = subPart.Attribute("PartFile")?.Value,
                    };
                }
            }

        private static IEnumerable<string> ReadSubProducts (XElement node)
            {
            var subParts = node.Elements().Where(e => e.Name.LocalName.Equals("SubPart", StringComparison.OrdinalIgnoreCase));
            foreach (var subPart in subParts)
                {
                var sName = subPart.Attribute("ProductName")?.Value;
                if (string.IsNullOrEmpty(sName))
                    continue;

                yield return sName;
                }
            }

        public static PartFile LoadPartFile (string directory, string partFile)
            {
            string fileName = $"{partFile}.PartFile.xml";
            string fullPath = Path.Combine(directory, fileName);

            if(!File.Exists(fullPath))
                {
                log.Info($"Part file not found: {fullPath}.");
                return null;
                }

            var xml = XDocument.Load(fullPath);
            var rootNode = xml.Elements().Where(e => string.Equals(e.Name.LocalName, "BuildContext", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (rootNode == null)
                return null;

            var result = new PartFile();
            foreach (var node in rootNode.Elements())
                {
                if (node.NodeType != System.Xml.XmlNodeType.Element)
                    continue;

                switch (node.Name.LocalName)
                    {
                    case "Part":
                        var name = node.Attribute("Name")?.Value;
                        if (string.IsNullOrEmpty(name))
                            continue;
                        
                        var part = new Part()
                            {
                            Name = name,
                            MakeFile = node.Attribute("BentleyBuildMakeFile")?.Value
                            };

                        foreach(var subPart in ReadSubParts(node))
                            {
                            part.SubParts.Add(subPart);
                            }

                        result.Parts[name] = part;
                        break;

                    case "Product":
                        var pName = node.Attribute("Name")?.Value;
                        if (string.IsNullOrEmpty(pName))
                            continue;

                        var product = new Product()
                            {
                            Name = pName
                            };

                        foreach (var subPart in ReadSubParts(node))
                            {
                            product.SubParts.Add(subPart);
                            }

                        foreach (var subProduct in ReadSubProducts(node))
                            {
                            product.SubProducts.Add(subProduct);
                            }

                        result.Products[pName] = product;
                        break;
                        
                    //simply ignore these, they are not relevant
                    case "ProductDirectoryList":
                        break;

                    default:
                        log.InfoFormat("Unknown Element encountered: {0} in file {1}", node.Name, fullPath);
                        break;
                    }
                }

            return result;
            }
        }
    }
