# Building RealFlag

## What you need

- The .NET SDK (any recent one: the project targets `net35` through the
  `Microsoft.NETFramework.ReferenceAssemblies.net35` package, which it downloads).
- Cities: Skylines installed through Steam at the default path (the game's DLLs are referenced
  from `steamapps\common\Cities_Skylines\Cities_Data\Managed`, never copied).
- For the shader and the GPU preview only: **Unity 5.6.6** at
  `C:\Program Files\Unity\Editor\Unity.exe` (with the Mac and Linux Build Support modules for
  those bundles). The game runs Unity 5.6.7; 5.6.6 bundles load fine.

## The mod

The panel is built with **ModsLibCS1**, a UI library kept as a git submodule at the repository
root and compiled into the DLL with its sprite sheet. Clone with `git clone --recurse-submodules`,
or run `git submodule update --init` after a plain clone; without it the build stops and says so.
The panel's own icons (its tabs, the images folder) are listed in
`RealFlag/Resources/PanelIcons.lucide.txt`; after a change to that list, draw them again with
`powershell -File ModsLibCS1\tools\make-icons.ps1 -Spec RealFlag\Resources\PanelIcons.lucide.txt`.

```
dotnet build RealFlag/RealFlag.csproj
```

builds and deploys `RealFlag.dll`, `UnifiedUILib.dll`, `CitiesHarmony.API.dll` and
`MoveItIntegration.dll` to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\RealFlag`.
To build without deploying, add `-p:Deploy=false`; to compile only,

```
dotnet build RealFlag/RealFlag.csproj -t:Compile
```

which leaves the DLL in `RealFlag\obj\Debug` (give the tests `-Dll RealFlag\obj\Debug\RealFlag.dll`).
A bare `-t:Compile` skips MSBuild's PrepareResources, and used to leave the shader bundle, the
icon and the language files out of the DLL -- and the next full build, finding the DLL up to
date, deployed it so. The project now prepares the resources before every compile
(`PrepareResourcesBeforeCompile`), and `tools\test-cloth.ps1` checks they are in.

Every build gets a new assembly version (`AssemblyVersion(Mod.Version + ".*")` with
`<Deterministic>false</Deterministic>`), which is what lets the game load a rebuilt DLL. The IL
is optimised in every configuration: the cloth is stepped 60 times a second for every flag in
view, and an unoptimised build was seven times slower.

## The shader

```
.\build-bundle.ps1 -Only win
```

after editing anything under `UnityProject\Assets`. It first copies the mod's shared sources
into `UnityProject\Assets\Sim` (`tools\sync-sim.ps1`: the editor compiles every script there
before anything else), then asks Unity whether the cloth shader compiled
(`Assets\Editor\ShaderCheck.cs`: a surface shader Unity cannot parse becomes a one-pass
stand-in with NO error in the log -- "Parser stack overflow" on a long `else if` ladder did
exactly that), then builds the bundle, refuses to copy anything if Unity logged a shader error
or warning, and runs `tools\bundle-apis.ps1`: every shader must carry real code for every
graphics API of its platform. Unity leaves an EMPTY blob, with no error, for an API it cannot
compile for -- which is why the shader is `#pragma target 3.5` and never 4.0 (4.0 means
"geometry shaders", which Metal lacks). Without `-Only win` it builds all three bundles
(Windows: Direct3D 11 + OpenGL Core, Linux: OpenGL Core, Mac: Metal + OpenGL Core). Then
rebuild the mod to embed them.

## Checking changes without the game

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-cloth.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-settings.ps1 -Dll RealFlag\obj\Debug\RealFlag.dll
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-localization.ps1
```

`test-cloth` holds the physics to what flags do (limp in calm air, out in a moderate breeze,
waves running to the fly slower than the wind and growing towards it, never stretched past a
tenth), the wind to the Eurocode, the cloth's thread to the main thread's result bit for bit,
the frame planning to its budget, and the save and Move It formats to their round trips. The
other two check the settings file format and the language files.

Apple's Metal compiler (the Metal Developer Tools for Windows, free behind an Apple account sign-in
at developer.apple.com/download/all; looked for in `Program Files\Metal Developer Tools` or
`$env:RF_METAL`; its installer's PATH step is not needed):

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-metal.ps1
```

cuts every Metal program out of the shipped mac bundle (Unity 5.6 ships Metal as source, compiled
by the Mac when the shader loads) and compiles each at the oldest and newest Metal language
versions; it fails on a program Apple's compiler refuses, which on a Mac makes the whole shader
unsupported. The table shows each program's textures, samplers and buffers (Metal: 16 samplers).

AMD's compiler (Radeon GPU Analyzer, free: github.com/GPUOpen-Tools/radeon_gpu_analyzer; runs on
any GPU, looked for in `Documents\Projects\RGA` or `$env:RF_RGA`):

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-amd.ps1
```

builds the exact programs in the shipped Windows bundle for RX 7000 and RX 9000 (through RGA's
Direct3D 12 mode) and for RX 6000, RX 5000 and Vega (Direct3D 11), and fails on a program that
does not compile or spills registers.

**Before a release:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -DryRun
```

runs every check of a release without changing anything: the tree committed, the submodule
pushed, the version new and dated in the changelog, the bundles newer than the shaders, the
Workshop texts and image, the build and what the DLL embeds, the five shipped files, the tests
above. Without `-DryRun` it also stages an update in the subscribed Workshop folder and parks the
local copy. `docs\RELEASE-CHECKLIST.md` has the rest; `tools\bbcode-preview.html` (open it from
disk) previews the Workshop description and counts it as Steam does.

**Look at a change before playing it:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\preview\gpu-render.ps1 -Winds "0,2,6,12" -Times "6,12"
```

steps the mod's own cloth in Unity 5.6 and draws it with the REAL shader through the mod's own
`Graphics.DrawMesh` path, in the game's legacy deferred lighting with a shadowing sun
(`Assets\Editor\FlagPreview.cs`), and lays the frames out as a contact sheet in
`tools\preview\out\`. `-Patterns`, `-Colours`, `-Seq`, `-Night`, `-SunYaw`, `-Yaw`/`-Pitch`/`-Dist`
and the flag's own `-Length`/`-Proportion`/`-Waviness` narrow it down; `-Bench 600` times the
cloth in Unity's Mono instead, on this thread and on the cloth's own.
