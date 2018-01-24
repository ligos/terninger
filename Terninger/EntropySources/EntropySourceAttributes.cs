using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MurrayGrant.Terninger.Generator;
using MurrayGrant.Terninger.Helpers;

namespace MurrayGrant.Terninger.EntropySources
{
    /// <summary>
    /// Indicates this entropy source makes network requests to derive entropy.
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NetworkSourceAttribute : Attribute
    {
    }
    /// <summary>
    /// Indicates this entropy source uses local IO to derive entropy.
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class LocalIOSourceAttribute : Attribute
    {
    }
    /// <summary>
    /// Indicates this entropy source is disabled by default and must have some configuration set to be loaded.
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SourceDisabledByDefaultAttribute : Attribute
    {
    }
}
