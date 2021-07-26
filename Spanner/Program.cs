using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using Tommy;
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
            File.Delete("spanner_log.txt");
            Logger.Information("Spanner started");
            Logger.Information("Parsing config");
            
            TomlTable configData;
            using (var reader = File.OpenText("spanner_config.toml"))
            {
                try
                {
                    configData = TOML.Parse(reader);

                }
                catch (Exception e)
                {
                    Logger.Fatal("Unable to parse the configuration file", e);
                    return;
                }
            }

            Logger.Information("Looking up the game directory");
            var gamePath = configData["game_path"];
            if (!Directory.Exists(gamePath))
            {
                Logger.Fatal("The specified game directory doesn't exist");
                return;
            }
            
            Logger.Information("Looking up the game assembly directory");
            var gameAssemblyPath = Path.Combine(gamePath, "GunsOfIcarusOnline_Data", "Managed");
            if (!Directory.Exists(gameAssemblyPath))
            {
                Logger.Fatal("The specified game directory doesn't contain an assembly folder");
                return;
            }

            Logger.Information("Looking up the project assembly directory");
            string projectAssemblyPath;
            try
            {
                projectAssemblyPath = Path.Combine(
                    Directory.GetParent(
                            Directory.GetCurrentDirectory()
                        )
                        ?.ToString() ?? throw new IOException("Can't get the parent directory"),
                    "Assemblies"
                );
            }
            catch (IOException e)
            {
                Logger.Fatal("Can't get the parent directory\n{e}", e);
                return;
            }

            Logger.Information("Copying the game assemblies if needed");
            var assemblies = Directory.GetFiles(gameAssemblyPath);
            foreach (var assembly in assemblies)
            {
                var name = Path.GetFileName(assembly);
                var dest = Path.Combine(projectAssemblyPath, name);
                
                if (!File.Exists(dest))
                {
                    File.Copy(assembly, dest);
                    Logger.Information($"Copied {name} to {dest}");
                }
            }

            
            var basePath = Path.Combine(projectAssemblyPath, "Assembly-CSharp.dll");
            var backupPath = Path.Combine(projectAssemblyPath, "Assembly-CSharp_backup.dll");
            var vanillaAssemblyPath = File.Exists(backupPath) ? backupPath : basePath;
            var patchedAssemblyPath = File.Exists(backupPath) ? basePath : null;
            
            if (!File.Exists(patchedAssemblyPath))
            {
                Logger.Information("Backing up the main assembly");
                if (!File.Exists(vanillaAssemblyPath))
                {
                    Logger.Fatal("The main assembly doesn't exist in the project's Assemblies folder");
                    return;
                }

                try
                {
                    File.Copy(vanillaAssemblyPath, backupPath);
                    vanillaAssemblyPath = backupPath;
                    patchedAssemblyPath = basePath;
                    Logger.Information("Backed up the original assembly");
                }
                catch (Exception e)
                {
                    Logger.Fatal("Unable to backup the original assembly\n{e}", e);
                    return;
                }
            }

            Logger.Information("Reading the main assembly");
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(projectAssemblyPath);
            var assemblyToPatch = AssemblyDefinition.ReadAssembly(
                vanillaAssemblyPath, 
                new ReaderParameters {AssemblyResolver = resolver});
            
            Logger.Information("Patching the main assembly");
            var classesToPatch = configData["classes_to_patch"].AsArray;
            foreach (var ctp in classesToPatch)
            {
                var name = ctp.ToString();
                if (name is null) continue;
                
                var type = assemblyToPatch.MainModule.Types.FirstOrDefault(t => t.Name.Equals(name));
                if (type is null)
                {
                    Logger.Error($"Unable to find the specified class {name}");
                    continue;
                }
                
                Nationalize(type);
                Logger.Information($"Patched {name}");
            }

            Logger.Information("Writing changes to the main assembly");
            try
            {
                assemblyToPatch.Write(patchedAssemblyPath);
            }
            catch (Exception e)
            {
                Logger.Fatal("Unable to write the changes to the main assembly\n{e}", e);
                return;
            }
            Logger.Information("Rebuild finished, spanner shutting down");
        }

        private static void Nationalize(TypeDefinition type)
        {
            Logger.Information($"Deprivatizing class {type.FullName}");
            
            if(type.IsNested)
            {
                type.Attributes &= ~TypeAttributes.NestedPrivate;
                type.Attributes |= TypeAttributes.NestedPublic;
            }
            else
            {
                type.Attributes |= TypeAttributes.Public;
            }

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