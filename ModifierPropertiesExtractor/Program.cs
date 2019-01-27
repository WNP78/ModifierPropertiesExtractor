using Mono.Cecil;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ModifierPropertiesExtractor
{
    class Program
    {
        static Dictionary<string, string> TypeMap = new Dictionary<string, string>
        {
            { "System.Single", "float" },
            { "System.Int32", "int" },
            { "System.Double", "double" },
            { "System.String", "string" }
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

        static void GetPartModifierOptions()
        {
            Console.Write("Output root dir> ");
            DirectoryInfo rootDir = new DirectoryInfo(Console.ReadLine());
            foreach (TypeDefinition type in module.Types.Concat(modApi.Types))
            {
                bool isMod = false;
                TypeReference t = type.BaseType;
                while (t != null)
                {
                    if (t.FullName.StartsWith("ModApi.Craft.Parts.PartModifierData"))
                    {
                        isMod = true;
                        break;
                    }
                    t = t.Resolve().BaseType;
                }

                if (isMod)
                {
                    string tname = type.Name;
                    if (tname.EndsWith("Data")) { tname = tname.Remove(tname.Length - 4); }
                    string filePath = rootDir.FullName + "\\" + tname + ".md";
                    StringBuilder s = new StringBuilder();
                    bool wrote = false;
                    foreach (FieldDefinition field in type.Fields)
                    {
                        string description = "";
                        bool isProp = false;
                        try
                        {
                            foreach (var attr in field.CustomAttributes)
                            {
                                TypeDefinition at = attr.AttributeType.Resolve();
                                while (at != null)
                                {
                                    if (at.FullName == "ModApi.Craft.Parts.Attributes.PartModifierPropertyAttribute")
                                    {
                                        isProp = true;
                                        foreach (var a in attr.Properties)
                                        {
                                            if (a.Name == "Tooltip") { description = (string)a.Argument.Value; }
                                            if (a.Name == "DesignerOnly" && (bool) a.Argument.Value == true)
                                            {
                                                description = "Designer only. " + description;
                                            }
                                        }
                                        break;
                                    }
                                    if (at.BaseType == null) { break; }
                                    at = at.BaseType.Resolve();
                                }
                            }
                        }
                        catch (AssemblyResolutionException) { }

                        if (isProp)
                        {
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
    }
}
