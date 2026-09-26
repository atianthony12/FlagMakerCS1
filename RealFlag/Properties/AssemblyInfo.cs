using System.Reflection;
using System.Runtime.InteropServices;
using RealFlag;

[assembly: AssemblyTitle("RealFlag")]
[assembly: AssemblyDescription("Realistic cloth flags that wave in the wind.")]
[assembly: AssemblyProduct("RealFlag")]
[assembly: ComVisible(false)]

// The release is Mod.Version; the wildcard fills the fourth part with the time of day
// (seconds / 2), which gives every build a distinct assembly identity -- what lets the
// game load a rebuilt DLL instead of the copy it already has cached. Paired with
// <Deterministic>false</Deterministic> in the .csproj -- it does nothing without it.
[assembly: AssemblyVersion(Mod.Version + ".*")]
[assembly: AssemblyInformationalVersion(Mod.Version)]
