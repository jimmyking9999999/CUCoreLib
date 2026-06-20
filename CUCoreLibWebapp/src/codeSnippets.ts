import type { Ingredient, ItemState, PageId, RecipeState } from "./types";
import { pages } from "./docsPages";

let currentPage: PageId;
let itemState: ItemState;
let recipeState: RecipeState;
let ingredients: Ingredient[];

function escapeCsharp(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, "\\\"");
}

function floatLiteral(value: string | number): string {
  const number = Number(value);
  const clean = Number.isFinite(number) ? String(Number(number.toFixed(4))) : "0";
  return `${clean}f`;
}

function intLiteral(value: string): string {
  const number = Number.parseInt(value, 10);
  return Number.isFinite(number) ? String(number) : "0";
}

function csharpIdentifier(value: string, fallback: string): string {
  const cleaned = (value || fallback).replace(/[^A-Za-z0-9_]/g, "_").replace(/^[^A-Za-z_]+/, "");
  return cleaned || fallback;
}

function setupCode(): string {
  return `using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
// Used for CUCoreLib features. Remove if you don't use any of said features :)
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using CUCoreLib.Data;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;

namespace ModNamespace
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        // Change the following to something unique to your mod. GUIDs should be in reverse domain format (e.g. com.yourName.modName), otherwise may break.
        public const string ModGUID = "com.yourName.modName";
        public const string ModName = "My First Casualties: Unknown Mod";
        public const string ModVersion = "1.0.0";

        // This logger allows us to see messages from our mod in the BepInEx console and log files
        internal static new ManualLogSource Logger;
        private readonly Harmony _harmony = new(ModGUID);
        public static Plugin Instance { get; private set; } = null!;

        public void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            _harmony.PatchAll();
            Logger.LogInfo($"Plugin ModName is loaded!");

            DoStuff();
        }

        private void DoStuff()
        {
          // Sky's the limit! Create items, methods, classes (other files), patches, or whatever you want here.
        }
    }
}`;
}

function itemCode(): string {
  const id = itemState.id.trim() || "myitem";
  const spriteVariable = `${csharpIdentifier(id, "item")}Sprite`;
  const eat = Number(itemState.eat);
  const drink = Number(itemState.drink);
  const happiness = Number(itemState.happiness);
  const sickness = Number(itemState.sickness);
  const limbSkinHealth = Number(itemState.limbSkinHealth);
  const limbMuscleHealth = Number(itemState.limbMuscleHealth);
  const limbPain = Number(itemState.limbPain);
  const limbTemperature = Number(itemState.limbTemperature);
  const limbChillSeconds = Number(itemState.limbChillSeconds);
  const useLines: string[] = [];
  const limbLines: string[] = [];

  if (eat !== 0) useLines.push(`            body.Eat(${floatLiteral(eat)}, 0.5f);`);
  if (drink !== 0) useLines.push(`            body.Drink(${floatLiteral(drink)});`);
  if (happiness !== 0) useLines.push(`            body.happiness += ${floatLiteral(happiness)};`);
  if (sickness !== 0) useLines.push(`            body.sicknessAmount = Mathf.Clamp(body.sicknessAmount + ${floatLiteral(sickness)}, 0f, 100f);`);
  useLines.push("            item.condition -= 1f;");
  if (itemState.sound) useLines.push(`            Sound.Play("${escapeCsharp(itemState.sound)}", body.transform.position);`);

  const useAction = itemState.usable
    ? `,
       useAction = (body, item) =>
       {
${useLines.join("\n")}
        }`
    : "";
  if (limbSkinHealth !== 0) {
    limbLines.push(`            limb.skinHealth = Mathf.Clamp(limb.skinHealth + ${floatLiteral(limbSkinHealth)}, 0f, 100f);`);
  }
  if (limbMuscleHealth !== 0) {
    limbLines.push(`            limb.muscleHealth = Mathf.Clamp(limb.muscleHealth + ${floatLiteral(limbMuscleHealth)}, 0f, 100f);`);
  }
  if (limbPain !== 0) {
    limbLines.push(`            limb.pain = Mathf.Clamp(limb.pain + ${floatLiteral(limbPain)}, 0f, 100f);`);
  }
  if (limbTemperature !== 0) {
    limbLines.push(`            limb.body.temperature += ${floatLiteral(limbTemperature)};`);
  }
  if (limbChillSeconds > 0) {
    limbLines.push(`            ChilledLimb chilled = limb.gameObject.GetComponent<ChilledLimb>() ?? limb.gameObject.AddComponent<ChilledLimb>();`);
    limbLines.push(`            chilled.timeLeft = ${floatLiteral(limbChillSeconds)};`);
    limbLines.push(`            chilled.maxTime = ${floatLiteral(limbChillSeconds)};`);
  }
  const useLimbAction = itemState.usableOnLimb
    ? `,
    usableOnLimb = true,
    useLimbAction = (limb, item) =>
    {
        if (item.condition < 0.9f) return;

        item.condition -= 1f;
${limbLines.join("\n")}
    }`
    : "";
  const spawnFrequency = intLiteral(itemState.spawnFrequency);
  const spawnFrequencyArgument = spawnFrequency === "1" ? "" : `, ${spawnFrequency}`;
  const recognition = intLiteral(itemState.recognition);
  const recognitionLine = `,
    rec = new Recognition(${recognition})`;

  return `Sprite ${spriteVariable} = AssetLoader.LoadEmbeddedSprite("${escapeCsharp(itemState.sprite || `${id}.png`)}");

ItemRegistry.Register("${escapeCsharp(id)}", new ItemInfo
{
    fullName = "${escapeCsharp(itemState.name || "My Item")}",
    description = "${escapeCsharp(itemState.description)}",
    category = "${escapeCsharp(itemState.category)}",
    weight = ${floatLiteral(itemState.weight)},
    value = ${intLiteral(itemState.value)},
    usable = ${itemState.usable ? "true" : "false"},
    decayMinutes = ${floatLiteral(itemState.decayMinutes)},
    tags = "${escapeCsharp(itemState.tags)}"${recognitionLine}${useAction}${useLimbAction}
}, ${spriteVariable}${spawnFrequencyArgument});`;
}

