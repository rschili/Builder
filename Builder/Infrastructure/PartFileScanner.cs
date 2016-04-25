using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using log4net;
using RSCoreLib;

namespace Builder
    {
    public class PartFile
        {
        public IList<Product> Products { get; } = new List<Product>();
        public IList<Part> Parts { get; } = new List<Part>();
        public string Directory { get; internal set; }
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
        public bool IsProduct { get; set; } = false;
        }

    public class Product : Part
        {
        }

    public class PartFileScanner
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(PartFileScanner));
        
        public static PartFile LoadPartFile (string name, string repository, IDictionary<string, LocalRepository> repositories, string srcPath)
            {
            LocalRepository rep;
            if(!repositories.TryGetValue(repository, out rep) || rep == null)
                {
                throw new UserfriendlyException($"Did not find repository {repository} path.");
                }
            string fileName = $"{name}.PartFile.xml";

            if(fileName.StartsWith("${SrcRoot}", true, CultureInfo.InvariantCulture))
                {
                //this is hacky. Some people seem to put the fill path starting with SrcRoot into the Part File Name instead of
                //having it resolved automatically.
                var p = Path.Combine(srcPath, fileName.Substring(10));
                return LoadPartFile(p, Path.GetDirectoryName(p));
                }

            if (fileName.StartsWith("${SrcBsiCommon}", true, CultureInfo.InvariantCulture))
                {
                //this is hacky. Some people seem to put the fill path starting with SrcBsiCommon into the Part File Name instead of
                //having it resolved automatically.
                var p = Path.Combine(srcPath, "bsicommon", fileName.Substring(15));
                return LoadPartFile(p, Path.GetDirectoryName(p));
                }
            
            string directory = Path.Combine(srcPath, rep.Directory);
            string fullPath = Path.Combine(directory, fileName);
            return LoadPartFile(fullPath, repository);
            }

        private static IEnumerable<SubPart> ReadSubParts(XElement node, string repository)
            {
            var subParts = node.Elements().Where(e => e.Name.LocalName.Equals("SubPart", StringComparison.OrdinalIgnoreCase));
            foreach (var subPart in subParts)
                {
                var sName = subPart.Attribute("PartName")?.Value;
                if (string.IsNullOrEmpty(sName))
                    continue;

                var sp = new SubPart()
                    {
                    Name = sName,
                    Repository = subPart.Attribute("Repository")?.Value,
                    PartFile = subPart.Attribute("PartFile")?.Value,
                    };

                if (!string.IsNullOrEmpty(sp.PartFile) && string.IsNullOrEmpty(sp.Repository))
                    sp.Repository = repository;

                yield return sp;
                }
            }

        private static IEnumerable<SubPart> ReadSubProducts (XElement node, string repository)
            {
            var subParts = node.Elements().Where(e => e.Name.LocalName.Equals("SubProduct", StringComparison.OrdinalIgnoreCase));
            foreach (var subPart in subParts)
                {
                var sName = subPart.Attribute("ProductName")?.Value;
                if (string.IsNullOrEmpty(sName))
                    continue;

                var sp = new SubPart()
                    {
                    Name = sName,
                    Repository = subPart.Attribute("Repository")?.Value,
                    PartFile = subPart.Attribute("PartFile")?.Value,
                    IsProduct = true,
                    };

                if (!string.IsNullOrEmpty(sp.PartFile) && string.IsNullOrEmpty(sp.Repository))
                    sp.Repository = repository;

                yield return sp;
                }
            }
        
        public static PartFile LoadPartFile (string fullPath, string repository)
            {
            if(!File.Exists(fullPath))
                {
                throw new UserfriendlyException($"Part file not found: {fullPath}.");
                }

            var xml = XDocument.Load(fullPath);
            var rootNode = xml.Elements().Where(e => string.Equals(e.Name.LocalName, "BuildContext", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (rootNode == null)
                return null;

            var result = new PartFile();
            result.Directory = Path.GetDirectoryName(fullPath);
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

                        if (string.IsNullOrEmpty(part.MakeFile))
                            part.MakeFile = node.Attribute("BMakeFile")?.Value;

                        foreach (var subPart in ReadSubParts(node, repository))
                            {
                            part.SubParts.Add(subPart);
                            }

                        result.Parts.Add(part);
                        break;

                    case "Product":
                        var pName = node.Attribute("Name")?.Value;
                        if (string.IsNullOrEmpty(pName))
                            continue;

                        var product = new Product()
                            {
                            Name = pName
                            };

                        foreach (var subProduct in ReadSubProducts(node, repository))
                            {
                            product.SubParts.Add(subProduct);
                            }

                        foreach (var subPart in ReadSubParts(node, repository))
                            {
                            product.SubParts.Add(subPart);
                            }

                        result.Products.Add(product);
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
