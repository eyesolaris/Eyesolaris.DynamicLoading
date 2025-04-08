using Eyesolaris.DynamicLoading;

namespace DynamicTestInterfaces
{
    public abstract class DynamicModule : Eyesolaris.DynamicLoading.DynamicModule
    {
        protected DynamicModule()
            : base(initializationNecessary: false)
        {
        }

        public override DynamicEntityName ModuleId => new("Module", new Version(1, 0));

        public override string Description => "Description";

        public override Task? WorkerTask => null;
    }
}
