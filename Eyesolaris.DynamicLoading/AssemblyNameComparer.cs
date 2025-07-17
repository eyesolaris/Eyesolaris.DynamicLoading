using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Eyesolaris.DynamicLoading
{
    internal class AssemblyNameComparer : IEqualityComparer<AssemblyName>
    {
        public static AssemblyNameComparer Instance { get; } = new();

        public bool Equals(AssemblyName? x, AssemblyName? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }
            if (x is null || y is null)
            {
                return false;
            }
            return AssemblyName.ReferenceMatchesDefinition(x, y);
        }

        public int GetHashCode([DisallowNull] AssemblyName obj)
        {
            return obj.Name?.GetHashCode() ?? 0;
        }

        private AssemblyNameComparer()
        {
        }
    }
}
