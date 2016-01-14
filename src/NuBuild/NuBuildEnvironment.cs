﻿
namespace NuBuild
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Reflection;

    using Microsoft.CSharp.RuntimeBinder;
    using Microsoft.WindowsAzure.Storage;

    using Newtonsoft.Json;

    public static class NuBuildEnvironment
    {
        public const string DotNuBuild = ".nubuild";
        public const string ConfigFileRelativePath = ".\\.nubuild\\config.json";
        public const string LogPath = ".\\.nubuild\\log.txt";

        public static AbsoluteFileSystemPath InvocationPath { get; private set; }

        public static Options Options { get; private set; }
        private static AbsoluteFileSystemPath rootDirectoryPath = null;


        public static void initialize(string specifiedRootPath)
        {
            if (isInitialized())
            {
                throw new InvalidOperationException("Attempt to initialize NuBuildEnvironment twice.");
            }
            InvocationPath = AbsoluteFileSystemPath.FromCurrentDirectory();
            rootDirectoryPath = initNuBuildRoot(specifiedRootPath);
            Logger.Start(AbsoluteFileSystemPath.FromRelative(RelativeFileSystemPath.Parse(LogPath), rootDirectoryPath));
            Options = LoadConfig();
            // NuBuild seems flakey unless the current directory is the NuBuild root.
            Directory.SetCurrentDirectory(rootDirectoryPath.ToString());
        }

        public static bool isInitialized()
        {
            return rootDirectoryPath != null;
        }

        private static void throwIfNotInitialized()
        {
            if (!isInitialized())
            {
                throw new InvalidOperationException("NuBuildEnvironment is not yet intialized.");
            }
        }

        public static AbsoluteFileSystemPath RootDirectoryPath
        {
            get
            {
                throwIfNotInitialized();
                return rootDirectoryPath;
            }
        }

        private static AbsoluteFileSystemPath initNuBuildRoot(string specifiedRootPath)
        {
            if (specifiedRootPath != null)
            {
                AbsoluteFileSystemPath p = AbsoluteFileSystemPath.Parse(specifiedRootPath, permitImplicit: true);
                if (p.IsExistingDirectory && p.CreateChildPath(DotNuBuild).IsExistingDirectory)
                {
                    Logger.WriteLine(string.Format("Specified NuBuild root path found at `{0}`.", p));
                    return p;
                }
                else
                {
                    throw new DirectoryNotFoundException(string.Format("Specified NuBuild root path (`{0}`) not found.", specifiedRootPath));
                }
            }
            else
            {
                var p = findNuBuildRoot();
                Logger.WriteLine(string.Format("NuBuild root found at `{0}`.", p));
                return p;
            }
        }

        public static RelativeFileSystemPath ObjRootPath
        {
            get
            {
                return RelativeFileSystemPath.Parse(BuildEngine.theEngine.getObjRoot(), permitImplicit: true);
            }
        }

        public static AbsoluteFileSystemPath findNuBuildRoot()
        {
            for (var i = InvocationPath; i != null; i = i.ParentDirectoryPath)
            {
                var p = i.CreateChildPath(DotNuBuild);
                if (p.IsExistingDirectory)
                {
                    return i;
                }
            }

            throw new DirectoryNotFoundException(string.Format("Unable to find NuBuild root (`{0}`).", DotNuBuild));
        }

        public static string getDefaultIronRoot()
        {
            return RootDirectoryPath.ToString();
        }


        public static string getAssemblyPath()
        {
            string assyUri = Assembly.GetExecutingAssembly().CodeBase;
            string assyPath = new Uri(assyUri).LocalPath;
            return assyPath;
        }

        private static dynamic LoadConfig()
        {
            var path = AbsoluteFileSystemPath.FromRelative(RelativeFileSystemPath.Parse(ConfigFileRelativePath), RootDirectoryPath);
            var pathStr = path.ToString();
            if (path.IsExistingFile)
            {
                using (TextReader stream = File.OpenText(pathStr))
                {
                    var s = stream.ReadToEnd();
                    return Options.FromString(s);
                }
            }
            else
            {
                Logger.WriteLine(string.Format("Unable to find {0}; assuming empty document.", pathStr), "warning");
                return new Dictionary<string, object>();
            }
        }
    }
}
