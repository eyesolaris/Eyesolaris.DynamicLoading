using Eyesolaris.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyesolaris.DynamicLoading
{
    public sealed class PackageLoader : IDisposable
    {
        private Dictionary<DynamicEntityName, LoadedPackage> _loadedPackages = new();
        public IReadOnlyDictionary<DynamicEntityName, LoadedPackage> LoadedPackages => _loadedPackages;

        public string PackagesDirectory { get; }

        public PackageLoader(string packagesDir)
        {
            PackagesDirectory = packagesDir;
        }

        public void LoadAll(CultureInfo expectedCulture)
        {
            foreach (var dir in Directory.GetDirectories(PackagesDirectory))
            {
                try
                {
                    Logger.Global.LogInformation("Loading package {package}", dir);
                    LoadedPackage pack = new(dir, expectedCulture);
                    Logger.Global.LogInformation("The package is successfully loaded with ID {ver}, name {name}, version {ver}", pack.PackageId, pack.PackageName, pack.Version);
                    _loadedPackages.Add(pack.PackageName, pack);
                }

                catch (Exception e)
                {
                    Logger.Global.LogWarning("Package {package} couldn't be loaded because of an exception:{ex}", dir, e);
                }
            }
        }

        public IReadOnlyList<LoadedPackage> GetPackageById(string id)
        {
            var packages = _loadedPackages.Values.Where(p => p.PackageId == id).OrderBy(p => p.Version);
            return packages.ToArray();
        }

        public IDynamicModuleFactory? FindFactoryByName(DynamicEntityName name)
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
    }
}