function ingredientCode(ingredient: Ingredient): string {
  const amount = floatLiteral(ingredient.amount);
  const props: string[] = [];

  if (ingredient.mode === "specific") {
    props.push("specific = true");
    props.push(`specificId = "${escapeCsharp(ingredient.id)}"`);
  } else {
    props.push(`quality = new CraftingQuality("${escapeCsharp(ingredient.id)}", ${amount})`);
  }

  if (ingredient.isLiquid) props.push("isLiquid = true");
  if (!ingredient.destroyItem) props.push("destroyItem = false");

  return `new RecipeItem(${amount}) { ${props.join(", ")} }`;
}

function recipeCode(): string {
  const ingredientLines = ingredients.map((ingredient) => `        ${ingredientCode(ingredient)}`).join(",\n");

  return `RecipeRegistry.Register(new Recipe
{
    INT = ${intLiteral(recipeState.intRequirement)},
    category = Recipes.RecipeCategory.${recipeState.category},
    result = new RecipeResult
    {
        id = "${escapeCsharp(recipeState.resultId || "myitem")}",
        amount = ${intLiteral(recipeState.resultAmount)},
        isLiquid = ${recipeState.isLiquidResult ? "true" : "false"},
        resultCondition = ${floatLiteral(recipeState.resultCondition)}
    },
    items = new List<RecipeItem>
    {
${ingredientLines}
    }
});`;
}

function savingCode(): string {
  return `public static class MarkerManager
{
    // The data we are working with and need to save upon exit + continue
    public static readonly Dictionary<string, Vector2> Markers = new Dictionary<string, Vector2>();
    public static string ActiveMarkerId;
}

public sealed class TeleportMarkerSaveProvider : ICustomSaveProvider
{
    public int GetVersion()
    {
        // Bump this when you change the payload, e.g adding a new layer int.
        return 1;
    }

    // Call this when you want to save the game, e.g on <span class="inline-code">SaveSystem.SaveGame()</span>
    public JToken Capture()
    {
        JArray markers = new JArray();
        foreach (KeyValuePair<string, Vector2> entry in MarkerManager.Markers)
        {
            markers.Add(new JObject
            {
                ["id"] = entry.Key,
                ["x"] = entry.Value.x,
                ["y"] = entry.Value.y
            });
        }

        // Return JSON data representing the current state of whatever you want to save. This will be passed to Restore when loading.
        return new JObject
        {
            ["activeMarkerId"] = MarkerManager.ActiveMarkerId,
            ["markers"] = markers
        };
    }

    // Call then when you want to load the game from a save, e.g on <span class="inline-code">SaveSystem.TryLoadGame()</span>
    // There's a reason why I wanted to abstract this LMAO
    public void Restore(JToken payload, int version, SaveRestoreContext context)
    {
        JObject obj = payload as JObject;
        // Add a null check for obj, obj["markers"], and (string)token["id"] for best practice in your own mod

        MarkerManager.Markers.Clear();

        JArray markers = obj["markers"] as JArray;
        foreach (JToken token in markers)
        {
            // Here we restore the saved data, that being the markers dict
            string id = (string)token["id"];

            float x = (float?)token["x"] ?? 0f;
            float y = (float?)token["y"] ?? 0f;
            MarkerManager.Markers[id] = new Vector2(x, y);
        }

        MarkerManager.ActiveMarkerId = (string)obj["activeMarkerId"];
    }
}

// In order for saves to work, it MUST be registered once during plugin startup!
SaveRegistry.RegisterGlobalProvider("mymod.teleportMarkers", new TeleportMarkerSaveProvider());`;
}

function multiplayerCode(): string {
  return `using System;
using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Attached to the Kiln building entity prefab
public sealed class KilnController : MonoBehaviour
{
    private const string SetModeChannel = "glassworks.kiln.setmode";
    private const string ModeChangedChannel = "glassworks.kiln.modechanged";

    private string selectedMode = "idle";

    private void Awake()
    {
        // Server receives requested changes, validates them, then tells everyone the accepted value.
        MultiplayerApi.RegisterServerHandler(SetModeChannel, request =>
        {
            string requestedMode = request?.Value<string>("mode") ?? "idle";
            string acceptedMode = IsAllowedMode(requestedMode) ? requestedMode : "idle";

            ApplyMode(acceptedMode);

            MultiplayerApi.Broadcast(
                ModeChangedChannel,
                new JObject { ["mode"] = acceptedMode },
                includeHost: true
            );

            return new JObject
            {
                ["ok"] = true,
                ["mode"] = acceptedMode
            };
        });

        // Clients receive the accepted value and update their local machine script.
        MultiplayerApi.RegisterClientHandler(ModeChangedChannel, payload =>
        {
            ApplyMode(payload?.Value<string>("mode") ?? "idle");
        });

        // If data needs to be constantly updated, consider a timer in Update() every few seconds that sends/requests the current value from the server, or broadcasting on change.
    }

    public void Button_SetMode(string requestedMode)
    {
        if (!MultiplayerApi.IsAvailable)
        {
            ApplyMode(requestedMode);
            return;
        }

        if (MultiplayerApi.IsServer)
        {
            ApplyMode(requestedMode);
            MultiplayerApi.Broadcast(
                ModeChangedChannel,
                new JObject { ["mode"] = selectedMode },
                includeHost: false
            );
            return;
        }

        // The client asks, and the server then responds :)
        MultiplayerApi.RequestServer(
            SetModeChannel,
            new JObject { ["mode"] = requestedMode },
            response =>
            {
                bool ok = response?.Value<bool?>("ok") ?? false;
                string acceptedMode = response?.Value<string>("mode") ?? "idle";

                Debug.Log(ok
                    ? "Kiln mode accepted: " + acceptedMode
                    : "Kiln mode rejected.");
            }
        );
    }

    private void ApplyMode(string mode)
    {
        selectedMode = IsAllowedMode(mode) ? mode : "idle";

        // Update your real machine behavior here:
        // sprite, loop audio, temperature target, some internal data, etc.
        Debug.Log("Kiln mode is now " + selectedMode);
    }

    private static bool IsAllowedMode(string mode)
    {
        return string.Equals(mode, "idle", StringComparison.Ordinal) ||
            string.Equals(mode, "anneal", StringComparison.Ordinal) ||
            string.Equals(mode, "melt", StringComparison.Ordinal);
    }
}`;
}

