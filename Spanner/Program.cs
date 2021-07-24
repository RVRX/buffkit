using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Spanner
{
    static class Program
    {
        private static readonly Logger Logger = new LoggerConfiguration()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "spanner_log.txt"),
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .MinimumLevel.Debug()
            .CreateLogger();
        
        static void Main(string[] args)
        {

            if (args[0].Length == 0)
            {
                Logger.Fatal("No assembly supplied, shutting down");
                return;
            }
            
            
            var path = args[0];
            if (!Path.GetExtension(path).Equals(".dll"))
            {
                Logger.Fatal("The file supplied is not a DLL, shutting down");
                return;
            }

            var copyPath =
                $"{Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(path))}_patched.dll";
            
            Logger.Debug(copyPath);
            try
            {
                Logger.Information("Copying the assembly");
                File.Copy(path, copyPath, overwrite: true);
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Unable to copy the assembly, shutting down");
                return;
            }

            var asm = AssemblyDefinition.ReadAssembly(path);
            foreach (var module in asm.Modules)
            {
                Logger.Debug($"Processing module {module.Name}");
            }

            var types = asm.MainModule.Types.Where(t => t.Name.Contains("UIManager")).ToList();
            foreach (var type in types)
            {
                Nationalize(type);
            }

            asm.Write(copyPath);
        }

        private static void Nationalize(TypeDefinition type)
        {
            Logger.Debug($"Deprivatizing class {type.FullName}");
            
            if(type.IsNested)
            {
                type.Attributes &= ~TypeAttributes.NestedPrivate;
                type.Attributes |= TypeAttributes.NestedPublic;
            }
            else
            {
                type.Attributes |= TypeAttributes.Public;
            }

            // if (type.HasFields)
            // {
                // foreach (var field in type.Fields)
                // {
                    // field.Attributes &= ~FieldAttributes.Private;
                    // field.Attributes &= ~FieldAttributes.Family;
                    // field.Attributes &= ~FieldAttributes.Assembly;
                    // field.Attributes |= FieldAttributes.Public;
                // }
            // }
            
            if (type.HasMethods)
            {
                foreach (var method in type.Methods)
                {
                    Nationalize(method);
                }
            }

            if (!type.HasNestedTypes) return;
            foreach (var nestedType in type.NestedTypes)
            {
                Nationalize(nestedType);
            }
        }

        private static void Nationalize(MethodDefinition method)
        {
            if (method.IsPublic) return;
            Logger.Debug($"Deprivatizing method {method.FullName}");
            
            method.Attributes &= ~MethodAttributes.Private;
            method.Attributes |= MethodAttributes.Public;
        }
    }
}