﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using Ziks.WebServer;

namespace SourceUtils.WebExport
{
    class BaseOptions
    {
        [Option('g', "gamedir", HelpText = "Game directory to export from.", Required = true)]
        public string GameDir { get; set; }

        [Option('p', "packages", HelpText = "Comma separated VPK file names.")]
        public string Packages { get; set; } = "pak01_dir.vpk";

        [Option('v', "verbose", HelpText = "Write every action to standard output.")]
        public bool Verbose { get; set; }

        [Option("untextured", HelpText = "Only export a single colour for each texture.")]
        public bool Untextured { get; set; }

        [Option('s', "resdir", HelpText = "Directory containing static files to serve (css / html etc).")]
        public string ResourcesDir { get; set; }

        [Option('m', "mapsdir", HelpText = "Directory to export maps from, relative to gamedir.")]
        public string MapsDir { get; set; } = "maps";

        [Option("debug-pakfile", HelpText = "Save pakfile to disk for each map, for debugging.")]
        public bool DebugPakFile { get; set; }

        [Option("debug-materials", HelpText = "Include all material properties.")]
        public bool DebugMaterials { get; set; }
    }

    [Verb("host", HelpText = "Run a HTTP server that exports requested resources.")]
    class HostOptions : BaseOptions
    {
        [Option('p', "port", HelpText = "Port to listen on.", Default = 8080)]
        public int Port { get; set; }
    }

    partial class Program
    {
        public static BaseOptions BaseOptions { get; private set; }

        public static string GetGameFilePath(string path)
        {
            return Path.Combine(BaseOptions.GameDir, path);
        }

        private static readonly Dictionary<string, ValveBspFile> _sOpenMaps = new Dictionary<string, ValveBspFile>();
        private static Dictionary<string, string> _sWorkshopMaps;

        public static IResourceProvider Resources { get; private set; }

        private static void FindWorkshopMaps()
        {
            _sWorkshopMaps = new Dictionary<string, string>();

            var workshopDir = Path.Combine(BaseOptions.MapsDir, "workshop");

            if (!Directory.Exists(workshopDir)) return;

            foreach (var directory in Directory.GetDirectories(workshopDir))
            {
                ulong workshopId;
                if (!ulong.TryParse(Path.GetFileName(directory), out workshopId)) continue;

                foreach (var bsp in Directory.GetFiles(directory, "*.bsp", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileNameWithoutExtension(bsp).ToLower();
                    if (!_sWorkshopMaps.ContainsKey(name))
                    {
                        _sWorkshopMaps.Add(name, bsp);
                    }
                }
            }
        }

        public static ValveBspFile GetMap(string name)
        {
            ValveBspFile map;
            if (_sOpenMaps.TryGetValue(name, out map)) return map;

            if (_sWorkshopMaps == null) FindWorkshopMaps();

            if (!_sWorkshopMaps.TryGetValue(name.ToLower(), out string bspPath))
            {
                bspPath = Path.Combine(BaseOptions.MapsDir, $"{name}.bsp");
            }

            map = new ValveBspFile(bspPath);
            _sOpenMaps.Add(name, map);

            return map;
        }

        public static void UnloadMap(string name)
        {
            if (!_sOpenMaps.TryGetValue(name, out var map)) return;

            _sOpenMaps.Remove(name);
            map.Dispose();
        }

        static void SetBaseOptions(BaseOptions args)
        {
            BaseOptions = args;

            var vpkNames = args.Packages.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Path.IsPathRooted(x) ? x.Trim() : Path.Combine(args.GameDir, x.Trim()))
                .ToArray();

            if (vpkNames.Length == 1)
            {
                Resources = new ValvePackage(vpkNames[0]);
            }
            else
            {
                var loader = new ResourceLoader();

                foreach (var path in vpkNames)
                {
                    loader.AddResourceProvider(new ValvePackage(path));
                }

                Resources = loader;
            }

            if (string.IsNullOrEmpty(args.ResourcesDir))
            {
                args.ResourcesDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "..", "Resources");
            }

            if (!Directory.Exists(args.ResourcesDir))
            {
                args.ResourcesDir = null;
            }

            if (string.IsNullOrEmpty(args.MapsDir))
            {
                args.MapsDir = "maps";
            }

            if (!Path.IsPathRooted(args.MapsDir))
            {
                args.MapsDir = Path.Combine(args.GameDir, args.MapsDir);
            }

            ValveBspFile.PakFileLump.DebugContents = args.DebugPakFile;
        }

        static int Host(HostOptions args)
        {
            SetBaseOptions(args);

            var server = new Server(args.Port);

            AddStaticFileControllers(server);

            server.Controllers.Add(Assembly.GetExecutingAssembly());
            server.Run();

            return 0;
        }

        static int Main(string[] args)
        {
            string[] a = new string[7];
            a[0] = "export";
            a[1] = "--outdir";
            a[2] = "C:\\Users\\Dojo Madness\\Documents";
            a[3] = "--maps";
            a[4] = "de_mirage";
            a[5] = "--gamedir";
            a[6] = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive\\csgo";

            foreach (var item in a)
            {
                Console.WriteLine(item.ToString());
            }

            var result = Parser.Default.ParseArguments<ExportOptions, HostOptions>(a);
            return result.MapResult<ExportOptions, HostOptions, int>(Export, Host, _ => 1);
        }
    }
}