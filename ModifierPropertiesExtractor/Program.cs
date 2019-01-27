using Mono.Cecil;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

namespace ModifierPropertiesExtractor
{
    class Program
    {
        static Dictionary<string, string> TypeMap = new Dictionary<string, string>
        {
            { "System.Single", "float" },
            { "System.Int32", "int" },
            { "System.Double", "double" },
            { "System.String", "string" },
            { "System.Boolean", "bool" }
        };

        static ModuleDefinition module;
        static ModuleDefinition modApi;
        static void Main(string[] args)
        {
            Console.Write("File Path> ");
            string filePath = Console.ReadLine();
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = "E:\\SR2Debug\\Game\\SimpleRockets2_Data\\Managed\\SimpleRockets2.dll";
            }
            module = ModuleDefinition.ReadModule(filePath);
            ((DefaultAssemblyResolver)module.AssemblyResolver).AddSearchDirectory(new FileInfo(filePath).DirectoryName);
            modApi = module.AssemblyResolver.Resolve(module.AssemblyReferences.First(e => e.Name == "ModApi")).MainModule;


            Console.WriteLine("Got module: " + module.Name);
            Console.WriteLine("Got module: " + modApi.Name);
            Console.Write("Action> ");
            string command = Console.ReadLine();
            switch (command)
            {
                case "GetModifierScriptProperties":
                    GetModifierScriptProperties();
                    break;
                case "GetPartModifierOptions":
                    GetPartModifierOptions();
                    break;
                case "GenPages":
                    GeneratePages();
                    break;
                default:
                    Console.WriteLine("Unknown Command");
                    break;
            }
            Console.Write("Press any key to exit.");
            Console.ReadKey();

        }

        static void GetModifierScriptProperties()
        {
            Console.WriteLine("|Name|Type|\n|--|--|");
            foreach (TypeDefinition type in module.Types)
            {
                bool isMod = false;
                TypeReference t = type.BaseType;
                while (t != null)
                {
                    if (t.FullName.StartsWith("ModApi.Craft.Parts.PartModifierScript"))
                    {
                        isMod = true;
                        break;
                    }
                    if (t.Scope.Name != module.Name) { break; }
                    t = t.Resolve().BaseType;
                }

                if (isMod)
                {
                    string name = type.Name;
                    if (name.EndsWith("Script"))
                    {
                        name = name.Remove(name.Length - 6);
                    }
                    name = "|`" + name + ".";
                    foreach (var property in type.Properties)
                    {
                        string typeName = null;
                        var propType = property.PropertyType;
                        if (propType.FullName == "System.Single")
                        {
                            typeName = "float";
                        }
                        else if (propType.FullName == "System.Double")
                        {
                            typeName = "double";
                        }
                        else if (propType.FullName == "System.Boolean")
                        {
                            typeName = "bool";
                        }
                        else
                        {
                            continue;
                        }
                        string pname = property.Name;
                        if (pname.Contains("."))
                        {
                            pname = pname.Substring(pname.LastIndexOf('.') + 1);
                        }
                        if (pname.StartsWith("_")) { continue; } // relies on style rules being obeyed...
                        Console.WriteLine(name + pname + "`|" + typeName + "|");
                    }
                }
            }
        }

        static void GeneratePages()
        {
            Console.Write("Output root dir> ");
            DirectoryInfo rootDir = new DirectoryInfo(Console.ReadLine());
            XDocument Manifest = XDocument.Load(rootDir.FullName + "\\/manifest.xml");
            XElement ManifestRoot = Manifest.Root;
            foreach (TypeDefinition modifierType in module.Types.Concat(modApi.Types).Where(IsPartModifierData))
            {
                foreach (var field in modifierType.Fields)
                {
                    List<PropertyEntry> properties = new List<PropertyEntry>();
                    foreach (var attr in field.CustomAttributes)
                    {
                        if (IsPartModifierProperty(field, out string description, out bool designerOnly))
                        {
                            if (designerOnly)
                            {
                                description = "Designer only. " + (description ?? "");
                            }
                            if (!TypeMap.TryGetValue(field.FieldType.Name, out string type)) { type = field.FieldType.Name; }
                            properties.Add(new PropertyEntry {
                                Name = field.Name.TrimStart('_'),
                                Type = type,
                                Desc = description ?? ""
                            });
                        }
                    }
                    string name = modifierType.Name;
                    if (name.EndsWith("Data"))
                    {
                        name = name.Remove(name.Length - 4);
                    }
                    GeneratePage(rootDir.FullName + "\\" + name + ".md", name, ManifestRoot, properties);
                }
            }
            Manifest.Save(rootDir.FullName + "\\/manifest.xml");
        }

