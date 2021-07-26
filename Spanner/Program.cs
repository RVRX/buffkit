﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private static Logger _logger;
        
        static void Main(string[] args)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "spanner_log.txt");
            if (File.Exists(logPath)) File.Delete(logPath);
            
            var configPath = Path.Combine(AppContext.BaseDirectory, "spanner_config.toml");
            _logger = new LoggerConfiguration()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    path: Path.Combine(AppContext.BaseDirectory, logPath),
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .MinimumLevel.Debug()
                .CreateLogger();
            
            

            _logger.Information("Spanner started");
            _logger.Information("Parsing config");
            
            TomlTable configData;
            using (var reader = File.OpenText(configPath))
            {
                try
                {
                    configData = TOML.Parse(reader);

                }
                catch (Exception e)
                {
                    _logger.Fatal("Unable to parse the configuration file", e);
                    return;
                }
            }

            _logger.Information("Looking up the game directory");
            var gamePath = configData["game_path"];
            if (!Directory.Exists(gamePath))
            {
                _logger.Fatal("The specified game directory doesn't exist");
                return;
            }
            
            _logger.Information("Looking up the game assembly directory");
            var gameAssemblyPath = Path.Combine(gamePath, "GunsOfIcarusOnline_Data", "Managed");
            if (!Directory.Exists(gameAssemblyPath))
            {
                _logger.Fatal("The specified game directory doesn't contain an assembly folder");
                return;
            }

            _logger.Information("Looking up the project assembly directory");
            string projectAssemblyPath;
            try
            {
                projectAssemblyPath = Path.Combine(
                    //If the directory name ends in separator, Directory.GetParent just returns it without the separator 
                    Directory.GetParent(Directory.GetParent(
                        AppContext.BaseDirectory
                    ).ToString())?.ToString() ?? throw new IOException("Can't get the parent directory"),
                    "Assemblies"
                );
            }
            catch (IOException e)
            {
                _logger.Fatal("Can't get the parent directory\n{e}", e);
                return;
            }

            _logger.Information("Copying the game assemblies if needed");
            var assemblies = Directory.GetFiles(gameAssemblyPath);
            foreach (var assembly in assemblies)
            {
                var name = Path.GetFileName(assembly);
                var dest = Path.Combine(projectAssemblyPath, name);
                
                if (!File.Exists(dest))
                {
                    File.Copy(assembly, dest);
                    _logger.Information($"Copied {name} to {dest}");
                }
            }

            
            var basePath = Path.Combine(projectAssemblyPath, "Assembly-CSharp.dll");
            var backupPath = Path.Combine(projectAssemblyPath, "Assembly-CSharp_backup.dll");
            var vanillaAssemblyPath = File.Exists(backupPath) ? backupPath : basePath;
            var patchedAssemblyPath = File.Exists(backupPath) ? basePath : null;
            
            if (!File.Exists(patchedAssemblyPath))
            {
                _logger.Information("Backing up the main assembly");
                if (!File.Exists(vanillaAssemblyPath))
                {
                    _logger.Fatal("The main assembly doesn't exist in the project's Assemblies folder");
                    return;
                }

                try
                {
                    File.Copy(vanillaAssemblyPath, backupPath);
                    vanillaAssemblyPath = backupPath;
                    patchedAssemblyPath = basePath;
                    _logger.Information("Backed up the original assembly");
                }
                catch (Exception e)
                {
                    _logger.Fatal("Unable to backup the original assembly\n{e}", e);
                    return;
                }
            }

            _logger.Information("Reading the main assembly");
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(projectAssemblyPath);
            var assemblyToPatch = AssemblyDefinition.ReadAssembly(
                vanillaAssemblyPath, 
                new ReaderParameters {AssemblyResolver = resolver});
            
            _logger.Information("Patching the main assembly");
            var classesToPatch = configData["classes_to_patch"].AsArray;
            foreach (var ctp in classesToPatch)
            {
                var name = ctp.ToString();
                if (name is null) continue;
                
                var type = assemblyToPatch.MainModule.Types.FirstOrDefault(t => t.Name.Equals(name));
                if (type is null)
                {
                    _logger.Error($"Unable to find the specified class {name}");
                    continue;
                }
                
                Nationalize(type);
                _logger.Information($"Patched {name}");
            }

            _logger.Information("Writing changes to the main assembly");
            try
            {
                assemblyToPatch.Write(patchedAssemblyPath);
            }
            catch (Exception e)
            {
                _logger.Fatal("Unable to write the changes to the main assembly\n{e}", e);
                return;
            }
            _logger.Information("Rebuild finished, spanner shutting down");
        }

        private static void Nationalize(TypeDefinition type)
        {
            _logger.Information($"Deprivatizing class {type.FullName}");
            
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
            _logger.Debug($"Deprivatizing method {method.FullName}");
            
            method.Attributes &= ~MethodAttributes.Private;
            method.Attributes |= MethodAttributes.Public;
        }
    }
}