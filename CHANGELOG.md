# Changelog
I figured that this would be nice to have, as to easily take a look at everything that's been done and why your mod might not work with the new update (sorry!)

**New stuff** documents new stuff introduced in the latest version

**Changes** refer to potential mod-breaking changes, which may need manual work to update

**Fixes** refers to bugfixes that will not or are very unlikely to break your mod

## v1.0.4

### New Stuff!
- The good part about making a library mod is that whenever I want a new feature for one of my other mods, I can share it with the rest of the world :)
- Well, you can, too. It's open source!
- Potentially related, added `TryGetOwnerModGuid` queries to the item, liquid, building, tile, and recipe registries
- Not related, added `GunProperties`. Super basic fields for now, might think about custom stuff later down the line
- Custom liquids can set `unobtainable = true` to exclude themselves from random minibarrel contents.
- Added `AssetLoader.LoadFrameAnimationFromEmbeddedResources(...)` for ordered embedded sprite-frame animations, with hot-reload cache invalidation.
- Added `CustomItemInfo.LiquidMaskAnimationId` so container fill masks can use registered sprite animations while `LiquidMask` remains supported for static masks.
- Added the built-in `bug-report [description] [bool screenshot] [severity]` command. Send quick reports to your favourite (?) modders!
- Added `CUCoreUtils.EditVanillaItem(...)` 
- Added `CUCoreUtils.OnHeal` and `CUCoreUtils.OnLastStand` callbacks, yay!
- Added `ItemRegistry.ToCustomItemInfo(...)` and `CUCoreUtils.ToCustomItemInfo(...)` for converting vanilla item definitions
- You can now have your entire mod's items in one image with `CUCoreUtils.SplitSpriteSheet(...)`, if you want that for some reason 
- You'd be better off using ^ for animations, of which now works with LiquidMasks and can be imported from embedded assets!
- Cool missing item textures now for missing sprites, thanks @comradefoxx!
- Spritesheet support (very basic)

### Changes
- QoL settings menu compatibility
- Fixed ScaleWithCondition scaling towards 0.1f. Technically this is basegame, but a bit confusing to most
- Tiles now can use alphanumeric IDs `TileRegistry.Register("auric", ... )` (o7 calamity)

### Fixes
- KrokMP v4 support
- Fixed multiplayer statuses and `CCLBody` contributions leaking from a client body onto the host player.
- It feels kinda weird making QoL changelogs in here, but the reason that one hasn't gotten any updates is since it's tied to mpv5, (which is in its early playtest phase) and I really don't want to navigate my tangled version control to handpick the features and remove v5 support. Guuuh.
- `LightProperties` supports local `Rotation` (light rotation), `Offset` (light offset), and `FalloffIntensity` (harshness/softness)
- Added optional sync between `ModOptionsRegistry` settings and manually bound BepInEx config entries when they share the same namespaced ID or key suffix
- Added `CCLBody` for formula-owned vanilla `Body` fields such as blood pressure and encumberance values, so mods can inject simple per-mod contributions without writing their own Harmony patches.
- ^ do send suggestions for more body fields that might want to be added!
- `StatusMoodleDefinition` can now carry a direct sprite icon instead of requiring a pre-existing vanilla icon ID.
- Tiles? Liquids? Why not both? Added early liquid tile support
- Liquid registration now has an onuse field and inject field that can be used in place of the item
- Removed the watermark, except it wasn't there in the first place and didn't even work, erm...
- QoL Unknown-multiplayer-CUCorelib compatibility patch, may or may not work
- Custom tile drops are no longer tied to KrokMP compatibility (yeah.)
- Settings menu description fixes
- Fixed a startup `InvalidProgramException`, affecting only a few people (?)
- Mod options now fall back to their registered label/description/dropdown text when no locale overrides exist (whoops!)
- Item.stats works properly for a couple more immutable stats
- ^ once more, do send suggestions for more body fields that might want to be added!
- Autofills now exist for `floodfill` and `settile`
- Documentation, bleh...
- Moved a bunch of stuff around, really sorry for your forks if you touched the hot reload code
- Fixed custom items spawning without box colliders for the first instance. Thanks, @Sylviebbq!
- Sorry, germs are now present in the ground water again (fixed liquid tile wrappings). Thanks again, @Sylviebbq!

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

### New stuff!
- Hot reload! See https://cucorelib.web.app/docs/debug-testing for more details
- ^ You can now develop your mod without needing to close the game at all! (for basic functionality)
- Automatic sandbox/debug world config
- Version checker
- Liquid sprite mask

### Changes
- Added more smart defaults and regular defaults
- Battery fixes

### Fixes
- Fixed tiles not working
- Fixed lifepod in mp
- Fixed createLocale
- Improved webapp, added sitemap
- V4.0.0.0 mp compatibility
- DestroyAtZeroCondition fixes
- Recovered the changelogs from the great time catastrophe! Not so great now, huh?

## v1.0
- Release!

### Reminder for mod developers:
- This .dll must be in the BepInEx/plugins/ folder
- Your own mod must add an assembly reference to this mod. For non-visual studio users, you must add a reference in your .csproj
- For visual studio users, right click assemblies -> add assembly -> navigate to the CUCoreLib.dll file
- For more info, see https://cucorelib.jimmyking.dev#setup

Happy modding!
