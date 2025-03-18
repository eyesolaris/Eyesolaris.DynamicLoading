using System.Reflection;
using System.Runtime.Loader;

namespace Eyesolaris.DynamicLoading
{
    public class PackageAssemblyLoadContext : AssemblyLoadContext
    {
        public PackageAssemblyLoadContext(string? name, bool isCollectible)
            : base(name, isCollectible)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName == _thisAssemblyName)
            {
                return _thisAssembly;
            }
            Assembly? foundAssembly = AssemblyLoadContext.Default.Assemblies.Where(a =>
            {
                AssemblyName loadedAssemblyName = a.GetName();
                bool equal = loadedAssemblyName.FullName == assemblyName.FullName;
                return equal;
            }).SingleOrDefault();
            if (foundAssembly is not null)
            {
                return foundAssembly;
            }
            return base.Load(assemblyName);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            nint dll = base.LoadUnmanagedDll(unmanagedDllName);
            return dll;
        }

        private static readonly Assembly _thisAssembly = Assembly.GetExecutingAssembly();
        private static readonly AssemblyName _thisAssemblyName = _thisAssembly.GetName();
    }
}
