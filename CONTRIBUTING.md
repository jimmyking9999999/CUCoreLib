# Contributing to CUCoreLib

Thanks for wanting to help out!

<div align="center">
<img width="648" height="424" alt="image" src="https://github.com/user-attachments/assets/178ae7b4-5b9e-4d53-be1c-f65357c6bb25" />
</div>

Contributions are welcome! We especially like fixes and improvements that make CUCoreLib a more stable shared modding foundation.

That includes documentation work. Especially documentation work.

<b>Before opening a change:</b>

- Start from behavior in the current CUCoreLib C# source! Try not to make new systems if unneeded.
- Instead, prefer extending existing registries, helpers, and patch families instead of adding parallel systems. (i.e. embedding a new video format into the assetloader rather then a new method)
- Keep runtime behavior deterministic and backward-friendly for dependent mods! Backwards compatibility is one of the most important aspects of CUCoreLib.

<b>When working on code:</b>

- Keep side effects in mind and an open mind. This doesn't mean test it with every mod, but be mindful of potential failure points.
- Add null and runtime-state guards where a patch might run before game state is ready, or other mods may also call the method. Defensive coding is always appreciated ^^

<b>When changing public behavior:</b>

- Update docs or examples in `CUCoreLibWebapp/` if the supported API has been changed. Not explicitly needed, but very much appreciated!
- Validate the impact quickly with existing mods if possible.
- Mention any required migration notes clearly in your PR or commit message, or edit `CHANGELOG.md`

## Getting set up
1. Fork and clone the repo.
2. Copy `CUCoreLib.Local.props.example` to `CUCoreLib.Local.props` and point `BaseGamePath` at your Casualties: Unknown install.
3. Open `CUCoreLib.sln` in Visual Studio (2022 works great), restore NuGet packages, and build.

If `dotnet build` gives you missing-reference errors, try building with Visual Studio's MSBuild instead. The project is .NET Framework 4.7.2, and the SDK doesn't always like it.

## Repo Format
- `Registries/`: the public APIs mods call (items, recipes, buildings, etc.)
- `Patches/`: Harmony patches that wire said registries into the game
- `Helpers/`: shared utilities
- `Data/`: the definition types, for modders
- `CUCoreLibWebapp/`: the documentation site! (Astro + MDX)



## Pull requests
- Keep PRs reasonably focused! One feature or fix per PR is ideal, so do split up multiple changes!
- A short description of what changed and why is swell.

## Bugs? Questions?
- It's worth popping into the [Discord](https://discord.gg/ehCptz9pwU) or opening an issue for and questions you might have :)

Happy modding!