export function currentCode(nextPage: PageId, nextItemState: ItemState, nextRecipeState: RecipeState, nextIngredients: Ingredient[]): string {
  currentPage = nextPage;
  itemState = nextItemState;
  recipeState = nextRecipeState;
  ingredients = nextIngredients;

  if (currentPage === "welcome") return welcomeCode();
  if (currentPage === "unity-csharp") return unityCsharpCode();
  if (currentPage === "setup") return setupCode();
  if (currentPage === "harmony0") return harmony0Code();
  if (currentPage === "recipe") return recipeCode();
  if (currentPage === "assets") return assetCode();
  if (currentPage === "audio") return audioCode();
  if (currentPage === "console") return consoleCode();
  if (currentPage === "utils") return utilsCode();
  if (currentPage === "tools") return toolsCode();
  if (currentPage === "item") return itemCode();
  if (currentPage === "advanced-item") return advancedItemCode();
  if (currentPage === "liquids") return liquidCode();
  if (currentPage === "player") return playerCode();
  if (currentPage === "statuses") return statusesCode();
  if (currentPage === "moodles") return moodlesCode();
  if (currentPage === "building-entities") return buildingEntityCode();
  if (currentPage === "advanced-building-entities") return advancedBuildingEntityCode();
  if (currentPage === "minigames") return minigameCode();
  if (currentPage === "tiles") return tileCode();
  if (currentPage === "traps") return trapsCode();
  if (currentPage === "settings") return settingsCode();
  if (currentPage === "locale") return localeCode();
  if (currentPage === "saving") return savingCode();
  if (currentPage === "multi-mod-compatibility") return multiplayerCode();
  return placeholderCode();
}

export function codeTitle(currentPage: PageId): string {
  if (currentPage === "welcome") return "Plugin.cs";
  if (currentPage === "unity-csharp") return "MyFirstPlugin.cs";
  if (currentPage === "setup") return "Plugin.cs";
  if (currentPage === "harmony0") return "HarmonyPatches.cs";
  if (currentPage === "recipe") return "RegisterRecipes.cs";
  if (currentPage === "saving") return "MarkerSaveProvider.cs";
  if (currentPage === "assets") return "LoadAssets.cs";
  if (currentPage === "audio") return "LoadAudio.cs";
  if (currentPage === "console") return "RegisterConsole.cs";
  if (currentPage === "utils") return "UseCUCoreUtils.cs";
  if (currentPage === "tools") return "CustomItemTool.cs";
  if (currentPage === "item") return "RegisterItems.cs";
  if (currentPage === "advanced-item") return "RegisterAdvancedItems.cs";
  if (currentPage === "liquids") return "RegisterLiquids.cs";
  if (currentPage === "player") return "PlayerHelpers.cs";
  if (currentPage === "statuses") return "StatusesExample.cs";
  if (currentPage === "moodles") return "LeadPoisoningMoodles.cs";
  if (currentPage === "building-entities") return "RegisterBuildings.cs";
  if (currentPage === "advanced-building-entities") return "RegisterAdvancedBuildings.cs";
  if (currentPage === "minigames") return "CustomMinigame.cs";
  if (currentPage === "tiles") return "RegisterTiles.cs";
  if (currentPage === "traps") return "RegisterTraps.cs";
  if (currentPage === "settings") return "RegisterSettings.cs";
  if (currentPage === "locale") return "CAT.json";
  if (currentPage === "multi-mod-compatibility") return "MultiplayerSync.cs";
  return "ComingSoon.cs";
}

function localeCode(): string {
  return `{
  "item": {
    "glass": "Glass",
    "glassdsc": "A werkable peece ob glass.",
    "conicalFlask": "Conical flask",
    "conicalFlaskdsc": "A simple glass flask fer careful mixin'.",
    "cinderstalk": "Cinderstalk",
    "cinderstalkdsc": "A tough, fibrous stalk blackened by heat. Not tasty like cheezburger.",
    "charredfern": "Charred Fern",
    "charredferndsc": "Brittle leaves dat crumble eazily. Smells like ash.",
    "clothpatch": "Ruined yarn ball",
    "clothpatchdsc": "Why'd they have to ruin the yarn and make this?",
    "reliefinjector": "Relief injector",
    "reliefinjectordsc": "A smol injector prefilled wif relief cream.",
    "cinderstalkbag": "Cinderstalk Bag",
    "cinderstalkbagdsc": "A compact all-natural bag. Who says plants can't be stylish?",
    "portablelamp": "Portable lamp",
    "portablelampdsc": "A battery-fed lamp fer da fuzzy cave explorer.",
    "glassshiv": "Glass shiv",
    "glassshivdsc": "This cuts my fur too easily...",
    "fieldpack": "Field Pack",
    "fieldpackdsc": "A versatile pack fer carryin' all the inner workings of a cat!."
  },
  "building": {
    "glasskiln": "Glass kiln",
    "glasskilndsc": "A smol kiln fer careful glasswork.",
    "glassworkscentrifuge": "Centrifuge",
    "glassworkscentrifugedsc": "I don't like the noise it makes..."
  },
  "moodle": {},
  "other": {
    "pineapplejuice": "Pineapple Juice",
    "pineapplejuicedsc": "Yoo've never seen this liquid before. A light yellow drink dat smells fruity. Yum yum!",
    "gamesetglassworks.audio.clinkvolume": "Glass clink volume",
    "gamesetglassworks.audio.clinkvolumedsc": "Protects your furry ears from clitter-clatter noises.",
    "galena": "Galena"
  }
}`;
}

function buildingEntityCode(): string {
  return `using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterGlassBuildings()
{
    BuildingEntityRegistry.Register("glassworkscentrifuge", new CustomBuildingEntityDefinition
    {
        Name = "Centrifuge",
        Description = "A mechanical device capable of separating selected liquids from containers.",
        Sprite = AssetLoader.LoadEmbeddedSprite("Images.centrifuge.png", 8f),
        Health = 500f,
        Metallic = true,
        HitSoundReferenceId = "metal",
        Placement = BuildingPlacementType.Floor,
        SpawnMinPerChunk = 0.07f,
        SpawnMaxPerChunk = 0.07f,
        SurfaceOffset = 0.5f,
        Components = new[] { typeof(CentrifugeScript) },
        ItemsDropOnDestroy = new[]
        {
            BuildingEntityRegistry.AddDrop("scrapmetal", 1f, 0.8f, 1f),
            BuildingEntityRegistry.AddDrop("conicalFlask", 0.8f, 0f, 0f)
        }
    });
}

public sealed class CentrifugeScript : MonoBehaviour
{
    private UsableObject usable;

    private void Start()
    {
        usable = gameObject.AddComponent<UsableObject>();
        usable.didLangString = true;
        usable.toggleString = "Separate held liquid";
    }

    public void OnUse()
    {
        Item heldItem = PlayerCamera.main.body.GetItem(PlayerCamera.main.body.handSlot);
        if (heldItem == null || !heldItem.TryGetComponent(out WaterContainerItem container))
        {
            PlayerCamera.main.DoAlert("Hold a liquid container first.");
            return;
        }

        // Start a coroutine here to drain a supported liquid, add its
        // separated result, and eject the container when processing ends.
}
}`;
}

