using Eyesolaris.DynamicLoading;

namespace DynamicTestInterfaces
{
    public interface IFactory<TType> : IDynamicModuleFactory
        where TType : IDynamicModule
    {
        string IDynamicModuleFactory.FactoryId => GetType().FullName!;

        IReadOnlyList<DynamicEntityName> IDynamicModuleFactory.SupportedModuleTypes => new DynamicEntityName[] {
            new("Module", new Version(1, 0)) };

        IDynamicModule? IDynamicModuleFactory.CreateDynamicModule(DynamicEntityName id)
            => Create();

        TType Create();
    }
}
