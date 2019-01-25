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
                    Console.WriteLine(type.Name);
                }
            }


            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

    }
}