function advancedBuildingEntityCode(): string {
  return `using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterAdvancedBuildings()
{
    BuildingEntityRegistry.Register("moddedlootcrate", new CustomBuildingEntityDefinition
    {
        Name = "Modded Loot Crate",
        Description = "What'll it be this time?",
        Sprite = AssetLoader.LoadEmbeddedSprite("Images.modded-loot-crate.png", 8f),
        Health = 250f,
        Metallic = true,
        HitSoundReferenceId = "metal",
        Placement = BuildingPlacementType.Floor,
        GenerationStyle = BuildingGenerationStyle.DropPod, // Removes tiles around it when spawning
        SpawnMinPerChunk = 0.01f,
        SpawnMaxPerChunk = 0.015f,
        SurfaceOffset = 0.4f,
        Components = new[] { typeof(ModdedLootCrateScript) }
    });
}

public sealed class ModdedLootCrateScript : MonoBehaviour
{
    private BuildingEntity building;
    private UsableObject usable;
    private readonly List<string> crateItems = new List<string>();

    private void Awake()
    {
        building = GetComponent<BuildingEntity>();
        usable = gameObject.AddComponent<UsableObject>();
        usable.toggleString = "Open crate";
        usable.didLangString = true;
    }

    private void Start()
    {
        crateItems.Clear();

        foreach (string itemId in ItemRegistry.GetRegisteredItemIds())
        {
            if (CUCoreUtils.IsModdedItem(itemId))
            {
                crateItems.Add(itemId);
            }
        }
            
        string rewardId = crateItems[Random.Range(0, crateItems.Count)];
        CustomInstantiate.InstantiateReturn(rewardId, transform.position + Vector3.up * 0.25f, transform.rotation, 1f);
    }

    public void OnUse()
    {
        if (MinigameBase.main.currentMinigame != null)
        {
            return;
        }

        building.description = "A crate packed with only modded goods.";
        usable.didLangString = true;
    }
}`;
}

function minigameCode(): string {
  return `using System.Collections.Generic;
using CUCoreLib.Helpers;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WireSpliceMinigame : CUCoreMinigameDefinition
{
    public override Minigame.HandSpriteType HandType(CUCoreMinigameSession session)
    {
        return Minigame.HandSpriteType.Tweezers;
    }

    public override bool NeedsItem(CUCoreMinigameSession session)
    {
        return false;
    }

    public override string GuideLocaleKey(CUCoreMinigameSession session)
    {
        return "wireSpliceGuide";
    }

    public override void Start(CUCoreMinigameSession session)
    {
        session.TryCreateScreen("Special/WireSpliceMinigame");
    }

    public override void Update(CUCoreMinigameSession session, List<RaycastResult> uiCasts)
    {
        if (Input.GetKeyDown(KeyCode.Escape) && CanExit())
        {
            session.End();
        }
    }
}

public static class WireSpliceStarter
{
    public static bool Start()
    {
        return CUCoreMinigames.TryStartDefinition(() => new WireSpliceMinigame());
    }
}`;
}

function tileCode(): string {
  return `using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private const ushort GalenaTileIndex = 36;
private const ushort AuricTileIndex = 37;
private const string LeadToxicityKey = "leadToxicity";

private void RegisterTiles()
{
    ItemRegistry.Register("auricfragment", new ItemInfo // Item drop from the tile
    {
        fullName = "Auric Fragment",
        description = "A fragment broken from a rare Auric ore vein. <size=8>It shocks you to the touch.</size>",
        category = "nospawn",
        weight = 0.4f,
        value = 45
    }, AssetLoader.LoadEmbeddedSprite("Images.auricfragment.png", 8f));

    TileRegistry.Register(AuricTileIndex, new CustomTileDefinition
    {
        ID = "auric",
        Name = "Auric",
        Sprite = AssetLoader.LoadEmbeddedSprite("Images.auric.png", 8f),
        Health = 777f,
        HitSound = "crystal",
        StepSound = "Rock",
        SleepQuality = Body.SleepQuality.Bad,
        Metallic = true,
        SpawnAmount = 2f,
        SpawnLayers = TileRegistry.LayersToMask(4, 5, 6, 7, 8, 9, 10),
        GenerationStyle = TileGenerationStyle.HeavyVeins | TileGenerationStyle.Inner,
        Drops = new[]
        {
            BuildingEntityRegistry.AddDrop("auricfragment", 1f, 0.5f, 1f)
        }
    });
    
    TileRegistry.Register(GalenaTileIndex, new CustomTileDefinition
    {
    ID = "galena",
    Name = "Galena",
    // Technically a description field can be set, but tiles will never use the description in-game
    Sprite = AssetLoader.LoadEmbeddedSprite("Images.galena.png", 8f),
    Health = 300f,
    HitSound = "rock",
    StepSound = "Gravel",
    SleepQuality = Body.SleepQuality.Mediocre,
        Metallic = false,
        SpawnAmount = 0.5f,
        SpawnLayers = TileRegistry.AllLayersExcept(1, 3),
        GenerationStyle = TileGenerationStyle.Vein | TileGenerationStyle.Outskirt,
        CustomData = new Dictionary<string, object>
        {
            [LeadToxicityKey] = 10f
        }
    });
}

[HarmonyPatch(typeof(Body), "Update")]
private static class GalenaLeadToxicityPatch
{
    private static void Postfix(Body __instance)
    {

        ushort block = WorldGeneration.world.GetBlock(__instance.transform.position);
        if (!TileRegistry.TryGetCustomData<float>(block, LeadToxicityKey, out float leadToxicity)) return;

        __instance.sicknessAmount += leadToxicity * Time.deltaTime * 0.1f;
    }
}`;
}

