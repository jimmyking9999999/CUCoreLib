# CUCoreLib
A definitive modding library for Casualties: Unknown.

<div align="center">
<img width="648" height="400" alt="CUCoreLib" src="https://github.com/user-attachments/assets/24e022ba-0af1-4ce9-a66c-6b28b5e941f1" />
</div>


<div align="center">
  
[![Latest Release](https://img.shields.io/github/v/release/jimmyking9999999/CUCoreLib?include_prereleases&color=orange&logo=github)](https://github.com/jimmyking9999999/QoL-Unknown/releases/latest)
[![Total Downloads](https://img.shields.io/github/downloads/jimmyking9999999/CUCoreLib/total?color=blue&logo=github)](https://github.com/jimmyking9999999/QoL-Unknown/releases)
![Game Version](https://img.shields.io/badge/Game_version-v7.1.0-green)
[![Documentation](https://img.shields.io/badge/Documentation-blue)](https://cucorelib.web.app/#welcome)

</div>


CUCoreLib is a BepInEx-based library for Casualties: Unknown. It exists to make common modding work reusable, via custom content registration, shared helpers, patch-safe integration points, and stable save, world, UI, and other APIs for dependent mods.

Note: The project is not the base game source!


## Documentation
<div align="center">
<img width="516" height="400" alt="Webapp" src="https://github.com/user-attachments/assets/c836ede6-7aa3-4676-8051-37560beaa419" />
</div>

For developers looking to use this mod, please refer to the [Setup](https://cucorelib.web.app#setup) page in the online documentation.

- API documentation is in `CUCoreLibWebapp/`.
- The live website exists at https://cucorelib.web.app


---

For contributors, please refer below:

## First-Time Setup

CUCoreLib builds against the game's managed assemblies, so you'll need a local Casualties Unknown install available before the project can compile. Those game DLLs are intentionally not bundled in this repository.

1. Clone the repo.
2. Make sure you have the Casualties Unknown Demo installed locally.
3. Copy the contents of `CUCoreLib.Local.props.example` to `CUCoreLib.Local.props`.
4. Set `BaseGamePath` in `CUCoreLib.Local.props` to your local game install if the default Steam path does not match your machine.

Note: If you're on a Windows operating system using the Steam version of the game, you can disregard steps 3/4 and head straight to building.

If you prefer environment variables instead of a local props file, you can set:

- `CUCORELIB_BASE_GAME_PATH`
- `CUCORELIB_ASSEMBLY_CSHARP_PATH`

The project resolves Unity and `Assembly-CSharp` references from your local game `CasualtiesUnknown_Data\Managed` folder.


## Basic Build

```powershell
dotnet build "CUCoreLib.sln" -c Debug
```

You can also open the solution in Visual Studio and build via Ctrl + Shift + B.

### Local Build Without Deploying To The Game Folder

By default, the project copies the built DLL into `BepInEx\plugins` when `BepInExDir` is available, for ease of use. If you want a build that stays inside the repo, use:

```powershell
dotnet build "CUCoreLib.sln" -c Debug -p:BepInExDir="C:\EDIT_THIS_PATH\TO\CUCoreLib\_builddeploy\BepInEx"
```

## Contributing

Contributions are welcome! We especially like fixes and improvements that make CUCoreLib a more stable shared modding foundation.

Before opening a change:

- Start from behavior in the current CUCoreLib C# source.
- Check `CUCoreLibWebapp/` before changing public-facing API guidance or examples.
- Prefer extending existing registries, helpers, and patch families instead of adding parallel systems. (i.e. embedding a new video format into the assetloader rather then a new method)
- Keep runtime behavior deterministic and backward-friendly for dependent mods unless starting from a new major version (X.0.0)

When working on code:

- Preserve the existing C# style and Harmony patch patterns. (See the contributing page in the webapp)
- Keep side effects in mind and an open mind
- Add null and runtime-state guards where a patch might run before game state is ready, or other mods may also call the method
- Avoid committing generated outputs from `bin/`, `obj/`, or `CUCoreLibWebapp/dist/`. 

When changing public behavior:

- Update docs or examples in `CUCoreLibWebapp/` if the supported API has been changed.
- Validate the impact quickly with existing mods if possible
- Mention any required migration notes clearly in your PR or commit message

## Repo Format

- `Patches/`, `Registries/`, `Helpers/`, `Data/`, and `Saving/` contain the main runtime source.
- `CUCoreLibWebapp/` is the contributor-facing API and usage reference.
