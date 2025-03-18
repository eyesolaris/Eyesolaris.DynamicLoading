using Eyesolaris.Logging;
using Eyesolaris.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eyesolaris.DynamicLoading
{
    /// <summary>
    /// Currently dependencies are ignored
    /// </summary>
    public sealed class LoadedPackage : IDisposable
    {
        internal const string PACKAGE_FILE_NAME = "package.json";
        internal const string PACKAGE_ID_PROPERTY = "PackageId";
        internal const string PACKAGE_VERSION_PROPERTY = "Version";
        internal const string PACKAGE_ROOT_ASSEMBLY_PROPERTY = "RootAssembly";

        internal const string DEFAULT_CULTURE_DIR = "en";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">A path to the package directory</param>
        public LoadedPackage(string path, CultureInfo expectedCulture)
        {
            RootDir = path;
            ExpectedCulture = expectedCulture;
            var dirsInPackage = Directory.GetDirectories(RootDir);
            var cultureDir = dirsInPackage.Where(path => expectedCulture.Name == System.IO.Path.GetFileName(path)).SingleOrDefault();
            if (cultureDir is null)
            {
                var cultureDirs = dirsInPackage
                    .Where(path => expectedCulture.Name.Contains(System.IO.Path.GetFileName(path)));
                cultureDir = cultureDirs.FirstOrDefault();
            }
            cultureDir ??= Path.Combine(RootDir, "en");
            if (Directory.Exists(cultureDir))
            {
                CultureDir = cultureDir;
            }
            string nativeSpecificPath = Path.Combine(RootDir, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
            if (Directory.Exists(nativeSpecificPath))
            {
                RuntimeSpecificNativeDir = nativeSpecificPath;
            }
            string assemblySpecificPath = Path.Combine(RootDir, "runtimes", RuntimeUtilities.OsSpecificAssemblyDirectoryName, "lib");
            if (Directory.Exists(assemblySpecificPath))
            {
                RuntimeSpecificAssembliesDir = assemblySpecificPath;
            }
            try
            {
                Properties = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(path, PACKAGE_FILE_NAME)));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Couldn't parse the package descriptor. Package: {path}", ex);
            }
            try
            {
                var jsonElem = Properties.RootElement.GetProperty(PACKAGE_ID_PROPERTY);
                PackageId = jsonElem.GetString() ?? throw new InvalidOperationException($"{PACKAGE_ID_PROPERTY} can't be null");
                jsonElem = Properties.RootElement.GetProperty(PACKAGE_VERSION_PROPERTY);
                Version = new Version(jsonElem.GetString() ?? throw new InvalidOperationException($"{PACKAGE_VERSION_PROPERTY} can't be null"));
                jsonElem = Properties.RootElement.GetProperty(PACKAGE_ROOT_ASSEMBLY_PROPERTY);
                string rootAssemblyRelativePath = jsonElem.GetString() ?? throw new InvalidOperationException($"{PACKAGE_ROOT_ASSEMBLY_PROPERTY} can't be null");
                RootAssemblyPath = System.IO.Path.Combine(path, rootAssemblyRelativePath);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"Can't load the package {path}", ex);
            }

            AssemblyContext = new PackageAssemblyLoadContext($"{PackageId} {Version}", isCollectible: false);
            AssemblyContext.Resolving += AssemblyContext_Resolving;
            AssemblyContext.ResolvingUnmanagedDll += AssemblyContext_ResolvingUnmanagedDll;
            AssemblyContext.Unloading += AssemblyContext_Unloading;
            
            Factories = _CreateFactories();
        }

        public string RootDir { get; }

        public CultureInfo ExpectedCulture { get; }

        public string? CultureDir { get; }

        /// <summary>
        /// A path to native resources specific for the current platform
        /// </summary>
        public string? RuntimeSpecificNativeDir { get; }

        public string? RuntimeSpecificAssembliesDir { get; }

        public string PackageId { get; }

        public string RootAssemblyPath { get; }

        public Version Version { get; }

        public DynamicEntityName PackageName => new(PackageId, Version);

        public AssemblyLoadContext AssemblyContext { get; }

        public JsonDocument Properties { get; }

        public IReadOnlyList<Assembly> LoadedAssemblies => AssemblyContext.Assemblies.ToImmutableList();

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return "Package " +  PackageName.ToString();
        }

        private IReadOnlyDictionary<DynamicEntityName, IDynamicModuleFactory> _CreateFactories()
        {
            AssemblyContext.LoadFromAssemblyPath(RootAssemblyPath);
            List<IDynamicModuleFactory> dynamicModuleFactoryObjects = new();
            foreach (var loadedAssembly in LoadedAssemblies)
            {
                var dynamicModuleFactoryTypes = loadedAssembly.ExportedTypes.Where(t => t.IsAssignableTo(typeof(IDynamicModuleFactory)));
                foreach (Type t in dynamicModuleFactoryTypes)
                {
                    IDynamicModuleFactory? factory = (IDynamicModuleFactory?)Activator.CreateInstance(t);
                    if (factory is not null)
                    {
                        dynamicModuleFactoryObjects.Add(factory);
                    }
                }
            }
            if (dynamicModuleFactoryObjects.Count > 0)
            {
                Dictionary<DynamicEntityName, IDynamicModuleFactory> factories = new();
                foreach (var factory in dynamicModuleFactoryObjects)
                {
                    foreach (var moduleName in factory.SupportedModuleTypes)
                    {
                        factories.Add(moduleName, factory);
                    }
                }
                return factories;
            }
            else
            {
                return ImmutableDictionary<DynamicEntityName, IDynamicModuleFactory>.Empty;
            }
        }

        public IReadOnlyDictionary<DynamicEntityName, IDynamicModuleFactory> Factories { get; }

        // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
        ~LoadedPackage()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: false);
        }

        private readonly List<nint> _nativeLibraries = new();
        private bool disposedValue;

        private void AssemblyContext_Unloading(AssemblyLoadContext obj)
        {
            Logger.Global.LogDebug("AssemblyContext {ctxName} is unloading", obj.Name);
            foreach (var handle in _nativeLibraries)
            {
                NativeLibrary.Free(handle);
            }
            _nativeLibraries.Clear();
        }

        private nint AssemblyContext_ResolvingUnmanagedDll(Assembly resolvingAssembly, string dllName)
        {
            Logger.Global.LogTrace("Resolving the unmanaged dynamic library {dllName} for assembly {asmName}", dllName, resolvingAssembly.GetName());
            string[] foundFiles = Array.Empty<string>();
            
            static string[] TryFind(string path, string dllName)
            {
                string[] foundFiles = Directory.GetFiles(path, dllName + ".*", SearchOption.AllDirectories);
                if (foundFiles.Length == 0 && Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    foundFiles = Directory.GetFiles(path, "lib" + dllName + ".*");
                }
                return foundFiles;
            }

            if (RuntimeSpecificNativeDir is not null)
            {
                foundFiles = TryFind(RuntimeSpecificNativeDir, dllName);
            }
            if (foundFiles.Length == 0)
            {
                foundFiles = TryFind(RootDir, dllName);
            }
            
            if (foundFiles.Length == 0)
            {
                Logger.Global.LogWarning("Native library {lib} is not found in {path}", dllName, RootDir);
                return 0;
            }
            else if (foundFiles.Length > 1)
            {
                StringBuilder sb = new();
                foreach (string file in foundFiles)
                {
                    sb.AppendLine(file);
                }
                Logger.Global.LogWarning("Multiple native library \"{lib}\" files found:\n{filesList}", dllName, sb);
            }
            nint libHandle = NativeLibrary.Load(foundFiles[0], resolvingAssembly, DllImportSearchPath.UseDllDirectoryForDependencies);
            _nativeLibraries.Add(libHandle);
            return libHandle;
        }

        private Assembly? AssemblyContext_Resolving(AssemblyLoadContext ctx, AssemblyName assemblyBeingResolved)
        {

            void LogNotFound()
            {
                Logger.Global.LogWarning("Assembly not found in path. Assembly: {asm}, path: {path}", assemblyBeingResolved, RootDir);
            }

            Logger.Global.LogTrace("Resolving the assembly {asm} in context {ctxName}", assemblyBeingResolved, ctx.Name);
            string? assemblySimpleName = assemblyBeingResolved.Name;
            if (assemblySimpleName is null)
            {
                return null;
            }
            string searchPath = RootDir;
            if (assemblySimpleName.EndsWith(".resources") && CultureDir is not null)
            {
                searchPath = CultureDir;
            }
            string[] foundFiles = Directory.GetFiles(searchPath, assemblySimpleName + ".dll", SearchOption.AllDirectories);
            if (foundFiles.Length == 0)
            {
                // Try to search in OS-specific directory
                if (RuntimeSpecificAssembliesDir is null)
                {
                    LogNotFound();
                    return null;
                }
                foundFiles = Directory.GetFiles(RuntimeSpecificAssembliesDir, assemblySimpleName + ".dll", SearchOption.AllDirectories);
                if (foundFiles.Length == 0)
                {
                    LogNotFound();
                    return null;
                }
                RuntimeUtilities.SortPathsByFrameworkNamePriority(foundFiles);
            }
            return ctx.LoadFromAssemblyPath(foundFiles[0]);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                    if (AssemblyContext.IsCollectible)
                    {
                        AssemblyContext.Unload();
                    }
                }

                // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить метод завершения
                
                // TODO: установить значение NULL для больших полей
                disposedValue = true;
            }
        }
    }
}