function trapsCode(): string {
  return `using System.Collections;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterTraps()
{
    BuildingEntityRegistry.Register("SporeMine", new CustomBuildingEntityDefinition
    {
        Name = "Spore Mine",
        Description = "A mine that releases spores when triggered.",
        Sprite = AssetLoader.LoadEmbeddedSprite("Images.SporeMine.png"),
        Health = 250f,
        Placement = BuildingPlacementType.Floor,
        SpawnMinPerChunk = 0.5f,
        SpawnMaxPerChunk = 0.6f,
        SurfaceOffset = 0.1f,
        RandomFlip = false,
        Metallic = false,
        HitSoundReferenceId = "rubber",
        Components = new[] { typeof(SporeMineScript) },
        ItemsDropOnDestroy = new[]
        {
            BuildingEntityRegistry.AddDrop("scrappanel", 0.5f, 1f, 1f),
            BuildingEntityRegistry.AddDrop("nails", 0.25f, 1f, 1f),
            BuildingEntityRegistry.AddDrop("processedcopper", 0.2f, 1f, 1f)
        }
    });
}

public class SporeMineScript : MonoBehaviour
{
    public float triggerDistance = 4f;
    public float detonateDelay = 0.2f;
    public float effectRadius = 7f;
    public float gasDuration = 4f;
    public int totalParticles = 100;

    private bool triggered = false;
    private bool exploding = false;
    private float timer = 0f;
    private BuildingEntity build;

    void Start()
    {
        build = GetComponent<BuildingEntity>();
    }

    void Update()
    {
        if (exploding) return;

        if (build.health <= 0)
        {
            StartCoroutine(GasCloudRoutine());
            return;
        }

        if (!triggered)
        {
            if (Vector2.Distance(transform.position, PlayerCamera.main.body.transform.position) < triggerDistance)
            {
                triggered = true;
                Sound.Play("mine", transform.position);
            }
        }
        else
        {
            timer += Time.deltaTime;

            if (timer >= detonateDelay)
            {
                StartCoroutine(GasCloudRoutine());
            }
        }
    }

    IEnumerator GasCloudRoutine()
    {
        exploding = true;

        build.enabled = false;
        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;

        Sound.Play("gasleak", transform.position);

        GameObject gasCloudPrefab = Resources.Load<GameObject>("DustBig");
        float startTime = Time.time;
        float particleTimer = 0f;
        float timeBetweenParticles = gasDuration / totalParticles;

        while (Time.time < startTime + gasDuration)
        {
            particleTimer += Time.deltaTime;
            while (particleTimer >= timeBetweenParticles)
            {
                SpawnSingleParticle(gasCloudPrefab);
                particleTimer -= timeBetweenParticles;
            }

            ApplyAreaEffect();

            yield return null;
        }

        Destroy(gameObject);
    }

    void SpawnSingleParticle(GameObject prefab)
    {
        Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 12f);
        GameObject vfx = Instantiate(prefab, spawnPos, Quaternion.identity);

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.2f, 0.8f, 0.2f));
        main.startLifetime = 2f;
        main.startSize = 3f;

        foreach (var childPs in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            var childMain = childPs.main;
            childMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.2f, 0.8f, 0.2f));
        }
    }

    void ApplyAreaEffect()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, effectRadius);

        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent<Body>(out Body body))
            {
                float dt = Time.deltaTime;

                body.sicknessAmount += 8f * dt;
                body.happiness -= 5f * dt;

                var data = body.GetAdditionalData();
                data.spice += 20f * dt;
                data.overstim += 15f * dt;

                if (Random.value < 0.01f)
                {
                    body.talker.Talk(Locale.GetCharacter("cantBreathe"));
                }
            }
        }
    }
}`;
}

function settingsCode(): string {
    return `using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterGlassworksSettings()
{
    ModOptionsRegistry.Register(ModOptionDefinition.Bool(
        "glassworks.game.enabled",
        "Enable Glassworks",
        "Controls whether Glassworks content is active.",
        Setting.SettingCategory.Game,
        PlayerPrefs.GetInt("Glassworks_Enabled", 1) == 1,
        value =>
        {
            PlayerPrefs.SetInt("Glassworks_Enabled", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    ));

    ModOptionsRegistry.Register(ModOptionDefinition.Bool(
        "glassworks.furnace.enabled",
        "Enable kiln sparks",
        "Controls extra kiln ambience and helpers.",
        "Glassworks",
        PlayerPrefs.GetInt("Glassworks_KilnSparks", 1) == 1,
        value =>
        {
            PlayerPrefs.SetInt("Glassworks_KilnSparks", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    ));

    ModOptionsRegistry.Register(ModOptionDefinition.Float(
        "glassworks.audio.clinkvolume",
        "Glass clink volume",
        "Scales Glassworks custom sound effects.",
        Setting.SettingCategory.Audio,
        PlayerPrefs.GetFloat("Glassworks_ClinkVolume", 0.8f),
        0f,
        1f,
        value =>
        {
            PlayerPrefs.SetFloat("Glassworks_ClinkVolume", value);
            PlayerPrefs.Save();
        },
        value => Mathf.RoundToInt(value * 100f) + "%"
    ));

    ModOptionsRegistry.Register(ModOptionDefinition.Int(
        "glassworks.game.spawnweight",
        "Glass loot weight",
        "Controls how strongly Glassworks items are added to loot.",
        Setting.SettingCategory.Game,
        PlayerPrefs.GetInt("Glassworks_SpawnWeight", 1),
        0,
        10,
        value =>
        {
            PlayerPrefs.SetInt("Glassworks_SpawnWeight", value);
            PlayerPrefs.Save();
        }
    ));

    ModOptionsRegistry.Register(ModOptionDefinition.Dropdown(
        "glassworks.game.recipevisibility",
        "Recipe visibility",
        "Controls how Glassworks recipes should be exposed.",
        Setting.SettingCategory.Game,
        PlayerPrefs.GetInt("Glassworks_RecipeVisibility", 0),
        new[]
        {
            new ModDropdownChoice("normal", "Normal"),
            new ModDropdownChoice("always", "Always visible"),
            new ModDropdownChoice("hidden", "Hidden")
        },
        value =>
        {
            PlayerPrefs.SetInt("Glassworks_RecipeVisibility", value);
            PlayerPrefs.Save();
        }
    ));

    ModOptionsRegistry.Register(ModOptionDefinition.Keybind(
        "glassworks.input.quickinspect",
        "Quick inspect",
        "Opens the Glassworks inspect helper.",
        Setting.SettingCategory.Input,
        KeyCode.G,
        value =>
        {
            PlayerPrefs.SetInt("Glassworks_QuickInspect", (int)value);
            PlayerPrefs.Save();
        }
    ));
}`;
}