        static void GetPartModifierOptions()
        {
            Console.Write("Output root dir> ");
            DirectoryInfo rootDir = new DirectoryInfo(Console.ReadLine());
            foreach (TypeDefinition type in module.Types.Concat(modApi.Types))
            {
                if (IsPartModifierData(type))
                {
                    string tname = type.Name;
                    if (tname.EndsWith("Data")) { tname = tname.Remove(tname.Length - 4); }
                    string filePath = rootDir.FullName + "\\" + tname + ".md";
                    StringBuilder s = new StringBuilder();
                    bool wrote = false;
                    foreach (FieldDefinition field in type.Fields)
                    {
                        if (IsPartModifierProperty(field, out string description, out bool designerOnly))
                        {
                            if (designerOnly)
                            {
                                description = "Designer only. " + (description ?? "");
                            }
                            string name = field.Name.TrimStart('_');
                            if (!TypeMap.TryGetValue(field.FieldType.FullName, out string typeName))
                            {
                                typeName = field.FieldType.Name;
                            }
                            s.Append("|`");
                            s.Append(name);
                            s.Append("`|`");
                            s.Append(typeName);
                            s.Append("`|");
                            s.Append(description);
                            s.AppendLine("|");
                            wrote = true;
                        }
                    }
                    string tableHead = "|Name|Type|Description|\n|--|--|--|\n";
                    if (!wrote)
                    {
                        s.AppendLine("This Part Modifier type has no XML properties.");
                        tableHead = "";
                    }
                    File.WriteAllText(filePath, "# " + tname + "\n\n" + tableHead + s.ToString());
                    Console.WriteLine("   - [" + tname + "](/Sr2Xml/" + tname + ")");
                }
            }
        }

        static bool IsPartModifierData(TypeDefinition type)
        {
            if (type.IsAbstract) { return false; }
            TypeReference t = type.BaseType;
            while (t != null)
            {
                if (t.FullName.StartsWith("ModApi.Craft.Parts.PartModifierData"))
                {
                    return true;
                }
                t = t.Resolve().BaseType;
            }
            return false;
        }
        static bool IsPartModifierProperty(FieldDefinition field, out string tooltip, out bool designerOnly)
        {
            designerOnly = false;
            tooltip = null;
            try
            {
                foreach (var attr in field.CustomAttributes)
                {
                    TypeDefinition at = attr.AttributeType.Resolve();
                    while (at != null)
                    {
                        if (at.FullName == "ModApi.Craft.Parts.Attributes.PartModifierPropertyAttribute")
                        {
                            foreach (var a in attr.Properties)
                            {
                                if (a.Name == "Tooltip") { tooltip = (string)a.Argument.Value; }
                                if (a.Name == "DesignerOnly" && (bool)a.Argument.Value == true)
                                {
                                    designerOnly = true;
                                }
                            }
                            return true;
                        }
                        if (at.BaseType == null) { break; }
                        at = at.BaseType.Resolve();
                    }
                }
            }
            catch (AssemblyResolutionException) { }
            return false;
        }

        class PropertyEntry
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Desc { get; set; }

        }

        static void GeneratePage(string path, string name, XElement manifest, List<PropertyEntry> properties)
        {
            XElement element = manifest.Element(name);
            if (element == null)
            {
                element = new XElement(name);
                manifest.Add(element);
            }
            var overrides = element.Element("Overrides")?.Elements();
            if (overrides == null) { overrides = new List<XElement>(); }
            using (var file = File.CreateText(path))
            {
                string head = element.GetChildContents("Head");
                if (string.IsNullOrEmpty(head))
                {
                    head = "# " + name + "\n";
                }
                file.WriteLine(head);

                file.WriteLine("\n|Name|Type|Description|\n|--|--|--|");
                foreach (var property in properties)
                {
                    var over = overrides.First(e => e.Name == property.Name);
                    string type = over.GetStringAttribute("type", property.Type);
                    string desc = over.Value?.Trim();
                    if (string.IsNullOrEmpty(desc)) { desc = property.Desc; }
                    desc = desc.Replace("\r\n", "\n").Replace('\n', ' ');
                    file.WriteLine("|`" + over.Name + "`|`" + type + "`|" + desc + "|");
                }

                file.Write('\n');
                string foot = element.GetChildContents("Footer", "");
                file.WriteLine(foot);
            }
        }
    }

    public static class Extensions
    {
        public static string GetChildContents(this XElement element, XName childName, string def = "")
        {
            var el = element.Element(childName);
            return el != null ? el.Value.Trim() : def;
        }
        public static string GetStringAttribute(this XElement element, XName name, string def = null)
        {
            var a = element.Attribute(name);
            return a != null ? a.Value : def;
        }
    }
}
