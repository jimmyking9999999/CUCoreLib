import type { HoverPanel } from "./types";

export const hoverPanels: Record<string, HoverPanel> = {
  ModOptionsRegistry: {
    title: "ModOptionsRegistry",
    signature: "bool ModOptionsRegistry.Register(ModOptionDefinition option)",
    body: "Registers a CUCoreLib settings row, validates the ID and values, adds locale entries, and merges the setting into the loaded Settings list when possible."
  },
  ModOptionDefinition: {
    title: "ModOptionDefinition",
    body: "Immutable description of one mod settings row. Build one with the static Bool, Float, Int, Dropdown, or Keybind helpers, then pass it to ModOptionsRegistry.Register."
  },
  "ModOptionDefinition.Float": {
    title: "ModOptionDefinition.Float",
    signature: "ModOptionDefinition ModOptionDefinition.Float(string id, string label, string description, Setting.SettingCategory category, float defaultValue, float min, float max, Action<float> apply = null, Func<float, string> formatValue = null)",
    body: "Creates a slider-backed setting row. CUCoreLib also provides a string-category overload when you want a custom tab button. Defaults are clamped between min and max, apply receives the live value, and formatValue controls the display text when provided."
  },
  "ModOptionDefinition.Int": {
    title: "ModOptionDefinition.Int",
    signature: "ModOptionDefinition ModOptionDefinition.Int(string id, string label, string description, Setting.SettingCategory category, int defaultValue, int min, int max, Action<int> apply = null)",
    body: "Creates an integer setting row. CUCoreLib clamps the default to the declared range and invokes apply when the user applies settings."
  },
  "ModOptionDefinition.Bool": {
    title: "ModOptionDefinition.Bool",
    signature: "ModOptionDefinition ModOptionDefinition.Bool(string id, string label, string description, Setting.SettingCategory category, bool defaultValue, Action<bool> apply = null)",
    body: "Creates a toggle setting row. Use the string-category overload if your mod should get a custom tab button. The apply callback receives the live checked value when the options screen applies settings."
  },
  "ModOptionDefinition.Dropdown": {
    title: "ModOptionDefinition.Dropdown",
    signature: "ModOptionDefinition ModOptionDefinition.Dropdown(string id, string label, string description, Setting.SettingCategory category, int defaultValue, ModDropdownChoice[] choices, Action<int> apply = null)",
    body: "Creates a dropdown row. defaultValue is the selected choice index, not the choice key string, and choices must contain at least one unique key/label pair."
  },
  "ModOptionDefinition.Keybind": {
    title: "ModOptionDefinition.Keybind",
    signature: "ModOptionDefinition ModOptionDefinition.Keybind(string id, string label, string description, Setting.SettingCategory category, KeyCode defaultValue, Action<KeyCode> apply = null)",
    body: "Creates a keybind row that stores a Unity KeyCode and invokes apply with the selected key when settings apply."
  },
  ModDropdownChoice: {
    title: "ModDropdownChoice",
    signature: "new ModDropdownChoice(string key, string label)",
    body: "One dropdown entry. key is the stable internal suffix used for locale registration, while label is the display text shown to players."
  },
  ItemRegistry: {
    title: "ItemRegistry",
    body: "CUCoreLib registry for custom items. It injects registered ItemInfo/CustomItemInfo entries into the game's Item.GlobalItems table."
  },
  RecipeRegistry: {
    title: "RecipeRegistry",
    body: "CUCoreLib registry for custom recipes. Registered recipes are injected after the game builds its vanilla recipe list."
  },
  ConsoleCommandRegistry: {
    title: "ConsoleCommandRegistry",
    body: "CUCoreLib wrapper for adding commands to ConsoleScript.Commands without writing your own Harmony postfix."
  },
  ConsoleCheckForWorld: {
    title: "ConsoleCheckForWorld",
    body: "CUCoreUtils wrapper around the game's console world check. Call it before command logic that needs PlayerCamera, body, world, or run state."
  },
  ConsoleLog: {
    title: "ConsoleLog",
    body: "CUCoreUtils wrapper for writing to the in-game console log. It supports Unity rich text, so color and alpha tags can be used in messages."
  },
  PickUpItem: {
    title: "PickUpItem",
    body: "Body method that puts an Item into an inventory/body slot. The force argument bypasses normal pickup checks, but the target slot still must be valid."
  },
  DropItem: {
    title: "DropItem",
    body: "Body method that drops either the item in a slot or a specific held Item into the world."
  },
  argAutofill: {
    title: "argAutofill",
    body: "Optional console autocomplete dictionary. Key 0 maps to args[1], the first user-provided argument after the command name."
  },
  argDescription: {
    title: "argDescription",
    body: "Optional command usage metadata. Each tuple is a short label plus a longer explanation shown by console help/usage."
  },
  Command: {
    title: "Command",
    body: "Vanilla console command object. It stores name, description, action, optional autofill, and optional argument descriptions."
  },
  AssetLoader: {
    title: "AssetLoader",
    body: "CUCoreLib helper for embedded and loose sprites, audio clips, text resources, UI sprites, and shared asset cache lookup."
  },
  CustomInstantiate: {
    title: "CustomInstantiate",
    body: "CUCoreLib helper for spawning vanilla prefabs or registered custom item templates by ID."
  },
  CUCoreMinigame: {
    title: "CUCoreMinigame",
    body: "Compatibility-focused abstract wrapper around the vanilla Minigame contract. Prefer the newer CUCoreMinigames plus CUCoreMinigameDefinition layer for new work, and keep CUCoreMinigame when a direct Minigame subclass is still the cleanest fit."
  },
  CUCoreMinigames: {
    title: "CUCoreMinigames",
    body: "Static runner facade for CUCoreLib minigames. It owns start/end helpers, exposes the active CUCoreMinigameSession, and can launch either vanilla Minigame instances or definition-based CUCoreLib minigames."
  },
  CUCoreMinigameSession: {
    title: "CUCoreMinigameSession",
    body: "Live view over the active MinigameBase runner. It exposes the active body, current item, hand state, spawned minigame UI, screen creation helpers, and runtime hand-sprite utilities without forcing global lookups throughout your minigame logic."
  },
  CUCoreMinigameDefinition: {
    title: "CUCoreMinigameDefinition",
    body: "Composable minigame definition surface. Override only the behavior you need, then start it through CUCoreMinigames.TryStart so your game logic works against a CUCoreMinigameSession instead of inheriting directly from the vanilla Minigame type."
  },
  AddDrop: {
    title: "BuildingEntityRegistry.AddDrop",
    body: "Convenience helper that creates an ItemDrop with an ID, chance, and condition range. Custom building drops use CUCoreLib spawning, so registered custom item IDs work here too."
  },
  SurfaceOffset: {
    title: "SurfaceOffset",
    body: "Distance to move the spawned building away from the raycast hit surface. Floor objects move upward from the ground, ceiling objects move downward, and wall objects move away from the wall."
  },
  SpawnMinPerChunk: {
    title: "SpawnMinPerChunk / SpawnMaxPerChunk",
    body: "Worldgen density range multiplied by chunkWidth * chunkHeight. Keep values small: 0.01f means roughly one spawn attempt per 100 chunks before placement checks."
  },
  SpawnMaxPerChunk: {
    title: "SpawnMaxPerChunk",
    body: "Upper end of the random worldgen density range. CUCoreLib picks a value between SpawnMinPerChunk and SpawnMaxPerChunk each placement pass."
  },
  Placement: {
    title: "Placement",
    body: "Which surface type CUCoreLib searches for during placement. Floor casts downward, Ceiling casts upward, and Wall casts sideways with automatic horizontal flipping."
  },
  GenerationStyle: {
    title: "GenerationStyle",
    body: "Controls whether CUCoreLib places the building during world generation. None disables automatic spawning, Standard uses surface raycasts, and DropPod carves terrain around a rare impact-style object."
  },
  HitSoundReferenceId: {
    title: "HitSoundReferenceId",
    body: "Copies a vanilla BuildingEntity hit sound from a reference prefab. Short aliases include metal -> turret, rubber -> glowplant, rustle -> geotree, and crystal -> BloodCrystal."
  },
  InstantiateReturn: {
    title: "InstantiateReturn",
    body: "Spawns an object by ID at a position/rotation and returns the created GameObject. For custom items, CUCoreLib builds a template from the registered CustomItemInfo."
  },
  BepInPlugin: {
    title: "BepInPlugin",
    body: "BepInEx attribute that gives your mod a unique GUID, a display name, and a version. BepInEx uses it to discover and load the plugin."
  },
  BepInDependency: {
    title: "BepInDependency",
    body: "BepInEx attribute that tells the loader another plugin must load before yours. Without this, CUCoreLib can't be used."
  },
  BaseUnityPlugin: {
    title: "BaseUnityPlugin",
    body: "The Unity-facing base class for a BepInEx plugin. It gives your plugin lifecycle methods, logging, config access, and coroutine support."
  },
  Awake: {
    title: "Awake",
    body: "Unity calls Awake when this plugin object is created. For mods, this is the normal place to register content and apply Harmony patches."
  },
  Logger: {
    title: "Logger",
    body: "BepInEx plugin logger. Messages show up in the BepInEx console/log and are usually your first debugging tool."
  },
  GameObject: {
    title: "GameObject",
    body: "Unity's container object in a scene. Behavior usually comes from components attached to a GameObject."
  },
  Component: {
    title: "Component",
    body: "A piece of behavior or data attached to a GameObject. Scripts, renderers, colliders, inventory objects, and many game systems are component-driven."
  },
  MonoBehaviour: {
    title: "MonoBehaviour",
    body: "Unity component base class. BaseUnityPlugin ultimately participates in this Unity lifecycle model, which is why methods like Awake exist."
  },
  Transform: {
    title: "Transform",
    body: "Every GameObject has a Transform. It stores position, rotation, scale, and parent/child hierarchy."
  },
  Scene: {
    title: "Scene",
    body: "A loaded Unity world or menu container. Casualties Unknown swaps between menu, loading, and gameplay scenes during normal play."
  },
  Prefab: {
    title: "Prefab",
    body: "A reusable GameObject template saved as an asset. Mods usually inspect or instantiate existing game prefabs rather than editing the project prefab file."
  },
  AssemblyCSharp: {
    title: "Assembly-CSharp.dll",
    body: "The main compiled game-code assembly for many Unity games. Open it in dnSpyEx to inspect Casualties Unknown script classes and values."
  },
  Texture2D: {
    title: "Texture2D",
    body: "Unity texture asset type. Extracted item art and sprites often start as Texture2D assets."
  },
  Start: {
    title: "Start",
    body: "Unity calls Start before the first Update on an enabled MonoBehaviour. Use it when other objects need one frame to initialize."
  },
  Update: {
    title: "Update",
    body: "Unity calls Update once per rendered frame. Use it sparingly in mods because it can run thousands of times per minute."
  },
  FixedUpdate: {
    title: "FixedUpdate",
    body: "Unity calls FixedUpdate on the fixed timestep used for physics-style work. Most CUCoreLib registration code does not belong here."
  },
  Time: {
    title: "Time",
    body: "Unity's timing API. Time.deltaTime is the previous frame duration and is commonly used for frame-rate-independent movement or timers."
  },
  deltaTime: {
    title: "deltaTime",
    body: "Seconds since the previous frame. Multiply per-second changes by deltaTime when code runs from Update."
  },
  Harmony: {
    title: "Harmony",
    body: "Runtime patching library commonly used by BepInEx mods. Use it when you need to hook a game method instead of registering through CUCoreLib."
  },
  CUCoreUtils: {
    title: "CUCoreUtils",
    body: "Shared utility helpers for delayed work, readiness checks, PlayerPrefs booleans, reflection, console bridging, key labels, and compression."
  },
  Register: {
    title: "Register(...)",
    body: "Main CUCoreLib entry point for adding items or recipes from a dependent mod."
  },
  CustomItemInfo: {
    title: "CustomItemInfo",
    body: "CUCoreLib item definition with vanilla ItemInfo fields, vanilla LiquidItemInfo fields such as capacity/defaultContents/autoFill, and extras like Container, Battery, WornSprite, SpawnFrequency, SpriteScale, SpriteScaleDimensions, or CustomData."
  },
  WornSpriteOffset: {
    title: "WornSpriteOffset",
    body: "Vector2 local offset applied after the item is worn. X moves left/right on the limb, Y moves up/down. Start tiny, such as new Vector2(0f, -0.04f)."
  },
  ContainerProperties: {
    title: "ContainerProperties",
    body: "CUCoreLib module data that adds/configures a vanilla Container component on a spawned custom item."
  },
  BatteryProperties: {
    title: "BatteryProperties",
    body: "CUCoreLib module data that adds/configures a vanilla BatteryItem component on a spawned custom item."
  },
  BandageProperties: {
    title: "BandageProperties",
    body: "CUCoreLib module data that installs a vanilla-style BandageMinigame limb action and applies limb healing, bleeding slowdown, pain reduction, and timer reductions."
  },
  SyringeProperties: {
    title: "SyringeProperties",
    body: "CUCoreLib module data that adds syringe-style WaterContainerItem behavior and uses SyringeMinigame to inject liquid into a limb."
  },
  CustomData: {
    title: "CustomData",
    body: "Dictionary for mod-owned metadata stored on CustomItemInfo. Use it for registration-time values, not per-spawn mutable state."
  },
  ExtensionData: {
    title: "ExtensionData",
    body: "CUCoreLib helper backed by ConditionalWeakTable. Use it when your mod wants to attach per-instance state without editing vanilla game classes."
  },
  BodyStatus: {
    title: "BodyStatus",
    body: "Base class for mod-owned per-Body runtime state. Access it through body.GetStatus<TStatus>()."
  },
  LimbStatus: {
    title: "LimbStatus",
    body: "Base class for mod-owned per-Limb runtime state. Access it through limb.GetStatus<TStatus>()."
  },
  StatusOptions: {
    title: "StatusOptionsAttribute",
    signature: "[StatusOptions(Key = \"com.yourname.status\", SaveEnabled = true)]",
    body: "Optional metadata for a status type. Use it to set a stable save key or disable automatic status persistence."
  },
  StatusMoodleDefinition: {
    title: "StatusMoodleDefinition",
    body: "Return one of these from a MoodleRegistry body/limb callback when your custom status should show a vanilla moodle entry."
  },
  MoodleRegistry: {
    title: "MoodleRegistry",
    body: "CUCoreLib registry for contributing custom body or limb moodles without putting UI override methods directly on the status class."
  },
  Capacity: {
    title: "Capacity",
    body: "ContainerProperties value mapped to Container.maxWeight: the total weight this container can hold."
  },
  MaxWeightPerItem: {
    title: "MaxWeightPerItem",
    body: "ContainerProperties value mapped to Container.maxWeightPerItem: the largest single item the container accepts."
  },
  EncumbranceReduction: {
    title: "EncumbranceReduction",
    body: "ContainerProperties value mapped to Container.encumberanceMult. 1 is normal; lower values reduce carried-content burden."
  },
  MaxCharge: {
    title: "MaxCharge",
    body: "BatteryProperties maximum charge used by the vanilla BatteryItem component."
  },
  StartCharge: {
    title: "StartCharge",
    body: "BatteryProperties initial charge applied on fresh spawned BatteryItem components when possible."
  },
  Effectiveness: {
    title: "Effectiveness",
    body: "BandageProperties divisor for BandageMinigame normalAngle. Most game dressings sit around 8-15. Lower values spend more condition and apply stronger effects per use; higher values are gentler/weaker."
  },
  SkinHealAmount: {
    title: "SkinHealAmount",
    body: "BandageProperties multiplier added to Limb.skinHealAmount after the minigame."
  },
  BandageSlowAmount: {
    title: "BandageSlowAmount",
    body: "BandageProperties multiplier added to Limb.bandageSlowAmount, which slows bleeding while it remains above zero."
  },
  PainReduction: {
    title: "PainReduction",
    body: "BandageProperties multiplier subtracted from Limb.pain after a successful bandage minigame."
  },
  BoneHealTimerReduction: {
    title: "BoneHealTimerReduction",
    body: "BandageProperties multiplier subtracted from Limb.boneHealTimer."
  },
  DislocationTimerReduction: {
    title: "DislocationTimerReduction",
    body: "BandageProperties multiplier subtracted from Limb.dislocationTimer."
  },
  CreateWrapSprite: {
    title: "CreateWrapSprite",
    body: "When true, CUCoreLib creates the temporary vanilla bandage wrap sprite on the target limb."
  },
  WrapSpritePath: {
    title: "WrapSpritePath",
    body: "Resources path used by CreateTemporarySprite for the bandage wrap. Vanilla uses Special/bandageWrap."
  },
  AmountPerFullUse: {
    title: "AmountPerFullUse",
    body: "SyringeProperties amount injected when SyringeMinigame returns a full multiplier."
  },
  DefaultContents: {
    title: "DefaultContents",
    body: "Initial LiquidStack list placed into the syringe's WaterContainerItem when the spawned item starts."
  },
  ItemInfo: {
    title: "ItemInfo",
    body: "The game's vanilla item stat block. CUCoreLib reuses this instead of inventing a parallel item data model."
  },
  Recipe: {
    title: "Recipe",
    body: "A vanilla crafting recipe. CUCoreLib registers normal Recipe objects so crafting behavior stays on the game's own path."
  },
  RecipeItem: {
    title: "RecipeItem",
    body: "A required ingredient. It can match an exact item/liquid ID or any item/liquid with a CraftingQuality."
  },
  RecipeResult: {
    title: "RecipeResult",
    body: "The recipe output. For items, resultCondition is condition from 0 to 1. For liquids, resultCondition is mL."
  },
  category: {
    title: "category",
    body: "There are two types of categories: ItemInfo.category and Recipe.category. Iteminfo:",
    items: [
      "container (Container items)",
      "drug",
      "food",
      "medical",
      "tool",
      "trash (Junk. Not in trader stock).",
      "unobtainable (Unobtainable without console or other mod help)",
      "utility (General equipment and utility items that don't fit other categories, like backpacks or lamps)",
      "water (Liquid containers)",
      "custom (Miscellaneous items, that don't fit other categories. Not in trader stock by default, but can be added with custom loot pools.)",
      "Iteminfo.category is used for loot pools, trader stock, and trader categories.",
      " -- ",
      "Recipe categories: Materials, Tools, Medicine, Utilities, Food.",
      "Recipe.category is used for UI purposes in the crafting menu only. It does not decide loot pools or trader stock."
    ]
  },
  decayMinutes: {
    title: "decayMinutes",
    body: "How long the item lasts before condition decay. Minutes."
  },
  tags: {
    title: "tags",
    body: "Comma-separated vanilla item tags.",
    items: [
      "antiseptic: used on antiseptic/water-style material. For antiseptic crafting.",
      "backflip: item can be visually flipped when held in later hand slots, commonly paired with tools.",
      "battery: battery-like item, referenced by UI/drag logic for battery interactions.",
      "belttool: item suitable for belt storage.",
      "bullet: ^ but with bandoliers.",
      "medicine: ^ but with medkits.",
      "cangetwet: item can enter the wet state. Being wet increase decay speed and makes food taste worse. In some cases, can be used to disable functionality (i.e. jetpack, torch, campfire)",
      "dressing: bandage/dressing medical item. Traders treat this specially for free dressing logic.",
      "fruit: fruit-like food item. For crafting purposes.",
      "gun: firearm item. Used by body animation, player controls, and trader threat checks.",
      "noautopickup: blocks automatic pickup behavior.",
      "placeable: item can be placed through PlayerCamera placement logic.",
      "tool: tool item, affects player interaction/drag logic.",
      "water: water-related item/material tag. For crafting purposes."
    ]
  },
  weight: {
    title: "weight",
    body: "Item weight used by inventory and container logic."
  }, 
  Eat: {
    title: "eat",
    body: "Eat(x, y). x is the amount of hunger restored, y is the amount of body weight increased. They can both be negative, and y should be between 0 and 1.5f."
  },
  scaleWeightWithCondition : {
    title: "scaleWeightWithCondition",
    body: "When true, the item's weight scales linearly with its condition. At 0% condition, the item has no weight. At 100% condition, it has full weight."
  },
  injectionSickness : {
    title: "injectionSickness",
    body: "For liquids, the amount of sickness applied on the body when injected or drunk. Don't use this, as it is only here to imitate the vanilla field."
  },
  Drink: {
    title: "drink",
    body: "Amount of thirst restored. Can be negative."
  },
  value: {
    title: "value",
    body: "Default trade value for the item."
  },
  autofill: {
    title: "autofill",
    body: "Filled when touching ground liquids in the world."
  },
  weightOffset : {
    title: "weightOffset",
    body: "Body weight offset. Calculated as (50 + weightOffset * 0.34)"
  },
  valuePerLiter : {
    title: "valuePerLiter",
    body: "Only for liquids. Value/liter. The game calculates total value as valuePerLiter * (current mL / 1000) + container value"
  },
  usable: {
    title: "usable",
    body: "Allows the item to run useAction when the player uses it. WARNING: An item with a useAction but usable=false will not run the delegate when used."
  },
  usableOnLimb: {
    title: "usableOnLimb",
    body: "Allows the item to target a specific Limb and run useLimbAction. Use this for targeted medical tools, limb temperature changes, or wound effects."
  },
  autoAttack: {
    title: "autoAttack",
    body: "Vanilla ItemInfo field used by weapon-like items so player controls treat the item as an attack tool."
  },
  usableWithLMB: {
    title: "usableWithLMB",
    body: "Vanilla ItemInfo field that lets left mouse button trigger use/attack behavior."
  },
  wearable: {
    title: "wearable",
    body: "Marks an item as equipment that can be worn by Body.WearWearable and saved through wearable slots."
  },
  wearableCanBeHeld: {
    title: "wearableCanBeHeld",
    body: "Allows a wearable item to still be held/carried normally."
  },
  desiredWearLimb: {
    title: "desiredWearLimb",
    body: "Vanilla wearable field naming the limb/body region the item wants to attach to:",
    items: [
        "UpTorso",
        "DownTorso",
        "UpArmF",
        "DownArmF",
        "HandF",
        "UpArmB",
        "DownArmB",
        "HandB",
        "ThighF",
        "CrusF",
        "FootF",
        "ThighB",
        "CrusB",
        "FootB",
    ]
  },
  wearSlotId: {
    title: "wearSlotId",
    body: "Slot key. Only one wearable can be equipped per key. (i.e., 'back' for backpacks or 'hat' for headwear)",
    items: ["Some common in-game used slot IDs: eye, hat, back, torso, torsofont, thigh, neck, arms, wrap, feet"],
  },
  wearableArmor: {
    title: "wearableArmor",
    body: "Armor contribution from a wearable. The game factors this into limb damage reduction."
  },
  wearableIsolation: {
    title: "wearableIsolation",
    body: "Insulation contribution from a wearable. The game uses this for body temperature behavior."
  },
  wearableHitDurabilityLossMultiplier: {
    title: "wearableHitDurabilityLossMultiplier",
    body: "Multiplier for durability loss when a wearable absorbs hits."
  },
  decayInfo: {
    title: "decayInfo",
    body: "Byte storing ItemInfo.DecayType bit flags. Combine flags with |, then cast to byte."
  },
  DecayType: {
    title: "DecayType",
    body: "Vanilla ItemInfo enum with bit flags: NoDecayWithoutContainerItem, NoDecayWhenNotWorn, and NoDecayWhenStill."
  },
  useAction: {
    title: "useAction",
    body: "Delegate called when a usable item is used. For instance, foods granting hunger/thirst/happiness and consuming condition."
  },
  useLimbAction: {
    title: "useLimbAction",
    body: "Delegate called when a usable-on-limb item is used on a Limb. It receives the targeted limb and the item being used."
  },
  DoAmputate: {
    title: "DoAmputate",
    body: "CUCoreLib helper that forwards to the vanilla amputation interaction. Use it in useLimbAction when your item should trigger the game's built-in amputation minigame and eligibility rules."
  },
  Limb: {
    title: "Limb",
    body: "A body part object. Limb-targeted item logic can inspect or modify limb.body, wounds, temperature, or limb components."
  },
  ChilledLimb: {
    title: "ChilledLimb",
    body: "Vanilla limb component used for a timed chilled effect. Add it only when the target limb should receive that effect."
  },
  skinHealth: {
    title: "skinHealth",
    body: "Limb skin health from 0 to 100. Lower values represent surface damage and can affect bleeding/infection behavior."
  },
  muscleHealth: {
    title: "muscleHealth",
    body: "Limb muscle health from 0 to 100. Lower values weaken the limb and can affect movement/force."
  },
  pain: {
    title: "pain",
    body: "Limb pain from 0 to 100. Pain contributes to body-wide pain and movement penalties. ow!"
  },
  sicknessAmount: {
    title: "sicknessAmount",
    body: "Body sickness value from 0 to 100."
  },
  SpawnFrequency: {
    title: "SpawnFrequency",
    body: "CUCoreLib custom item setting. 0 means craft-only, higher values allow loot pool injection. 5 -> 5x more spawns."
  },
  spawnFrequency: {
    title: "spawnFrequency",
    body: "CUCoreLib custom item setting. 0 means craft-only, higher values allow loot pool injection. 5 -> 5x more spawns."
  },
  specificId: {
    title: "specificId",
    body: "Exact (case sensitive) item or liquid ID required by a RecipeItem."
  },
  isLiquid: {
    title: "isLiquid",
    body: "Switches recipe ingredient/result behavior from item condition/count to liquid mL."
  },
  destroyItem: {
    title: "destroyItem",
    body: "When false, the ingredient will not be destroyed at 0% condition. Good for liquid containers."
  },
  CraftingQuality: {
    title: "CraftingQuality",
    body: "Vanilla quality matcher used for flexible ingredients/tags like heatsource, cutting, hammering, produce, or water."
  },
  resultCondition: {
    title: "resultCondition",
    body: "For item results, output condition from 0f to 1f (0% to 100%). For liquid results, output amount in mL (i.e. 100ml -> 100f)."
  },
  INT: {
    title: "INT",
    body: "Recipe intelligence requirement. Vanilla crafting can penalize or fail recipes when the player is below this requirement."
  },
  Recognition: {
    title: "Recognition",
    body: "Item recognition/identification data. Player INT must be above this level to see the item name and stats instead of 'Unknown Item'"
  },
  rec: {
    title: "rec",
    body: "ItemInfo recognition field. Use rec = new Recognition(level) to set data. Defaults to 2."
  },
  amount: {
    title: "amount",
    body: "Number of result items to create."
  },
  LoadEmbeddedSprite: {
    title: "LoadEmbeddedSprite",
    body: "Loads a PNG embedded into the calling assembly. Use this for assets that should ship inside the mod DLL."
  },
  LoadEmbeddedAudio: {
    title: "LoadEmbeddedAudio",
    body: "Loads an embedded audio file from the calling assembly by resource-name suffix. Use it for shipped hit sounds, loops, or other required clips."
  },
  EmbeddedResource: {
    title: "EmbeddedResource",
    body: "MSBuild item/build action that packs a file into your DLL. Use it for required sprites, JSON, text files, or default assets."
  },
  Build: {
    title: "Build Action",
    body: "Visual Studio file property that controls how the project handles the file during build. Use Embedded Resource when packing an asset into the DLL."
  },
  Resource: {
    title: "Resource names",
    body: "Embedded resource names usually include default namespace, folder, and filename. CUCoreLib resolves by suffix when that suffix is unique."
  },
  LoadSpriteFromPluginFolder: {
    title: "LoadSpriteFromPluginFolder",
    body: "Loads a loose PNG from a path relative to the plugin DLL. Use this for user-editable art or optional asset packs."
  },
  LoadAudioFromPluginFolder: {
    title: "LoadAudioFromPluginFolder",
    body: "Loads a loose audio file from a path relative to the plugin DLL. Use this for sound packs or user-replaceable clips."
  },
  LoadUISprite: {
    title: "LoadUISprite",
    body: "Loads an embedded sprite with UI pixels-per-unit defaults."
  },
  CacheSprite: {
    title: "CacheSprite",
    body: "Stores a sprite under an ID so CUCoreLib item/recipe paths can reuse it."
  },
  CacheAudioClip: {
    title: "CacheAudioClip",
    body: "Stores an AudioClip under an ID so other systems can resolve the same clip later without reloading it."
  },
  GetCachedAudioClip: {
    title: "GetCachedAudioClip",
    body: "Returns an AudioClip previously stored with CacheAudioClip, or null if that ID was never cached."
  },
  DelayCall: {
    title: "DelayCall",
    body: "Runs an action after a realtime delay using CUCoreLib's hidden coroutine runner."
  },
  CallWhen: {
    title: "CallWhen",
    body: "Polls a condition and runs an action once that condition becomes true."
  },
  AwaitWorldGeneration: {
    title: "AwaitWorldGeneration",
    body: "Coroutine that waits until WorldGeneration.world exists and active generation is finished."
  },
  GetFriendlyKeyName: {
    title: "GetFriendlyKeyName",
    body: "Returns a player-facing label for common KeyCode values. Also adds support for rebinding keys."
  }
};
