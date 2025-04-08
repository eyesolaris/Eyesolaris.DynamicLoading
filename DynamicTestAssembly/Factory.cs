using DynamicTestInterfaces;

namespace DynamicTestAssembly
{
    public class Factory : IFactory<DynamicModule>
    {
        public DynamicModule Create()
        {
            return new Module();
        }
    }
}
