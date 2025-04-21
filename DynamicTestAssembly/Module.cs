using DynamicTestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicTestAssembly
{
    internal class Module : DynamicModule
    {
        protected override Task StartModule(CancellationToken token)
        {
            C.F();
            return Task.CompletedTask;
        }
    }
}
