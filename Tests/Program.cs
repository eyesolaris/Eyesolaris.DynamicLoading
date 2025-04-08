using DynamicTestInterfaces;
using Eyesolaris.DynamicLoading;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace Tests
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Testing of backward compatibility after adding a new interface into the old one
            // and default implementing this new interface in the old one
            DynamicEntityName moduleName = new("Module", new Version(1, 0));

            string assemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "DynamicTestAssembly.dll");
            Assembly a = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            Type t = a.ExportedTypes.Where(t => t.Name == "Factory").Single();
            IFactory<DynamicTestInterfaces.DynamicModule>? factory = (IFactory<DynamicTestInterfaces.DynamicModule>)Activator.CreateInstance(t)!;
            IDynamicModule? module = factory.CreateDynamicModule(moduleName);

            PackageLoader<IFactory<DynamicTestInterfaces.DynamicModule>> loader = new("packages");
            loader.LoadAll(CultureInfo.InvariantCulture);
            factory = loader.FindFactoryByModuleName(moduleName);
            module = factory?.CreateDynamicModule(moduleName);
        }
    }
}
