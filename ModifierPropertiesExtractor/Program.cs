using Mono.Cecil;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Mono.Cecil.Cil;

namespace ModifierPropertiesExtractor
{
    static class Program
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
        static string rootDir;
        static XDocument _Manifest;
        static XElement ManifestRoot;
        static void Main(string[] args)
        {
            Console.Write("Output root dir> ");
            rootDir = Console.ReadLine().TrimEnd('\\');
            if (rootDir == "") { rootDir = "E:\\OtherRandomCSProjects\\Sr2Xml"; }

            _Manifest = XDocument.Load(rootDir + "\\manifest.xml");
            ManifestRoot = _Manifest.Root;

            Console.Write("File Path> ");
            string filePath = Console.ReadLine();
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = @"E:\SteamLibrary\steamapps\common\SimpleRockets2\SimpleRockets2_Data\Managed\SimpleRockets2.dll";
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
                    var w = new StreamWriter(Console.OpenStandardOutput());
                    GetModifierScriptProperties(w);
                    w.Close();
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

        static void GetModifierScriptProperties(StreamWriter stream)
        {
            stream.WriteLine("|Name|Type|\n|--|--|");
            foreach (TypeDefinition type in module.Types)
            {
                bool isMod = false;
                bool isModData = false;
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
                if (!isMod)
                {
                    while (t != null)
                    {
                        if (t.FullName.StartsWith("ModApi.Craft.Parts.PartModifierData"))
                        {
                            isModData = true;
                            break;
                        }
                        if (t.Scope.Name != module.Name) { break; }
                        t = t.Resolve().BaseType;
                    }
                }

                if (isMod || isModData)
                {
                    string name = type.Name;
                    if (name.EndsWith("Script"))
                    {
                        name = name.Remove(name.Length - 6);
                    }
                    else if (isModData && name.EndsWith("Data"))
                    {
                        name = name.Remove(name.Length - 4);
                    }
                    name = name + ".";
                    if (isModData) { name += "Data."; }
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
                        stream.WriteLine("|`" + (name + pname).Replace("|", "\\|") + "`|" + typeName.Replace("|", "\\|") + "|");
                    }
                }
            }
        }

