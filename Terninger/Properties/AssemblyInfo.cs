using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Terninger")]
[assembly: AssemblyDescription("Implementation of the Fortuna CRNG, plus other random number helpers")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Murray Grant")]
[assembly: AssemblyProduct("Terninger")]
[assembly: AssemblyCopyright("Copyright © Murray Grant 2017-2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("3341dacf-fe94-4497-b71b-ab3326b530fd")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

// For unit testing.
[assembly: InternalsVisibleTo("Terninger.Test"),
           InternalsVisibleTo("Terninger.Test.Slow")]