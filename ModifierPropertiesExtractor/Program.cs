using Mono.Cecil;
using System.Text;
using System;

namespace ModifierPropertiesExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("File Path> ");
            string filePath = Console.ReadLine();
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = "E:\\SR2Debug\\Game\\SimpleRockets2_Data\\Managed\\SimpleRockets2.dll";
            }
            ModuleDefinition module = ModuleDefinition.ReadModule(filePath);
            
            Console.WriteLine("Got module: " + module.Name);

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
                    StringBuilder s = new StringBuilder();
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
                        s.AppendLine("|" + pname + "|" + typeName + "|");
                    }
                    if (s.Length == 0) { continue; }
                    string name = type.Name;
                    if (name.EndsWith("Script"))
                    {
                        name = name.Remove(name.Length - 6);
                    }
                    Console.Write("## ");
                    Console.WriteLine(name);
                    Console.WriteLine("|Name|Type|");
                    Console.WriteLine("|---|---|");
                    Console.Write(s);
                    Console.Write("\n\n");
                }
            }


            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

    }
}
