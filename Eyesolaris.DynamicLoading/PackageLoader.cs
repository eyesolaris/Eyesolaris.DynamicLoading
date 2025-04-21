using Eyesolaris.DynamicLoading;
using Eyesolaris.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Eyesolaris.DynamicLoading
{
    public class PackageLoader<TFactoryType> : IDisposable
        where TFactoryType : class, IDynamicModuleFactory
    {
        private readonly Dictionary<DynamicEntityId, LoadedPackage<TFactoryType>> _loadedPackages = new();
        public IReadOnlyDictionary<DynamicEntityId, LoadedPackage<TFactoryType>> LoadedPackages
        {
            get
            {
                IReadOnlyDictionary<DynamicEntityId, LoadedPackage<TFactoryType>> dict;
                lock (_lock)
                {
                    dict = _loadedPackages.ToImmutableDictionary(_loadedPackages.Comparer);
                }
                return dict;
            }
        }

        public string PackagesDirectory { get; }

        private readonly ImmutableHashSet<AssemblyName> _interfaceAssemblyNames;
        public IReadOnlySet<AssemblyName> InterfaceAssemblyNames => _interfaceAssemblyNames;

        public PackageLoader(string packagesDir)
        {
            _logger = new GlobalLoggerProxy();
            if (!Path.IsPathRooted(packagesDir))
            {
                packagesDir = Path.Combine(Environment.CurrentDirectory, packagesDir);
            }
            PackagesDirectory = packagesDir;
            _interfaceAssemblyNames = ImmutableHashSet<AssemblyName>.Empty.WithComparer(AssemblyNameComparer.Instance);
        }

        public void SetLogger(Logger logger)
        {
            lock (_lock)
            {
                _logger = logger;
            }
        }

        /// <summary>
        /// Creates a package loader with interface assemblies
        /// </summary>
        /// <param name="packagesDir">A package directory</param>
        /// <param name="interfaceAssemblyNames">Assemblies to be shared between the <see cref="System.Runtime.Loader.AssemblyLoadContext.Default"/> and this one<br/>
        /// <paramref name="interfaceAssemblyNames"/> describes assemblies to be loaded from the default assembly directories (such as root app directory, system directories, etc.). These assemblies are interfaces between assemblies in different <see cref="System.Runtime.Loader.AssemblyLoadContext"/>'s</param>
        public PackageLoader(string packagesDir, IEnumerable<AssemblyName> interfaceAssemblyNames)
            : this(packagesDir)
        {
            _interfaceAssemblyNames = interfaceAssemblyNames.ToImmutableHashSet(AssemblyNameComparer.Instance);
        }

        public void LoadAll(CultureInfo expectedCulture)
        {
            lock (_lock)
            {
                foreach (var dir in Directory.GetDirectories(PackagesDirectory))
                {
                    try
                    {
                        _logger.LogInformation("Loading package {package}", dir);
                        LoadedPackage<TFactoryType> pack;
                        pack = new(dir, expectedCulture, _interfaceAssemblyNames, _logger);
                        if (!_loadedPackages.ContainsKey(pack.PackageName))
                        {
                            _logger.LogInformation("The package is successfully loaded with ID {id}, name {name}, version {ver}", pack.PackageId, pack.PackageName, pack.Version);
                            _loadedPackages.Add(pack.PackageName, pack);
                        }
                        else
                        {
                            pack.Dispose();
                            _logger.LogDebug("The package with ID {id}, name {name}, version {version} is already loaded", pack.PackageId, pack.PackageName, pack.Version);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning("Package {package} couldn't be loaded because of an exception:{ex}", dir, e);
                    }
                }
            }
        }

        public IReadOnlyList<LoadedPackage<TFactoryType>> GetPackageById(string id)
        {
            var allPackages = LoadedPackages.Values.Where(p => p.PackageId == id).OrderBy(p => p.Version);
            return allPackages.ToArray();
        }

        public TFactoryType? FindFactoryByModuleName(DynamicEntityIdTemplate idTemplate)
        {
            DynamicEntityId? preciseName = null;
            if (idTemplate.Name is not null && idTemplate.Version is not null)
            {
                preciseName = new(idTemplate.Name, idTemplate.Version);
            }
            var allPackages = LoadedPackages.Values;
            foreach (var pkg in allPackages)
            {
                var factories = pkg.Factories;
                if (preciseName is not null)
                {
                    DynamicEntityId id = preciseName.GetValueOrDefault();
                    if (factories.ContainsKey(preciseName.Value))
                    {
                        return factories[id];
                    }
                }
                else
                {
                    string? name = idTemplate.Name;
                    if (name is null)
                    {
                        return factories.Values.FirstOrDefault();
                    }
                    return factories.Values.Where(f => f.FactoryId == name).FirstOrDefault();
                }
            }
            return null;
        }

        public override string ToString()
        {
            return PackagesDirectory;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            foreach (var package in _loadedPackages.Values)
            {
                package.Dispose();
            }
            _loadedPackages.Clear();
        }

        private Logger _logger;

        private readonly object _lock = new();
    }
}