function welcomeCode(): string {
return `// Hey! This side panel will display code relevant to the current page.

// Underscored lines may be highlighted. Hover over said lines for information and context

// Try it! 
private void Awake()
{
    Logger.LogInfo("Hello, world!");
}

// Some pages may contain user inputs, which will dynamically update the generated code as you type. The code is meant to be copied into your mod project and adapted from there.
`;
}

function harmony0Code(): string {
  return `using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Body), "Eat")]
public static class BodyEatPatch
{
    [HarmonyPrefix]
    private static void Prefix(Body __instance, ref float hungerAmount)
    {
        // Runs before Body.Eat.
        // ref lets this patch change the argument vanilla receives.
        hungerAmount = Mathf.Max(0f, hungerAmount);
    }

    [HarmonyPostfix]
    private static void Postfix(Body __instance)
    {
        // Runs after Body.Eat.
        // __instance is the Body object that just ate.
        Plugin.Logger.LogInfo($"Hunger after eating: {__instance.hunger}");
    }
}

[HarmonyPatch(typeof(Item), "get_fullName")]
public static class ItemDisplayNamePatch
{
    [HarmonyPostfix]
    private static void Postfix(Item __instance, ref string __result)
    {
        // __result is the return value.
        // ref lets a postfix replace what callers receive.
        if (__instance.id == "sunpear")
        {
            __result += " (modded)";
        }
    }
}`;
}

function placeholderCode(): string {
  const page = pages.find((item) => item.id === currentPage);
  const label = page?.label ?? "This API";
  return `// ${label}
// This API page is planned, but the guide has not been written yet.
`;
}

function unityCsharpCode(): string {
  return `// Not sure what any of this is? Not to worry. Skip past this page for now and come back to it after you read the rest of the docs. 
  
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CUCoreLib.Registries;

namespace MyFirstCasualtiesMod
{
    // ...

    public class SimpleUnityExample : MonoBehaviour
    {
        private UnityEngine.Object glassShard;
        private elapsed = 0f;

        private void Awake()
        {
            // Cache components you use often.
            glassShard = Resources.Load("glassshard");
        }

        private void Start()
        {
            // Start is a good place for setup that needs other objects to exist, as it's called first
            Debug.Log("Bring an umbrella!");
        }

        private void Update()
        {
            // Update runs every rendered frame.
            // Use Time.deltaTime for frame-rate-independent timers.
            elapsed += Time.deltaTime;

            if (elapsed > 1f)
            { 
                // It's raining glass shards! Ahhhh!

                // Spawn a glass shard every 1 second above the player
                UnityEngine.Object.Instantiate(glassShard, PlayerCamera.main.body.transform.position + Vector3.up * 3f, Quaternion.identity);
                
                // Reset the timer
                elapsed = 0f;
            }
        }
    }
}`;
}

function assetCode(): string {
  return `using CUCoreLib.Helpers;
using BepInEx;
using UnityEngine;

private void LoadModAssets()
{
    // Embedded assets live inside the DLL.
    Sprite embeddedIcon = AssetLoader.LoadEmbeddedSprite("Assets.sunpear.png");

    // Loose files live next to your plugin DLL.
    Sprite externalIcon = AssetLoader.LoadSpriteFromPluginFolder(this, "Assets/sunpear.png");

    // Cache shared sprites when multiple systems should resolve them by ID later.
    AssetLoader.CacheSprite("sunpear", embeddedIcon);
}`;
}

function audioCode(): string {
  return `using BepInEx;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private AudioClip centrifugeLoop;

private void LoadModAudio()
{
    // Embedded clips ship inside the DLL.
    AudioClip embeddedLoop = AssetLoader.LoadEmbeddedAudio("Audio.centrifuge-loop.wav");
    AudioClip embeddedHit = AssetLoader.LoadEmbeddedAudio("Audio.centrifuge-hit.wav");

    // Loose clips live next to your plugin DLL.
    AudioClip externalLoop = AssetLoader.LoadAudioFromPluginFolder(this, "Audio/cooler-loop.wav");

    // Cache shared clips when multiple systems should resolve them by ID later.
    AssetLoader.CacheAudioClip("glassworks.centrifuge.loop", embeddedLoop);
    AssetLoader.CacheAudioClip("glassworks.centrifuge.hit", embeddedHit);

    centrifugeLoop = AssetLoader.GetCachedAudioClip("glassworks.centrifuge.loop") ?? externalLoop;
}

private void RegisterAudioDrivenBuilding()
{
    BuildingEntityRegistry.Register("glassworkscentrifuge", new CustomBuildingEntityDefinition
    {
        Name = "Centrifuge",
        Sprite = AssetLoader.LoadEmbeddedSprite("Images.centrifuge.png", 8f),
        HitSound = AssetLoader.GetCachedAudioClip("glassworks.centrifuge.hit")
    });
}

private void AttachLoop(AudioSource source)
{
    source.clip = centrifugeLoop;
    source.loop = true;
    source.playOnAwake = false;
    source.Play();
}`;
}

function consoleCode(): string {
  return `using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterConsoleCommands()
{
    // ConsoleScript passes args split by spaces.
    // args[0] is the command name, args[1] is the first real argument.
    ConsoleCommandRegistry.Register(
        "whereami",
        "Prints the player's current world position.",
        args =>
        {
            CUCoreUtils.ConsoleCheckForWorld(ConsoleScript.instance);
            CUCoreUtils.ConsoleLog(
                ConsoleScript.instance,
                $"Player position: {PlayerCamera.main.body.transform.position}"
            );
        },
        null
    );

    ConsoleCommandRegistry.Register(
        "givesunpears",
        "I heard you liked Sunpears...",
        args =>
        {
            CUCoreUtils.ConsoleCheckForWorld(ConsoleScript.instance);

            Body body = PlayerCamera.main.body;
            Vector3 dropPosition = body.transform.position;

            // The first six slots are the main held/equipment-style slots.

            int pickupSlots = 6; // No need to check for amputations, but it's good to be thorough.
            for (int slot = 0; slot < pickupSlots; slot++)
            {
                body.DropItem(slot);
            }

            for (int index = 0; index < 100; index++)
            {
                GameObject obj = CustomInstantiate.InstantiateReturn(
                    "sunpear",
                    dropPosition + (Vector3)Random.insideUnitCircle * 0.8f,
                    Quaternion.identity,
                    1f
                );

                Item sunpear = obj.GetComponent<Item>();

                // Only six can be force-picked into the cleared slots. The rest are still spawned around the player.
                if (index < pickupSlots)
                {
                    body.PickUpItem(sunpear, index, force: true);
                }
            }

            CUCoreUtils.ConsoleLog(ConsoleScript.instance, "..so I put a sunpear in your sunpears, so that you can, uh.. sunpear while you sunpear?");
        },
        null
    );
}`;
}

