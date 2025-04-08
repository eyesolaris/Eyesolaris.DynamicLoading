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
        private readonly Dictionary<DynamicEntityName, LoadedPackage<TFactoryType>> _loadedPackages = new();
        public IReadOnlyDictionary<DynamicEntityName, LoadedPackage<TFactoryType>> LoadedPackages => _loadedPackages;

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
            _logger = logger;
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
            foreach (var dir in Directory.GetDirectories(PackagesDirectory))
            {
                try
                {
                    _logger.LogInformation("Loading package {package}", dir);
                    LoadedPackage<TFactoryType> pack;
                    if (typeof(TFactoryType) == typeof(IDynamicModuleFactory))
                    {
                        LoadedPackage plainPackage = new(dir, expectedCulture, _interfaceAssemblyNames, _logger);
                        pack = (LoadedPackage<TFactoryType>)(object)plainPackage;
                    }
                    else
                    {
                        pack = new(dir, expectedCulture, _interfaceAssemblyNames, _logger);
                    }
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

        public IReadOnlyList<LoadedPackage<TFactoryType>> GetPackageById(string id)
        {
            var packages = _loadedPackages.Values.Where(p => p.PackageId == id).OrderBy(p => p.Version);
            if (typeof(TFactoryType) == typeof(IDynamicModule))
            {
                return (IReadOnlyList<LoadedPackage<TFactoryType>>)(object)packages.Cast<LoadedPackage>().ToArray();
            }
            return packages.ToArray();
        }

        /// <summary>
        /// Finds factory by it's supported module name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [Obsolete("Incorrect method name. Use FindFactoryByModuleName instead")]
        public TFactoryType? FindFactoryByName(DynamicEntityName name)
            => FindFactoryByModuleName(name);

        public TFactoryType? FindFactoryByModuleName(DynamicEntityName name)
        {
            foreach (var pkg in _loadedPackages.Values)
            {
                if (pkg.Factories.ContainsKey(name))
                {
                    return pkg.Factories[name];
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
            foreach (var package in _loadedPackages.Values)
            {
                package.Dispose();
            }
            _loadedPackages.Clear();
        }

        private Logger _logger;
    }

    public sealed class PackageLoader : PackageLoader<IDynamicModuleFactory>
    {
        /// <inheritdoc/>
        public PackageLoader(string packagesDir)
            : base(packagesDir)
        {
        }

        /// <inheritdoc/>
        public PackageLoader(string packagesDir, IEnumerable<AssemblyName> interfaceAssemblyNames)
            : base(packagesDir, interfaceAssemblyNames)
        {
        }

        [Obsolete("Incorrect method name. Use FindFactoryByModuleName instead")]
        public new IDynamicModuleFactory? FindFactoryByName(DynamicEntityName name)
            => FindFactoryByModuleName(name);

        public new IDynamicModuleFactory? FindFactoryByModuleName(DynamicEntityName name)
            => base.FindFactoryByModuleName(name);

        public new IReadOnlyList<LoadedPackage> GetPackageById(string id)
            => (IReadOnlyList<LoadedPackage>)base.GetPackageById(id);
    }
}