        static void GeneratePages()
        {
            List<Page> contents = new List<Page>();
            foreach (TypeDefinition modifierType in module.Types.Concat(modApi.Types).Where(IsPartModifierData))
            {
                List<PropertyEntry> properties = new List<PropertyEntry>();
                foreach (var field in modifierType.Fields)
                {
                    foreach (var attr in field.CustomAttributes)
                    {
                        if (IsPartModifierProperty(field, out string description, out bool designerOnly))
                        {
                            if (designerOnly)
                            {
                                description = "Designer only. " + (description ?? "");
                            }
                            if (!TypeMap.TryGetValue(field.FieldType.FullName, out string type)) { type = field.FieldType.Name; }
                            properties.Add(new PropertyEntry {
                                Name = field.Name.TrimStart('_'),
                                Type = type,
                                Desc = description ?? ""
                            });
                            break;
                        }
                    }
                }
                string name = modifierType.Name;
                if (name.EndsWith("Data"))
                {
                    name = name.Remove(name.Length - 4);
                }
                GeneratePage(rootDir + "\\" + name + ".md", name, ManifestRoot, properties, contents);
            }

            var Game = module.GetType("Assets.Scripts.Game");
            string version = null;
            var cctor = Game?.Methods.First(m => m.Name == ".cctor");
            if (cctor != null)
            {
                List<int> versionQueue = new List<int>();
                foreach (var i in cctor.Body.Instructions)
                {
                    if (i.OpCode == OpCodes.Newobj && (i.Operand as MethodReference).DeclaringType.Name == "Version")
                    {
                        if (versionQueue.Count > 3)
                        {
                            version = (string.Join(".", versionQueue.GetRange(versionQueue.Count - 4, 4)));
                        }
                        break;
                    }
                    if (!i.OpCode.Code.ToString().StartsWith("Ldc_I4")) { continue; }
                    if (i.OpCode == OpCodes.Ldc_I4)
                    {
                        versionQueue.Add((int)i.Operand);
                    }
                    else if (i.OpCode == OpCodes.Ldc_I4_S)
                    {
                        versionQueue.Add(((IConvertible)i.Operand).ToInt32(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        versionQueue.Add(int.Parse(i.OpCode.Code.ToString().Split('_').Last()));
                    }
                }
                
            }
            

            UpdatePartModifierPropertiesPage();
            UpdateMainPage(contents, version);
            _Manifest.Save(rootDir + "\\/manifest.xml");
        }

        static void GetPartModifierOptions()
        {
            foreach (TypeDefinition type in module.Types.Concat(modApi.Types))
            {
                if (IsPartModifierData(type))
                {
                    string tname = type.Name;
                    if (tname.EndsWith("Data")) { tname = tname.Remove(tname.Length - 4); }
                    string filePath = rootDir + "\\" + tname + ".md";
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
                            s.Append(name.Replace("|", "\\|"));
                            s.Append("`|`");
                            s.Append(typeName.Replace("|", "\\|"));
                            s.Append("`|");
                            s.Append(description.Replace("|", "\\|"));
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

        static void GeneratePage(string path, string name, XElement manifest, List<PropertyEntry> properties, List<Page> contents)
        {
            XElement element = manifest.Element(name);
            if (element == null)
            {
                element = new XElement(name);
                manifest.Add(element);
            }
            var overrides = element.Element("Overrides");
            if (overrides == null)
            {
                var o = new XElement("Overrides");
                element.Add(o);
                overrides = o;
            }
            var customs = element.Element("Customs");

            contents.Add(new Page() {
                name = name,
                children = element.GetStringAttribute("childPages")?.Split(',') ?? new string[] { }
            });
            using (var file = File.CreateText(path))
            {
                file.WriteLine("[Home](https://wnp78.github.io/Sr2Xml/)\n");
                string head = element.GetChildContents("Head");
                if (string.IsNullOrEmpty(head))
                {
                    head = "# " + name + "\n";
                }
                file.WriteLine(head);
                if (properties.Count == 0 && customs == null)
                {
                    file.WriteLine("This Part Modifier type has no XML properties.");
                }
                else
                {
                    file.WriteLine("\n|Name|Type|Description|\n|--|--|--|");
                    foreach (var property in properties)
                    {
                        var over = overrides.Element(property.Name);
                        if (over == null)
                        {
                            over = new XElement(property.Name);
                            overrides.Add(over);
                        }
                        string type = over.GetStringAttribute("type", property.Type);
                        if (TypeMap.ContainsKey(type)) { type = TypeMap[type]; }
                        string desc = over.Value?.Trim();
                        if (string.IsNullOrEmpty(desc)) { desc = property.Desc; }
                        desc = desc.Replace("\r\n", "\n").Replace('\n', ' ');
                        file.WriteLine("|`" + (over.Name.LocalName ?? property.Name).Replace("|", "\\|") + "`|`" + type.Replace("|", "\\|") + "`|" + desc.Replace("|", "\\|") + "|");
                    }
                    if (customs != null)
                    {
                        foreach (var custom in customs.Elements())
                        {
                            file.WriteLine("|`" + custom.Name.LocalName + "`|`" + custom.GetStringAttribute("type", "string") + "`|" + custom.Value.Trim().Replace("\r", "").Replace("\n", "") + "|");
                        }
                    }
                }

                file.Write('\n');
                string foot = element.GetChildContents("Footer", "");
                file.WriteLine(foot);
            }
        }

        static void UpdateMainPage(List<Page> modifiers, string version)
        {
            string path = Path.Combine(rootDir, "README.md");
            XElement over = ManifestRoot.Element("README");
            if (over == null)
            {
                over = new XElement("README");
                ManifestRoot.Add(over);
            }
            using (var file = File.CreateText(path))
            {
                string head = over.GetChildContents("Head", "# SR2 XML Guide");
                file.WriteLine(head + "\n");
                file.WriteLine("Game Version: `" + (version ?? "unknown") + "`\n");
                file.WriteLine("Contents:");
                foreach (var page in modifiers.OrderBy(p => p.name))
                {
                    file.WriteLine(" - [" + page.name + "](/Sr2Xml/" + page.name + ")");
                    foreach (var p in page.children)
                    {
                        file.WriteLine("   - [" + p + "](/Sr2Xml/" + p + ")");
                    }
                }
            }
        }

        static void UpdatePartModifierPropertiesPage()
        {
            string path = Path.Combine(rootDir, "PartModifierScriptProperties.md");
            XElement over = ManifestRoot.Element("PartModifierScriptProperties");
            if (over == null)
            {
                over = new XElement("PartModifierScriptProperties");
                ManifestRoot.Add(over);
            }
            using (var file = File.CreateText(path))
            {
                file.WriteLine("[Home](https://wnp78.github.io/Sr2Xml/)\n");
                file.WriteLine(over.GetChildContents("Head"));
                file.WriteLine("");
                GetModifierScriptProperties(file);
                file.Write(over.GetChildContents("Footer"));
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
    struct Page
    {
        public string name;
        public string[] children;
    }
}