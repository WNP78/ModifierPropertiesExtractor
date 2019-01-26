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
            


            Console.WriteLine("Got module: " + module.Name);
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
            foreach (TypeDefinition type in module.Types)
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
                    Console.WriteLine("\n\n# " + tname);
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
                                    if (at.BaseType == null) { break; }
                                    at = at.BaseType.Resolve();
                                    if (at.FullName == "ModApi.Craft.Parts.Attributes.PartModifierPropertyAttribute")
                                    {
                                        isProp = true;
                                        foreach (var a in attr.Properties.Where(e => e.Name == "Tooltip"))
                                        {
                                            description = (string)a.Argument.Value;
                                        }
                                        break;
                                    }
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
                            Console.Write("|`");
                            Console.Write(name);
                            Console.Write("`|`");
                            Console.Write(typeName);
                            Console.Write("`|");
                            Console.Write(description);
                            Console.WriteLine('|');
                        }
                    }
                }
            }
        }
    }
}