function utilsCode(): string {
  return `using CUCoreLib.Helpers;
using UnityEngine;

private void Awake()
{
    // Run code after a short realtime delay.
    CUCoreUtils.DelayCall(1f, () => Logger.LogInfo("One second later."));

    // Wait for the runtime world before touching PlayerCamera or WorldGeneration.
    CUCoreUtils.StartCoroutine(WaitForWorld());

    // Store simple persistent toggles without repeating PlayerPrefs. Also allows for external .cfg files and in-game toggling,
    bool enabled = CUCoreUtils.GetBool("mymod.enabled", true);
    CUCoreUtils.SetBool("mymod.enabled", enabled);

    // Display keys with nicer labels in UI text, and handle custom keybind support. (This defaults to "Mouse2")
    string keyName = CUCoreUtils.GetFriendlyKeyName(KeyCode.Mouse2);
}

private System.Collections.IEnumerator WaitForWorld()
{
    yield return CUCoreUtils.AwaitWorldGeneration();

    // Safe point for world/player dependent work.
    Logger.LogInfo("World is ready!");
}`;
}

function toolsCode(): string {
  return `// Custom item tool page
//  working on it~
`;
}

function liquidCode(): string {
  return `using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterPineappleJuiceContent()
{
    // CUCoreLib automatically turns this into a "vanilla" LiquidType and adds locale entries
    LiquidRegistry.Register("pineapplejuice", new CustomLiquidInfo
    {
        name = "Pineapple Juice",
        description = "You've never seen this liquid before. A light yellow drink that smells fruity.",
        color = new Color(0.94f, 0.88f, 0.66f),
        valuePerLiter = 22f,
        healthUsable = true,
        injectionSickness = 0.4f,
        onDrink = (ml, body) =>
        {
            float liters = ml * 0.01f; // Convert ml to 100ml for more intuitive values. 100ml is a sip.
            body.Drink(liters * 9f); // 9 thirst / 100ml
            body.weightOffset += liters; // + ~0.3kg 
            body.temperature -= liters * 3f; // -3C 
            body.happiness += 2f * liters; // +2 happiness
            body.talker.EatGood();
        },
        onHealthUse = (ml, limb) =>
        {
            float dose = ml * 0.01f; // 100ml = 1 full opium syringe
            
            limb.body.happiness -= dose * 8f;
            limb.body.bloodVolume += dose * 0.5f; 
            limb.body.sicknessAmount += dose * 7f;
            limb.infected = true;
            limb.infectionAmount += dose * 10f;
            
        },
        qualities = new List<CraftingQuality>
        {
            new CraftingQuality("water", 0.5f)
        }
    });


    Sprite juiceIcon = AssetLoader.LoadEmbeddedSprite("Images.pineapplejuice.png");

    ItemRegistry.Register(
        "pineapplejuicebottle",
        new CustomItemInfo
        {
            fullName = "Pineapple juice bottle",
            description = "A bottle of pale yellow fruit juice.",
            category = "water",
            slotRotation = -45f,
            tags = "cangetwet",
            usable = true,
            usableOnLimb = false,
            destroyAtZeroCondition = false,
            combineable = true,
            weight = 1.25f,
            scaleWeightWithCondition = true,
            capacity = 500f,
            defaultContents = new List<LiquidStack>
            {
                new LiquidStack("pineapplejuice", 500f) 
            },
            useAction = (body, item) =>
            {
                item.GetComponent<WaterContainerItem>().Drink(body);
            },
            value = 2,
            rec = new Recognition(2),
            SpawnFrequency = 1
        },
        juiceIcon
    );

}`;
}

function advancedItemCode(): string {
  return `using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private void RegisterAdvancedItems()
{
    Sprite fieldPackIcon = AssetLoader.LoadEmbeddedSprite("Images.fieldpack.png");
    Sprite apronIcon = AssetLoader.LoadEmbeddedSprite("Images.rubberapron.png");
    Sprite apronWorn = AssetLoader.LoadEmbeddedSprite("Images.rubberapron_worn.png");
    Sprite portableLampIcon = AssetLoader.LoadEmbeddedSprite("Images.portablelamp.png");
    Sprite glassShivIcon = AssetLoader.LoadEmbeddedSprite("Images.glassshiv.png");

    ItemRegistry.Register(
        "fieldpack",
        new CustomItemInfo
        {
            fullName = "Field pack",
            description = "A compact shoulder bag with room for tools.",
            category = "tool",
            weight = 1.2f,
            value = 18,
            tags = "cangetwet",
            rec = new Recognition(4),
            Container = new ContainerProperties
            {
                Capacity = 14f,
                MaxWeightPerItem = 6f,
                EncumbranceReduction = 0.65f
            },
            SpawnFrequency = 1,
            CustomData =
            {
                ["lootTheme"] = "utility",
                ["allowedToolTier"] = 2
            }
        },
        fieldPackIcon
    );

    ItemRegistry.Register(
        "rubberapron",
        new CustomItemInfo
        {
            fullName = "Rubber apron",
            description = "Heavy protective wear for messy work.",
            category = "tool",
            wearable = true,
            wearableCanBeHeld = true,
            desiredWearLimb = "UpTorso",
            wearSlotId = "outertorso",
            wearableArmor = 0.18f,
            wearableIsolation = 0.08f,
            wearableHitDurabilityLossMultiplier = 0.7f,
            decayMinutes = 240f,
            decayInfo = (byte)(
                ItemInfo.DecayType.NoDecayWhenNotWorn |
                ItemInfo.DecayType.NoDecayWhenStill
            ),
            weight = 1.1f,
            value = 14,
            tags = "cangetwet",
            rec = new Recognition(4),
            WornSprite = apronWorn,
            WornSpriteOffset = new Vector2(0f, -0.04f),
            SpawnFrequency = 1
        },
        apronIcon
    );

    ItemRegistry.Register(
        "portablelamp",
        new CustomItemInfo
        {
            fullName = "Portable lamp",
            description = "A battery-fed lamp for careful cave work.",
            category = "utility",
            tags = "belttool",
            usable = true,
            decayMinutes = 180f,
            decayInfo = (byte)ItemInfo.DecayType.BatteryDecay,
            weight = 0.8f,
            value = 22,
            rec = new Recognition(5),
            Battery = new BatteryProperties
            {
                MaxCharge = 100f,
                StartCharge = 35f,
                Preset = BatteryItem.BatteryPreset.Medium,
                BatteryType = "mediumbattery"
            },
            Light = new LightProperties
            {
                Intensity = 0.75f,
                Color = Color.white,
                PointLightOuterRadius = 7.5f,
                PointLightInnerRadius = 0f,
                LightType = CustomLightType.Point
            },
            SpawnFrequency = 1
        },
        portableLampIcon
    );

    ItemRegistry.Register(
        "glassshiv",
        new CustomItemInfo
        {
            fullName = "Glass shiv",
            description = "A sharpened piece of glass wrapped in cloth.",
            category = "weapon",
            tags = "tool,cangetwet,cutting",
            weight = 0.25f,
            value = 4,
            rec = new Recognition(3),
            Tool = new ToolProperties
            {
                Damage = 22f,
                StructuralDamage = 8f,
                AttackCooldownMultiplier = 0.7f,
                Distance = 4.6f,
                KnockBack = 120f,
                Cooldown = 0.28f,
                StaminaUse = 0.35f,
                Piercing = true,
                ConditionLossOnHit = 0.08f
            },
            SpawnFrequency = 1
        },
        glassShivIcon
    );
}

private float GetCustomFloat(Item item, string key, float fallback)
{
    return ItemRegistry.TryGetCustomData<float>(item, key, out float value)
        ? value
        : fallback;
}`;
}

