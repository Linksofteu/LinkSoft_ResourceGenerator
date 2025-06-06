using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace LinkSoft.ResourceGenerator
{
    [Generator]
    public class ResxSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization required
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Always add a diagnostic file so we can see the generator is working
            var diagnosticSb = new StringBuilder();
            diagnosticSb.AppendLine("// Diagnostic information from ResourceGenerator");
            diagnosticSb.AppendLine("// Generated on: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            diagnosticSb.AppendLine("// Total AdditionalFiles: " + context.AdditionalFiles.Count());
            
            // Get all .resx files in the project
            var resxFiles = context.AdditionalFiles
                .Where(file => Path.GetExtension(file.Path).Equals(".resx", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            diagnosticSb.AppendLine("// Found .resx files: " + resxFiles.Count);
            foreach (var file in resxFiles)
            {
                diagnosticSb.AppendLine("// - " + file.Path);
            }
            
            // Add compilation info to diagnostics
            diagnosticSb.AppendLine("// Compilation.AssemblyName: " + context.Compilation.AssemblyName);
            var assemblyIdentity = context.Compilation.Assembly.Identity;
            diagnosticSb.AppendLine("// Assembly.Identity.Name: " + assemblyIdentity.Name);
            
            if(!TryGetAssemblyRootNamespace(context, out var defaultNamespace))
            {
                defaultNamespace = "GeneratedNamespace";
                
                diagnosticSb.AppendLine("// Assembly root namespace NOT set (FIX this in project settings).");
                diagnosticSb.AppendLine("// Using default placeholder namespace name: " + defaultNamespace);
            }
            else
                diagnosticSb.AppendLine("// Using Assembly root namespace for generated code: " + defaultNamespace);
            
            // Add diagnostic info regardless of whether we found any resx files
            context.AddSource("ResourceGenerator.Diagnostics.g.cs", diagnosticSb.ToString());
            
            if (!resxFiles.Any())
                return;
            
            // Instead of grouping by directory, let's just generate one file for all resources
            var resourcesCode = GenerateResourcesCodeForAllFiles(resxFiles, defaultNamespace);
            context.AddSource("Resources.g.cs", resourcesCode);
        }
        
        private bool TryGetAssemblyRootNamespace(GeneratorExecutionContext context, out string rootNamespace)
        {
            // this how Microsoft itself does it...
            // https://github.com/dotnet/roslyn/blob/d7f5e615c6b66792b332a56cd655045fd15fea45/src/Analyzers/Core/Analyzers/MatchFolderAndNamespace/AbstractMatchFolderAndNamespaceDiagnosticAnalyzer.cs#L61
            return context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out rootNamespace);
        }
        
        private string GenerateResourcesCodeForAllFiles(List<AdditionalText> resxFiles, string defaultNamespace)
        {
            var sb = new StringBuilder();
            
            // Use the detected namespace from the project
            string resourceNamespace = defaultNamespace;
            
            // Comment indicating the namespace being used
            sb.AppendLine($"// Generated for namespace: {resourceNamespace}");
            
            // Add usings
            sb.AppendLine("using System;");
            sb.AppendLine("using Abp.Dependency;");
            sb.AppendLine("using LinkSoft.Localization;");
            sb.AppendLine("// ReSharper disable MemberHidesStaticFromOuterClass");
            sb.AppendLine("// ReSharper disable InconsistentNaming");
            sb.AppendLine();
            
            // Start namespace
            sb.AppendLine($"namespace {resourceNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class Resx");
            sb.AppendLine("    {");
            sb.AppendLine("        public static ILocalizationService Service { get; set; }");
            sb.AppendLine("        public static ILocalizationService GetService()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Service == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                // ReSharper disable once IsSingleton");
            sb.AppendLine("                Service = IocManager.Instance.Resolve<ILocalizationService>();");
            sb.AppendLine("            }");
            sb.AppendLine("            return Service;");
            sb.AppendLine("        }");
            sb.AppendLine("        public static string GetString(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return GetService().GetString(key) ?? string.Empty;");
            sb.AppendLine("        }");
            
            // Process each .resx file
            foreach (var resxFile in resxFiles.Where(f => !Regex.IsMatch(Path.GetFileName(f.Path), @"\.[a-zA-z]{2}(-[a-zA-z]{2})?\.resx")))
            {
                try
                {
                    var fileContent = resxFile.GetText()?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(fileContent))
                        continue;
                    
                    var className = Path.GetFileNameWithoutExtension(resxFile.Path);
                    var entries = GetEntriesFromResxContent(fileContent);
                    
                    if (!entries.Any())
                        continue;
                    
                    sb.AppendLine($"        public static class {className}");
                    sb.AppendLine("        {");
                    sb.AppendLine("            public static class Keys");
                    sb.AppendLine("            {");
                    sb.AppendLine("                #pragma warning disable S3218");
                    
                    // Generate keys
                    foreach (var entry in entries)
                    {
                        sb.AppendLine($"                public const string {entry.Name} = \"{className}:{entry.Key}\";");
                    }
                    
                    sb.AppendLine("                #pragma warning restore S3218");
                    sb.AppendLine("            }");
                    sb.AppendLine();
                    
                    // Generate properties
                    foreach (var entry in entries)
                    {
                        sb.AppendLine($"            /// <summary>");
                        if (!string.IsNullOrEmpty(entry.Value))
                        {
                            foreach (var line in entry.Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                            {
                                sb.AppendLine($"            /// {System.Net.WebUtility.HtmlEncode(line)}");
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(entry.Comment))
                        {
                            sb.AppendLine($"            /// ({entry.Comment})");
                        }
                        
                        sb.AppendLine($"            /// </summary>");
                        sb.AppendLine($"            public static string {entry.Name} => GetString(Keys.{entry.Name});");
                    }
                    
                    sb.AppendLine("        }");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("        // Error processing " + Path.GetFileName(resxFile.Path) + ": " + ex.Message);
                }
            }
            
            // End namespace and class
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private string GenerateResourcesCodeForDirectory(List<AdditionalText> resxFiles, string defaultNamespace)
        {
            var sb = new StringBuilder();
            
            // Add usings
            sb.AppendLine("using System;");
            sb.AppendLine("using Abp.Dependency;");
            sb.AppendLine("using LinkSoft.Localization;");
            sb.AppendLine("// ReSharper disable MemberHidesStaticFromOuterClass");
            sb.AppendLine("// ReSharper disable InconsistentNaming");
            sb.AppendLine();
            
            // Get directory name for namespace
            var directory = Path.GetDirectoryName(resxFiles.First().Path);
            var directoryName = Path.GetFileName(directory);
            
            // Start namespace
            sb.AppendLine($"namespace {defaultNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class Resx");
            sb.AppendLine("    {");
            sb.AppendLine("        public static ILocalizationService Service { get; set; }");
            sb.AppendLine("        public static ILocalizationService GetService()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Service == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                // ReSharper disable once IsSingleton");
            sb.AppendLine("                Service = IocManager.Instance.Resolve<ILocalizationService>();");
            sb.AppendLine("            }");
            sb.AppendLine("            return Service;");
            sb.AppendLine("        }");
            sb.AppendLine("        public static string GetString(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            return GetService().GetString(key) ?? string.Empty;");
            sb.AppendLine("        }");
            
            // Process each .resx file
            foreach (var resxFile in resxFiles.Where(f => !Regex.IsMatch(Path.GetFileName(f.Path), @"\.[a-zA-z]{2}(-[a-zA-z]{2})?\.resx")))
            {
                var fileContent = resxFile.GetText()?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(fileContent))
                    continue;
                
                var className = Path.GetFileNameWithoutExtension(resxFile.Path);
                var entries = GetEntriesFromResxContent(fileContent);
                
                if (!entries.Any())
                    continue;
                
                sb.AppendLine($"        public static class {className}");
                sb.AppendLine("        {");
                sb.AppendLine("            public static class Keys");
                sb.AppendLine("            {");
                sb.AppendLine("                #pragma warning disable S3218");
                
                // Generate keys
                foreach (var entry in entries)
                {
                    sb.AppendLine($"                public const string {entry.Name} = \"{className}:{entry.Key}\";");
                }
                
                sb.AppendLine("                #pragma warning restore S3218");
                sb.AppendLine("            }");
                sb.AppendLine();
                
                // Generate properties
                foreach (var entry in entries)
                {
                    sb.AppendLine($"            /// <summary>");
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        foreach (var line in entry.Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                        {
                            sb.AppendLine($"            /// {System.Net.WebUtility.HtmlEncode(line)}");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        sb.AppendLine($"            /// ({entry.Comment})");
                    }
                    
                    sb.AppendLine($"            /// </summary>");
                    sb.AppendLine($"            public static string {entry.Name} => GetString(Keys.{entry.Name});");
                    sb.AppendLine();
                }
                
                sb.AppendLine("        }");
            }
            
            // End namespace and class
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private List<ResourceEntry> GetEntriesFromResxContent(string resxContent)
        {
            var entries = new List<ResourceEntry>();
            
            try
            {
                var xml = new XmlDocument();
                xml.LoadXml(resxContent);
                
                var dataNodes = xml.SelectNodes("/root/data");
                if (dataNodes == null)
                    return entries;
                
                foreach (XmlElement element in dataNodes)
                {
                    var name = GetAttrValue(element, "name");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    
                    var entry = new ResourceEntry
                    {
                        Key = name,
                        Name = name,
                        Comment = GetElementText(element, "comment"),
                        Value = GetElementText(element, "value")
                    };
                    
                    var hashIndex = entry.Name.IndexOf("#");
                    if (hashIndex >= 0)
                    {
                        entry.Name = entry.Name.Substring(0, hashIndex);
                        entry.Key = entry.Name + "#";
                    }
                    
                    entry.Name = SafeIdentifier(entry.Name);
                    
                    var existingEntry = entries.FirstOrDefault(x => x.Name == entry.Name);
                    if (existingEntry != null)
                    {
                        if (entry.Key.EndsWith("#"))
                        {
                            existingEntry.Key = entry.Key;
                        }
                    }
                    else
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch
            {
                // If there are any issues parsing the XML, return an empty list
            }
            
            return entries;
        }
        
        private string GetAttrValue(XmlNode node, string attrName)
        {
            if (node == null) return null;
            var attr = node.Attributes?[attrName];
            return attr?.Value;
        }
        
        private string GetElementText(XmlNode node, string xpath)
        {
            if (node == null) return null;
            var n = node.SelectSingleNode(xpath);
            return n?.InnerText;
        }
        
        private string SafeIdentifier(string identifier)
        {
            return identifier.Replace("-", "_").Replace(".", "_");
        }
        
        private class ResourceEntry
        {
            public string Name { get; set; }
            public string Key { get; set; }
            public string Value { get; set; }
            public string Comment { get; set; }
        }
    }
}
