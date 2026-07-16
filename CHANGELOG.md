# Changelog
I figured that this would be nice to have, as to easily take a look at everything that's been done and why your mod might not work with the new update (sorry!)
**New stuff** documents new stuff introduced in the latest version
**Changes** refers to potential mod-breaking changes, which may need manual work to update
**Fixes** refers to bugfixes that are very unlikely or will not break your mod

## v1.0.4

### New Stuff!
-

### Changes
-

### Fixes
-

## v1.0.3

### New Stuff!
- Hotloading is a lot more mod-author friendly, so that you don't have to wrangle your code around it (as much)
- Check out the new docs for information! ^^
- Nightly/on-commit releases for CUCoreLib!
- Added itemsVisible and tagRestriction (tags only, no crafting quality)
- Added more verbose warnings
- Runtime debugger for variables was introduced, under the console command `debugwatch`
- Added early AssetBundle support for minigame screens, with a guide to follow soon
- Added bundled body animation curve/profile loading support, ^
- Added `DropPool` support for custom items, so they can target corpses, crates, traders, and capsules directly. Access this via the new itemregistry field `DropPool = DropPool.FoodCrate | DropPool.MedicalCrate`.
- The specific fields are `Corpse, MedicalCrate, FoodCrate, ContainerCrate, Trader1, Trader2, Trader3, (or, for all three trader species) AllTraders, DropCapsule, CapsuleContainer`
- Added itemregistry field `WorldSpawnPerChunk` for randomly spawning raw items into the world per chunk
- Added `SetWornSprite` / `setWornSprite` helpers for refreshing live worn sprites
- Added a few more CUCoreUtils functions akin to ^
- Animation support

### Changes
- !! Recipes with no args now default to 90% condition instead of 0% condition !! (Sorry, it was the lesser or two evils. You need to use (0f) to have it back to 0% condition requirement)
- Locale recursive directory search was accidentally removed and now re-added
- Slimmed down batteryProperties, as I was overcomplicating it. Your mods will work still, but you may face some slight changes
- Custom structures now no longer spawn in the tutorial, and spawn up to only once in the debug world
- Massively overhauled `customData` item properties. Tl;dr it's now a per-`Item` runtime conditionalweaktable.
- This means that it's now per-item state and can use the new apis: `SetCustomData`, `RemoveCustomData`, `HasCustomData`, `GetAllCustomData`.  It works well with custom scripts, as well
- This means it's no longer shared between all of the same item type (???), and as always, saves between layers and runs ^^

### Fixes 
- Battery 0% condition issue...
- Fix keycode optimization (Thanks, @Jacbo1)!
- Crafting qualities now work and are added to the locale 
- `spawncategory` now can spawn all modded items
- Fixed lighting module pointLightOuterAngle/pointLightInnerAngle
- Fixed equippables teleporting when trying to equip on top on another, as well as console error logs whilst equipping for the first time
- Minor optimization changes
- Fixed QoL image integration again
- Fix modded watermark (this is a modded..)
- Item colliders now ignore image transparent pixels
- Fixed `spawncategory` duplicate autofill weirdness
- `spawncategory` now supports an optional `modGUID` filter
- Fixed late applications of icon sprites not working for wearables (e.g. auto equipping via traders and whatnot)
- More docs work, once again :)
- Added more warning code (i.e. for missing moodle sprites)
- Fix wearable sprite defaults
- Settings menu changes (Thanks, @Black_Moss)!

## v1.0.2

### New Stuff!
- Added a multi-block structure system via `StructureRegistry`, for custom structures. Will need to integrate still via the custom-structure-webapp side, though...
- Added expanded minigame helper support, subject to change
- Added keybind and keycode-related support
- Added XML documentation comments across the codebase! (this is the large change of the version)
- Added preloading for embedded images for optimization
- BuildingEntites how has a SpawnLayers field
- Added InventoryIconScale field to items
- Embedded locale files now work instead of needing to be bundled alongside the mod 

### Changes 
- Expanded settings and locale UX support (locale category work, EN fallback behavior, and menu improvements)
- Updated moodle image defaults to use a `33.33f` pixels-per-unit baseline.
- Set `BuildingEntity`s to default to being Standard placement style instead of None.
- Having a light property now doesn't give your item a battery for some reason 

### Fixes 
- Sprite PPU behavior for is no longer fixed.
- Fixed several settings-page interaction issues, thanks @Black-Moss!
- Added console and utility support, thanks @Black-Moss!
- Reduced duplicate-warning noise.
- Added a dedicated multi-block structures docs page 
- Refreshed setup, settings, assets, utils, minigame, moodle, and status documentation.
- Documentation, documentation, documentation.
- Battery fixes once more
- Fix console errors with RegisterSpawnEntities
- Added a few enums for clarity

## v1.0.1 
- All update logs prior were lost in the great time catastrophe...

## v1.0
- Release!