function playerCode(): string {
  return `using System.Collections;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using UnityEngine;

private IEnumerator LogPlayerStateWhenReady()
{
    yield return CUCoreUtils.AwaitWorldGeneration();

    Body body = PlayerCamera.main.body;
    Logger.LogInfo($"Player position: {body.transform.position}");
    Logger.LogInfo($"Hunger={body.hunger}, Thirst={body.thirst}, Stamina={body.stamina}");
    Logger.LogInfo($"Blood={body.bloodVolume}, Temp={body.temperature}, Happiness={body.happiness}");
}

private void RegisterPlayerDebugCommands()
{
    ConsoleCommandRegistry.Register(
        "playerstats",
        "Prints a few common Body fields.",
        args =>
        {
            CUCoreUtils.ConsoleCheckForWorld(ConsoleScript.instance);

            Body body = PlayerCamera.main.body;
            CUCoreUtils.ConsoleLog(
                ConsoleScript.instance,
                $"Hunger={body.hunger}, Thirst={body.thirst}, Stamina={body.stamina}, Blood={body.bloodVolume}"
            );
        },
        null
    );

    ConsoleCommandRegistry.Register(
        "playerpos",
        "Prints the player's position.",
        args =>
        {
            CUCoreUtils.ConsoleCheckForWorld(ConsoleScript.instance);
            CUCoreUtils.ConsoleLog(ConsoleScript.instance, $"Player position: {PlayerCamera.main.body.transform.position}");
        },
        null
    );
}`;
}

function statusesCode(): string {
  return `using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

[StatusOptions(Key = "com.yourname.sunstroke", SaveEnabled = true)]
public sealed class SunstrokeStatus : BodyStatus
{
    public float ExposureSeconds;
    public float CoolingGraceSeconds;
    public bool WarnedPlayer;
}

[HarmonyPatch(typeof(Body), "Update")]
public static class BodyUpdateStatusPatch
{
    private static void Postfix(Body __instance)
    {

        SunstrokeStatus status = __instance.GetStatus<SunstrokeStatus>();

        if (__instance.temperature > 39.2f)
        {
            status.ExposureSeconds += Time.deltaTime;
            status.CoolingGraceSeconds = 6f;
        }
        else if (status.CoolingGraceSeconds > 0f)
        {
            status.CoolingGraceSeconds -= Time.deltaTime;
        }
        else
        {
            status.ExposureSeconds = Mathf.Max(0f, status.ExposureSeconds - Time.deltaTime * 4f);
        }

        if (status.ExposureSeconds > 30f)
        {
            __instance.thirst = Mathf.Max(0f, __instance.thirst - Time.deltaTime * 1.5f);
            __instance.stamina = Mathf.Max(0f, __instance.stamina - Time.deltaTime * 2f);
        }

        if (status.ExposureSeconds > 50f)
        {
            __instance.sicknessAmount = Mathf.Clamp(__instance.sicknessAmount + Time.deltaTime * 3f, 0f, 100f);
        }

        if (!status.WarnedPlayer && status.ExposureSeconds > 45f)
        {
            status.WarnedPlayer = true;
            __instance.talker.Say("I need shade.");
        }
    }
}

[StatusOptions(Key = "com.yourname.temporaryHeatPulse", SaveEnabled = false)]
public sealed class TemporaryHeatPulseStatus : BodyStatus
{
    public float RemainingSeconds;
}

[StatusOptions(Key = "com.yourname.leadpoisoning", SaveEnabled = true)]
public sealed class RespiratoryWarningStatus : BodyStatus
{
    public float SlowBreathingSeconds;
}

[HarmonyPatch(typeof(Body), "Update")]
public static class RespiratoryWarningPatch
{
    private static void Postfix(Body __instance)
    {

        RespiratoryWarningStatus status = __instance.GetStatus<RespiratoryWarningStatus>();

        if (__instance.respiratoryRate < 50f)
        {
            status.SlowBreathingSeconds += Time.deltaTime;
        }
        else
        {
            status.SlowBreathingSeconds = 0f;
        }
    }
}`;
}

function moodlesCode(): string {
  return `using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Body), "Update")]
public static class LeadPoisoningPatch
{
    private static void Postfix(Body __instance)
    {
        LeadPoisoningStatus status = __instance.GetStatus<LeadPoisoningStatus>();

        if (status.touchingLeadTiles == true) // or some other custom mod flag
        {
            boolean HasOpenWounds = __instance.totalBleedSpeed > 0.3f;

            status.LeadPoisoning = Mathf.Clamp(status.LeadPoisoning + Time.deltaTime * 0.35f * HasOpenWounds ? 3f : 1f, 0f, 100f);  
        }

        if (status.LeadPoisoning > 18f)
        {
            MoodleRegistry.AddMoodle(
                2,
                AssetLoader.LoadEmbeddedSprite("Images.lead-moodle-1.png"),
                "Lead Poisoning",
                $"You're feeling a bit woozy and fatigued...",
                chippedOnly: true,
                important: true
            );
        }
    }
}`;
}
