import type { Ingredient, ItemState, Page, PageId, RecipeState } from "./types.ts";

export const pages: Page[] = [
  {
    id: "welcome",
    label: "Welcome",
    crumb: "CUCoreLib",
    title: "Welcome to CUCoreLib",
    lead: "A Casualties Unknown modding library for content registration, runtime helpers, and patch-safe integration points."
  },
  {
    id: "unity-csharp",
    label: "Modding TL;DR",
    crumb: "Getting Started",
    title: "A quick TL;DR on Unity, C#, Decompilation, and BepInEx",
    lead: "To create a BepInEx plugin, we must first create the universe..."
  },
  {
    id: "setup",
    label: "Setup",
    crumb: "Getting Started",
    title: "Install ScavTemplate",
    lead: "Create a template mod, point it at your local game install, and add CUCoreLib as a hard dependency."
  },
  {
    id: "harmony0",
    label: "Harmony0",
    crumb: "Getting Started",
    title: "Harmony0",
    lead: "What is this, music theory?"
  },
  {
    id: "tutorial-first-mod",
    label: "Making A First Mod",
    crumb: "Tutorial",
    title: "Making a first mod",
    lead: "Is this a subnautica reference?"
  },
  {
    id: "assets",
    label: "Loading Assets",
    crumb: "Content APIs",
    title: "Loading assets",
    lead: "Load embedded and loose sprites, audio clips, UI sprites, and text resources through CUCoreLib."
  },
  {
    id: "audio",
    label: "Audio",
    crumb: "Content APIs",
    title: "Loading audio",
    lead: "Load embedded and loose audio clips, reuse cached sounds, and wire them into buildings or AudioSource components."
  },
  {
    id: "item",
    label: "Item API",
    crumb: "Content APIs",
    title: "Create an item",
    lead: "Register an item with vanilla ItemInfo fields and a CUCoreLib sprite lookup. "
  },
  {
    id: "advanced-item",
    label: "Advanced Item API",
    crumb: "Items / Liquids",
    title: "Advanced Item API",
    lead: "Using CustomItemInfo"
  },
  {
    id: "custom-item-scripts",
    label: "Creating Custom Item Scripts",
    crumb: "Items / Liquids",
    title: "Creating Custom Item Scripts",
    lead: "Attach MonoBehaviours to custom items, read custom item metadata, and structure wearable runtime logic."
  },
  {
    id: "recipe",
    label: "Recipe API",
    crumb: "Content APIs",
    title: "Creating recipes",
    lead: "Wanna make a super secret item? Have it require 500 INT to craft, so that only blueprints can reveal it."
  },
  {
    id: "liquids",
    label: "Liquid API",
    crumb: "Items / Liquids",
    title: "Liquid API",
    lead: "Register liquid definitions, fill liquid containers, and understand WaterContainerItem behavior."
  },
  {
    id: "guns",
    label: "Guns",
    crumb: "Items / Liquids",
    title: "Guns",
    lead: "Planned documentation for firearm-style items."
  },
  {
    id: "player",
    label: "Player",
    crumb: "Player",
    title: "Player",
    lead: "Reference for the vanilla Body player fields you will usually read or modify from mods, plus a few CUCoreLib helpers for accessing them safely."
  },
  {
    id: "statuses",
    label: "Statuses",
    crumb: "Player",
    title: "Statuses",
    lead: "Reference for status effects, common status state, and the gameplay hooks mods usually read or modify."
  },
  {
    id: "limb-statuses",
    label: "Woundview",
    crumb: "Player",
    title: "Woundview",
    lead: "Planned documentation for limb-specific status effects."
  },
  {
    id: "moodles",
    label: "Moodles",
    crumb: "Player",
    title: "Moodles",
    lead: "Reference for moodle display, intensity tiers, and the player condition indicators mods commonly surface."
  },
  {
    id: "building-entities",
    label: "Building Entities",
    crumb: "World",
    title: "BuildingEntity API",
    lead: "Register sprite-backed world objects with placement rules, custom scripts, drops, and optional world generation."
  },
  {
    id: "advanced-building-entities",
    label: "Advanced Buildings",
    crumb: "World",
    title: "Advanced BuildingEntity API",
    lead: "Advanced building prefab hooks, worldgen details, loot crate patterns, and minigame-driven buildings."
  },
  {
    id: "minigames",
    label: "Minigames",
    crumb: "Misc / API",
    title: "Minigame API",
    lead: "Shared helpers for creating custom minigames on top of the game's vanilla Minigame base."
  },
  {
    id: "tiles",
    label: "Tiles",
    crumb: "World",
    title: "Tile API",
    lead: "Register terrain tiles with stable block indices, vanilla block behavior, and normal world or Tilemap placement."
  },
  {
    id: "traps",
    label: "Traps",
    crumb: "World",
    title: "Traps",
    lead: "Woah, custom layers and biomes?"
  },
  {
    id: "enemies",
    label: "Enemies",
    crumb: "World",
    title: "Enemies",
    lead: "Planned documentation for enemy registration or enemy-adjacent helpers."
  },
  {
    id: "multi-block-structures",
    label: "Multi-Block Structures",
    crumb: "World",
    title: "Multi-block structures",
    lead: "Planned documentation for structures made from multiple world blocks."
  },
  {
    id: "visuals",
    label: "Visuals",
    crumb: "Misc / API",
    title: "Visuals",
    lead: "Planned documentation for visual helpers and display-facing APIs."
  },
  {
    id: "shaders-vfx",
    label: "Shaders / VFX",
    crumb: "Misc / API",
    title: "Shaders and VFX",
    lead: "Planned documentation for shader and visual-effect workflows."
  },
  {
    id: "settings",
    label: "Settings API",
    crumb: "Misc / API",
    title: "Settings API",
    lead: "Register native settings rows in the vanilla options menu with CUCoreLib handling save, reset, apply, and labels."
  },
  {
    id: "locale",
    label: "Locale",
    crumb: "Misc / API",
    title: "Locale",
    lead: "Generate item, liquid, and manual locale keys for your mod."
  },
  {
    id: "saving",
    label: "Saving",
    crumb: "Misc / API",
    title: "Saving",
    lead: "Extend the vanilla save.sv flow with versioned CUCoreLib save providers."
  },
  {
    id: "animations",
    label: "Animations",
    crumb: "Misc / API",
    title: "Animations",
    lead: "Planned documentation for animation helpers and animation asset usage."
  },
  {
    id: "multi-mod-compatibility",
    label: "Multiplayer Sync",
    crumb: "Misc / API",
    title: "Multiplayer sync",
    lead: "Experimental early automatic multiplayer sync for CUCoreLib"
  },
  {
    id: "console",
    label: "Console and You",
    crumb: "Debug APIs",
    title: "The Console and You",
    lead: "...and the many commands for the few~. Register development commands that can be ran via the game's console."
  },
  {
    id: "debug-testing",
    label: "Hot Reload / Testing",
    crumb: "Debug APIs",
    title: "Debugging, testing, and hot reload",
    lead: "Are you tired of restarting the game to test your mod?"
  },
  {
    id: "utils",
    label: "CUCoreUtils",
    crumb: "Helper APIs",
    title: "CUCoreUtils",
      lead: "Small helper APIs for timing, readiness checks, PlayerPrefs accessors, world/item helpers, alerts, reflection, console bridging, keys, audio, and compression."
  },
  {
    id: "tools",
    label: "Tools",
    crumb: "Tools",
    title: "Custom item tools",
    lead: "A dedicated place for richer CUCoreLib item creation helpers."
  }
];


function escapeHtml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

let itemState: ItemState;
let recipeState: RecipeState;
let ingredients: Ingredient[];

export function pageBody(page: PageId, nextItemState: ItemState, nextRecipeState: RecipeState, nextIngredients: Ingredient[]): string {
  itemState = nextItemState;
  recipeState = nextRecipeState;
  ingredients = nextIngredients;
  let content: string;
  if (page === "welcome") content = welcomePage();
  else if (page === "unity-csharp") content = unityCsharpPage();
  else if (page === "setup") content = setupPage();
  else if (page === "harmony0") content = harmony0Page();
  else if (page === "tutorial-first-mod") content = tutorialFirstModPage();
  else if (page === "recipe") content = recipePage();
  else if (page === "assets") content = assetPage();
  else if (page === "audio") content = audioPage();
  else if (page === "console") content = consolePage();
  else if (page === "debug-testing") content = debugTestingPage();
  else if (page === "utils") content = utilsPage();
  else if (page === "tools") content = toolsPage();
  else if (page === "item") content = itemPage();
  else if (page === "advanced-item") content = advancedItemPage();
  else if (page === "custom-item-scripts") content = customItemScriptsPage();
  else if (page === "liquids") content = liquidsPage();
  else if (page === "player") content = playerPage();
  else if (page === "statuses") content = statusesPage();
  else if (page === "moodles") content = moodlesPage();
  else if (page === "building-entities") content = buildingEntitiesPage();
  else if (page === "advanced-building-entities") content = advancedBuildingEntitiesPage();
  else if (page === "minigames") content = minigamesPage();
  else if (page === "tiles") content = tilesPage();
  else if (page === "multi-block-structures") content = multiBlockStructuresPage();
  else if (page === "traps") content = trapsPage();
  else if (page === "locale") content = localePage();
  else if (page === "multi-mod-compatibility") content = multiplayerPage();
  else if (page === "settings") content = settingsPage();
  else if (page === "saving") content = savingPage();
  else content = placeholderPage(page);

  return normalizeMediaUrls(content);
}

function normalizeMediaUrls(value: string): string {
  return value
    .replace(/(src|href)=(")images\//g, '$1=$2/images/')
    .replace(/(src|href)=(")videos\//g, '$1=$2/videos/')
    .replace(/(poster)=(")images\//g, '$1=$2/images/');
}

const externalVideoUrls = {
  setup: "https://jimmyking9999999.github.io/Metadata-generator/videos/setup.mp4",
  embeddingImages: "https://jimmyking9999999.github.io/Metadata-generator/videos/embedding-images.mp4",
  hotReload: "https://jimmyking9999999.github.io/Metadata-generator/videos/hot-reload.mp4",
  sporeTrap: "https://jimmyking9999999.github.io/Metadata-generator/videos/spore-trap-ingame.mp4"
} as const;

function docsVideo(src: string, fallbackSrc: string, className: string): string {
  return `<video src="${src}" data-local-fallback-src="${fallbackSrc}" autoplay loop muted playsinline controls class="${className}"></video>`;
}

function buildingEntitiesPage(): string {
  return `
    <section class="lesson-card">
      <h2>Register a building prefab</h2>
      <p><span class="inline-code">BuildingEntityRegistry.Register</span> creates an inactive prefab for a custom <span class="inline-code">BuildingEntity</span>. The prefab can be spawned by <span class="inline-code">BuildingEntityRegistry.Spawn</span>, <span class="inline-code">Utils.Create</span>, console commands, save loading, or CUCoreLib world generation.</p>
      <p>Use this page for straightforward buildings: simple props, veins, floor objects, and other cases where you mainly need a sprite, health, placement, and drops.</p>
      <pre><code>BuildingEntityRegistry.Register("glassworkscentrifuge", new CustomBuildingEntityDefinition
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
    SpawnLayers = BuildingEntityRegistry.LayersToMask(3, 4, 5),
    SurfaceOffset = 0.5f,
    HeatRadius = 3f,
    HeatPerSecond = 1.2f,
    MaxHeatBodyTemperature = 37.6f,
    Components = new[] { typeof(CentrifugeScript) },
    ItemsDropOnDestroy = new[]
    {
        BuildingEntityRegistry.AddDrop("scrapmetal", 1f, 0.8f, 1f),
        BuildingEntityRegistry.AddDrop("conicalFlask", 0.8f, 0f, 0f)
    }
});</code></pre>
    <img src="images/centrifuge-ingame.png" alt="In-game screenshot of the centrifuge building entity, showing its sprite and health bar." class="screenshot">

    </section>

    <section class="lesson-card">
      <h2>Placement and world generation</h2>
      <p>Use <span class="inline-code">Placement</span> for the surface type. <span class="inline-code">GenerationStyle</span> stays on this page only because it is still part of the simple worldgen story: <span class="inline-code">None</span> means the building only appears when your mod spawns or places it directly.</p>
      <pre><code>BuildingEntityRegistry.Register("glassvein", new CustomBuildingEntityDefinition
{
    Name = "Glass vein",
    Description = "A brittle mineral seam in the wall.",
    Sprite = AssetLoader.LoadEmbeddedSprite("Images.glassvein.png"),
    Placement = BuildingPlacementType.Wall,
    SpawnMinPerChunk = 0.02f,
    SpawnMaxPerChunk = 0.03f,
    SpawnLayers = BuildingEntityRegistry.AllLayersExcept(1, 2),
    SurfaceOffset = 0.35f,
    RandomFlip = false,
    ItemsDropOnDestroy = new[]
    {
        BuildingEntityRegistry.AddDrop("glass", 1f, 0.4f, 1f)
    }
});</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Simple building fields</h2>
      <p>For most custom buildingEntities, you probably do not need anything beyond these fields.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Field</th>
              <th>Type</th>
              <th>What CUCoreLib uses it for</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">ID</span></td><td><span class="inline-code">string</span></td><td>Filled from the registration ID. You usually leave this unset unless collisions occur between mods.</td></tr>
            <tr><td><span class="inline-code">Name</span></td><td><span class="inline-code">string</span></td><td>Locale name registered as <span class="inline-code">building.ID</span>.</td></tr>
            <tr><td><span class="inline-code">Description</span></td><td><span class="inline-code">string</span></td><td>Locale description registered as <span class="inline-code">building.IDdsc</span>.</td></tr>
            <tr><td><span class="inline-code">Sprite</span></td><td><span class="inline-code">Sprite</span></td><td>Required artwork for the custom building prefab.</td></tr>
            <tr><td><span class="inline-code">SortingOrder</span></td><td><span class="inline-code">int</span></td><td>Sprite render order. Defaults to <span class="inline-code">5</span>. Higher in front.</td></tr>
            <tr><td><span class="inline-code">Scale</span></td><td><span class="inline-code">Vector3</span></td><td>Local scale applied to the prefab root. Defaults to <span class="inline-code">Vector3.one</span>.</td></tr>
            <tr><td><span class="inline-code">Layer</span></td><td><span class="inline-code">int?</span></td><td>Optional raw Unity layer override. Defaults to the render reference layer.</td></tr>
            <tr><td><span class="inline-code">LayerEnum</span></td><td><span class="inline-code">BuildingLayer?</span></td><td>Optional named layer helper for <span class="inline-code">Default</span>, <span class="inline-code">TransparentFix</span>, <span class="inline-code">IgnoreRaycast</span>, <span class="inline-code">Layer3</span>, <span class="inline-code">Water</span>, <span class="inline-code">UI</span>, and <span class="inline-code">Ground</span>. Used only when <span class="inline-code">Layer</span> is unset.</td></tr>
            <tr><td><span class="inline-code">RenderReferenceId</span></td><td><span class="inline-code">string</span></td><td>Resource ID used for the render reference. Defaults to <span class="inline-code">stoneplant</span>.</td></tr>
            <tr><td><span class="inline-code">Health</span></td><td><span class="inline-code">float</span></td><td>Building health. Defaults to <span class="inline-code">250f</span>.</td></tr>
            <tr><td><span class="inline-code">RequireGround</span></td><td><span class="inline-code">bool</span></td><td>Whether the building should break if the ground below is destroyed.</td></tr>
            <tr><td><span class="inline-code">Metallic</span></td><td><span class="inline-code">bool</span></td><td>Enables vanilla metallic damage behavior. (Plasma cutter)</td></tr>
            <tr><td><span class="inline-code">CantHit</span></td><td><span class="inline-code">bool</span></td><td>Uses the vanilla can't-hit behavior. Usually for decoration.</td></tr>
            <tr><td><span class="inline-code">Animal</span></td><td><span class="inline-code">bool</span></td><td>It's complicated. Read more in the Enemies API page.</td></tr>
            <tr><td><span class="inline-code">IgnoreBodyOptimize</span></td><td><span class="inline-code">bool</span></td><td>Skips vanilla body optimization for this object.</td></tr>
            <tr><td><span class="inline-code">DropChanceMultiplier</span></td><td><span class="inline-code">float</span></td><td>Multiplies drop chances for this building. Defaults to <span class="inline-code">1f</span>.</td></tr>
            <tr><td><span class="inline-code">ItemsDropOnDestroy</span></td><td><span class="inline-code">ItemDrop[]</span></td><td>Conditional drops rolled when the building breaks.</td></tr>
            <tr><td><span class="inline-code">AlwaysDrop</span></td><td><span class="inline-code">ItemDrop[]</span></td><td>Guaranteed drops spawned when the building breaks.</td></tr>
            <tr><td><span class="inline-code">ItemCategoriesToAdd</span></td><td><span class="inline-code">string[]</span></td><td>Loot-pool categories used for guaranteed category drops.</td></tr>
            <tr><td><span class="inline-code">GuaranteedDropAmount</span></td><td><span class="inline-code">int</span></td><td>How many category drops to spawn from <span class="inline-code">ItemCategoriesToAdd</span>.</td></tr>
            <tr><td><span class="inline-code">Placement</span></td><td><span class="inline-code">BuildingPlacementType</span></td><td>Surface type used for placement. Defaults to <span class="inline-code">Floor</span>.</td></tr>
            <tr><td><span class="inline-code">GenerationStyle</span></td><td><span class="inline-code">BuildingGenerationStyle</span></td><td>Worldgen mode. Defaults to <span class="inline-code">None</span>, so buildings only appear when spawned or placed directly.</td></tr>
            <tr><td><span class="inline-code">SpawnMinPerChunk</span></td><td><span class="inline-code">float</span></td><td>Lower bound for worldgen spawn density.</td></tr>
            <tr><td><span class="inline-code">SpawnMaxPerChunk</span></td><td><span class="inline-code">float</span></td><td>Upper bound for worldgen spawn density.</td></tr>
            <tr><td><span class="inline-code">SpawnLayers</span></td><td><span class="inline-code">int</span></td><td>Bitmask for allowed in-game layers during building worldgen. Defaults to all layers. Use <span class="inline-code">BuildingEntityRegistry.LayersToMask(2, 4, 5)</span> or <span class="inline-code">BuildingEntityRegistry.AllLayersExcept(1, 3)</span>.</td></tr>
            <tr><td><span class="inline-code">SurfaceOffset</span></td><td><span class="inline-code">float</span></td><td>Offset from the hit surface used when placing or generating the object. Defaults to <span class="inline-code">0.5f</span>.</td></tr>
            <tr><td><span class="inline-code">RandomFlip</span></td><td><span class="inline-code">bool</span></td><td>Randomly mirrors the prefab on spawn. Defaults to <span class="inline-code">true</span>.</td></tr>
            <tr><td><span class="inline-code">SpawnInGround</span></td><td><span class="inline-code">bool</span></td><td>Allows standard worldgen placement even when the target is inside ground.</td></tr>
            <tr><td><span class="inline-code">HitSoundReferenceId</span></td><td><span class="inline-code">string</span></td><td>Short sound alias used to resolve vanilla hit audio.</td></tr>
            <tr><td><span class="inline-code">HitSound</span></td><td><span class="inline-code">AudioClip</span></td><td>Direct hit sound override. Takes priority over <span class="inline-code">HitSoundReferenceId</span>.</td></tr>
            <tr><td><span class="inline-code">BlockFootstepSoundId</span></td><td><span class="inline-code">ushort</span></td><td>Vanilla footstep sound ID for the building block.</td></tr>
            <tr><td><span class="inline-code">HeatRadius</span></td><td><span class="inline-code">float</span></td><td>Optional passive warming radius around the building in world units. Set above <span class="inline-code">0</span> to enable the aura.</td></tr>
            <tr><td><span class="inline-code">HeatPerSecond</span></td><td><span class="inline-code">float</span></td><td>How quickly the aura raises the nearby player's <span class="inline-code">body.temperature</span>.</td></tr>
            <tr><td><span class="inline-code">MaxHeatBodyTemperature</span></td><td><span class="inline-code">float</span></td><td>Optional cap for aura heating. <span class="inline-code">0</span> means no cap.</td></tr>
          </tbody>
        </table>
      </div>
      <p><span class="inline-code">SpawnLayers</span> uses the same 1-based layer numbering as the tile registry helpers. Layer <span class="inline-code">1</span> means the first playable depth, layer <span class="inline-code">4</span> means biome depth <span class="inline-code">3</span>, and leaving the field alone keeps the building eligible everywhere.</p>
    </section>
  `;
}

function tutorialFirstModPage(): string {
  return `
    <section class="lesson-card">
      <h2>What are we building</h2>
      <p>This introductory guide will add one custom item called <span class="inline-code">acidshroom</span>, one custom liquid called <span class="inline-code">hydrochloricacid</span>, one buildingEntity called <span class="inline-code">acidmushroom</span>, and two simple recipes.</p>
      <p>The code sample on the right is intentionally written as one compact registration flow so you can copy it into a first mod and then split it into files later.</p>
      <p>Still, it's best to follow step-by-step rather then copy-paste.</p>
      
    </section>

    <section class="lesson-card">
      <h2>Folder layout and assets</h2>
      <p>Create three embedded resources in your project: <span class="inline-code">Images.acidshroom.png</span>, <span class="inline-code">Images.acidmushroom.png</span>, and <span class="inline-code">Audio.acid-pop.wav</span>. Set each file's Build Action to <span class="inline-code">Embedded Resource</span>.</p>
      <p>If you need a refresher on the loaders themselves, the dedicated <a href="/docs/assets/" data-page="assets">Loading Assets</a> and <a href="/docs/audio/" data-page="audio">Audio</a> pages go deeper on suffix matching, caching, and loose file loading.</p>
      <pre><code>// Examples used by this tutorial
Sprite acidShroomSprite = AssetLoader.LoadEmbeddedSprite("Images.acidshroom.png");
Sprite acidMushroomSprite = AssetLoader.LoadEmbeddedSprite("Images.acidmushroom.png", 8f);
AudioClip acidPop = AssetLoader.LoadEmbeddedAudio("Audio.acid-pop.wav");</code></pre>
    <p>Consider <a href="https://opengameart.org" target="_blank">opengameart.org</a> for free assets and sounds.</p>
    </section>

    <section class="lesson-card">
      <h2>Register the item and liquid</h2>
      <p>The <span class="inline-code">acidshroom</span> item is a normal pickup item that can be eaten for a tiny benefit and a little risk. The custom liquid uses <span class="inline-code">CustomLiquidInfo</span> so it can later be put into containers or recipe inputs.</p>
      <p>For more field-by-field references, see <a href="/docs/item/" data-page="item">Item API</a>, <a href="/docs/advanced-item/" data-page="advanced-item">Advanced Item API</a>, and <a href="/docs/liquids/" data-page="liquids">Liquid API</a>.</p>
    </section>

    <section class="lesson-card">
      <h2>Register the building entity</h2>
      <p>The <span class="inline-code">acidmushroom</span> buildingEntity is a simple floor plant. It uses the embedded sprite, reuses the loaded sound as a custom hit sound, and drops the harvest item when destroyed.</p>
      <p>If you want to expand this into a more interactive machine later, jump to <a href="/docs/building-entities/" data-page="building-entities">Building Entities</a> and <a href="/docs/advanced-building-entities/" data-page="advanced-building-entities">Advanced Buildings</a>.</p>
    </section>

    <section class="lesson-card">
      <h2>Register the recipes</h2>
      <p>This walkthrough uses two recipes:</p>
      <ul>
        <li><span class="inline-code">acidmushroom</span> into <span class="inline-code">40</span> biochem.</li>
        <li><span class="inline-code">2 acidshroom</span> plus <span class="inline-code">processedcopper</span> into one <span class="inline-code">mediumbattery</span>.</li>
      </ul>
      <p>The dedicated <a href="/docs/recipe/" data-page="recipe">Recipe API</a> page explains the recipe object model in more depth, especially when you start mixing qualities, liquids, and condition values.</p>
    </section>

    <section class="lesson-card">
      <h2>Where to go next</h2>
      <p>Once this works, the next useful additions are usually <a href="/docs/locale/" data-page="locale">Locale</a> for explicit translations, <a href="/docs/console/" data-page="console">Console</a> for spawn/debug commands, and more content.</p>
      <p>The tutorial sample keeps everything in one place on purpose. When you are ready, split it into <span class="inline-code">RegisterItems.cs</span>, <span class="inline-code">RegisterLiquids.cs</span>, <span class="inline-code">RegisterBuildings.cs</span>, and <span class="inline-code">RegisterRecipes.cs</span>.</p>
    </section>
  `;
}

function advancedBuildingEntitiesPage(): string {
  return `
    <section class="lesson-card">
      <h2>When to use this page</h2>
      <p>Use this page when your building needs prefab hooks, custom components, or special interaction logic. If the object is basically a prop or a vein, the normal BuildingEntity page is probably enough.</p>
    </section>

    <section class="lesson-card">
      <h2>Advanced building fields</h2>
      <p>These are the knobs you reach for when a building needs extra Unity behavior, custom runtime wiring, or minigame-style interaction. Normal props usually do not need any of these.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Field</th>
              <th>Type</th>
              <th>What CUCoreLib uses it for</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">ConfigurePrefab</span></td><td><span class="inline-code">Action&lt;GameObject&gt;</span></td><td>Runs once while the prefab is being built.</td></tr>
            <tr><td><span class="inline-code">ConfigureInstance</span></td><td><span class="inline-code">Action&lt;GameObject&gt;</span></td><td>Runs on each spawned instance after instantiation.</td></tr>
            <tr><td><span class="inline-code">Components</span></td><td><span class="inline-code">Type[]</span></td><td>Extra Unity components added to the prefab before it is cached.</td></tr>
            <tr><td><span class="inline-code">SpawnComponents</span></td><td><span class="inline-code">List&lt;string&gt;</span></td><td>Qualified MonoBehaviour type names added to each spawned building instance the first time it wakes up.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>Spawn-time MonoBehaviours</h2>
      <p>Use <span class="inline-code">SpawnComponents</span> when you want a quick per-instance script hook without manually wiring <span class="inline-code">ConfigureInstance</span> or building a larger prefab setup path.</p>
      <pre><code>BuildingEntityRegistry.Register("campheater", new CustomBuildingEntityDefinition
{
    Name = "Camp heater",
    Description = "A small heater with a simple runtime script.",
    Sprite = mySprite,
    Placement = BuildingPlacementType.Floor,
    SpawnComponents = new List&lt;string&gt;
    {
        "MyMod.CampHeaterRuntime, MyModAssembly"
    }
});</code></pre>
    </section>
    
    <section class="lesson-card">
      <h2>Keypad and lockpick minigames</h2>
      <p>Vanilla buildings use an <span class="inline-code">Openable</span> component for these interactions. If <span class="inline-code">isKeypad</span> is true, <span class="inline-code">Openable.OnUse()</span> launches <span class="inline-code">KeypadMinigame</span>. Otherwise it launches <span class="inline-code">LockpingMinigame</span> with <span class="inline-code">lockpickAnglePrecision</span> scaled by the run setting <span class="inline-code">lockpickprecision</span>.</p>
      <p><span class="inline-code">instantOpen</span> bypasses the minigame and just zeroes the building's health. That is the simplest pattern if you want a locked or coded building that still fits the normal game flow.</p>
      <pre><code>public sealed class SafeDoorScript : MonoBehaviour
{
    private void Awake()
    {
        Openable openable = gameObject.AddComponent&lt;Openable&gt;();
        openable.isKeypad = true;
        openable.lockpickAnglePrecision = 1.25f;
    }
}</code></pre>
      <p>For a keypad-style building, give the prefab an <span class="inline-code">Openable</span> with <span class="inline-code">isKeypad = true</span>. For a lockpick-style building, leave that false and adjust <span class="inline-code">lockpickAnglePrecision</span> to tune how strict the minigame is.</p>
    </section>
  `;
}

function multiBlockStructuresPage(): string {
  return `
    <section class="lesson-card">
      <h2>What this API is for</h2>
      <p><span class="inline-code">StructureRegistry</span> is for authored multi-block structures exported from the CU Structure Editor / More Structures webapp as v2 JSON. This is not a replacement for <span class="inline-code">BuildingEntityRegistry</span>; it is the bigger worldgen-oriented sibling for whole prefabs made of many blocks, liquids, items, and placed world objects.</p>
      <p>CUCoreLib v1 optimizes for deterministic world generation. It parses the JSON once, compiles the structure once, caches the result, and reuses that compiled payload during world generation instead of reparsing the file every placement.</p>
    </section>

    <section class="lesson-card">
      <h2>Structure editor</h2>
      <p>Author the layout in the hosted <a href="https://cu-custom-structures.jimmyking.dev/index.html" target="_blank">CU Custom Structures editor</a>, export the compact v2 JSON, then register that exported payload through <span class="inline-code">StructureRegistry</span>.</p>
      <img src="images/custom-structure-editor.png" alt="CU Custom Structures editor showing a multi-block structure authoring layout." class="screenshot">
    </section>

    <section class="lesson-card">
      <h2>Register structure JSON</h2>
      <p>Author the structure in the webapp, export the compact v2 JSON, and register that exact payload. You can load the text from an embedded resource or a loose file path.</p>
      <pre><code>string campJson = AssetLoader.LoadEmbeddedText("Structures.camp_heater_room.json");
StructureRegistry.RegisterFromJson("campheaterroom", campJson);

StructureRegistry.RegisterFromEmbeddedJson(
    "fieldlab",
    "Structures.field_lab.json");

StructureRegistry.RegisterFromFile(
    "crashsite",
    Path.Combine(Paths.PluginPath, "MyMod", "Structures", "crashsite.json"));</code></pre>
      <p>The structure ID is the stable runtime key. The exported JSON keeps ownership of the actual block/object/item layout and the default five-depth <span class="inline-code">spawnCounts</span> values.</p>
    </section>

    <section class="lesson-card">
      <h2>Imported fields</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Payload field</th><th>Status in v1</th><th>Notes</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">layers</span></td><td>Supported</td><td>Visible foreground and background layers are composed once during import.</td></tr>
            <tr><td><span class="inline-code">objectsByCell</span></td><td>Supported</td><td>Object markers are flattened onto the visible foreground layer and spawned during placement.</td></tr>
            <tr><td><span class="inline-code">itemsByCell</span></td><td>Supported</td><td>Only deterministic single-entry item assignments are accepted in v1.</td></tr>
            <tr><td><span class="inline-code">customPropertiesByCell</span></td><td>Supported</td><td>Imported for item/object placements and simple runtime overrides like condition or health.</td></tr>
            <tr><td><span class="inline-code">precisePlacements</span></td><td>Supported</td><td>Rotation, scale, flip, and per-cell offsets are compiled into the cached placement data.</td></tr>
            <tr><td><span class="inline-code">metadata.spawnCounts</span></td><td>Supported</td><td>Used as the default per-depth worldgen counts.</td></tr>
            <tr><td><span class="inline-code">terrainGenAreas</span></td><td>Imported, not executed</td><td>CUCoreLib keeps compatibility with the webapp payload shape, but terrain sub-generation is deferred past v1.</td></tr>
            <tr><td><span class="inline-code">lootRules</span> / <span class="inline-code">lootPools</span></td><td>Supported</td><td>Compiled into a second-pass seeded loot-marker roll path so the static structure still stays precompiled.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>Spawn counts and overrides</h2>
      <p>The importer reuses the same five-depth <span class="inline-code">spawnCounts</span> array the structure editor already exports. If your mod wants to tweak density after import, override the counts in code without rewriting the JSON.</p>
      <pre><code>StructureRegistry.RegisterFromEmbeddedJson(
    "fieldlab",
    "Structures.field_lab.json");

StructureRegistry.TrySetSpawnCounts("fieldlab", 0, 1, 2, 2, 3);</code></pre>
      <p>During world generation, CUCoreLib reads the current biome depth, picks the matching count, and places the already-compiled structure payload for each spawn.</p>
    </section>


  `;
}

function minigamesPage(): string {
  return `
    <section class="lesson-card">
      <h2>What this wraps</h2>
      <p>CUCoreLib does not replace the game's minigame system. It now mirrors the game's real split more closely: the vanilla <span class="inline-code">Minigame</span> contract still does the actual work, while CUCoreLib adds a runner facade, a live session object, and a definition layer so mods can target stable seams instead of poking globals directly.</p>
      <p>The important vanilla hooks are <span class="inline-code">Start()</span>, <span class="inline-code">Update()</span>, <span class="inline-code">PhysicsUpdate()</span>, <span class="inline-code">HandType()</span>, <span class="inline-code">GuideLocaleString()</span>, <span class="inline-code">NeedsItem()</span>, and <span class="inline-code">CanExit()</span>.</p>
      <p>For quick visual wiring, the session layer exposes the live hand sprite, hand state, spawned minigame root, session-scoped state storage, and the shared screen factory so custom minigames can swap art or grab child GameObjects without repeating reflection code everywhere.</p>
    </section>

    <section class="lesson-card">
      <h2>CUCoreLib helper surface</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Helper</th><th>What it does</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">CUCoreMinigames</span></td><td>Static runner facade that starts or ends minigames and exposes the current live session.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigameSession</span></td><td>Live wrapper around the shared runner with access to body, current item, hand state, spawned UI, session-scoped state, and screen/hand helpers.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigameConfig</span></td><td>Cached policy object returned from <span class="inline-code">Configure(...)</span> for hand type, guide key, item requirement, rotation offset, and exit behavior.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigameDefinition</span></td><td>Composable definition layer for new minigames. Override <span class="inline-code">Configure</span>, <span class="inline-code">Start</span>, <span class="inline-code">Update</span>, and optional <span class="inline-code">End</span> hooks.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigames.TryStart(...)</span> / <span class="inline-code">TryStartDefinition(...)</span></td><td>Starts either a vanilla <span class="inline-code">Minigame</span> or a definition-based CUCoreLib minigame only when the shared runner is idle.</td></tr>
            <tr><td><span class="inline-code">session.TryCreateScreen(...)</span></td><td>Loads a minigame screen prefab through the game's existing UI system.</td></tr>
            <tr><td><span class="inline-code">session.TryCreateBundledScreen(bundleId, assetName)</span></td><td>Instantiates a bundled screen prefab under the live minigame canvas using the same parenting and <span class="inline-code">spawnedMiniGame</span> bookkeeping as the vanilla screen factory.</td></tr>
            <tr><td><span class="inline-code">cclbundle://bundleId/assetName</span></td><td>Reserved screen-resource prefix that lets existing <span class="inline-code">TryCreateScreen(...)</span> style code route through a bundled prefab without inventing a second user-facing identifier format.</td></tr>
            <tr><td><span class="inline-code">session.Complete()</span> / <span class="inline-code">Fail()</span> / <span class="inline-code">Cancel()</span></td><td>Ends the active minigame through the shared runner and reports an explicit end reason into the definition lifecycle.</td></tr>
            <tr><td><span class="inline-code">session.TrySetHandSprite(...)</span></td><td>Swaps the current hand sprite immediately, either from a sprite asset or one of the game's existing hand slots.</td></tr>
            <tr><td><span class="inline-code">session.GetOrCreateState&lt;T&gt;(...)</span></td><td>Stores modular per-run state without inventing extra globals or singleton managers.</td></tr>
            <tr><td><span class="inline-code">session.TryGetSpawnedMiniGameObject(...)</span></td><td>Gets a child GameObject from the spawned minigame root by index or by name.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigameTimer</span> / <span class="inline-code">CUCoreMinigameProgress</span></td><td>Optional small helper objects for countdowns and progress-based minigame state.</td></tr>
            <tr><td><span class="inline-code">CUCoreMinigame</span></td><td>Legacy-compatible abstract wrapper for mods that still prefer direct inheritance from a <span class="inline-code">Minigame</span>-style base.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>Minimal custom minigame</h2>
      <p>For new work, subclass <span class="inline-code">CUCoreMinigameDefinition</span> and start it through <span class="inline-code">CUCoreMinigames.TryStartDefinition(...)</span>. That keeps your minigame logic focused on one live session object instead of scattered global calls. Put static policy in <span class="inline-code">Configure(...)</span>, then keep runtime state inside the session.</p>
      <pre><code>using System.Collections.Generic;
using CUCoreLib.Helpers;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WireSpliceMinigame : CUCoreMinigameDefinition
{
    private sealed class WireSpliceState
    {
        public CUCoreMinigameTimer Timer = new CUCoreMinigameTimer(6f);
        public CUCoreMinigameProgress Progress = new CUCoreMinigameProgress(3f);
    }

    public override CUCoreMinigameConfig Configure(CUCoreMinigameSession session)
    {
        return new CUCoreMinigameConfig
        {
            HandType = _ =&gt; Minigame.HandSpriteType.Tweezers,
            NeedsItem = _ =&gt; false,
            GuideLocaleKey = _ =&gt; "wireSpliceGuide"
        };
    }

    public override void Start(CUCoreMinigameSession session)
    {
        session.TryCreateScreen("Special/WireSpliceMinigame");
        session.GetOrCreateState(() =&gt; new WireSpliceState());
    }

    public override void Update(CUCoreMinigameSession session, List&lt;RaycastResult&gt; uiCasts)
    {
        WireSpliceState state = session.GetOrCreateState(() =&gt; new WireSpliceState());

        if (state.Timer.Tick(Time.deltaTime))
        {
            session.Fail();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            state.Progress.Add(1f);
            if (state.Progress.IsComplete) session.Complete();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            session.Cancel();
        }
    }

    public override void End(CUCoreMinigameSession session, CUCoreMinigameEndReason reason)
    {
        Debug.Log("WireSplice ended with reason: " + reason);
    }
}</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Bundled screen prefabs</h2>
      <p>If your minigame UI lives in an AssetBundle instead of the game's normal <span class="inline-code">Resources</span> path, register the bundle once during plugin setup and then use either the typed bundled helper or the reserved <span class="inline-code">cclbundle://</span> screen ID.</p>
      <pre><code>private void Awake()
{
    AssetLoader.RegisterBundleFromPluginFolder(this, "glassworks.minigames", "Bundles/glassworks-minigames");
}

public override void Start(CUCoreMinigameSession session)
{
    session.TryCreateBundledScreen("glassworks.minigames", "WireSpliceScreen");
}</code></pre>
      <pre><code>public override void Start(CUCoreMinigameSession session)
{
    session.TryCreateScreen("cclbundle://glassworks.minigames/WireSpliceScreen");
}</code></pre>
      <p>Both paths instantiate the prefab under <span class="inline-code">minigameScreen</span>, move it to sibling index <span class="inline-code">0</span>, and update <span class="inline-code">spawnedMiniGame</span> so the rest of CUCoreLib's lookup helpers behave exactly the same as a vanilla screen.</p>
    </section>

    <section class="lesson-card">
      <h2>Starting a minigame</h2>
      <p>Use the runner guard so you do not accidentally stack minigames. If the runner is busy, the start request fails cleanly.</p>
      <pre><code>if (CUCoreMinigames.TryStartDefinition(() => new WireSpliceMinigame()))
{
    // Started successfully.
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Compatibility path</h2>
      <p>If you already have a custom class that wants to inherit directly from <span class="inline-code">Minigame</span>-style helpers, <span class="inline-code">CUCoreMinigame</span> still works and now delegates through the same runner/session layer. Use it when direct subclassing is the least awkward fit, but prefer the definition/session surface for new APIs.</p>
    </section>

    <section class="lesson-card">
      <h2>Built-in overlap</h2>
      <p>CUCoreLib already maps <span class="inline-code">BandageProperties</span> to <span class="inline-code">BandageMinigame</span> and <span class="inline-code">SyringeProperties</span> to <span class="inline-code">SyringeMinigame</span>. Those are the best examples to copy if your custom minigame needs a custom hand type, item requirement, or screen prefab.</p>
      <p>For building interactions, the vanilla <span class="inline-code">Openable</span> flow still handles keypad and lockpick minigames. The helper framework is meant to make new custom minigames easier, not replace that existing behavior.</p>
    </section>
  `;
}

function tilesPage(): string {
  return `
    <section class="lesson-card">
      <h2>Register a terrain tile</h2>
      <p><span class="inline-code">TileRegistry.Register</span> adds a Unity <span class="inline-code">Tile</span> to every <span class="inline-code">WorldGeneration.tiles</span> palette, supplies the matching vanilla <span class="inline-code">BlockInfo</span>, and can optionally hook the tile into vanilla-style ore generation.</p>
      <pre><code>TileRegistry.Register(36, new CustomTileDefinition
{
    ID = "galena",
    Name = "Galena",
    Sprite = AssetLoader.LoadEmbeddedSprite("Images.galena.png", 8f),
    Health = 300f,
    HitSound = "stone",
    StepSound = "Gravel",
    SleepQuality = Body.SleepQuality.Mediocre,
    SpawnAmount = 0.5f,
    SpawnLayers = TileRegistry.AllLayersExcept(1, 3),
    GenerationStyle = TileGenerationStyle.Vein | TileGenerationStyle.Outskirt
});</code></pre>
      <img src="images/galena-tile-ingame.png" alt="In-game screenshot of the galena tile, with its sprite and health bar." class="screenshot" scale="0.5">
      <p>Tile indices are written into the world's block array and saves. Pick an unused index of <span class="inline-code">36</span> or higher, keep it assigned to the same tile forever, and coordinate indices with other mods you expect players to combine. CUCoreLib rejects vanilla indices and duplicate custom registrations instead of changing an existing block's meaning.</p>
    </section>

    <section class="lesson-card">
      <h2>Place the registered tile</h2>
      <p>Registration makes the tile available; your mod still decides where it appears. For live world terrain, use <span class="inline-code">TileRegistry.SetBlock</span> or vanilla <span class="inline-code">WorldGeneration.SetBlock</span> with the registered index.</p>
      <pre><code>Vector2Int blockPosition = WorldGeneration.world.WorldToBlockPos(transform.position);
TileRegistry.SetBlock(WorldGeneration.world, blockPosition, 36);

// The vanilla equivalent after registration:
WorldGeneration.world.SetBlock(blockPosition, 36);</code></pre>
      <p><span class="inline-code">Tilemap.SetTile()</span> is one of the ways of adding the tile into the game. Use <span class="inline-code">TileRegistry.TryGetTile</span> to retrieve the registered <span class="inline-code">TileBase</span>, then place it into a structure or another Unity <span class="inline-code">Tilemap</span>. When the game imports that tilemap through its normal structure generation methods, it resolves the tile back to the registered block index.</p>
      <p>For now, you'll have to swap into Unity for this. (Eventually I'll have the Custom Structures webapp support it though ^^)</p>
      <pre><code>if (TileRegistry.TryGetTile(36, out TileBase galenaTile))
{
    structureTilemap.SetTile(new Vector3Int(4, -2, 0), galenaTile);
}</code></pre>
      <p>Use <span class="inline-code">SetBlockNoUpdate()</span> only while batching generation work where the caller will refresh affected chunks afterward.</p>
    </section>

    <section class="lesson-card">
      <h2>CustomTileDefinition fields</h2>
      <table>
        <thead>
          <tr><th>Field</th><th>Type</th><th>Purpose</th></tr>
        </thead>
        <tbody>
          <tr><td><span class="inline-code">ID</span></td><td><span class="inline-code">string</span></td><td>Stable locale key and default Unity tile name.</td></tr>
          <tr><td><span class="inline-code">Name</span></td><td><span class="inline-code">string</span></td><td>Registered as <span class="inline-code">other.ID</span>.</td></tr>
          <tr><td><span class="inline-code">Sprite</span></td><td><span class="inline-code">Sprite</span></td><td>Required tile artwork, usually loaded through <span class="inline-code">AssetLoader</span>.</td></tr>
          <tr><td><span class="inline-code">TileName</span></td><td><span class="inline-code">string</span></td><td>Optional Unity object name. Defaults to <span class="inline-code">ID</span>.</td></tr>
          <tr><td><span class="inline-code">Color</span></td><td><span class="inline-code">Color</span></td><td>Sprite tint. Defaults to white.</td></tr>
          <tr><td><span class="inline-code">ColliderType</span></td><td><span class="inline-code">Tile.ColliderType</span></td><td>Unity Tilemap collider behavior. Defaults to <span class="inline-code">Grid</span>.</td></tr>
          <tr><td><span class="inline-code">Health</span></td><td><span class="inline-code">float</span></td><td>Damage required to break the block.</td></tr>
          <tr><td><span class="inline-code">HitSound</span></td><td><span class="inline-code">string</span></td><td>Vanilla hit-sound shorthand. You can point at another tile or a vanilla sound id.</td></tr>
          <tr><td><span class="inline-code">HitSoundClip</span></td><td><span class="inline-code">AudioClip</span></td><td>Direct hit sound override. Takes priority over <span class="inline-code">HitSound</span>.</td></tr>
          <tr><td><span class="inline-code">StepSound</span></td><td><span class="inline-code">string</span></td><td>Vanilla sound id used for footsteps.</td></tr>
          <tr><td><span class="inline-code">SleepQuality</span></td><td><span class="inline-code">Body.SleepQuality</span></td><td>Rest quality while lying on the tile.</td></tr>
          <tr><td><span class="inline-code">NoVariation</span></td><td><span class="inline-code">bool</span></td><td>Disables the game's visual tile variation. (random flips)</td></tr>
          <tr><td><span class="inline-code">Metallic</span></td><td><span class="inline-code">bool</span></td><td>Enables vanilla metallic damage behavior.</td></tr>
          <tr><td><span class="inline-code">Toxicity</span></td><td><span class="inline-code">float</span></td><td>Uses the vanilla block toxicity (irradiation) field.</td></tr>
          <tr><td><span class="inline-code">Slippery</span></td><td><span class="inline-code">bool</span></td><td>Uses the vanilla slippery-floor behavior.</td></tr>
          <tr><td><span class="inline-code">SpawnAmount</span></td><td><span class="inline-code">float</span></td><td>Optional copper-spawn multiplier. <span class="inline-code">0f</span> disables automatic spawning, <span class="inline-code">1f</span> matches copper, <span class="inline-code">2f</span> doubles it, and <span class="inline-code">0.5f</span> halves it.</td></tr>
          <tr><td><span class="inline-code">SpawnLayers</span></td><td><span class="inline-code">int</span></td><td>Bitmask for allowed in-game layers. Defaults to all layers. Use helpers like <span class="inline-code">TileRegistry.LayersToMask(2, 4, 5)</span> or <span class="inline-code">TileRegistry.AllLayersExcept(1, 3)</span>.</td></tr>
          <tr><td><span class="inline-code">GenerationStyle</span></td><td><span class="inline-code">TileGenerationStyle</span></td><td>Byte-backed preset flags for automatic worldgen shape. Defaults to <span class="inline-code">Vein</span>. Combine flags like <span class="inline-code">Vein | Outskirt</span> when you want mixed behavior.</td></tr>
          <tr><td><span class="inline-code">Drops</span></td><td><span class="inline-code">ItemDrop[]</span></td><td>Optional break drops using the same <span class="inline-code">id</span>, <span class="inline-code">chance</span>, <span class="inline-code">conditionMin</span>, and <span class="inline-code">conditionMax</span> fields as custom building drops.</td></tr>
          <tr><td><span class="inline-code">CustomData</span></td><td><span class="inline-code">Dictionary&lt;string, object&gt;</span></td><td>Registration-time metadata for your own tile behavior, such as a custom <span class="inline-code">leadToxicity</span> value read later with <span class="inline-code">TileRegistry.TryGetCustomData&lt;T&gt;</span>.</td></tr>
        </tbody>
      </table>
    </section>

    <section class="lesson-card">
      <h2>Automatic ore-style spawning</h2>
      <p>Set <span class="inline-code">SpawnAmount</span> above <span class="inline-code">0f</span> and CUCoreLib will distribute the tile during <span class="inline-code">WorldGeneration.GenerateOres()</span> using the same vein-walk pattern vanilla copper uses. The value is a multiplier on copper's own spawn count, and the current run's <span class="inline-code">oreamount</span> setting still scales the density.</p>
      <p><span class="inline-code">SpawnLayers</span> uses 1-based layer numbers for mod authors. Layer 1 means the first playable layer, layer 4 means biome depth 3, and so on. Leave it alone for all layers, or use the helper masks when you want exclusions.</p>
      <p><span class="inline-code">GenerationStyle</span> is a byte flag enum with these presets: <span class="inline-code">Vein</span>, <span class="inline-code">HeavyVeins</span>, <span class="inline-code">Singular</span>, <span class="inline-code">Stripe</span>, <span class="inline-code">Inner</span>, and <span class="inline-code">Outskirt</span>. If you combine more than one, CUCoreLib splits the spawn budget across the selected presets so <span class="inline-code">SpawnAmount</span> stays predictable.</p>
      <pre><code>TileRegistry.Register(37, new CustomTileDefinition
{
    ID = "auric",
    Name = "Auric",
    Sprite = AssetLoader.LoadEmbeddedSprite("Images.auric.png", 8f),
    Health = 777f,
    HitSound = "crystal",
    StepSound = "Rock",
    Metallic = true,
    SpawnAmount = 2f,
    SpawnLayers = TileRegistry.LayersToMask(4, 5, 6, 7, 8, 9, 10),
    GenerationStyle = TileGenerationStyle.HeavyVeins | TileGenerationStyle.Inner
});</code></pre>
    </section>
  `;
}

function playerPage(): string {
  return `
    <section class="lesson-card">
      <h2>The player object</h2>
      <p>The live player object is the vanilla <span class="inline-code">Body</span> component. Most player values you inspect or change from mods live on <span class="inline-code">PlayerCamera.main.body</span>.</p>
      <pre><code>Body body = PlayerCamera.main.body;
float hunger = body.hunger;
Vector3 position = body.transform.position;</code></pre>
      <p>This page is intentionally a reference page. Field names come from the decompiled vanilla <span class="inline-code">Body.cs</span>, so these are the names to search for in dnSpyEx or patch against in your own code.</p>
    </section>
    <section class="lesson-card">
      <h2>Typed body-curve helpers</h2>
      <p>CUCoreLib now exposes a lightweight typed layer for the repetitive <span class="inline-code">Body.AnimationCurve</span> fields. The goal is ergonomics, not abstraction for its own sake: generic bundle asset loading still exists, but common body curve assignments no longer need every mod to repeat the same switch statement.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Helper</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">BodyAnimationCurveField</span></td><td>Named enum values</td><td>Lists the supported vanilla body curve fields such as <span class="inline-code">StaminaStrength</span>, <span class="inline-code">WeightMovementCurve</span>, and <span class="inline-code">HeartCurveNormal</span>.</td></tr>
            <tr><td><span class="inline-code">BodyAnimationCurves.TryApplyCurve</span></td><td><span class="inline-code">Body body, BodyAnimationCurveField field, AnimationCurve curve</span></td><td>Applies one already-resolved curve to one supported body field.</td></tr>
            <tr><td><span class="inline-code">BodyAnimationCurves.TryApplyBundledCurve</span></td><td><span class="inline-code">Body body, BodyAnimationCurveField field, string bundleId, string assetName</span></td><td>Loads one bundled <span class="inline-code">AnimationCurveAsset</span> and applies it to the chosen body field.</td></tr>
            <tr><td><span class="inline-code">BodyAnimationCurves.TryApplyBundledCurves</span></td><td><span class="inline-code">Body body, string bundleId, params BodyAnimationCurveOverride[] overrides</span></td><td>Resolves a small batch first, then applies the whole set only if every referenced curve asset loads successfully.</td></tr>
            <tr><td><span class="inline-code">BodyAnimationCurves.TryApplyBundledProfile</span></td><td><span class="inline-code">Body body, string bundleId, string assetName</span></td><td>Loads a bundled <span class="inline-code">BodyAnimationCurveProfileAsset</span> and applies its named overrides.</td></tr>
            <tr><td><span class="inline-code">AssetLoader.TryLoadBundleAnimationCurve</span></td><td><span class="inline-code">string bundleId, string assetName, out AnimationCurve curve</span></td><td>Keeps raw generic curve resolution available for advanced mods that want to evaluate or clone the curve themselves before assignment.</td></tr>
          </tbody>
        </table>
      </div>
    </section>
    <section class="lesson-card">
      <h2>Bundle authoring for curves</h2>
      <p>Bundle curve assets through CUCoreLib's ScriptableObject wrappers. Unity does not hand CUCoreLib a bare <span class="inline-code">AnimationCurve</span> as a normal bundle asset, so the supported pattern is: create an <span class="inline-code">AnimationCurveAsset</span>, assign its <span class="inline-code">Curve</span>, then optionally reference multiple named curve assets from a <span class="inline-code">BodyAnimationCurveProfileAsset</span>.</p>
      <pre><code>// Single curve asset name inside the bundle:
//   SprintStaminaCurve (AnimationCurveAsset)
//
// Optional batch profile asset:
//   WinterProfile (BodyAnimationCurveProfileAsset)
//   Overrides:
//     - Field = TemperatureMovementCurve, AssetName = "ColdMovementCurve"
//     - Field = ThirstBloodPressureCurve, AssetName = "DryBloodPressureCurve"</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Curve helper examples</h2>
      <pre><code>Body body = PlayerCamera.main.body;

BodyAnimationCurves.TryApplyBundledCurve(
    body,
    BodyAnimationCurveField.StaminaStrength,
    "glassworks.curves",
    "SprintStaminaCurve");</code></pre>
      <pre><code>BodyAnimationCurves.TryApplyBundledCurves(
    body,
    "glassworks.curves",
    new BodyAnimationCurveOverride
    {
        Field = BodyAnimationCurveField.WeightMovementCurve,
        AssetName = "HeavyPackCurve"
    },
    new BodyAnimationCurveOverride
    {
        Field = BodyAnimationCurveField.FoodMovementCurve,
        AssetName = "StarvingMovementCurve"
    });</code></pre>
      <pre><code>BodyAnimationCurves.TryApplyBundledProfile(
    body,
    "glassworks.curves",
    "WinterProfile");</code></pre>
      <p>If any referenced bundled curve asset is missing, the batch/profile helpers return <span class="inline-code">false</span> and leave unrelated body fields untouched.</p>
    </section>

   

    <section class="lesson-card">
      <h2>Player field reference</h2>
      <table>
        <thead>
          <tr><th>Field</th><th>Type</th><th>Description</th></tr>
        </thead>
        <tbody>
          <tr><td><span class="inline-code">limbs</span></td><td><span class="inline-code">Limb[]</span></td><td>All player limbs. Use for limb-specific wounds, treatment, and temperature work.</td></tr>
          <tr><td><span class="inline-code">legLimbs</span></td><td><span class="inline-code">Limb[]</span></td><td>Leg-only limb subset used by movement checks.</td></tr>
          <tr><td><span class="inline-code">baseLimb</span></td><td><span class="inline-code">Limb</span></td><td>Base/root limb reference for the body.</td></tr>
          <tr><td><span class="inline-code">bodyAnimator</span></td><td><span class="inline-code">Animator</span></td><td>Main body animator.</td></tr>
          <tr><td><span class="inline-code">idleClip</span></td><td><span class="inline-code">AnimationClip</span></td><td>Idle animation clip reference.</td></tr>
          <tr><td><span class="inline-code">armsAnimator</span></td><td><span class="inline-code">Animator</span></td><td>Animator for the arms layer.</td></tr>
          <tr><td><span class="inline-code">rb</span></td><td><span class="inline-code">Rigidbody2D</span></td><td>Player rigidbody used for physics and movement.</td></tr>
          <tr><td><span class="inline-code">standing</span></td><td><span class="inline-code">bool</span></td><td>Whether the player is standing.</td></tr>
          <tr><td><span class="inline-code">col</span></td><td><span class="inline-code">BoxCollider2D</span></td><td>Main player collider reference.</td></tr>
          <tr><td><span class="inline-code">isRight</span></td><td><span class="inline-code">bool</span></td><td>Facing/orientation flag used by look and animation logic.</td></tr>
          <tr><td><span class="inline-code">maxSpeed</span></td><td><span class="inline-code">float</span></td><td>Movement speed cap.</td></tr>
          <tr><td><span class="inline-code">targetLookPos</span></td><td><span class="inline-code">Vector3</span></td><td>World position the player is looking toward.</td></tr>
          <tr><td><span class="inline-code">staminaStrength</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve used for stamina-related scaling.</td></tr>
          <tr><td><span class="inline-code">vomiter</span></td><td><span class="inline-code">Vomiter</span></td><td>Vomiting behavior component.</td></tr>
          <tr><td><span class="inline-code">idleTime</span></td><td><span class="inline-code">float</span></td><td>How long the body has been idle.</td></tr>
          <tr><td><span class="inline-code">interactionRange</span></td><td><span class="inline-code">const float</span></td><td>Vanilla interaction distance limit.</td></tr>
          <tr><td><span class="inline-code">stimulantMultiplier</span></td><td><span class="inline-code">float</span></td><td>Multiplier affecting stimulant movement/response behavior.</td></tr>
          <tr><td><span class="inline-code">jumpSpeed</span></td><td><span class="inline-code">float</span></td><td>Jump velocity/speed.</td></tr>
          <tr><td><span class="inline-code">temporarySlowdown</span></td><td><span class="inline-code">float</span></td><td>Temporary movement penalty.</td></tr>
          <tr><td><span class="inline-code">grounded</span></td><td><span class="inline-code">bool</span></td><td>Whether the player is on the ground.</td></tr>
          <tr><td><span class="inline-code">moveDir</span></td><td><span class="inline-code">Vector2</span></td><td>Current movement input direction.</td></tr>
          <tr><td><span class="inline-code">moveForce</span></td><td><span class="inline-code">float</span></td><td>Base movement force/acceleration.</td></tr>
          <tr><td><span class="inline-code">liquidSlipTime</span></td><td><span class="inline-code">float</span></td><td>How long liquid slip effects remain active.</td></tr>
          <tr><td><span class="inline-code">liquidRagdollBar</span></td><td><span class="inline-code">float</span></td><td>Slip/ragdoll buildup related to liquids.</td></tr>
          <tr><td><span class="inline-code">slowdownAmount</span></td><td><span class="inline-code">float</span></td><td>Current total slowdown strength.</td></tr>
          <tr><td><span class="inline-code">lastTimeStepVelocity</span></td><td><span class="inline-code">Vector2</span></td><td>Most recent physics-step velocity.</td></tr>
          <tr><td><span class="inline-code">wallSlideSlowdown</span></td><td><span class="inline-code">float</span></td><td>Wall-slide movement reduction.</td></tr>
          <tr><td><span class="inline-code">timeSlidfor</span></td><td><span class="inline-code">float</span></td><td>How long wall sliding has been happening.</td></tr>
          <tr><td><span class="inline-code">bloodOxygen</span></td><td><span class="inline-code">float</span></td><td>Current oxygenation value.</td></tr>
          <tr><td><span class="inline-code">bloodVolume</span></td><td><span class="inline-code">float</span></td><td>Current blood amount. Important for bleeding, shock, and survival logic.</td></tr>
          <tr><td><span class="inline-code">heartRate</span></td><td><span class="inline-code">float</span></td><td>Current heart rate.</td></tr>
          <tr><td><span class="inline-code">respiratoryRate</span></td><td><span class="inline-code">float</span></td><td>Current breathing rate.</td></tr>
          <tr><td><span class="inline-code">bloodPressure</span></td><td><span class="inline-code">float</span></td><td>Current blood pressure value.</td></tr>
          <tr><td><span class="inline-code">bloodVesselSize</span></td><td><span class="inline-code">float</span></td><td>Vasoconstriction/vasodilation style multiplier.</td></tr>
          <tr><td><span class="inline-code">fibrillationProgress</span></td><td><span class="inline-code">float</span></td><td>Progress toward or through fibrillation.</td></tr>
          <tr><td><span class="inline-code">fibrillationForced</span></td><td><span class="inline-code">bool</span></td><td>Whether fibrillation is being forced.</td></tr>
          <tr><td><span class="inline-code">bloodViscosity</span></td><td><span class="inline-code">float</span></td><td>Blood thickness/viscosity value.</td></tr>
          <tr><td><span class="inline-code">heartRatePressureOffset</span></td><td><span class="inline-code">float</span></td><td>Internal blood-pressure offset tied to heart rate.</td></tr>
          <tr><td><span class="inline-code">bloodPressureReadout</span></td><td><span class="inline-code">string</span></td><td>Formatted blood pressure display string.</td></tr>
          <tr><td><span class="inline-code">adrenaline</span></td><td><span class="inline-code">float</span></td><td>Stored adrenaline amount.</td></tr>
          <tr><td><span class="inline-code">curAdrenaline</span></td><td><span class="inline-code">float</span></td><td>Current live adrenaline effect strength.</td></tr>
          <tr><td><span class="inline-code">happiness</span></td><td><span class="inline-code">float</span></td><td>General mood/happiness stat.</td></tr>
          <tr><td><span class="inline-code">opiateHappiness</span></td><td><span class="inline-code">float</span></td><td>Opiate-derived happiness contribution.</td></tr>
          <tr><td><span class="inline-code">antidepressantHappiness</span></td><td><span class="inline-code">float</span></td><td>Antidepressant-derived happiness contribution.</td></tr>
          <tr><td><span class="inline-code">weightOffset</span></td><td><span class="inline-code">float</span></td><td>Extra body weight offset often changed by food or liquid effects.</td></tr>
          <tr><td><span class="inline-code">hunger</span></td><td><span class="inline-code">float</span></td><td>Current hunger meter.</td></tr>
          <tr><td><span class="inline-code">thirst</span></td><td><span class="inline-code">float</span></td><td>Current thirst meter.</td></tr>
          <tr><td><span class="inline-code">stamina</span></td><td><span class="inline-code">float</span></td><td>Current stamina amount.</td></tr>
          <tr><td><span class="inline-code">energy</span></td><td><span class="inline-code">float</span></td><td>Current energy/fatigue reserve.</td></tr>
          <tr><td><span class="inline-code">brainHealth</span></td><td><span class="inline-code">float</span></td><td>Brain health value.</td></tr>
          <tr><td><span class="inline-code">consciousness</span></td><td><span class="inline-code">float</span></td><td>Wakefulness/consciousness level.</td></tr>
          <tr><td><span class="inline-code">shock</span></td><td><span class="inline-code">float</span></td><td>General shock value.</td></tr>
          <tr><td><span class="inline-code">sleeping</span></td><td><span class="inline-code">bool</span></td><td>Whether the body is currently sleeping.</td></tr>
          <tr><td><span class="inline-code">temperature</span></td><td><span class="inline-code">float</span></td><td>Core body temperature.</td></tr>
          <tr><td><span class="inline-code">clothingTemperature</span></td><td><span class="inline-code">float</span></td><td>Temperature contribution from worn clothing.</td></tr>
          <tr><td><span class="inline-code">averagePain</span></td><td><span class="inline-code">float</span></td><td>Aggregate pain across the body.</td></tr>
          <tr><td><span class="inline-code">totalBleedSpeed</span></td><td><span class="inline-code">float</span></td><td>Total bleed rate from all wounds.</td></tr>
          <tr><td><span class="inline-code">slots</span></td><td><span class="inline-code">InventorySlot[]</span></td><td>Player inventory and equipment slots.</td></tr>
          <tr><td><span class="inline-code">breathing</span></td><td><span class="inline-code">bool</span></td><td>Whether the body is currently breathing.</td></tr>
          <tr><td><span class="inline-code">eatTime</span></td><td><span class="inline-code">float</span></td><td>Timer/state related to eating actions.</td></tr>
          <tr><td><span class="inline-code">attackCooldown</span></td><td><span class="inline-code">float</span></td><td>Attack delay/cooldown timer.</td></tr>
          <tr><td><span class="inline-code">crouchAmount</span></td><td><span class="inline-code">float</span></td><td>Blend amount for crouching.</td></tr>
          <tr><td><span class="inline-code">crouching</span></td><td><span class="inline-code">bool</span></td><td>Whether the player is crouching.</td></tr>
          <tr><td><span class="inline-code">sicknessAmount</span></td><td><span class="inline-code">float</span></td><td>General sickness or poison meter.</td></tr>
          <tr><td><span class="inline-code">talker</span></td><td><span class="inline-code">Talker</span></td><td>Speech/talker component for reactions and barks.</td></tr>
          <tr><td><span class="inline-code">eyeCloseTime</span></td><td><span class="inline-code">float</span></td><td>Eye closing animation timer.</td></tr>
          <tr><td><span class="inline-code">eyeScareTime</span></td><td><span class="inline-code">float</span></td><td>Scare eye animation timer.</td></tr>
          <tr><td><span class="inline-code">eyePanicTime</span></td><td><span class="inline-code">float</span></td><td>Panic eye animation timer.</td></tr>
          <tr><td><span class="inline-code">weightMovementCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Movement scaling from carried/body weight.</td></tr>
          <tr><td><span class="inline-code">temperatureMovementCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Movement scaling from temperature.</td></tr>
          <tr><td><span class="inline-code">foodMovementCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Movement scaling from hunger/food state.</td></tr>
          <tr><td><span class="inline-code">consciousnessRiseRate</span></td><td><span class="inline-code">static float</span></td><td>Vanilla rate constant for regaining consciousness.</td></tr>
          <tr><td><span class="inline-code">consciousnessFallRate</span></td><td><span class="inline-code">static float</span></td><td>Vanilla rate constant for losing consciousness.</td></tr>
          <tr><td><span class="inline-code">desensitizedMult</span></td><td><span class="inline-code">float</span></td><td>Multiplier for desensitization effects.</td></tr>
          <tr><td><span class="inline-code">corpsesSeen</span></td><td><span class="inline-code">int</span></td><td>Counter used for corpse-related mental effects.</td></tr>
          <tr><td><span class="inline-code">soundCooldown</span></td><td><span class="inline-code">float</span></td><td>Cooldown for sound-producing actions.</td></tr>
          <tr><td><span class="inline-code">bonusRot</span></td><td><span class="inline-code">float</span></td><td>Extra body rotation offset.</td></tr>
          <tr><td><span class="inline-code">accelRot</span></td><td><span class="inline-code">float</span></td><td>Rotation caused by acceleration/movement.</td></tr>
          <tr><td><span class="inline-code">attackRot</span></td><td><span class="inline-code">float</span></td><td>Rotation added by attacks.</td></tr>
          <tr><td><span class="inline-code">septicShock</span></td><td><span class="inline-code">float</span></td><td>Septic shock severity.</td></tr>
          <tr><td><span class="inline-code">harmer</span></td><td><span class="inline-code">SelfHarmer</span></td><td>Self-harm related component/reference.</td></tr>
          <tr><td><span class="inline-code">disfigured</span></td><td><span class="inline-code">bool</span></td><td>Whether the body is marked as disfigured.</td></tr>
          <tr><td><span class="inline-code">eyeGone</span></td><td><span class="inline-code">bool</span></td><td>Whether one eye is gone.</td></tr>
          <tr><td><span class="inline-code">bothEyesGone</span></td><td><span class="inline-code">bool</span></td><td>Whether both eyes are gone.</td></tr>
          <tr><td><span class="inline-code">visualBodyOffset</span></td><td><span class="inline-code">Vector2</span></td><td>Visual offset for the rendered body.</td></tr>
          <tr><td><span class="inline-code">overrideLookTime</span></td><td><span class="inline-code">float</span></td><td>Duration for a forced look override.</td></tr>
          <tr><td><span class="inline-code">overrideLookPos</span></td><td><span class="inline-code">Vector2</span></td><td>Forced look target position.</td></tr>
          <tr><td><span class="inline-code">charType</span></td><td><span class="inline-code">int</span></td><td>Character/body type selector.</td></tr>
          <tr><td><span class="inline-code">armOffset</span></td><td><span class="inline-code">float</span></td><td>Arm visual offset.</td></tr>
          <tr><td><span class="inline-code">wallSlideParticle</span></td><td><span class="inline-code">ParticleSystem</span></td><td>Particle effect used while wall sliding.</td></tr>
          <tr><td><span class="inline-code">standLerpTime</span></td><td><span class="inline-code">float</span></td><td>Stand transition lerp timer.</td></tr>
          <tr><td><span class="inline-code">totalEncumberance</span></td><td><span class="inline-code">float</span></td><td>Total carried weight burden.</td></tr>
          <tr><td><span class="inline-code">overEncumberance</span></td><td><span class="inline-code">float</span></td><td>How far over the encumbrance limit the player is.</td></tr>
          <tr><td><span class="inline-code">limpAnimatorSpeed</span></td><td><span class="inline-code">float</span></td><td>Animator speed modifier for limping.</td></tr>
          <tr><td><span class="inline-code">radiationSickness</span></td><td><span class="inline-code">float</span></td><td>Radiation sickness amount.</td></tr>
          <tr><td><span class="inline-code">maxEncumberance</span></td><td><span class="inline-code">float</span></td><td>Maximum normal encumbrance before penalties.</td></tr>
          <tr><td><span class="inline-code">fallShakeCooldown</span></td><td><span class="inline-code">float</span></td><td>Cooldown for fall/camera shake effects.</td></tr>
          <tr><td><span class="inline-code">caffeinated</span></td><td><span class="inline-code">float</span></td><td>Caffeine effect strength.</td></tr>
          <tr><td><span class="inline-code">handSlot</span></td><td><span class="inline-code">int</span></td><td>Currently active hand slot index.</td></tr>
          <tr><td><span class="inline-code">hearingLoss</span></td><td><span class="inline-code">float</span></td><td>Current hearing loss severity.</td></tr>
          <tr><td><span class="inline-code">internalBleeding</span></td><td><span class="inline-code">float</span></td><td>Internal bleeding amount.</td></tr>
          <tr><td><span class="inline-code">hemothorax</span></td><td><span class="inline-code">float</span></td><td>Blood/fluid in the chest severity.</td></tr>
          <tr><td><span class="inline-code">forceWalk</span></td><td><span class="inline-code">bool</span></td><td>Whether movement is forced into walk mode.</td></tr>
          <tr><td><span class="inline-code">painShock</span></td><td><span class="inline-code">float</span></td><td>Pain-induced shock amount.</td></tr>
          <tr><td><span class="inline-code">limbBloodUpdateTimer</span></td><td><span class="inline-code">float</span></td><td>Timer for limb blood updates.</td></tr>
          <tr><td><span class="inline-code">traumaAmount</span></td><td><span class="inline-code">float</span></td><td>General trauma severity.</td></tr>
          <tr><td><span class="inline-code">hungerLimbHeal</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve controlling healing relative to hunger.</td></tr>
          <tr><td><span class="inline-code">overdoseIndex</span></td><td><span class="inline-code">int</span></td><td>Runtime overdose tracking index.</td></tr>
          <tr><td><span class="inline-code">impactSmall</span></td><td><span class="inline-code">AudioClip[]</span></td><td>Small impact sounds.</td></tr>
          <tr><td><span class="inline-code">impactMedium</span></td><td><span class="inline-code">AudioClip[]</span></td><td>Medium impact sounds.</td></tr>
          <tr><td><span class="inline-code">impactLarge</span></td><td><span class="inline-code">AudioClip[]</span></td><td>Large impact sounds.</td></tr>
          <tr><td><span class="inline-code">reversedControls</span></td><td><span class="inline-code">bool</span></td><td>Whether controls are currently reversed.</td></tr>
          <tr><td><span class="inline-code">furColors</span></td><td><span class="inline-code">Gradient</span></td><td>Gradient used for fur coloration.</td></tr>
          <tr><td><span class="inline-code">endedJump</span></td><td><span class="inline-code">bool</span></td><td>Whether the current jump has ended.</td></tr>
          <tr><td><span class="inline-code">depressionChanceCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve tied to depression-style chance/effect logic.</td></tr>
          <tr><td><span class="inline-code">wetness</span></td><td><span class="inline-code">float</span></td><td>How wet the player is.</td></tr>
          <tr><td><span class="inline-code">specialCrying</span></td><td><span class="inline-code">bool</span></td><td>Special crying state toggle.</td></tr>
          <tr><td><span class="inline-code">inWater</span></td><td><span class="inline-code">bool</span></td><td>Whether the body is currently in water.</td></tr>
          <tr><td><span class="inline-code">liquidDrinkTime</span></td><td><span class="inline-code">float</span></td><td>Timer/state for liquid drinking.</td></tr>
          <tr><td><span class="inline-code">bodyAffect</span></td><td><span class="inline-code">LiquidAffect</span></td><td>Active body-wide liquid effect reference.</td></tr>
          <tr><td><span class="inline-code">dogShakeIntensity</span></td><td><span class="inline-code">float</span></td><td>Shake intensity for wet shake behavior.</td></tr>
          <tr><td><span class="inline-code">brainShakeIntensity</span></td><td><span class="inline-code">float</span></td><td>Shake caused by brain effects.</td></tr>
          <tr><td><span class="inline-code">miscShakeIntensity</span></td><td><span class="inline-code">float</span></td><td>Catch-all extra shake intensity.</td></tr>
          <tr><td><span class="inline-code">hasScubaGear</span></td><td><span class="inline-code">bool</span></td><td>Whether the player is treated as having scuba gear.</td></tr>
          <tr><td><span class="inline-code">mindWipe</span></td><td><span class="inline-code">MindwipeScript</span></td><td>Mindwipe effect component/reference.</td></tr>
          <tr><td><span class="inline-code">curSleep</span></td><td><span class="inline-code">SleepQuality</span></td><td>Current sleep quality enum.</td></tr>
          <tr><td><span class="inline-code">badSleepAmount</span></td><td><span class="inline-code">float</span></td><td>Accumulated poor sleep amount.</td></tr>
          <tr><td><span class="inline-code">goodSleepTime</span></td><td><span class="inline-code">float</span></td><td>Accumulated good sleep time.</td></tr>
          <tr><td><span class="inline-code">snowAmount</span></td><td><span class="inline-code">float</span></td><td>Snow buildup on the player.</td></tr>
          <tr><td><span class="inline-code">immunity</span></td><td><span class="inline-code">float</span></td><td>Immune system strength value.</td></tr>
          <tr><td><span class="inline-code">immunityInfectionSpeed</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve affecting infection speed from immunity.</td></tr>
          <tr><td><span class="inline-code">antibioticImmunityTime</span></td><td><span class="inline-code">float</span></td><td>Remaining boosted-immunity time from antibiotics.</td></tr>
          <tr><td><span class="inline-code">curImmunityMult</span></td><td><span class="inline-code">float</span></td><td>Current immunity multiplier.</td></tr>
          <tr><td><span class="inline-code">tail</span></td><td><span class="inline-code">Transform</span></td><td>Tail transform reference, when present.</td></tr>
          <tr><td><span class="inline-code">lastHappiness</span></td><td><span class="inline-code">float[]</span></td><td>Recent happiness history buffer.</td></tr>
          <tr><td><span class="inline-code">triedRollingLastStand</span></td><td><span class="inline-code">bool</span></td><td>Whether the game already tried a last-stand roll.</td></tr>
          <tr><td><span class="inline-code">succesfullyRolledLastStand</span></td><td><span class="inline-code">bool</span></td><td>Whether the last-stand roll succeeded.</td></tr>
          <tr><td><span class="inline-code">lastLastChanceHappiness</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve involved in last-stand happiness logic.</td></tr>
          <tr><td><span class="inline-code">lastStandTime</span></td><td><span class="inline-code">float</span></td><td>Time spent in or around last-stand state.</td></tr>
          <tr><td><span class="inline-code">dirtyness</span></td><td><span class="inline-code">float</span></td><td>Dirtiness amount.</td></tr>
          <tr><td><span class="inline-code">brainGrowSickness</span></td><td><span class="inline-code">float</span></td><td>Brain-growth sickness amount.</td></tr>
          <tr><td><span class="inline-code">usedNeuralBooster</span></td><td><span class="inline-code">bool</span></td><td>Whether a neural booster has already been used.</td></tr>
          <tr><td><span class="inline-code">forcedSleepQuality</span></td><td><span class="inline-code">SleepQuality?</span></td><td>Optional forced sleep quality override.</td></tr>
          <tr><td><span class="inline-code">clawDamageCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve used for claw damage scaling.</td></tr>
          <tr><td><span class="inline-code">clawHealth</span></td><td><span class="inline-code">float</span></td><td>Current claw health.</td></tr>
          <tr><td><span class="inline-code">clawRegrowTime</span></td><td><span class="inline-code">float</span></td><td>Time for claw regrowth.</td></tr>
          <tr><td><span class="inline-code">heartProg</span></td><td><span class="inline-code">float</span></td><td>Heart cycle progression value.</td></tr>
          <tr><td><span class="inline-code">randomFibrillationVariation</span></td><td><span class="inline-code">float</span></td><td>Extra randomness applied to fibrillation behavior.</td></tr>
          <tr><td><span class="inline-code">tempDiffFromNormal</span></td><td><span class="inline-code">float</span></td><td>Difference from normal body temperature.</td></tr>
          <tr><td><span class="inline-code">skills</span></td><td><span class="inline-code">Skills</span></td><td>Player skill/stat container.</td></tr>
          <tr><td><span class="inline-code">currentClimbable</span></td><td><span class="inline-code">Climbable</span></td><td>Current climbable object reference.</td></tr>
          <tr><td><span class="inline-code">climbableProgress</span></td><td><span class="inline-code">float</span></td><td>Progress through the current climb action.</td></tr>
          <tr><td><span class="inline-code">climbVelocity</span></td><td><span class="inline-code">float</span></td><td>Current climb movement speed.</td></tr>
          <tr><td><span class="inline-code">onHardStimulants</span></td><td><span class="inline-code">bool</span></td><td>Whether hard stimulant effects are active.</td></tr>
          <tr><td><span class="inline-code">usingSleepingBag</span></td><td><span class="inline-code">bool</span></td><td>Whether the player is currently using a sleeping bag.</td></tr>
          <tr><td><span class="inline-code">heartCurveNormal</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Heart rhythm curve for normal state.</td></tr>
          <tr><td><span class="inline-code">heartCurveArrythmia</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Heart rhythm curve for arrhythmia state.</td></tr>
          <tr><td><span class="inline-code">defibShockedFrames</span></td><td><span class="inline-code">int</span></td><td>Short runtime counter for recent defib shock frames.</td></tr>
          <tr><td><span class="inline-code">thirstBloodPressureCurve</span></td><td><span class="inline-code">AnimationCurve</span></td><td>Curve connecting thirst state to blood pressure changes.</td></tr>
          <tr><td><span class="inline-code">hasPulmonaryEmbolism</span></td><td><span class="inline-code">bool</span></td><td>Whether pulmonary embolism is active.</td></tr>
          <tr><td><span class="inline-code">strokeAmount</span></td><td><span class="inline-code">float</span></td><td>Stroke severity amount.</td></tr>
        </tbody>
      </table>
    </section>
  `;
}

function statusesPage(): string {
  return `
    <section class="lesson-card">
      <h2>What statuses are for</h2>
      <p>Use CUCoreLib statuses when your mod wants new per-instance fields on a vanilla <span class="inline-code">Body</span> or <span class="inline-code">Limb</span> without editing the game's classes. The storage is backed by <span class="inline-code">ConditionalWeakTable</span>, but the supported consumer surface is the one-line extension call <span class="inline-code">body.GetStatus&lt;TStatus&gt;()</span> or <span class="inline-code">limb.GetStatus&lt;TStatus&gt;()</span>.</p>
      <pre><code>Body body = PlayerCamera.main.body;
SunstrokeStatus status = body.GetStatus&lt;SunstrokeStatus&gt;();

status.ExposureSeconds += Time.deltaTime;</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Define a body status with new fields</h2>
      <p>Body-first is the simplest pattern. Inherit from <span class="inline-code">BodyStatus</span>, add the fields your mod needs, and optionally add <span class="inline-code">[StatusOptions]</span> when you want a stable save key or want to disable saving for transient data.</p>
      <pre><code>[StatusOptions(Key = "com.yourname.sunstroke", SaveEnabled = false)]
      // The above ^ is optional, it defaults to being saved naturally without need for a key 
      public sealed class SunstrokeStatus : BodyStatus
      {
          public float ExposureSeconds;
          public float CoolingGraceSeconds;
          public bool WarnedPlayer;
      }</code></pre>
      <p>These fields are ordinary C# fields. Think of them as extra body data for your mod.</p>
    </section>

    <section class="lesson-card">
      <h2>Read vanilla fields and update your own state in Update()</h2>
      <p>A common pattern is: read real vanilla body values, update your attached status, then write the gameplay effect back into other vanilla body fields. This keeps your custom logic self-contained while still using the real game simulation.</p>
      <pre><code>[HarmonyPatch(typeof(Body), "Update")]
public static class BodyUpdateStatusPatch
{
    [HarmonyPostfix]
    private static void Postfix(Body __instance)
    {
       
        SunstrokeStatus status = __instance.GetStatus&lt;SunstrokeStatus&gt;();

        if (__instance.temperature &gt; 39.2f)
        {
            status.ExposureSeconds += Time.deltaTime;
        }
            
        if (status.ExposureSeconds &gt; 30f)
        {
            __instance.thirst = Mathf.Max(0f, __instance.thirst - Time.deltaTime * 1.5f);
            __instance.stamina = Mathf.Max(0f, __instance.stamina - Time.deltaTime * 2f);
        }
    }
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Limb statuses use the same pattern</h2>
      <p>Per-limb state works the same way, just from <span class="inline-code">LimbStatus</span> and <span class="inline-code">limb.GetStatus&lt;T&gt;()</span>. Use this when your extra data truly belongs to one wound, one treatment timer, one custom fracture rule, or one custom infection system on a specific limb.</p>
      <pre><code>public sealed class FrostbiteStatus : LimbStatus
      {
          public float ExposureSeconds;
          public bool HasNerveDamage;
      }

      Limb foreleg = body.limbs[2];
      FrostbiteStatus frostbite = foreleg.GetStatus&lt;FrostbiteStatus&gt;();</code></pre>
    </section>

    <section class="lesson-card">
      <h2>When to use statuses instead of other storage</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Use case</th><th>Best place</th></tr>
          </thead>
          <tbody>
            <tr><td>Per-body runtime fields that act like new vanilla body variables</td><td><span class="inline-code">BodyStatus</span></td></tr>
            <tr><td>Per-limb runtime fields tied to one limb instance</td><td><span class="inline-code">LimbStatus</span></td></tr>
            <tr><td>Registration-time metadata for a custom item/tile/building definition</td><td><span class="inline-code">CustomData</span> on the registered definition</td></tr>
            <tr><td>Large mod-wide systems not owned by one body/limb instance</td><td>Your own manager plus <span class="inline-code">SaveRegistry</span> provider if needed</td></tr>
          </tbody>
        </table>
      </div>
      <p>Statuses are meant for instance-attached mutable state, not generic global storage.</p>
      <p>Given that there will only ever be one <span class="inline-code">Body</span>, you technically can? But it's not great to do so regardless. :p</p>
    </section>

    <section class="lesson-card">
      <h2>Moodles</h2>
      <p>A status can optionally provide player-facing UI through <span class="inline-code">MoodleRegistry</span>. If you want the shortest pattern, call <span class="inline-code">MoodleRegistry.AddMoodle(...)</span> directly from your update logic whenever the condition is active.</p>
      <pre><code>if (body.respiratoryRate &lt; 50f)
{
    MoodleRegistry.AddMoodle(
        1,
        "hypoventilation",
        "Hypoventilation",
        "You're breathing unusually slowly.",
        important: true
    );
}</code></pre>
      <p>Use this when the status should show a normal moodle-like warning. For broader moodle behavior and icon expectations, see the <span class="inline-code">Moodles</span> page.</p>
    </section>
  `;
}

function moodlesPage(): string {
  return `
    <section class="lesson-card">
      <h2>Status moodle bridge</h2>
      <p>CUCoreLib appends <span class="inline-code">MoodleRegistry</span> and queues custom entries and feeds them into the real vanilla <span class="inline-code">MoodleManager</span> during its normal update pass.</p>
      <p>That means your custom moodle should behave like any other moodle. It'll needs a valid icon name, a display name, a description, an intensity that matches one of the background slots the game expects, and an <span class="inline-code">important</span> choice that decides whether it belongs in the main row or the side row.</p>
    </section>

    <section class="lesson-card">
      <h2>When to add a moodle</h2>
      
      <p>Use a moodle when the player needs visible feedback about a custom status effect. Keep it lightweight. The normal pattern is to call <span class="inline-code">MoodleRegistry.AddMoodle(...)</span> each update while the condition is active; CUCoreLib keeps it visible briefly and avoids duplicate spam for the same warning.</p>
      
      <pre><code>MoodleRegistry.AddMoodle(
        1,
        someLoadedImageAsset,
        "Lead Poisoning",
        "You're feeling a bit woozy and fatigued...",
        critical: false,
        chippedOnly: false,
    );</code></pre>
    <img src="/images/moodle-ingame.png" alt="Example custom moodle with a custom icon" class="screenshot">
    </section>

    <section class="lesson-card">
      <h2>AddMoodle parameters/fields</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Parameter</th><th>Type</th><th>What it does</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">intensity</span></td><td><span class="inline-code">int</span></td><td>Picks the vanilla moodle background tier. Use the intensity table below for the visual mapping.</td></tr>
            <tr><td><span class="inline-code">icon</span></td><td><span class="inline-code">Sprite</span></td><td>Sprite icon.</td></tr>
            <tr><td><span class="inline-code">iconId</span></td><td><span class="inline-code">string</span></td><td>String-based overload only. Don't use, unless the icon already exists in <span class="inline-code">MoodleManager.icons</span>.</td></tr>
            <tr><td><span class="inline-code">name</span></td><td><span class="inline-code">string</span></td><td>Mouseover title text shown on the moodle. Can be localized.</td></tr>
            <tr><td><span class="inline-code">description</span></td><td><span class="inline-code">string</span></td><td>Mouseover description text shown in the hover/details UI. Can be localized.</td></tr>
            <tr><td><span class="inline-code">critical</span></td><td><span class="inline-code">bool</span></td><td>Adds the vanilla critical overlay glow. This is separate from the intensity number.</td></tr>
            <tr><td><span class="inline-code">chippedOnly</span></td><td><span class="inline-code">bool</span></td><td>If <span class="inline-code">true</span>, the moodle will only be displayed when the player has a chip.</td></tr>
            <tr><td><span class="inline-code">important</span></td><td><span class="inline-code">bool</span></td><td>If <span class="inline-code">false</span>, the moodle will be displayed in the unimportant hidden-ish section to the right.</td></tr>
            <tr><td><span class="inline-code">key</span></td><td><span class="inline-code">string</span></td><td>Optional stable queue key. Supply this when a moodle changes severity over time. (Where you only want one moodle with the same key active at a time.)</td></tr>
            <tr><td><span class="inline-code">holdSeconds</span></td><td><span class="inline-code">float</span></td><td>How long the queued moodle stays visible without being refreshed. The default is <span class="inline-code">0.75f</span>.</td></tr>
          </tbody>
        </table>
      </div>
      <p>If you want animated icons instead, <span class="inline-code">AddAnimatedMoodle(...)</span> swaps the icon argument for <span class="inline-code">animationId</span> but otherwise follows the same parameter meanings.</p>
    </section>

    <section class="lesson-card">
    <p>The <span class="inline-code">intensity</span> value picks the same vanilla background frame used by the base game.</p>

    <div class="table-wrap">
        <table class="field-table moodle-intensity-table">
          <thead>
            <tr><th>Intensity</th><th>Visual</th><th>Notes</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">0</span></td><td><span class="moodle-swatch"><img src="/images/mood1.png" alt="Intensity 0 moodle background"></span></td><td>Lowest standard tier.</td></tr>
            <tr><td><span class="inline-code">1</span></td><td><span class="moodle-swatch"><img src="/images/mood2.png" alt="Intensity 1 moodle background"></span></td><td>Low warning tier.</td></tr>
            <tr><td><span class="inline-code">2</span></td><td><span class="moodle-swatch"><img src="/images/mood3.png" alt="Intensity 2 moodle background"></span></td><td>Moderate warning tier.</td></tr>
            <tr><td><span class="inline-code">3</span></td><td><span class="moodle-swatch"><img src="/images/mood4.png" alt="Intensity 3 moodle background"></span></td><td>Strong warning tier.</td></tr>
            <tr><td><span class="inline-code">4</span></td><td><span class="moodle-swatch"><img src="/images/mood5.png" alt="Intensity 4 moodle background"></span></td><td>Used by minor positive/sleep moodles.</td></tr>
            <tr><td><span class="inline-code">5</span></td><td><span class="moodle-swatch"><img src="/images/mood6.png" alt="Intensity 5 moodle background"></span></td><td>Higher positive or special tier.</td></tr>
            <tr><td><span class="inline-code">6</span></td><td><span class="moodle-swatch"><img src="/images/mood7.png" alt="Intensity 6 moodle background"></span></td><td>Strong positive or special tier.</td></tr>
            <tr><td><span class="inline-code">7</span></td><td><span class="moodle-swatch"><img src="/images/mood8.png" alt="Intensity 7 moodle background"></span></td><td>Special positive standard tier. (Life Support)</td></tr>
            <tr><td><span class="inline-code">8</span></td><td><span class="moodle-swatch"><img src="/images/moodlast.png" alt="Intensity 8 moodle background"></span></td><td>Special last-tier background used by rare moodles. (Last Stand)</td></tr>
            <tr><td><span class="inline-code">critical: true</span></td><td><span class="moodle-swatch"><img src="/images/moodleglow.png" scale="4" alt="Moodle high intensity glow"></span></td><td>Critical is an overlay behavior, not a separate intensity number</td></tr>
            
          </tbody>
        </table>
      </div>
    </section>

  `;
}

function placeholderPage(pageId: PageId): string {
  const page = pages.find((item) => item.id === pageId);
  const label = page?.label ?? "This API";
  return `
    <section class="lesson-card">
      <h2>${escapeHtml(label)}</h2>
      <p>This guide is planned, but it has not been written yet.</p>
    </section>
  `;
}

function welcomePage(): string {
  return `
    <section class="lesson-card">
      <h2>What is CUCoreLib?</h2>
      <p>CUCoreLib is a BepInEx + Harmony dependency mod for Casualties Unknown. It is not the game source and it is not a replacement framework. 
      
      Rather, It gives other mods a common place to register content and reuse small runtime helpers.</p>
      <p>If you were to make a mod for the first time to introduce a custom item, you would have to write boilerplate code for everything that may not be obvious. For instance, trader inventories, lootpools, saving said item. Heck, even recipes for said item.</p>
      <p>CUCoreLib provides a shared library of content registration and runtime helpers so that your mod can focus on the fun part: designing new items, recipes, and assets.</p>
    </section>
    <section class="lesson-card">
      <h2>How to read these docs</h2>
      <p>Use the dropdown above to move between APIs. The left side explains the API; the right side shows the generated C# shape. Some code identifiers have hover notes for field behavior and common traps.</p>
      <pre><code>
  Logger.LogInfo("Hey! I'm an in-line code block.");
  Logger.LogInfo("I'll tell you how this translates into the code on the right, ");
  Logger.LogInfo("and any special things to look out for when using it in your mod.");
      </code></pre>
      
      <p>As always, this documentation is a work in progress. </p>
      <p>If you have suggestions, PRs are available in <a href="https://github.com/jimmyking9999999/CUCoreLib" target="_blank">the github</a></p>
      <p>Questions? Check out the <a href="https://discord.gg/FuuEDPkQT" target="_blank">Discord</a> <a href="https://discord.com/channels/1360913627288178788/1506616266713202828" target="_blank">thread</a></p>
    </section>
    
    <section class="lesson-card"> 
      <h2>Next Steps</h2>
      <p>Use the navigation above to explore the APIs. The next logical step is the "Unity + C# TL;DR" page, which gives a quick mental model for how to write a BepInEx plugin for Casualties Unknown.</p>
      <p>Feel free to skip this if you're already familiar with BepInEx mod structure and Unity basics. Jump to the "Setup" page when you're ready to start writing code.</p>
      </section>

    <section class="lesson-card">
      <h2>Completely new to Unity and C#?</h2>
      <p>This guide may not be for you.</p>
      <p>Consider watching <a href="https://www.youtube.com/watch?v=Zrt0iEBBkRM">this tutorial</a> to get a feel for the basics.</p>
    </section>
  `;
}

function unityCsharpPage(): string {
  return `
    <section class="lesson-card">
      <h2>The Unity mental model</h2>
      <p>Casualties Unknown is a Unity game. That means the running game is mostly a collection of loaded <span class="inline-code">Scene</span> objects, each containing <span class="inline-code">GameObject</span> objects. A GameObject is just a container until you attach <span class="inline-code">Component</span> objects to it.</p>
      <p>Every GameObject has a <span class="inline-code">Transform</span>, which stores position, rotation, scale, and parent/child hierarchy. Most visible things in the game are GameObjects with components such as renderers, colliders, UI scripts, inventory scripts, or custom gameplay scripts.</p>
      <pre><code>GameObject player = PlayerCamera.main.body.gameObject;
Transform playerTransform = player.transform;
Vector3 position = playerTransform.position;</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Scripts are components</h2>
      <p>A normal Unity gameplay script usually inherits from <span class="inline-code">MonoBehaviour</span>. MonoBehaviours are components, so they live on GameObjects and receive Unity lifecycle methods like <span class="inline-code">Awake</span>, <span class="inline-code">Start</span>, and <span class="inline-code">Update</span>.</p>
      <p>In a mod, you do not usually create a Unity project and press Play. You compile C# into a DLL, BepInEx loads that DLL into the already-running game, and your code interacts with Unity objects the game created.</p>
      <p><a href="https://www.youtube.com/watch?v=GSDJQVQnbSA&t=91s">What is a MonoBehaviour?</a></p>
      <pre><code>public sealed class SimpleUnityExample : MonoBehaviour
{
    private void Awake() { }
    private void Start() { }
    private void Update() { }
}</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Lifecycle basics</h2>
      <p><span class="inline-code">Awake</span> runs when Unity creates the object. <span class="inline-code">Start</span> runs before the first frame update, once the component is enabled. <span class="inline-code">Update</span> runs every rendered frame. <span class="inline-code">FixedUpdate</span> runs on a fixed timestep and is mostly for physics-style work.</p>
      <p>For modding, this matters because not every game object exists when your plugin loads. Plugin <span class="inline-code">Awake</span> is great for registering content, config, and patches. World/player logic often needs to wait until the gameplay scene exists.</p>
      <pre><code>private void Update()
{
    elapsed += Time.deltaTime;
}</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Time, frames, and why Update is expensive</h2>
      <p>Unity games do not run code once and stop. Frame methods run constantly. If a mod does slow searches, reflection, file reads, or allocations in <span class="inline-code">Update</span>, the cost repeats every frame.</p>
      <p>I would know, I had 17 bug reports for QoL: Unknown due to this...</p>
      <p><span class="inline-code">Time.deltaTime</span> is the time in seconds since the previous frame. If code means "one unit per second" instead of "one unit per frame", multiply by <span class="inline-code">deltaTime</span>.</p>
      <pre><code>// Good shape for a timer in Update:
timer += Time.deltaTime;
if (timer > 1f)
{
    timer = 0f;
}</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Prefabs and assets</h2>
      <p>A <span class="inline-code">Prefab</span> is a reusable GameObject template. In normal Unity development, prefabs live in the project. In modding, you are usually reading, cloning, or referencing prefabs and assets that already exist in the compiled game.</p>
      <p>Making a new prefab from scratch is very hard without the Unity editor.</p>
      <p>For Casualties Unknown, decompiled code and runtime inspection help you learn what prefabs, components, fields, and method names the game already uses.</p>
      <p> Mod code usually instantiates or modifies runtime objects. It does not edit the original Unity project prefab.</p>
      <pre><code>
// The following code makes a UnityEngine Object in the game world, using the prefab "glowplantfruit" as a template.

Instantiate(Resources.Load("glowplantfruit"), transform.position, transform.rotation);

/*
Resources.Load("glowplantfruit") -> Loads prefab
Instantiate(prefab, position, rotation) -> Clones prefab into the world as a GameObject which then can be interacted with.
*/
</code></pre>

    </section>
    <section class="lesson-card">
      <h2>Decompiling for values</h2>
      <p>Use <a href="https://github.com/dnSpyEx/dnSpy/releases" target="_blank" rel="noopener">dnSpyEx</a> when you need to inspect C# code values, field names, method names, item IDs, or how an existing mechanic works. You are reading the compiled game code as reference. Do not edit the game DLL.</p>
      <p>The file is at:</p>
      <pre><code>C:\\Program Files (x86)\\Steam\\steamapps\\common\\Casualties Unknown Playtest\\CasualtiesUnknown_Data\\Managed\\Assembly-CSharp.dll</code></pre>
      <p>Open <span class="inline-code">Assembly-CSharp.dll</span> in dnSpyEx. That is where all of the game's scripts live.</p>
      <img src="images/dnspy-ex.png" alt="dnSpyEx showing decompiled code for CoilScript.cs" class="screenshot">
      <p>The decompiled code showing <span class="inline-code">CoilScript.cs</span>'s <span class="inline-code">Shock()</span> method. Ouch!</p>
    </section>
    <details closed>
      <summary>dnSpyEx searches worth learning</summary>
      <div class="details-body">
        <p><span class="inline-code">Ctrl + Shift + K</span> opens the search window. Search "All of the Above" for function/class names, or "Number/String" when you know a literal value shown in-game. For example, searching <span class="inline-code">loot amount</span> can lead you toward <span class="inline-code">PreRunScript</span>.</p>
        <p><span class="inline-code">Ctrl + Shift + R</span> analyzes references. If you are reading <span class="inline-code">Body.cs</span> and find a field like thirst, reference analysis helps find everything that reads or changes it.</p>
      </div>
    </details>
    <details>
      <summary>Important scripts</summary>
      <div class="details-body">
        <ul>
          <li><span class="inline-code">Item.cs</span>: item list and item properties.</li>
          <li><span class="inline-code">Body.cs</span>: movement, actions, and most body/player variables.</li>
          <li><span class="inline-code">Liquids.cs</span>: liquids and their effects.</li>
        </ul>
      </div>
    </details>
    <section class="lesson-card">
      <h2>Extracting images, sounds, and assets</h2>
      <p>Use <a href="https://github.com/AssetRipper/AssetRipper" target="_blank" rel="noopener">AssetRipper</a> when you need the game's textures, sounds, sprites, or other Unity assets as reference material.</p>
      <p>Run the AssetRipper GUI, select your installed Casualties Unknown folder, and extract <span class="inline-code">all content</span>. For this workflow, do not extract as a Unity project.</p>
      <p>After extraction, browse the output folders. Images are commonly under <span class="inline-code">Texture2D</span>, and sounds are commonly under <span class="inline-code">Sounds</span>.</p>
      
    </section>
    <section class="lesson-card">
      <h2>How BepInEx fits in</h2>
      <p>A BepInEx mod is a compiled C# DLL placed under <span class="inline-code">BepInEx/plugins</span>. BepInEx loads that DLL into the game process. Your plugin entry class inherits from <span class="inline-code">BaseUnityPlugin</span>, which gives you Unity lifecycle behavior plus BepInEx helpers like <span class="inline-code">Logger</span>.</p>
      <p>CUCoreLib is a BepInEx plugin dependency. It gives you shared registration APIs and runtime helpers, but it does not change how you write your plugin or scripts.</p>
    </section>
    <section class="lesson-card">
      <h2>Recommended references</h2>
      <ul>
        <li><a href="https://docs.unity3d.com/2023.1/Documentation/ScriptReference/MonoBehaviour.html" target="_blank" rel="noopener">Unity MonoBehaviour scripting API</a></li>
        <li><a href="https://docs.unity.cn/Documentation/Manual/Prefabs.html" target="_blank" rel="noopener">Unity Manual: Prefabs</a></li>
        <li><a href="https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html" target="_blank" rel="noopener">BepInEx: Writing a basic plugin</a></li>
      </ul>
    </section>
  `;
}

function setupPage(): string {
  return `
    <section class="lesson-card">
      <h2>Use ScavTemplate as the project shell</h2>
      <p><span class="inline-code">ScavTemplate</span> gives you the BepInEx project shape so you are not starting from an empty C# library. CUCoreLib is added on top as a dependency.</p>
      <ol>
        <li>Open <a href="https://github.com/05126619z/ScavTemplate" target="_blank" rel="noopener">05126619z/ScavTemplate</a>.</li>
        <li>Create a new repository from the template or clone/download it.</li>
        <pre><code>git clone https://github.com/05126619z/ScavTemplate</code></pre>
        <li>Open the folder in whichever C# editor you prefer, or stay in the terminal and build with <span class="inline-code">dotnet</span>.</li>
      </ol>
      <p><span class="inline-code">Template.sln</span> works in Visual Studio or Rider, but it is not required. VS Code users can open the folder directly once the .NET SDK and C# tooling are installed.</p>
    </section>

    <section class="lesson-card">
      <h2>Installing CUCoreLib</h2>
      <p><span class="inline-code">CUCoreLib</span> is a library that provides a ton of useful functionality for BepInEx modding.</p>
      <ol>
        <li>Open <a href="https://github.com/jimmyking9999999/CUCoreLib/releases/" target="_blank" rel="noopener">jimmyking9999999/CUCoreLib/releases/</a>.</li>
        <li>Download the latest <span class="inline-code">CUCoreLib.dll</span> and place it into your BepInEx\\plugins folder.</li>
        <pre><code>C:\\Program Files (x86)\\Steam\\steamapps\\common\\Casualties Unknown Playtest\\BepInEx\\plugins</code></pre>
        <li>Add the DLL as a reference in your mod project. The most universal way is to edit the <span class="inline-code">.csproj</span> file directly.</li>
      </ol>
      <pre><code>&lt;ItemGroup&gt;
  &lt;Reference Include="CUCoreLib"&gt;
    &lt;HintPath&gt;..\\..\\BepInEx\\plugins\\CUCoreLib.dll&lt;/HintPath&gt;
  &lt;/Reference&gt;
&lt;/ItemGroup&gt;</code></pre>
      <p>If your project lives somewhere else, change the <span class="inline-code">HintPath</span> so it points to the DLL you downloaded.</p>
      <p>Editor shortcuts if you prefer the UI:</p>
      <ul>
        <li>Visual Studio: right-click the project, choose <span class="inline-code">Add &gt; Reference</span>, then browse to <span class="inline-code">CUCoreLib.dll</span>.</li>
        <li>Rider: open the project file editor or project settings and add a local assembly reference.</li>
        <li>VS Code: edit the <span class="inline-code">.csproj</span> directly. That is the normal path there.</li>
      </ul>
      <img src="images/assembly-location.png" alt="Add Reference screenshot" class="screenshot" />
    </section>

    <section class="lesson-card">
      <h2>Where the code goes</h2>
      <p>The code panel on the right shows the classic plugin shape CUCoreLib expects</p>
      <p>In <code>plugins.cs</code>, copy-paste the right side code and change the namespace, class name, and BepInPlugin attributes to match your mod. </p>

      <p>This line must be present for your mod to use CUCoreLib:</p>
      <pre><code>[BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
</code></pre>

      ${docsVideo(externalVideoUrls.setup, "/videos/setup.mp4", "screenshot docs-video")}
    </section>
    <section class="lesson-card">
      <h2>Testing the mod</h2>
      <p>The ScavTemplate deploys your DLL when it builds. </p>
      <ol>
          <li>Build the project in Visual Studio with <span class="inline-code">Build &gt; Build Solution</span> (Ctrl + Shift + B), or run <span class="inline-code">dotnet build</span> from the project folder via the terminal.</li>
        <li>Start the game. BepInEx will load your mod from <span class="inline-code">BepInEx/plugins/&lt;ProjectName&gt;/&lt;ProjectName&gt;.dll</span>.</li>
      </ol>
      <pre><code>dotnet build</code></pre>
      <p>If you are using an IDE, it is just calling the same build underneath. Visual Studio and Rider can both build the solution normally after the reference is added.</p>
      <p>The compiled DLL also exists in your project output folder, <span class="inline-code">bin/Debug/net48/&lt;ProjectName&gt;.dll</span>. </p>
      <p>Once the basic build works, the <a href="/docs/debug-testing/">Debugging / Testing</a> page covers faster iteration loops, hot reload, and common config toggles.</p>

      <p>If you are getting a 'skipping due to missing net.cucorelib' error, ensure that the CUCoreLib.dll is placed in the correct BepInEx\\plugins folder and that the reference is properly added to your project.</p>
    </section>

    <section class="lesson-card">
      <h2>Viewing BepInEx logs</h2>
      <p>Logs are how you confirm the mod loaded, spot missing references, and read your own <span class="inline-code">Logger.LogInfo()</span> messages. If the console or log file is disabled, you must enable it in the BepInEx config.</p>
      <ol>
        <li>Open <span class="inline-code">BepInEx/config/BepInEx.cfg</span> inside the game folder.</li>
        <li>Find <span class="inline-code">[Logging.Console]</span> and set <span class="inline-code">Enabled = true</span> to show the live console window.</li>
        <li>Find <span class="inline-code">[Logging.Disk]</span> and set <span class="inline-code">Enabled = true</span> to write <span class="inline-code">BepInEx/LogOutput.log</span>.</li>
        <li>Launch the game and look for your plugin's startup line, such as <span class="inline-code">Plugin ModName is loaded!</span>.</li>
      </ol>
      <pre><code>[Logging.Console]

## Enables showing a console for log output.
# Setting type: Boolean
# Default value: false
Enabled = true

[Logging.Disk]

## Include unity log messages in log file output.
# Setting type: Boolean
# Default value: false
WriteUnityLog = false</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Why is my Start()/Update() not working?</h2>
      <p>Casualties: Unknown may have destroyed your plugin's game object. There is a BepInEx configuration option that can fix this</p>
      <p>BepInEx -&gt; Config -&gt; bepinex.cfg -&gt; <span class="inline-code">HideManagerGameObject = true</span></p>
      <pre><code>[Chainloader]
      ## If enabled, hides BepInEx Manager GameObject from Unity.
      # Setting type: Boolean
      # Default value: false
      HideManagerGameObject = true</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Why am I missing a using directive or an assembly reference?</h2>
      <p>If Visual Studio says it cannot find CUCoreLib types, the template usually still points at the wrong game install path. Update the <span class="inline-code">vars.targets</span> file inside your template folder so it points to the folder where Casualties Unknown Demo is installed.</p>
      <p>After saving <span class="inline-code">vars.targets</span>, restart Visual Studio if it was already open so it reloads the updated reference path.</p>
    </section>

    <section class="lesson-card">
      <h2>CUCoreLib using statements</h2>
      <p>After referencing CUCoreLib, you will need some namespaces imported at the top of your plugin files. You do not need every namespace in every file. Rather, these just make CUCoreLib classes available without typing their full namespace. Very neat, very handy.</p>
      <p>The supported public API for downstream mods is the documented surface in <span class="inline-code">CUCoreLib.Data</span>, <span class="inline-code">CUCoreLib.Registries</span>, <span class="inline-code">CUCoreLib.Saving</span>, and the intentionally public helpers documented here such as <span class="inline-code">AssetLoader</span>, <span class="inline-code">CUCoreUtils</span>, and <span class="inline-code">CustomInstantiate</span>. Types outside that documented surface are implementation details and may change without compatibility guarantees.</p>
      <pre><code>using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using CUCoreLib.Data;
using CUCoreLib.Saving;</code></pre>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Namespace</th>
              <th>Use it for</th>
              <th>Common early examples</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">CUCoreLib.Helpers</span></td><td>Selected public helper APIs.</td><td><span class="inline-code">AssetLoader</span>, <span class="inline-code">CUCoreUtils</span>, <span class="inline-code">CustomInstantiate</span>.</td></tr>
            <tr><td><span class="inline-code">CUCoreLib.Registries</span></td><td>Registering content or commands.</td><td><span class="inline-code">ItemRegistry</span>, <span class="inline-code">RecipeRegistry</span>, <span class="inline-code">LocaleRegistry</span>, <span class="inline-code">ConsoleCommandRegistry</span>.</td></tr>
            <tr><td><span class="inline-code">CUCoreLib.Data</span></td><td>Extra data types for advanced registration.</td><td><span class="inline-code">CustomItemInfo</span>, container properties, battery properties, custom recipe data.</td></tr>
            <tr><td><span class="inline-code">CUCoreLib.Saving</span></td><td>Save provider interfaces and save registration.</td><td><span class="inline-code">ICustomSaveProvider</span>, <span class="inline-code">SaveRegistry</span>.</td></tr>
          </tbody>
        </table>
      </div>
      <p>For most beginner item/recipe mods, <span class="inline-code">Helpers</span> and <span class="inline-code">Registries</span> are the important two. Avoid depending on patch classes or undocumented helper internals, because those are free to change as CUCoreLib evolves.</p>
    </section>
    
  `;
}

function harmony0Page(): string {
  return `
    <section class="lesson-card">
      <h2>What Harmony does</h2>
      <p><span class="inline-code">Harmony</span> lets a BepInEx mod attach code to methods that already exist in the game. You are not editing <span class="inline-code">Assembly-CSharp.dll</span> on disk. You are telling Harmony: when the game calls this method, also run my method before it, after it, or around it.</p>
      <p>That is how CUCoreLib and most Casualties Unknown mods add behavior. You find the target method in dnSpyEx or the decompiled reference, then write a small patch class that points at that exact type and method name.</p>
      <pre><code>[HarmonyPatch(typeof(Body), "Eat")] // Look on the right pane for an example</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Prefix patches</h2>
      <p>A <span class="inline-code">Prefix</span> runs before the original game method. Use it when you need to validate inputs, record state before vanilla changes it, tweak arguments, or optionally stop the original method from running.</p>
      <p>If a prefix returns <span class="inline-code">void</span>, the original method keeps running. If it returns <span class="inline-code">bool</span>, returning <span class="inline-code">false</span> skips the original method. Skipping vanilla is powerful, but it is also where bugs breed, so only do it when you are intentionally replacing the method.</p>
      <pre><code>[HarmonyPrefix]
private static void Prefix(Body __instance)
{
    // Make the player happier every time before they eat, without skipping the normal Eat code.
    __instance.happiness += 1f;
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Postfix patches</h2>
      <p>A <span class="inline-code">Postfix</span> runs after the original game method finishes. Use it when vanilla should do its normal work first and your mod only needs to react to the final state.</p>
      <p>Postfixes are usually safer for additive behavior: logging, registering extra content after vanilla setup, refreshing UI after a list rebuild, or changing a result after the original method calculated it.</p>
      <pre><code>[HarmonyPostfix]
private static void Postfix(Body __instance)
{
    Plugin.Logger.LogInfo($"Hunger is now {__instance.hunger}");
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>References and special parameters</h2>
      <p>Harmony matches patch parameters by name. Normal parameter names match the original method's parameters. Special names give you access to extra pieces of the call.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Parameter</th>
              <th>What it means</th>
              <th>Common use</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">__instance</span></td><td>The object the original method is running on.</td><td>Read or change fields on a non-static class instance.</td></tr>
            <tr><td><span class="inline-code">__result</span></td><td>The value the original method returns.</td><td>Read it in a postfix, or set it when replacing a method.</td></tr>
            <tr><td><span class="inline-code">ref hungerAmount</span></td><td>A writable reference to an original argument named <span class="inline-code">hungerAmount</span>.</td><td>Change an argument before vanilla uses it.</td></tr>
            <tr><td><span class="inline-code">__state</span></td><td>A value passed from your prefix to your postfix.</td><td>Compare before and after values without storing global data.</td></tr>
          </tbody>
        </table>
      </div>
      <p>The <span class="inline-code">ref</span> keyword matters. Without it, you receive a copy. With it, your patch can write back to the original argument or result.</p>
      <pre><code>[HarmonyPrefix]
private static void Prefix(Body __instance, ref float hungerAmount)
{
    // Make all foods half as filling (if >6 satiation), before running the normal Eat code.
    hungerAmount -= Mathf.Max(3f, hungerAmount * 0.5f);
}</code></pre>
      </section>

    <section class="lesson-card">
      <h2>Closing Notes</h2>
      <ul>
        <li>Make sure the decompiled game is up to date.</li>
        <li>BepInEx's Chainloader can skip your prefixes if there are more then one patch for the same method, i.e., if multiple mods try to patch the same method.</li>
        <li>Keep patch classes <span class="inline-code">static</span> unless you have a specific reason not to.</li>
        <li>Prefer postfixes for additive behavior and prefixes for input changes or intentional overrides.</li>
        <li>Try to avoid patching high-frequency methods like <span class="inline-code">Update</span> unless the work is tiny and carefully guarded.</li>
      </ul>
    </section>
  `;
}

function itemPage(): string {
  return `
    <section class="lesson-card">
      <p>It's finally time to make the first modded thing! What better way to start than by adding a new item to the game? We'll be adding a Sunpear, in honor of the mushpear having its name changed.</p>
    </section>

    <section class="lesson-card">
      <h2>ItemRegistry.Register</h2>
      <p>CUCoreLib's item API wraps the game's normal <span class="inline-code">ItemInfo</span>. Give the item a stable lowercase ID, fill the vanilla stat block, then pass a sprite loaded through <span class="inline-code">AssetLoader</span>.</p>
      <p>The item ID is the value that recipes, console spawning, save/load fallback, and locale lookup will use. Changing it later is a breaking change for saves and dependent recipes.</p>
      <pre><code>Sprite sunpearSprite = AssetLoader.LoadEmbeddedSprite("Images.sunpear.png");

ItemRegistry.Register(
    "sunpear",
    new ItemInfo
    {
        fullName = "Sunpear",
        description = "A pale yellow fruit. Probably edible.",
        category = "food",
        weight = 0.4f,
        
        value = 4,
        tags = "cangetwet",
        decayMinutes = 180f,
        rec = new Recognition(2)
    },
    sunpearSprite
);</code></pre>
    <img src="images/sunpear-ingame.png" alt="In-game screenshot of the sunpear item" class="screenshot">
    </section>
    <section class="lesson-card">
      <h2>Arguments and overloads</h2>
      <p>The short function is the one most mods should start with:</p>
      <pre><code>ItemRegistry.Register(string id, ItemInfo info, Sprite icon, int spawnFrequency = 1);</code></pre>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Argument</th>
              <th>Type</th>
              <th>What it needs</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">id</span></td><td><span class="inline-code">string</span></td><td>Stable item ID, usually lowercase with no spaces. Recipes, console spawn, save/load fallback, locale keys, and sprite cache lookup use this value.</td></tr>
            <tr><td><span class="inline-code">info</span></td><td><span class="inline-code">ItemInfo</span></td><td>The vanilla stat block. Fill fields like <span class="inline-code">fullName</span>, <span class="inline-code">description</span>, <span class="inline-code">category</span>, <span class="inline-code">weight</span>, <span class="inline-code">value</span>, <span class="inline-code">tags</span>, and use actions here.</td></tr>
            <tr><td><span class="inline-code">icon</span></td><td><span class="inline-code">Sprite</span></td><td>Inventory/item icon. Load it with <span class="inline-code">AssetLoader.LoadEmbeddedSprite</span> or <span class="inline-code">LoadSpriteFromPluginFolder</span>.</td></tr>
            <tr><td><span class="inline-code">spawnFrequency</span></td><td><span class="inline-code">int</span></td><td>Optional. Loot pool weight for this item. CUCoreLib adds the item ID to its <span class="inline-code">category</span> loot pool this many times. <span class="inline-code">0</span> means craft-only/no loot injection. Default is <span class="inline-code">1</span>, so most items can omit it.</td></tr>
          </tbody>
        </table>
      </div>
      <p>When you need CUCoreLib-only fields, keep the same register call and swap <span class="inline-code">ItemInfo</span> for <span class="inline-code">CustomItemInfo</span>. This avoids a second "definition" object while still giving you extras like worn sprites, custom data, containers, batteries, sprite sizing controls, and spawn weight.</p>
      <pre><code>ItemRegistry.Register(
    "sunpear",
    new CustomItemInfo
    {
        // CustomItemInfo still has every normal ItemInfo field.
        fullName = "Sunpear",
        description = "A pale yellow fruit. Probably edible.",
        category = "food",
        weight = 0.4f,
        value = 4,
        tags = "cangetwet",
        decayMinutes = 180f,
        rec = new Recognition(2),

        // CUCoreLib-only extras live next to the vanilla fields.
        SpriteScale = 1.0f,
        SpriteScaleDimensions = (14f, 14f, true),
        SpawnFrequency = 1,
        CustomData =
        {
            ["sourceMod"] = "My First Mod"
        }
    },
    sunpearSprite
);</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Finding values from game code</h2>
      <p>To find values for vanilla game items, open the decompiled <span class="inline-code">Item.cs</span> file and look inside <span class="inline-code">SetupItems()</span>. The game creates its normal <span class="inline-code">ItemInfo</span> entries there.</p>
      <p>You can usually copy the <span class="inline-code">new ItemInfo { ... }</span> block from <span class="inline-code">SetupItems()</span> into your own <span class="inline-code">ItemRegistry.Register(...)</span> call 1:1, and it will work. After that, adjust the fields you want to change for your modded item.</p>
      <img src="images/decompiled-item-cs.png" alt="Decompiled Item.cs SetupItems method showing vanilla ItemInfo values." class="screenshot">
    </section>
    <section class="lesson-card">
      <h2>Fields</h2>
      <p>A field is a named value stored on an object. When you write <span class="inline-code">new ItemInfo { weight = 0.4f }</span>, you are setting the <span class="inline-code">weight</span> field on that <span class="inline-code">ItemInfo</span> instance before CUCoreLib registers it.</p>
      <p>In the decompiled game, <span class="inline-code">Item.cs</span> builds <span class="inline-code">Item.GlobalItems</span> from <span class="inline-code">ItemInfo</span> objects. The table below is based on the decompiled <span class="inline-code">ItemInfo.cs</span> fields that those vanilla items use.</p>
      <p>For more context, recall the <span class="inline-code">DnSpyEX</span> guide a few sections back to decompile the game.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Field</th>
              <th>Type</th>
              <th>What it controls</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">fullName</span></td><td><span class="inline-code">string</span></td><td>Display name before wet/condition formatting.</td></tr>
            <tr><td><span class="inline-code">description</span></td><td><span class="inline-code">string</span></td><td>Inventory/tooltip description text.</td></tr>
            <tr><td><span class="inline-code">category</span></td><td><span class="inline-code">string</span></td><td>Broad item grouping used by game systems and UI/category logic.</td></tr>
            <tr><td><span class="inline-code">slotRotation</span></td><td><span class="inline-code">float</span></td><td>Rotation used when displaying or slotting the item.</td></tr>
            <tr><td><span class="inline-code">usable</span></td><td><span class="inline-code">bool</span></td><td>Whether the item can be used directly.</td></tr>
            <tr><td><span class="inline-code">usableOnLimb</span></td><td><span class="inline-code">bool</span></td><td>Whether the item can target a body limb.</td></tr>
            <tr><td><span class="inline-code">rotSpeed</span></td><td><span class="inline-code">float</span></td><td>Rotation speed behavior for usable/active items.</td></tr>
            <tr><td><span class="inline-code">useAction</span></td><td><span class="inline-code">ItemInfo.Use</span></td><td>Delegate called when the item is used on the body.</td></tr>
            <tr><td><span class="inline-code">useLimbAction</span></td><td><span class="inline-code">ItemInfo.UseLimb</span></td><td>Delegate called when the item is used on a limb.</td></tr>
            <tr><td><span class="inline-code">destroyAtZeroCondition</span></td><td><span class="inline-code">bool</span></td><td>Whether the item should be destroyed when condition reaches zero.</td></tr>
            <tr><td><span class="inline-code">weight</span></td><td><span class="inline-code">float</span></td><td>Inventory/container weight contribution.</td></tr>
            <tr><td><span class="inline-code">scaleWeightWithCondition</span></td><td><span class="inline-code">bool</span></td><td>Whether weight scales down as condition decreases.</td></tr>
            <tr><td><span class="inline-code">onlyHoldInHands</span></td><td><span class="inline-code">bool</span></td><td>Restricts handling/equipping behavior to hands.</td></tr>
            <tr><td><span class="inline-code">autoAttack</span></td><td><span class="inline-code">bool</span></td><td>Whether held use/attack behavior repeats automatically.</td></tr>
            <tr><td><span class="inline-code">usableWithLMB</span></td><td><span class="inline-code">bool</span></td><td>Whether left mouse button can use the item.</td></tr>
            <tr><td><span class="inline-code">wearable</span></td><td><span class="inline-code">bool</span></td><td>Whether the item can be worn.</td></tr>
            <tr><td><span class="inline-code">wearableCanBeHeld</span></td><td><span class="inline-code">bool</span></td><td>Whether a wearable can also be held like a normal item.</td></tr>
            <tr><td><span class="inline-code">desiredWearLimb</span></td><td><span class="inline-code">string</span></td><td>Preferred limb/body area for wearable placement.</td></tr>
            <tr><td><span class="inline-code">wearSlotId</span></td><td><span class="inline-code">string</span></td><td>Wear slot identifier used by equipment logic.</td></tr>
            <tr><td><span class="inline-code">wearableArmor</span></td><td><span class="inline-code">float</span></td><td>Armor/protection value when worn.</td></tr>
            <tr><td><span class="inline-code">wearableIsolation</span></td><td><span class="inline-code">float</span></td><td>Isolation/insulation-style stat when worn.</td></tr>
            <tr><td><span class="inline-code">wearableHitDurabilityLossMultiplier</span></td><td><span class="inline-code">float</span></td><td>Multiplier for condition loss when the wearable takes hits.</td></tr>
            <tr><td><span class="inline-code">jumpHeightMultChange</span></td><td><span class="inline-code">float</span></td><td>Jump-height multiplier change from worn/active item effects.</td></tr>
            <tr><td><span class="inline-code">combineable</span></td><td><span class="inline-code">bool</span></td><td>Whether the item participates in combine-style interactions.</td></tr>
            <tr><td><span class="inline-code">ignoreDepression</span></td><td><span class="inline-code">bool</span></td><td>Whether use behavior ignores depression-related restrictions/effects.</td></tr>
            <tr><td><span class="inline-code">value</span></td><td><span class="inline-code">int</span></td><td>Base value used by item value calculations.</td></tr>
            <tr><td><span class="inline-code">wearableVisualOffset</span></td><td><span class="inline-code">int</span></td><td>Visual offset for wearable rendering. Defaults to <span class="inline-code">5</span>.</td></tr>
            <tr><td><span class="inline-code">tags</span></td><td><span class="inline-code">string</span></td><td>Comma-separated vanilla tags. See mouseover text for more information.</td></tr>
            <tr><td><span class="inline-code">decayInfo</span></td><td><span class="inline-code">byte</span></td><td>Bit flags from <span class="inline-code">ItemInfo.DecayType</span> for decay rules. <span class="inline-code">16</span> is <span class="inline-code">BatteryDecay</span>, which drains battery charge instead of normal condition decay.</td></tr>
            <tr><td><span class="inline-code">decayMinutes</span></td><td><span class="inline-code">float</span></td><td>Minutes used by condition decay logic.</td></tr>
            <tr><td><span class="inline-code">rec</span></td><td><span class="inline-code">Recognition</span></td><td>Recognition/identification data. Defaults to <span class="inline-code">new Recognition(2)</span>.</td></tr>
            <tr><td><span class="inline-code">qualities</span></td><td><span class="inline-code">List&lt;CraftingQuality&gt;</span></td><td>Crafting qualities this item can satisfy in recipes. Use <span class="inline-code">CUCoreUtils.CreateCraftingQuality(...)</span> when you want to attach a fallback display name for a custom quality ID.</td></tr>
          </tbody>
        </table>
      </div>
      <p>Tip: hover field names in the generated code for quick notes on common fields like <span class="inline-code">category</span>, <span class="inline-code">decayMinutes</span>, and <span class="inline-code">tags</span>.</p>
    </section>
    <section class="lesson-card">
      <h2>Use actions</h2>
      <p>Items have two main use paths. <span class="inline-code">useAction</span> runs when the item is used normally and receives the player's <span class="inline-code">Body</span>. <span class="inline-code">useLimbAction</span> runs when the item is used on a specific <span class="inline-code">Limb</span>, but only if <span class="inline-code">usableOnLimb</span> is true.</p>
      <p>Use <span class="inline-code">useAction</span> for whole-body effects like eating, drinking, playing a sound, changing happiness, or running a simple tool action. Use <span class="inline-code">useLimbAction</span> for targeted medical effects, limb temperature changes, limb components, wound treatment, or anything that needs to know which limb was clicked.</p>
      <p>When you want vanilla limb interactions without copying the game's logic, call a CUCoreLib helper such as <span class="inline-code">CUCoreUtils.DoAmputate(item, limb)</span> inside <span class="inline-code">useLimbAction</span>. That keeps your item registration readable while still following the base game's amputation rules and minigame flow.</p>
      <pre><code>usable = true,
useAction = (body, item) =>
{
    Sound.Play("useItem", item.transform.position);
    body.Drink(-5f);
    body.Eat(4f);
    body.temperature += 1.5f;
    item.condition -= 1f;
    body.talker.Talk("So spicy!");
}

usableOnLimb = true,
useLimbAction = (limb, item) =>
{
    CUCoreUtils.DoAmputate(item, limb);
}</code></pre>
</section>
    <section class="lesson-card">
      <h2>Spawning registered items</h2>
      <p>After an item is registered, CUCoreLib can spawn it through <span class="inline-code">CustomInstantiate.InstantiateReturn</span>. This helper first checks vanilla/cached prefabs, then falls back to registered CUCoreLib items and builds a runtime item template when needed.</p>
      <p>Use this when a console command, event, recipe side effect, or debug tool needs a real in-world <span class="inline-code">GameObject</span> for your custom item. The returned object can be inspected for its <span class="inline-code">Item</span> component, dropped into the world, or force-picked into a body slot.</p>
      <pre><code>GameObject obj = CustomInstantiate.InstantiateReturn(
    "sunpear",
    PlayerCamera.main.body.transform.position,
    Quaternion.identity,
    1f
);

Item item = obj ? obj.GetComponent&lt;Item&gt;() : null;
if (item != null)
{
    PlayerCamera.main.body.PickUpItem(item, 0, force: true);
}</code></pre>
    </section>
<section class="lesson-card">
      <p>Woah, that's a lot of info. Don't worry about memorizing every field. You can always come back to this page as a reference when you want to try new things with your items. </p>
      <p>Let's cool down with some sliders and buttons. Mess around with the toggles below to see how the item's ItemInfo changes based on what you want. </p>
    </section>
    
    <details closed>
      <summary>Identity</summary>
      <div class="details-body form-grid">
        ${textInput("item-id", "Item ID", itemState.id, "Lowercase IDs are easiest. No spaces.")}
        ${textInput("item-name", "Display Name", itemState.name)}
        ${textareaInput("item-description", "Description", itemState.description)}
        ${selectInput("item-category", "Category", ["food", "tool", "weapon", "medicine", "nospawn", "trash"], itemState.category)}
        ${textInput("item-sprite", "Embedded Sprite File", itemState.sprite, "Embed this PNG in your mod project.")}
      </div>
    </details>
    <details closed>
      <summary>Stats</summary>
      <div class="details-body form-grid">
        ${rangeInput("item-weight", "Weight", "0", "20", "0.1", itemState.weight)}
        ${rangeInput("item-value", "Value", "0", "200", "1", itemState.value)}
        ${rangeInput("item-spawn", "Spawn Frequency", "0", "5", "1", itemState.spawnFrequency, "0 means craft-only.")}
        ${rangeInput("item-decay", "Decay Minutes", "0", "1440", "10", itemState.decayMinutes)}
        ${rangeInput("item-recognition", "Recognition", "0", "15", "1", itemState.recognition, "Vanilla items usually set this explicitly.")}
        ${textInput("item-tags", "Tags", itemState.tags, "Comma-separated vanilla tags.", true)}
      </div>
    </details>
    <details closed>
      <summary>Use actions</summary>
      <div class="details-body form-grid">
        ${checkboxInput("item-usable", "Item can be used", itemState.usable, true)}
        ${checkboxInput("item-usable-limb", "Item can be used on limbs", itemState.usableOnLimb, true)}
        ${itemState.usable ? rangeInput("item-eat", "Eat Amount", "-20", "60", "1", itemState.eat) : ""}
        ${itemState.usable ? rangeInput("item-drink", "Drink Amount", "-20", "60", "1", itemState.drink) : ""}
        ${itemState.usable ? rangeInput("item-happiness", "Happiness", "-10", "10", "0.5", itemState.happiness) : ""}
        ${itemState.usable ? rangeInput("item-sickness", "Sickness", "-50", "50", "1", itemState.sickness) : ""}
        ${itemState.usable ? selectInput("item-sound", "Sound", ["eatCrunch", "eatFlesh", "drink", "combine", ""], itemState.sound) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-skin", "Skin Health", "-100", "100", "1", itemState.limbSkinHealth) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-muscle", "Muscle Health", "-100", "100", "1", itemState.limbMuscleHealth) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-pain", "Pain", "-100", "100", "1", itemState.limbPain) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-temperature", "Limb Temperature Change", "-5", "5", "0.5", itemState.limbTemperature) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-chill", "Chilled Limb Seconds", "0", "300", "10", itemState.limbChillSeconds) : ""}
      </div>
    </details>
    <section class="lesson-card">
      <h2>Want to mess around more with creating custom items?</h2>
      <p>Check out the <a href="/tools/" data-page="tools">dedicated page</a> tool.</p>
    </section>
  `;
}

function toolsPage(): string {
  return `
    <section class="lesson-card">
      <h2>We're working on it~</h2>
      <p>This page will eventually hold dedicated tools with more room to experiment than the inline Item form.</p>
    </section>
  `;
}

function customItemScriptsPage(): string {
  return `
    <section class="lesson-card">
      <h2>When to use a custom item script</h2>
      <p>Use a custom item script when plain <span class="inline-code">useAction</span>, <span class="inline-code">useLimbAction</span>, <span class="inline-code">ToolProperties</span>, or wearable stats are not enough. A script is the right tool when the item needs its own runtime state, periodic logic, event-style reactions, or behavior that depends on whether it is lying in the world, held, or worn.</p>
      <p>CUCoreLib adds item scripts through <span class="inline-code">CustomItemInfo.AddSpawnComponent&lt;T&gt;()</span>. That helper stores the correct assembly-qualified type name in <span class="inline-code">SpawnComponents</span>, and CUCoreLib attaches the <span class="inline-code">MonoBehaviour</span> to each spawned item instance the first time the item appears.</p>
      <p>If you have not read the broader custom-item surface yet, keep the <a href="/docs/advanced-item/" data-page="advanced-item">Advanced Item API</a> page open beside this one. That page covers the item fields and built-in modules; this page is specifically about the script side.</p>
    </section>

    <section class="lesson-card">
      <h2>How the script gets attached</h2>
      <p>Register the item normally, then chain <span class="inline-code">AddSpawnComponent&lt;YourScript&gt;()</span> on the <span class="inline-code">CustomItemInfo</span> object. Your script runs on the spawned item GameObject, so you can use standard Unity callbacks like <span class="inline-code">Awake</span>, <span class="inline-code">Start</span>, and <span class="inline-code">Update</span>.</p>
      <pre><code>ItemRegistry.Register(
    "stormmantle",
    new CustomItemInfo
    {
        fullName = "Storm Mantle",
        category = "tool",
        wearable = true,
        wearableCanBeHeld = true,
        desiredWearLimb = "UpTorso",
        wearSlotId = "outertorso"
    }
    .AddSpawnComponent&lt;StormMantleScript&gt;(),
    icon
);</code></pre>
      <p>The script is added to every real item instance. That means dropped items, spawned loot, save-loaded items, and equipped wearables all use the same script class.</p>
    </section>

    <section class="lesson-card">
      <h2>Reading the item and its custom state</h2>
      <p>Inside the script, cache the attached <span class="inline-code">Item</span> component first. <span class="inline-code">CustomData</span> now acts as inline defaults for each spawned item instance, so use <span class="inline-code">ItemRegistry.GetCustomData&lt;T&gt;</span> or <span class="inline-code">ItemRegistry.SetCustomData(...)</span> when you want friendly per-item reads and writes without hard-coding everything directly into the script.</p>
      <pre><code>private Item item;
private float warmthBonus;

private void Awake()
{
    item = GetComponent&lt;Item&gt;();

    if (item != null)
        warmthBonus = ItemRegistry.GetCustomData(item, "warmthBonus", 0.25f);
}</code></pre>
      <p>Use <span class="inline-code">CustomData</span> when you want inline defaults that each item instance can later read or mutate independently. This is item-only behavior; tile <span class="inline-code">CustomData</span> still means registration metadata.</p>
      <p>If you need to adjust a vanilla item stat from the script, remember that the field usually lives on <span class="inline-code">item.Stats</span>, not directly on the <span class="inline-code">Item</span> component. For example, use <span class="inline-code">item.Stats.wearableIsolation = 0.20f;</span> rather than <span class="inline-code">item.wearableIsolation = 0.20f;</span>.</p>
    </section>

    <section class="lesson-card">
      <h2>Equippable example</h2>
      <p>This example registers a wearable mantle and adds a script that only applies its effect while the item is actually worn. The script detects the worn state by checking whether the item sits under a <span class="inline-code">Limb</span> transform, which matches the same parentage CUCoreLib already uses for worn-sprite refresh.</p>
      <pre><code>ItemRegistry.Register(
    "stormmantle",
    new CustomItemInfo
    {
        fullName = "Storm Mantle",
        description = "A lined mantle that steadies the body in cold weather.",
        category = "tool",
        wearable = true,
        wearableCanBeHeld = true,
        desiredWearLimb = "UpTorso",
        wearSlotId = "outertorso",
        wearableIsolation = 0.16f,
        wearableArmor = 0.08f,
        weight = 1.3f,
        value = 24,
        tags = "cangetwet",
        rec = new Recognition(5),
        CustomData =
        {
            ["warmthBonus"] = 0.35f
        }
    }
    .AddSpawnComponent&lt;StormMantleScript&gt;(),
    AssetLoader.LoadEmbeddedSprite("Images.stormmantle.png")
);</code></pre>
      <p>The paired script can then grant a mild warmth effect only while equipped:</p>
      <pre><code>public sealed class StormMantleScript : MonoBehaviour
{
    private Item item;
    private float warmthBonus = 0.25f;

    private void Awake()
    {
        item = GetComponent&lt;Item&gt;();

        if (item != null)
            warmthBonus = ItemRegistry.GetCustomData(item, "warmthBonus", 0.25f);
    }

    private void Update()
    {
        Limb wornLimb = item != null && item.transform.parent != null
            ? item.transform.parent.GetComponent&lt;Limb&gt;()
            : null;
        if (wornLimb == null) return;

        Body body = wornLimb.body;
        if (body == null) return;

        body.clothingTemperature += warmthBonus * Time.deltaTime;
    }
}</code></pre>
      <p>This pattern works well for passive wearable effects: warmth, movement modifiers, periodic healing, visual state, or compatibility hooks into other systems.</p>
    </section>

    <section class="lesson-card">
      <h2>Practical guidance</h2>
      <ul>
        <li>Cache <span class="inline-code">Item</span> and any other required components in <span class="inline-code">Awake</span> or <span class="inline-code">Start</span> instead of calling <span class="inline-code">GetComponent</span> every frame.</li>
        <li>Keep wearable-only behavior behind a worn-state check. The same item script also exists while the item is dropped or sitting in inventory.</li>
        <li>Use <span class="inline-code">CustomData</span> for inline per-item defaults, then read or mutate them through <span class="inline-code">ItemRegistry.GetCustomData</span> and <span class="inline-code">ItemRegistry.SetCustomData</span>.</li>
        <li>For vanilla item stats such as <span class="inline-code">wearableIsolation</span> or <span class="inline-code">wearableArmor</span>, mutate <span class="inline-code">item.Stats</span>, not the <span class="inline-code">Item</span> component itself.</li>
        <li>If your script needs save data, use CUCoreLib's save/status systems. A <span class="inline-code">MonoBehaviour</span> field alone is not a save format.</li>
        <li>Reach for plain <span class="inline-code">useAction</span> or built-in item modules first when they already solve the problem. Scripts are for the cases that actually need runtime behavior.</li>
      </ul>
    </section>
  `;
}

function liquidsPage(): string {
  return `
    <section class="lesson-card">
      <h2>What counts as a liquid?</h2>
      <p>Liquids in Casualties Unknown are not normal item IDs by themselves. A liquid is a <span class="inline-code">LiquidType</span> entry in the vanilla <span class="inline-code">Liquids.Registry</span>. A container stores one or more <span class="inline-code">LiquidStack</span> entries, where each stack points at a liquid ID and an amount in mL.</p>
      <p>CUCoreLib works with the game's liquid system without making you write vanilla locale glue. Use <span class="inline-code">LiquidRegistry.Register</span> for liquid definitions, <span class="inline-code">LiquidStack</span> for stored contents, and direct <span class="inline-code">CustomItemInfo</span> liquid fields when you want a bottle, pouch, canteen, or other normal liquid container.</p>
      <pre><code>LiquidRegistry.Register("pineapplejuice", new CustomLiquidInfo
{
    name = "Pineapple Juice",
    description = "You've never seen this before. A light yellow drink that smells fruity.",
    color = new Color(0.94f, 0.88f, 0.66f),
    valuePerLiter = 22f
});</code></pre>
    <img src="images/pineapple-juice-ingame.png" alt="In-game screenshot of pineapple juice in a minibarrel. Ignore the water quality, my bad!" class="screenshot">
    </section>

    <section class="lesson-card">
      <h2>CustomLiquidInfo</h2>
      <p><span class="inline-code">CustomLiquidInfo</span> describes what a custom liquid is and what it does when used. CUCoreLib creates the vanilla <span class="inline-code">LiquidType</span>, stores it in <span class="inline-code">Liquids.Registry[id]</span>, and sets vanilla <span class="inline-code">localeName</span> to the registry ID internally.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">name</span></td><td><span class="inline-code">string</span></td><td>Display name registered as <span class="inline-code">other/id</span> for locale lookup and generated locale files.</td></tr>
            <tr><td><span class="inline-code">description</span></td><td><span class="inline-code">string</span></td><td>Description registered as <span class="inline-code">other/id + "dsc"</span>.</td></tr>
            <tr><td><span class="inline-code">color</span></td><td><span class="inline-code">Color</span></td><td>Color used by liquid UI, average container color, and liquid fill visuals.</td></tr>
            <tr><td><span class="inline-code">valuePerLiter</span></td><td><span class="inline-code">float</span></td><td>Trade/value basis for 1000 mL of the liquid.</td></tr>
            <tr><td><span class="inline-code">onDrink</span></td><td><span class="inline-code">LiquidType.OnDrink</span></td><td>Called by <span class="inline-code">WaterContainerItem.Drink(body, amount)</span> for each drained liquid portion.</td></tr>
            <tr><td><span class="inline-code">onHealthUse</span></td><td><span class="inline-code">LiquidType.OnHealthUse</span></td><td>Called by limb use when allowed. Injection also uses this delegate when <span class="inline-code">injectable</span> is true.</td></tr>
            <tr><td><span class="inline-code">healthUsable</span></td><td><span class="inline-code">bool</span></td><td>Allows <span class="inline-code">ApplyToLimb</span> to call <span class="inline-code">onHealthUse</span>.</td></tr>
            <tr><td><span class="inline-code">injectable</span></td><td><span class="inline-code">bool</span></td><td>Allows <span class="inline-code">Inject</span> to call <span class="inline-code">onHealthUse</span>. Injection sickness can still apply even when this is false.</td></tr>
            <tr><td><span class="inline-code">injectionSickness</span></td><td><span class="inline-code">float</span></td><td>Multiplier for sickness and blood-viscosity changes during injection. Defaults to <span class="inline-code">1f</span>.</td></tr>
            <tr><td><span class="inline-code">localeFromItem</span></td><td><span class="inline-code">bool</span></td><td>If true, tooltip names/descriptions come from item locale keys using the liquid ID.</td></tr>
            <tr><td><span class="inline-code">qualities</span></td><td><span class="inline-code">List&lt;CraftingQuality&gt;</span></td><td>Liquid qualities used by recipe matching. Amounts scale by mL through <span class="inline-code">GetScaledQualities</span>. Use <span class="inline-code">CUCoreUtils.CreateCraftingQuality(...)</span> when you want to attach a fallback display name for a custom quality ID.</td></tr>
          </tbody>
        </table>
      </div>
      
      <pre><code>LiquidRegistry.Register("lidocainecream", new CustomLiquidInfo
{
    name = "Lidocaine cream",
    description = "A numbing cream meant for bare skin.",
    color = new Color(0.92f, 0.91f, 0.84f),
    valuePerLiter = 90f,
    healthUsable = true,
    injectable = false,
    onHealthUse = (ml, limb) =>
    {
        if (limb.skinHealth > 20f)
        {
            // it does nothing because the expie is covered in fur
            return;
        }

        float dose = ml * 0.01f;
        limb.pain -= dose * 6f;
    }
});</code></pre>
    </section>

    <section class="lesson-card">
      <h2>LiquidStack</h2>
      <p>A container does not store one big mixed object, rather a list of stacks. Liquid use is used proportionally from every stack, so a mixed syringe with 50 mL water and 100 mL morphine drains both at a (1:2 ratio) when injecting.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field / constructor</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">new LiquidStack(id, amount)</span></td><td><span class="inline-code">LiquidStack</span></td><td>Creates a stored liquid entry with an ID and amount in mL.</td></tr>
            <tr><td><span class="inline-code">liquidId</span></td><td><span class="inline-code">string</span></td><td>Must match a key in <span class="inline-code">Liquids.Registry</span>, such as <span class="inline-code">water</span> or <span class="inline-code">morphine</span>. Or your newly created liquid ID!</td></tr>
            <tr><td><span class="inline-code">amount</span></td><td><span class="inline-code">float</span></td><td>Amount in mL. Vanilla removes tiny remaining stacks below about <span class="inline-code">0.5f</span> mL after drains.</td></tr>
          </tbody>
          </table>

      </div>
             <pre><code>DefaultContents =
{
    new LiquidStack("estrogen", 100f)
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Liquid containers</h2>
      <p>The runtime component is <span class="inline-code">WaterContainerItem</span>. It owns the liquid stack list, tracks capacity and fill amount, adds or drains liquids, and calls liquid effects when the player drinks, applies, or injects contents.</p>
      <p><span class="inline-code">CustomItemInfo</span> inherits the vanilla <span class="inline-code">LiquidItemInfo</span> fields, so normal liquid containers use <span class="inline-code">capacity</span>, <span class="inline-code">defaultContents</span>, and <span class="inline-code">autoFill</span> directly. <span class="inline-code">SyringeProperties</span> is only for syringe minigame/injection behavior.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Member</th><th>Type</th><th>What it means</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">capacity</span></td><td><span class="inline-code">float</span></td><td>Maximum total mL for a custom liquid container item.</td></tr>
            <tr><td><span class="inline-code">defaultContents</span></td><td><span class="inline-code">List&lt;LiquidStack&gt;</span></td><td>Initial liquid stacks placed into freshly spawned custom liquid containers.</td></tr>
            <tr><td><span class="inline-code">autoFill</span></td><td><span class="inline-code">bool</span></td><td>Whether the loose item can fill from world liquid tiles through vanilla behavior.</td></tr>
            <tr><td><span class="inline-code">stack</span></td><td><span class="inline-code">List&lt;LiquidStack&gt;</span></td><td>Runtime contents stored on the spawned <span class="inline-code">WaterContainerItem</span>.</td></tr>
            <tr><td><span class="inline-code">Capacity</span></td><td><span class="inline-code">float</span></td><td>Runtime maximum total mL, read from item stats.</td></tr>
            <tr><td><span class="inline-code">CurrentTotal</span></td><td><span class="inline-code">float</span></td><td>Total mL across all liquid stacks.</td></tr>
            <tr><td><span class="inline-code">SpaceLeft</span></td><td><span class="inline-code">float</span></td><td>Remaining mL capacity: <span class="inline-code">Capacity - CurrentTotal</span>, clamped at zero.</td></tr>
            <tr><td><span class="inline-code">AddLiquid(id, amount)</span></td><td><span class="inline-code">float</span></td><td>Adds as much liquid as fits and returns the amount actually added.</td></tr>
            <tr><td><span class="inline-code">Drain(...)</span></td><td><span class="inline-code">void</span></td><td>Removes calculated liquid amounts and updates item condition to match fill percentage.</td></tr>
            <tr><td><span class="inline-code">Drink(body, amount)</span></td><td><span class="inline-code">void</span></td><td>Drains proportionally and calls each liquid's <span class="inline-code">onDrink</span>.</td></tr>
            <tr><td><span class="inline-code">ApplyToLimb(limb, amount)</span></td><td><span class="inline-code">void</span></td><td>Drains proportionally and calls <span class="inline-code">onHealthUse</span> only for liquids marked <span class="inline-code">healthUsable</span>.</td></tr>
            <tr><td><span class="inline-code">Inject(limb, amount)</span></td><td><span class="inline-code">void</span></td><td>Drains proportionally, applies injection sickness, then calls <span class="inline-code">onHealthUse</span> for liquids marked <span class="inline-code">injectable</span>.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

   

  `;
}

function trapsPage(): string {
  return `
    <section class="lesson-card">
      <h2>Traps are advanced buildings</h2>
      <p>Traps are still just <span class="inline-code">BuildingEntity</span> objects registered through <span class="inline-code">CustomBuildingEntityDefinition</span> and a custom script. Please read the BuildingEntity documentation for more details before this page, as this is only meant for a practical example.</p>
    </section>

    <section class="lesson-card">
      <h2>What to keep in mind</h2>
      <p>Use <span class="inline-code">Placement = BuildingPlacementType.Floor</span> for floor traps, keep <span class="inline-code">RandomFlip</span> off if the sprite cannot flip horizontally without readability issues, and use a small <span class="inline-code">SurfaceOffset</span> so the trap sits naturally on the ground.</p>
      <p>If your trap has a delayed trigger, make the pre-trigger state obvious. A light, sound, or animation helps players understand that the object is armed/active instead of bugged.</p>
      <p>After the effect finishes, disable collision and visuals or destroy the object so it does not keep re-triggering. If the trap needs persistent state, move that state into a save provider instead of relying on the scene object alone.</p>
    </section>

    <section class="lesson-card">
      <h2>Spore Mine</h2>
      <p>The example on the right is of a spore mine trap. Notice the <span class="inline-code">SporeMineScript</span> component that manages the trigger radius, detonation delay, warning light, and gas cloud.</p>
      ${docsVideo(externalVideoUrls.sporeTrap, "/videos/spore-trap-ingame.mp4", "screenshot")}
      </section>
  `;
}

function localePage(): string {
  return `
    <section class="lesson-card">
      <h2>What CUCoreLib localizes for you</h2>
      <p>CUCoreLib can collect locale text from the names, descriptions, and labels you write in code and write them into a single <span class="inline-code">EN.json</span> starter file. Item names and descriptions come from <span class="inline-code">ItemRegistry.Register</span>. Building names and descriptions come from <span class="inline-code">BuildingEntityRegistry.Register</span>. Liquid names and descriptions come from <span class="inline-code">LiquidRegistry.Register</span>. UI and other needed keys can be declared with <span class="inline-code">LocaleRegistry.Require</span> or resolved directly with <span class="inline-code">LocaleRegistry.Get</span>.</p>
      <p>That means your mod can stay English-only in code, generate one baseline file, hand that file to a translator, and then ship a matching locale file like <span class="inline-code">zh-CN.json</span> for automatic runtime replacement.</p>

      Some autogenerated locale types:
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Source</th><th>Generated key</th><th>Value</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">ItemRegistry.Register("clothpatch", ...)</span></td><td><span class="inline-code">item.clothpatch</span></td><td><span class="inline-code">fullName</span></td></tr>
            <tr><td><span class="inline-code">ItemRegistry.Register("clothpatch", ...)</span></td><td><span class="inline-code">item.clothpatchdsc</span></td><td><span class="inline-code">description</span></td></tr>
            <tr><td><span class="inline-code">BuildingEntityRegistry.Register("glassworkscentrifuge", ...)</span></td><td><span class="inline-code">building.glassworkscentrifuge</span></td><td><span class="inline-code">Name</span></td></tr>
            <tr><td><span class="inline-code">BuildingEntityRegistry.Register("glassworkscentrifuge", ...)</span></td><td><span class="inline-code">building.glassworkscentrifugedsc</span></td><td><span class="inline-code">Description</span></td></tr>
              <tr><td><span class="inline-code">LiquidRegistry.Register("pineapplejuice", ...)</span></td><td><span class="inline-code">other.pineapplejuice</span></td><td><span class="inline-code">name</span></td></tr>
              <tr><td><span class="inline-code">LiquidRegistry.Register("pineapplejuice", ...)</span></td><td><span class="inline-code">other.pineapplejuicedsc</span></td><td><span class="inline-code">description</span></td></tr>
              <tr><td><span class="inline-code">qualities = new List&lt;CraftingQuality&gt; { CUCoreUtils.CreateCraftingQuality("brewingKit", "Brewing Kit") }</span></td><td><span class="inline-code">other.cqbrewingKit</span></td><td>Readable fallback label such as <span class="inline-code">Brewing Kit</span></td></tr>
              <tr><td><span class="inline-code">LocaleRegistry.Get("glassworks.menu.title", "Glassworks")</span></td><td><span class="inline-code">other.glassworks.menu.title</span></td><td><span class="inline-code">fallback string</span></td></tr>
            </tbody>
          </table>
        </div>
    </section>

    <section class="lesson-card">
      <h2>Manual keys and ad hoc lookups</h2>
      <p>... ad hoc means "as needed"...</p>

      <p>For UI text, alerts, or other strings that do not come from registered items or liquids, (e.g. ui code, console command autofill) use <span class="inline-code">LocaleRegistry.Get</span>.</p>
      <pre><code>
      string heatTooltip = LocaleRegistry.Get("other", "glassblowing.tooltip.heat", "Hot enough to warp a glove.");
      // Edit EN.json to add the "other.glassblowing.tooltip.heat" key with your desired translation, and the game will use that instead of the fallback string at runtime.
      // Fallback strings are optional, and not recommended to use
      </code></pre>
    </section>

    <section class="lesson-card">
      <h2>createLocale</h2>
      <p>Run <span class="inline-code">createLocale</span> in the in-game console after your mod registers its content. CUCoreLib writes or updates <span class="inline-code">BepInEx/config/CUCoreLib/Locales/EN.json</span>. You can pass a path if you want to write somewhere else.</p>
      <pre><code>createLocale
createLocale C:/Temp/EN.json</code></pre>
      <p>Existing user-written values are preserved for manual required keys. Generated item, building, liquid, and ad hoc keys are refreshed from the current registered names and descriptions, so changing <span class="inline-code">fullName</span>, <span class="inline-code">description</span>, <span class="inline-code">Name</span>, <span class="inline-code">Description</span>, <span class="inline-code">name</span>, or liquid <span class="inline-code">description</span> updates the file on the next run.</p>
    </section>

    <section class="lesson-card">
      <h2>How to ship the locale file</h2>
      <p>CUCoreLib now supports two mod-owned distribution patterns for files like <span class="inline-code">zh-CN.json</span>, <span class="inline-code">fr-FR.json</span>, or <span class="inline-code">CAT.json</span>. In both cases, the file name must match the in-game locale name exactly.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Approach</th><th>How to ship it</th><th>When to use it</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Embedded Resource</span></td><td>Add <span class="inline-code">zh-CN.json</span> to your mod project and set its Build Action to <span class="inline-code">Embedded Resource</span>. CUCoreLib scans loaded mod assemblies for embedded <span class="inline-code">XX-xx.json</span> resources automatically, so users do not need to place the file anywhere manually.</td><td>Use this when the translation should always ship inside the DLL.</td></tr>
            <tr><td><span class="inline-code">Loose file beside the DLL</span></td><td>Ship or tell users to place the locale file directly beside your mod DLL, for example <span class="inline-code">BepInEx/plugins/MyMod/zh-CN.json</span>.</td><td>Use this when you want translators or players to swap locale files without rebuilding the mod.</td></tr>
          </tbody>
        </table>
      </div>
      <p>The older shared overlay folder still works too: <span class="inline-code">BepInEx/config/CUCoreLib/Locales/zh-CN.json</span>. Load order is English fallback first, then embedded mod locale files, then loose plugin/config overlays, so external files can still override shipped defaults.</p>
      <pre><code>&lt;ItemGroup&gt;
  &lt;EmbeddedResource Include="Locales\\zh-CN.json" /&gt;
&lt;/ItemGroup&gt;</code></pre>
      <pre><code>BepInEx/
  plugins/
    MyMod/
      MyMod.dll
      zh-CN.json</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Generated shape</h2>
      <p>The JSON is grouped by the same categories used by vanilla locale lookup: <span class="inline-code">item</span>, <span class="inline-code">building</span>, <span class="inline-code">moodle</span>, and <span class="inline-code">other</span>. A translator can rename <span class="inline-code">EN.json</span> to something like <span class="inline-code">zh-CN.json</span> and replace the values without changing the keys.</p>
      <p>Check the right side code for an example LOLCAT translation.</p>
      <pre><code>{
  "item": {
    "clothpatch": "Ruined yarn ball",
    "clothpatchdsc": "Why'd they have to ruin the yarn and make this?",
  },
  "building": {
    "glassworkscentrifuge": "Centrifuge",
    "glassworkscentrifugedsc": "A glassworks machine for separating liquids from containers."
  },
  "other": {
    "glassworks.menu.title": "Glassworks",
    "glassworks.tooltip.heat": "Hot enough to warp a glove."
  },
  "moodle": {
    "leadpoisoning": "Lead Poisoning",
    "leadpoisoningdsc": "Your body is having a very bad time."
  }
}</code></pre>
    <img src="images/lolcat-translation-ingame.png" alt="In-game LOLCAT translation." class="screenshot">
    </section>
  `;
}

function multiplayerPage(): string {
  return `
    <section class="lesson-card">
      <h2>Wait, what?</h2>
      <p>CUCoreLib has a soft compatibility layer for <span class="inline-code">KrokoshaCasualtiesMP</span>. That is, if KrokMP is not installed, nothing extra is loaded.</p>
      <p>Custom items or buildings registered through CUCoreLib can be spawned by KrokMP using the same string ID it already sends over the network.</p>
      <h3>This is experimental and subject to change!</h3><br>
      <p>Due to the proposed overhaul of MP in its next major version (4.0.0), sync methods will be vastly different. As such, CUCoreLib's MP support is focused on backfill rather then being feature-complete.</p>
      <p>In other words, it is more focused on making sure that your code will work after the update. </p>
      </section>


    <section class="lesson-card">
      <h2>Setup for normal content</h2>
      <p>For normal CUCoreLib registries, everything is automatic. All you need is the content (mod) with stable IDs on every machine.</p>
      <p>CUCoreLib registers built-in snapshot modules during startup for <span class="inline-code">items</span>, <span class="inline-code">tiles</span>, <span class="inline-code">buildings</span>, <span class="inline-code">liquids</span>, <span class="inline-code">statuses</span>, <span class="inline-code">moodles</span>, <span class="inline-code">settings</span>, and <span class="inline-code">save</span>.</p>
      <p>You do not need to add dedicated multiplayer support for these.</p>
      <img src="images/mp-integration-ingame.png" alt="In-game screenshot of registered content working in multiplayer. Look, ma! No mp code." class="screenshot">
      <pre><code>ItemRegistry.Register("conicalFlask", flaskInfo, flaskSprite);
// This will automatically have multiplayer support, alongside most custom fields ^
BuildingEntityRegistry.Register("SporeMine", new CustomBuildingEntityDefinition
{
    Name = "Spore mine",
    Sprite = sporeMineSprite,
    Components = new[] { typeof(SporeMineScript) }
}); // Only custom scripts will not have multiplayer support</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Simple request example</h2>
      <p>A <span class="inline-code">JObject</span> is just a small JSON-style bundle of values. Use it when one side of multiplayer needs to ask the other side a question.</p>
      <p>In this example, the client asks the server whether a feature is unlocked. The server sends back one value: <span class="inline-code">unlocked</span>.</p>
      <pre><code>using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

private void Awake()
{
    // This runs on the server when a client asks this question.
    MultiplayerApi.RegisterServerHandler("mymod.is-feature-unlocked", request =>
    {
        string featureId = request?.Value&lt;string&gt;("featureId");
        bool unlocked = featureId == "challenge-mode";

        return new JObject
        {
            ["unlocked"] = unlocked
        };
    });
}

private void AskServerIfGlassworksIsUnlocked()
{
    MultiplayerApi.RequestServer(
        "mymod.is-feature-unlocked",
        new JObject
        {
            ["featureId"] = "challenge-mode"
        },
        response =>
        {
            bool unlocked = response?.Value&lt;bool?&gt;("unlocked") ?? false;

            if (unlocked)
            {
                Debug.Log("Challenge mode is unlocked!");
            }
        }
    );
}</code></pre>
      <p><span class="inline-code">RegisterServerHandler</span> answers a question, <span class="inline-code">RequestServer</span> asks the question, and <span class="inline-code">JObject</span> is the note passed between them.</p>
    </section>

    <section class="lesson-card">
      <h2>Custom snapshot modules</h2>
      <p>Use <span class="inline-code">MultiplayerApi.RegisterSyncModule</span> when your mod owns extra multiplayer state, such as discovered markers, machine recipes, per-world unlocks, or cached server decisions. A module has one capture callback and, optionally, one apply callback.</p>
      <p>This may look familiar to the <span class="inline-code">Saving</span> API. Snapshots are sent over the network, instead of being written to the disk.</p>
      <pre><code>using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;

private readonly Dictionary&lt;string, Vector2&gt; markers = new Dictionary&lt;string, Vector2&gt;();

private void Awake()
{
    MultiplayerApi.RegisterSyncModule(
        "glassworks.markers",
        CaptureMarkerSnapshot,
        ApplyMarkerSnapshot
    );
}

private JObject CaptureMarkerSnapshot()
{
    JObject root = new JObject();
    foreach (KeyValuePair&lt;string, Vector2&gt; marker in markers)
    {
        root[marker.Key] = new JObject
        {
            ["x"] = marker.Value.x,
            ["y"] = marker.Value.y
        };
    }

    return root;
}

private void ApplyMarkerSnapshot(JObject snapshot)
{
    if (snapshot == null) return;

    markers.Clear();
    foreach (JProperty property in snapshot.Properties())
    {
        JObject marker = property.Value as JObject;
        if (marker == null) continue;

        markers[property.Name] = new Vector2(
            marker.Value&lt;float?&gt;("x") ?? 0f,
            marker.Value&lt;float?&gt;("y") ?? 0f
        );
    }
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Messages and requests</h2>
      <p>Use messages for live actions, and snapshots for durable state. The public surface is <span class="inline-code">CUCoreLib.Networking.MultiplayerApi</span>, so dependent mods do not need to reflect into KrokMP internals.</p>
      <p>These APIs may be lacking, apologies. Due to the situation of v4.0.0, some features may be limited for the time being.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>API</th><th>Use it for</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">IsAvailable</span></td><td>Whether the CUCoreLib bridge found KrokMP and installed its receivers.</td></tr>
            <tr><td><span class="inline-code">IsRunning</span></td><td>Whether KrokMP reports an active multiplayer runtime.</td></tr>
            <tr><td><span class="inline-code">IsClient</span>, <span class="inline-code">IsServer</span>, <span class="inline-code">IsHost</span></td><td>Role checks before sending server-only or client-only work.</td></tr>
            <tr><td><span class="inline-code">RegisterServerHandler</span></td><td>Handle client-to-server requests/events for your channel.</td></tr>
            <tr><td><span class="inline-code">RegisterClientHandler</span></td><td>Handle server-to-client events for your channel.</td></tr>
            <tr><td><span class="inline-code">SendToServer</span></td><td>Fire-and-forget client event.</td></tr>
            <tr><td><span class="inline-code">RequestServer</span></td><td>Client request with a server response callback.</td></tr>
            <tr><td><span class="inline-code">SendToClient</span></td><td>Server event to one client ID.</td></tr>
            <tr><td><span class="inline-code">Broadcast</span></td><td>Server event to all clients, optionally including host.</td></tr>
            <tr><td><span class="inline-code">BroadcastSnapshot</span></td><td>Server resend of the current full CUCoreLib snapshot.</td></tr>
          </tbody>
        </table>
      </div>
      
      <h3>Request custom player status data</h3>
      <p>For body-status sync, use KrokMP's <span class="inline-code">clientId</span> to choose which player body to read. A <span class="inline-code">clientId</span> is the multiplayer player ID, not the player's display name.</p>
      <p><span class="inline-code">GetCustomPlayerData</span> returns the saved custom <span class="inline-code">BodyStatus</span> payloads for that player's body. <span class="inline-code">GetCustomPlayerLimbData</span> returns custom <span class="inline-code">LimbStatus</span> payloads for that player's limbs.</p>
      <pre><code>// Ask the server for one player's custom body statuses.
MultiplayerApi.RequestCustomPlayerData(clientId, response =>
{
    JArray bodyStatuses = response?["body"] as JArray;
    Debug.Log("Body status payloads: " + (bodyStatuses?.Count ?? 0));
});

// Ask the server for one player's custom limb statuses.
MultiplayerApi.RequestCustomPlayerLimbData(clientId, response =>
{
    JArray limbStatuses = response?["limbs"] as JArray;
    Debug.Log("Limb status payloads: " + (limbStatuses?.Count ?? 0));
});</code></pre>
    </section>  
  `;
}

function settingsPage(): string {
  return `
    <section class="lesson-card">
      <h2>Register settings in startup</h2>
      <p>Use <span class="inline-code">ModOptionsRegistry.Register</span> from your plugin startup to add rows to the normal game options menu. CUCoreLib appends real vanilla <span class="inline-code">Setting</span> objects, so the game keeps owning rendering, <span class="inline-code">settings.json</span>, immediate apply, menu close saves, and Reset to Default.</p>
      <pre><code>using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

private void Awake()
{
    ModOptionsRegistry.Register(ModOptionDefinition.Bool(
        "glassworks.game.enabled",
        "Enable Glassworks",
        "Controls whether Glassworks content is active.",
        Setting.SettingCategory.Game,
        true,
        value => PlayerPrefs.SetInt("Glassworks_Enabled", value ? 1 : 0)
    ));
}</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Custom categories</h2>
      <p>Pass a plain string category when you want your mod to get its own tab button on the right side of the settings menu. CUCoreLib will reuse the same custom tab for matching strings, and long custom pages can be scrolled with the mouse wheel.</p>
      <pre><code>ModOptionsRegistry.Register(ModOptionDefinition.Bool(
    "glassworks.furnace.enabled",
    "Enable kiln sparks",
    "Controls extra kiln ambience and helpers.",
    "Glassworks",
    true,
    value => PlayerPrefs.SetInt("Glassworks_KilnSparks", value ? 1 : 0)
));</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Supported row types</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Builder</th><th>Vanilla row</th><th>Stored value</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">ModOptionDefinition.Float</span></td><td>Slider with optional formatter</td><td><span class="inline-code">float</span></td></tr>
            <tr><td><span class="inline-code">ModOptionDefinition.Int</span></td><td>Integer input</td><td><span class="inline-code">int</span></td></tr>
            <tr><td><span class="inline-code">ModOptionDefinition.Bool</span></td><td>Toggle</td><td><span class="inline-code">bool</span></td></tr>
            <tr><td><span class="inline-code">ModOptionDefinition.Dropdown</span></td><td>Dropdown</td><td>Selected choice index</td></tr>
            <tr><td><span class="inline-code">ModOptionDefinition.Keybind</span></td><td>Keybind button</td><td><span class="inline-code">KeyCode</span></td></tr>
          </tbody>
        </table>
      </div>
      <p>IDs must be namespaced, like <span class="inline-code">glassworks.audio.clinkvolume</span>. Labels, descriptions, and dropdown labels are registered as literal locale text for the vanilla <span class="inline-code">gameset{id}</span> keys. The category argument may be either a vanilla <span class="inline-code">Setting.SettingCategory</span> enum or a custom string.</p>
    </section>

    <section class="lesson-card">
      <h2>Friendly keybind helpers</h2>
      <p>CUCoreLib also helps with key input actions. Use <span class="inline-code">GetFriendlyKeyBind</span> or <span class="inline-code">GetFriendlyKeyName</span> with a default key like <span class="inline-code">"R"</span>.</p>
      <p>This allows you to easily reuse the same keycode, with the added benefit of being able to disable it in certain contexts, allowing settings rebinds, and mod collision handling.</p>
      <pre><code>using CUCoreLib.Helpers;
using UnityEngine;

private void Awake()
{
    CUCoreUtils.GetFriendlyKeyBind("R").disableInInputFields = true;
    CUCoreUtils.GetFriendlyKeyBind("R").disableInMainMenu = true;
    CUCoreUtils.GetFriendlyKeyBind("R").disableInHealthPanel = true;
    CUCoreUtils.GetFriendlyKeyBind("R").disableInInventory = true;
    CUCoreUtils.AllowKeybindRebind("R", "Reload gun");
}

private void Update()
{
    if (Input.GetKeyDown(CUCoreUtils.GetFriendlyKeyBind("R")))
    {
        ReloadGun();
    }

    string prompt = "Press " + CUCoreUtils.GetFriendlyKeyName("R") + " to reload";
}</code></pre>
      <p>The friendly-name overloads are intended for simple one-key actions. If you already have a stable namespaced settings ID and want full control over the row label/category, keep using <span class="inline-code">ModOptionDefinition.Keybind</span> directly.</p>
    </section>

    <section class="lesson-card">
      <h2>Apply callbacks</h2>
      <p>The callback receives the current value after the player changes the control or after settings are loaded. Keep it small: write your own persistent mirror if needed, update runtime state, and let vanilla settings handle the main save file.</p>
      <pre><code>ModOptionsRegistry.Register(ModOptionDefinition.Float(
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
));</code></pre>
    <img src="images/setting-ingame.png" alt="In-game screenshot of the clink volume slider in the options menu." class="screenshot">
    </section>

    <section class="lesson-card">
      <h2>Validation rules</h2>
      <ul>
        <li>Each ID must contain a namespace dot and must be unique.</li>
        <li>Float and int ranges must have <span class="inline-code">min &lt;= max</span>.</li>
        <li>Dropdowns must have at least one choice, no duplicate choice keys, and a default index inside the choice list.</li>
        <li>Only vanilla row types are supported. Custom prefab-backed rows should stay mod-local.</li>
      </ul>
    </section>
  `;
}

function savingPage(): string {
  return `
    <section class="lesson-card">
      <h2>How CUCoreLib saving works</h2>
      <p>CUCoreLib hooks the normal vanilla <span class="inline-code">SaveSystem.SaveGame()</span> and <span class="inline-code">SaveSystem.TryLoadGame()</span> flow, then stores a <span class="inline-code">CUCoreLib</span> object inside the existing compressed <span class="inline-code">save.sv</span> JSON.</p>
      <p>On save, vanilla writes the run first, then CUCoreLib reopens <span class="inline-code">save.sv</span> and appends its payload. On load, CUCoreLib reads that payload before vanilla finishes loading, then restores it after the player body and world exist.</p>
    </section>
    <section class="lesson-card">
      <h2>What is automatic</h2>
      <p>CUCoreLib already patches vanilla load so most of it's own registries can resolve back into generated runtime prefabs. I.e., registered custom items, liquids, body/limb statuses, settings, etc.. can survive the normal item save/load flow without you serializing the whole item yourself.</p>
      <p>CUCoreLib also registers a small set of built-in providers for its own systems. Right now that mainly means custom building entities and a small amount of extra custom item runtime state such as light on/off state.</p>
      <p>However, do not assume your mod's own data is saved automatically just because the item or building itself reloads. Extra instance data, global mod data, custom player state, and custom world systems still need your own save provider, explained further down.</p>
    </section>
    <section class="lesson-card">
      <h2>What is JObject?</h2>
      <p>A <span class="inline-code">JObject</span> is a JSON object that (in our case) represents the serialized state of a mod's custom data. It is used to store and retrieve complex data structures that cannot be directly serialized by the vanilla save system.</p>
      <p>For more info, see <a href="https://kodershop.com/blog/dot-net-tutorials-15/what-is-jobject-in-json-net-345#blog_content" target="_blank">https://kodershop.com/blog/dot-net-tutorials-15/what-is-jobject-in-json-net-345#blog_content</a></p>
    </section>
    <section class="lesson-card">
      <h2>Register a provider</h2>
      <p>- Sorry, this section is a bit more complicated for now. I'll see about abstracting this into a simpler API.</p>
      <p>To use this for now, see the right pane for an example with JObject.</p>
      <pre><code>
public static class DescriptionSaveManager
{
    // todo, check the right pane for an example of a saved teleport command provider
}</code></pre>`;
}

function advancedItemPage(): string {
  return `
    <section class="lesson-card">
      <h2>When to use this page</h2>
      <p>The basic Item API page uses vanilla <span class="inline-code">ItemInfo</span> to mimic the base game. Advanced items can use <span class="inline-code">CustomItemInfo</span>, which includes normal <span class="inline-code">ItemInfo</span> fields, vanilla <span class="inline-code">LiquidItemInfo</span> fields, and CUCoreLib-only fields like <span class="inline-code">Container</span>, <span class="inline-code">Battery</span>, <span class="inline-code">Light</span>, <span class="inline-code">Tool</span>, <span class="inline-code">WornSprite</span>, <span class="inline-code">MultiWornSprites</span>, <span class="inline-code">WornSpriteOffset</span>, <span class="inline-code">MultiWornSpriteOffsets</span>, <span class="inline-code">LiquidMask</span>, <span class="inline-code">SpriteScaleDimensions</span>, <span class="inline-code">SpawnFrequency</span>, and <span class="inline-code">CustomData</span>.</p>
      <p>Why is the mod doing it this way? Traditonally, the game sets these settings via the Unity prefab editor, and as such does not appear in the game's default item code.</p>
      <pre><code>// Replace new ItemInfo{ ... } with 
      new CustomItemInfo{ ... }</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Modded-only fields</h2>
      <p><span class="inline-code">CustomItemInfo</span> keeps vanilla fields like <span class="inline-code">fullName</span>, <span class="inline-code">category</span>, <span class="inline-code">weight</span>, <span class="inline-code">tags</span>, and <span class="inline-code">useAction</span>. It also exposes vanilla liquid-container fields like <span class="inline-code">capacity</span>, <span class="inline-code">defaultContents</span>, and <span class="inline-code">autoFill</span>. The fields below are CUCoreLib-only data. The base game does not know about them unless CUCoreLib reads them and applies the matching runtime behavior.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Field</th>
              <th>Type</th>
              <th>What CUCoreLib uses it for</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">ID</span></td><td><span class="inline-code">string</span></td><td>Filled by <span class="inline-code">ItemRegistry.Register</span> from the separate ID argument. You normally do not set this yourself.</td></tr>
            <tr><td><span class="inline-code">Icon</span></td><td><span class="inline-code">Sprite</span></td><td>Inventory/world sprite used by custom templates and cached under the item ID. Passing the sprite as the third register argument sets this for you.</td></tr>
            <tr><td><span class="inline-code">WornSprite</span></td><td><span class="inline-code">Sprite</span></td><td>Optional sprite applied while a wearable custom item is worn, then restored to <span class="inline-code">Icon</span> when dropped.</td></tr>
            <tr><td><span class="inline-code">MultiWornSprites</span></td><td><span class="inline-code">Dictionary&lt;string, Sprite&gt;</span></td><td>Optional extra worn sprites keyed by vanilla limb name. CUCoreLib maps these onto the game's secondary wearable sprite arrays, so one wearable can draw on multiple limbs while equipped.</td></tr>
            <tr><td><span class="inline-code">WornSpriteOffset</span></td><td><span class="inline-code">Vector2</span></td><td>Optional local-space offset applied to the worn item sprite after vanilla attaches it to <span class="inline-code">desiredWearLimb</span>.</td></tr>
            <tr><td><span class="inline-code">WearableSortingOrder</span></td><td><span class="inline-code">int?</span></td><td>Optional worn-renderer sorting order override. When set, CUCoreLib applies that exact order to the main worn sprite and any <span class="inline-code">MultiWornSprites</span>; higher values draw on top of lower values.</td></tr>
            <tr><td><span class="inline-code">MultiWornSpriteOffsets</span></td><td><span class="inline-code">Dictionary&lt;string, Vector2&gt;</span></td><td>Optional per-limb local offsets for entries in <span class="inline-code">MultiWornSprites</span>. Use matching limb keys, or fill it through <span class="inline-code">SetMultiWornSpriteOffset(...)</span>.</td></tr>
            <tr><td><span class="inline-code">LiquidMask</span></td><td><span class="inline-code">Sprite</span></td><td>Optional liquid-fill overlay mask for custom <span class="inline-code">WaterContainerItem</span> containers. Use a white or neutral grayscale sprite with transparency shaping the visible fill area so the game can tint it to the current liquid color.</td></tr>
            <tr><td><span class="inline-code">SpriteScale</span></td><td><span class="inline-code">float</span></td><td>Scale applied to the generated runtime template. Keep this near <span class="inline-code">1f</span> unless your art was made at a different size.</td></tr>
            <tr><td><span class="inline-code">InventoryIconScale</span></td><td><span class="inline-code">float</span></td><td>Extra multiplier applied only to the inventory icon UI size after the normal sprite scale has been resolved. Leave it at <span class="inline-code">1f</span> unless you want the inventory icon smaller or larger than the in-world sprite.</td></tr>
            <tr><td><span class="inline-code">SpriteScaleDimensions</span></td><td><span class="inline-code">SpriteScaleDimensions</span></td><td>Scales the sprite toward a target pixel size like <span class="inline-code">(14f, 14f)</span>. Add <span class="inline-code">true</span> as the third tuple value to stop once either axis reaches the requested size instead of forcing both axes to meet it.</td></tr>
            <tr><td><span class="inline-code">SpawnFrequency</span></td><td><span class="inline-code">int</span></td><td>Loot pool injection weight. <span class="inline-code">0</span> means craft-only, <span class="inline-code">1</span> is the normal default, higher values make it more common.</td></tr>
            <tr><td><span class="inline-code">Container</span></td><td><span class="inline-code">ContainerProperties</span></td><td>Adds/configures a vanilla <span class="inline-code">Container</span> component on spawned items.</td></tr>
            <tr><td><span class="inline-code">Battery</span></td><td><span class="inline-code">BatteryProperties</span></td><td>Adds/configures a vanilla <span class="inline-code">BatteryItem</span> component on spawned items.</td></tr>
            <tr><td><span class="inline-code">Light</span></td><td><span class="inline-code">LightProperties</span></td><td>Adds/configures a <span class="inline-code">Light2D</span> emitter and optionally wires it to vanilla <span class="inline-code">LightItem</span> behavior.</td></tr>
            <tr><td><span class="inline-code">Bandage</span></td><td><span class="inline-code">BandageProperties</span></td><td>Installs a vanilla-style <span class="inline-code">BandageMinigame</span> limb action and applies limb healing, pain reduction, and bandage slow values.</td></tr>
            <tr><td><span class="inline-code">Syringe</span></td><td><span class="inline-code">SyringeProperties</span></td><td>Adds syringe-style liquid injection behavior through <span class="inline-code">WaterContainerItem</span> and <span class="inline-code">SyringeMinigame</span>.</td></tr>
            <tr><td><span class="inline-code">Tool</span></td><td><span class="inline-code">ToolProperties</span></td><td>Builds a vanilla <span class="inline-code">AttackInfo</span> and calls <span class="inline-code">Body.Attack</span> for melee-style tools or weapons.</td></tr>
            <tr><td><span class="inline-code">SpawnComponents</span></td><td><span class="inline-code">List&lt;string&gt;</span></td><td>Backing list of runtime-resolvable <span class="inline-code">MonoBehaviour</span> type names CUCoreLib adds to the spawned item GameObject the first time the item appears. For plugin-defined scripts, prefer <span class="inline-code">AddSpawnComponent&lt;T&gt;()</span> so you do not have to hand-write the assembly-qualified name.</td></tr>
            <tr><td><span class="inline-code">CustomData</span></td><td><span class="inline-code">Dictionary&lt;string, object&gt;</span></td><td>Inline defaults for mod-owned per-item custom state. CUCoreLib copies them onto each spawned item instance so your mod can read or mutate them later with <span class="inline-code">ItemRegistry.GetCustomData&lt;T&gt;</span> and <span class="inline-code">ItemRegistry.SetCustomData</span>.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>LiquidItemInfo fields</h3>
      <p>Use these direct fields for normal liquid containers like bottles, cans, canteens, pouches, and drinkable items. Use <span class="inline-code">SyringeProperties</span> only when you want the syringe minigame and injection action.</p>
      <p>If you want the container to render a vanilla-style colored fill overlay, pair those fields with <span class="inline-code">LiquidMask</span> on <span class="inline-code">CustomItemInfo</span>. The mask should usually be white or neutral grayscale, with transparency defining where the liquid can appear.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">capacity</span></td><td><span class="inline-code">float</span></td><td>Maximum liquid capacity in mL.</td></tr>
            <tr><td><span class="inline-code">defaultContents</span></td><td><span class="inline-code">List&lt;LiquidStack&gt;</span></td><td>Liquids added to freshly spawned items.</td></tr>
            <tr><td><span class="inline-code">autoFill</span></td><td><span class="inline-code">bool</span></td><td>Whether a loose item can fill itself from world liquid tiles when it has space.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>ContainerProperties</h3>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Capacity</span></td><td><span class="inline-code">float</span></td><td>Maximum total held weight for the vanilla <span class="inline-code">Container</span>.</td></tr>
            <tr><td><span class="inline-code">MaxWeightPerItem</span></td><td><span class="inline-code">float</span></td><td>Maximum weight any one contained item may have.</td></tr>
            <tr><td><span class="inline-code">EncumbranceReduction</span></td><td><span class="inline-code">float</span></td><td>Multiplier for how much contained weight counts against the player. <span class="inline-code">1f</span> is normal; <span class="inline-code">0.5f</span> feels half as heavy.</td></tr>
            <tr><td><span class="inline-code">ItemsVisible</span></td><td><span class="inline-code">bool</span></td><td>Controls the vanilla <span class="inline-code">Container.itemsVisible</span> flag. When true, contained item sprites remain visible inside the container.</td></tr>
            <tr><td><span class="inline-code">TagRestriction</span></td><td><span class="inline-code">string[]</span></td><td>Optional whitelist of item tags accepted by the container. Leave empty to allow any tag.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>BatteryProperties</h3>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">StartCharge</span></td><td><span class="inline-code">float</span></td><td>Starting charge amount for fresh spawned items. Values from <span class="inline-code">0f</span> to <span class="inline-code">1f</span> are treated as a percentage of the preset max charge. Higher values are treated as absolute charge. Leave it below zero to spawn with a full preset-sized battery.</td></tr>
            <tr><td><span class="inline-code">Preset</span></td><td><span class="inline-code">BatteryItem.BatteryPreset</span></td><td>Vanilla preset for accepted battery size: <span class="inline-code">Small</span>, <span class="inline-code">Medium</span>, or <span class="inline-code">Large</span>.</td></tr>
            <tr><td><span class="inline-code">SpawnWithBattery</span></td><td><span class="inline-code">bool</span></td><td>If true, fresh items start with the preset-sized vanilla battery already loaded. If false, the item starts empty.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>BandageProperties</h3>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Effectiveness</span></td><td><span class="inline-code">float</span></td><td>Divisor for minigame use amount. Higher values make the item last longer.</td></tr>
            <tr><td><span class="inline-code">SkinHealAmount</span></td><td><span class="inline-code">float</span></td><td>Skin healing applied per use amount.</td></tr>
            <tr><td><span class="inline-code">BandageSlowAmount</span></td><td><span class="inline-code">float</span></td><td>Bleeding slowdown applied per use amount.</td></tr>
            <tr><td><span class="inline-code">PainReduction</span></td><td><span class="inline-code">float</span></td><td>Pain removed per use amount.</td></tr>
            <tr><td><span class="inline-code">BoneHealTimerReduction</span></td><td><span class="inline-code">float</span></td><td>Broken-bone recovery timer reduction per use amount.</td></tr>
            <tr><td><span class="inline-code">DislocationTimerReduction</span></td><td><span class="inline-code">float</span></td><td>Dislocation timer reduction per use amount.</td></tr>
            <tr><td><span class="inline-code">MinigameColor</span></td><td><span class="inline-code">Color</span></td><td>Color passed into the vanilla bandage minigame.</td></tr>
            <tr><td><span class="inline-code">CreateWrapSprite</span></td><td><span class="inline-code">bool</span></td><td>If true, CUCoreLib creates a temporary limb wrap sprite when used.</td></tr>
            <tr><td><span class="inline-code">WrapSpritePath</span></td><td><span class="inline-code">string</span></td><td>Resource path for the temporary wrap sprite.</td></tr>
            <tr><td><span class="inline-code">WrapSpriteColor</span></td><td><span class="inline-code">Color</span></td><td>Tint for the temporary wrap sprite.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>SyringeProperties</h3>
      <p>This is for syringe-style limb injection, not general bottle/canteen liquid storage.</p>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Capacity</span></td><td><span class="inline-code">float</span></td><td>Maximum liquid capacity in mL.</td></tr>
            <tr><td><span class="inline-code">AutoFill</span></td><td><span class="inline-code">bool</span></td><td>Whether the added <span class="inline-code">WaterContainerItem</span> auto-fills when supported by vanilla behavior.</td></tr>
            <tr><td><span class="inline-code">AmountPerFullUse</span></td><td><span class="inline-code">float</span></td><td>Liquid amount injected when the minigame multiplier is <span class="inline-code">1f</span>.</td></tr>
            <tr><td><span class="inline-code">UseAverageColor</span></td><td><span class="inline-code">bool</span></td><td>If true, the minigame color comes from the contained liquid average.</td></tr>
            <tr><td><span class="inline-code">MinigameColor</span></td><td><span class="inline-code">Color</span></td><td>Fallback minigame color when <span class="inline-code">UseAverageColor</span> is false.</td></tr>
            <tr><td><span class="inline-code">DefaultContents</span></td><td><span class="inline-code">List&lt;LiquidStack&gt;</span></td><td>Liquids added to fresh spawned items.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>ToolProperties</h3>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Damage</span></td><td><span class="inline-code">float</span></td><td>Damage against animals or living targets.</td></tr>
            <tr><td><span class="inline-code">StructuralDamage</span></td><td><span class="inline-code">float</span></td><td>Damage against blocks, buildings, and non-animal structures.</td></tr>
            <tr><td><span class="inline-code">AttackCooldownMultiplier</span></td><td><span class="inline-code">float</span></td><td>Multiplier applied to vanilla attack cooldown after hitting animals.</td></tr>
            <tr><td><span class="inline-code">Distance</span></td><td><span class="inline-code">float</span></td><td>Raycast reach for the attack.</td></tr>
            <tr><td><span class="inline-code">KnockBack</span></td><td><span class="inline-code">float</span></td><td>Impulse applied through vanilla attack knockback.</td></tr>
            <tr><td><span class="inline-code">Cooldown</span></td><td><span class="inline-code">float</span></td><td>Base attack cooldown.</td></tr>
            <tr><td><span class="inline-code">AttackAnimation</span></td><td><span class="inline-code">string</span></td><td>Resource name for the swing animation prefab, usually <span class="inline-code">SwingAnim</span>.</td></tr>
            <tr><td><span class="inline-code">StaminaUse</span></td><td><span class="inline-code">float</span></td><td>Stamina spent when swinging.</td></tr>
            <tr><td><span class="inline-code">Piercing</span></td><td><span class="inline-code">bool</span></td><td>If true, vanilla attack raycasts can continue through hits.</td></tr>
            <tr><td><span class="inline-code">SwingSounds</span></td><td><span class="inline-code">string[]</span></td><td>Sound IDs randomly chosen when swinging.</td></tr>
            <tr><td><span class="inline-code">Volume</span></td><td><span class="inline-code">float</span></td><td>Swing sound volume.</td></tr>
            <tr><td><span class="inline-code">RotateAmount</span></td><td><span class="inline-code">float</span></td><td>Visual attack rotation amount.</td></tr>
            <tr><td><span class="inline-code">PhysicalSwing</span></td><td><span class="inline-code">bool</span></td><td>If true, vanilla arm power, exertion, and physical swing effects apply.</td></tr>
            <tr><td><span class="inline-code">DoAttackAnimation</span></td><td><span class="inline-code">bool</span></td><td>If true, vanilla arms swing animation plays.</td></tr>
            <tr><td><span class="inline-code">MetalMoreDamage</span></td><td><span class="inline-code">bool</span></td><td>If true, vanilla metallic target bonus damage applies.</td></tr>
            <tr><td><span class="inline-code">ConditionLossOnHit</span></td><td><span class="inline-code">float</span></td><td>Item condition subtracted after a successful hit.</td></tr>
          </tbody>
        </table>
      </div>

      <h3>LightProperties</h3>
      <div class="table-wrap">
        <table class="field-table">
          <thead><tr><th>Field</th><th>Type</th><th>What it does</th></tr></thead>
          <tbody>
            <tr><td><span class="inline-code">Intensity</span></td><td><span class="inline-code">float</span></td><td>Brightness of the generated <span class="inline-code">Light2D</span>.</td></tr>
            <tr><td><span class="inline-code">Color</span></td><td><span class="inline-code">Color</span></td><td>Light tint. Defaults to white.</td></tr>
            <tr><td><span class="inline-code">PointLightOuterRadius</span></td><td><span class="inline-code">float</span></td><td>Outer radius for point lights.</td></tr>
            <tr><td><span class="inline-code">PointLightInnerRadius</span></td><td><span class="inline-code">float</span></td><td>Inner radius for point lights.</td></tr>
            <tr><td><span class="inline-code">PointLightOuterAngle</span></td><td><span class="inline-code">float</span></td><td>Outer cone angle for point lights. Defaults to <span class="inline-code">360f</span>.</td></tr>
            <tr><td><span class="inline-code">PointLightInnerAngle</span></td><td><span class="inline-code">float</span></td><td>Inner cone angle for point lights. Defaults to <span class="inline-code">360f</span>.</td></tr>
            <tr><td><span class="inline-code">LightType</span></td><td><span class="inline-code">CustomLightType</span></td><td>CUCoreLib light type mapped internally to URP <span class="inline-code">Light2D.LightType</span>. Use <span class="inline-code">CustomLightType.Point</span> for lantern-style item light.</td></tr>
            <tr><td><span class="inline-code">Offset</span></td><td><span class="inline-code">Vector2</span></td><td>Local offset for the light object relative to the item.</td></tr>
            <tr><td><span class="inline-code">AddLightItem</span></td><td><span class="inline-code">bool</span></td><td>If true, CUCoreLib wires the light through vanilla <span class="inline-code">LightItem</span> so container/on-off behavior can apply.</td></tr>
          </tbody>
        </table>
      </div>
      <pre><code>ItemRegistry.Register(
    "SomeSlightlyMoreAdvancedItem",
    new CustomItemInfo
    {
        fullName = "someName",
        // ... and other ItemInfo fields...

        // Cool CUCoreLib-only fields!
        SpawnFrequency = 0,
        SpriteScale = 1f,
        CustomData =
        {
            ["family"] = "utility-bag"
        } 
        // etc...
    },
    AssetLoader.LoadEmbeddedSprite("Images.someSweetPixelArt.png")
);</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Bandages</h2>
      <p>This is a bit different then the decompiled code. <span class="inline-code">BandageProperties</span> starts a <span class="inline-code">BandageMinigame</span>, converts the minigame angle into a use amount, reduces item condition, then adds limb healing and bleeding slowdown values.</p>
      <pre><code>ItemRegistry.Register(
    "clothpatch",
    new CustomItemInfo
    {
        fullName = "Cloth patch",
        description = "A small patch of clean cloth.",
        category = "medical",
        tags = "medicine,dressing,cangetwet",
        weight = 0.05f,
        value = 3,
        rec = new Recognition(3),
        Bandage = new BandageProperties
        {
            Effectiveness = 8f,
            SkinHealAmount = 8f,
            BandageSlowAmount = 18f,
            PainReduction = 40f,
            BoneHealTimerReduction = 5f,
            DislocationTimerReduction = 5f,
            MinigameColor = new Color(0.9f, 0.9f, 0.9f)
        },
        SpawnFrequency = 1
    },
    AssetLoader.LoadEmbeddedSprite("Images.clothpatch.png")
);</code></pre>
      <img src="images/cloth-patch-ingame.png" alt="In-game screenshot of the cloth patch's bandage minigame" class="screenshot" />
    </section>

    <section class="lesson-card">
      <h2>Syringes</h2>
      <p><span class="inline-code">SyringeProperties</span> makes the item behave like a liquid syringe: CUCoreLib adds <span class="inline-code">WaterContainerItem</span>, starts <span class="inline-code">SyringeMinigame</span>, then calls <span class="inline-code">wat.Inject(limb, amount)</span> using the minigame multiplier.</p>
      <pre><code>ItemRegistry.Register(
    "reliefinjector",
    new CustomItemInfo
    {
        fullName = "Relief injector",
        description = "A small injector prefilled with relief cream.",
        category = "medicine",
        tags = "medicine",
        slotRotation = -45f,
        destroyAtZeroCondition = false,
        combineable = true,
        scaleWeightWithCondition = true,
        weight = 0.25f,
        value = 8,
        rec = new Recognition(5),
        Syringe = new SyringeProperties
        {
            Capacity = 100f,
            AmountPerFullUse = 100f,
            AutoFill = false,
            // Not sure what this is? Read the next page for liquid documentation.
            DefaultContents =
            {
                new LiquidStack("reliefcream", 100f)
            }
        },
        SpawnFrequency = 1
    },
    AssetLoader.LoadEmbeddedSprite("Images.reliefinjector.png")
);</code></pre>
      <img src="images/relief-injector-ingame.png" alt="In-game screenshot of the relief injector's syringe minigame" class="screenshot" />
      <p>The liquid decides what injection actually does. <span class="inline-code">WaterContainerItem.Inject</span> drains the liquid stack and calls the liquid's health-use behavior when that liquid is injectable.</p>
      <p>You'll learn more about liquids and their use on the <a href="index.html#liquids">Liquids</a> page.</p>
    </section>

    <section class="lesson-card">
      <h2>Containers</h2>
      <p>Containers are runtime components, not just item stats. CUCoreLib's container module adds or configures a vanilla <span class="inline-code">Container</span> component when the item starts.</p>
      <p><span class="inline-code">Capacity</span> maps to the container's maximum held weight. <span class="inline-code">MaxWeightPerItem</span> limits individual items. <span class="inline-code">EncumbranceReduction</span> maps to the vanilla encumbrance multiplier: <span class="inline-code">1f</span> means normal weight, <span class="inline-code">0.5f</span> means the contents feel half as heavy.</p>
      <pre><code>ItemRegistry.Register(
    "cinderstalkbag",
    new CustomItemInfo
    {
        fullName = "Cinderstalk Bag",
        description = "A compact all-natural bag. Who says plants can't be stylish?",
        category = "custom",
        weight = 1.2f,
        value = 18,
        tags = "cangetwet",
        rec = new Recognition(6),
        Container = new ContainerProperties
        {
            Capacity = 5f,
            MaxWeightPerItem = 2f,
            EncumbranceReduction = 0.65f
        },
        SpawnFrequency = 1
    },
    AssetLoader.LoadEmbeddedSprite("Images.cinderstalk-bag.png")
);</code></pre>
    <img src="images/cinderstalk-bag-ingame.png" alt="In-game screenshot of the Cinderstalk Bag's container UI" class="screenshot" />
    </section>

    <section class="lesson-card">
      <h2>Battery-powered items</h2>
      <p>Battery-powered tools use the vanilla <span class="inline-code">BatteryItem</span> component. CUCoreLib's battery module adds one, chooses the vanilla battery size from <span class="inline-code">Preset</span>, and uses <span class="inline-code">StartCharge</span> only for the initial fill amount. Use values from <span class="inline-code">0f</span> to <span class="inline-code">1f</span> when you want a percentage of the preset size, or values above <span class="inline-code">1f</span> when you want an absolute charge amount. Use this when the item itself stores charge or accepts battery behavior; use vanilla <span class="inline-code">BatteryInfo</span> when you are registering an actual battery item.</p>
      <p>Battery items automatically gain <span class="inline-code">BatteryDecay</span>, so you usually just set <span class="inline-code">decayMinutes</span> to control how quickly charge drains.</p>
      <pre><code>ItemRegistry.Register(
    "portablelamp",
    new CustomItemInfo
    {
        fullName = "Portable lamp",
        description = "A battery-fed lamp for the fuzzy cave explorer.",
        category = "utility",
        tags = "belttool",
        usable = true,
        value = 22,
        weight = 0.8f,
        rec = new Recognition(5),
        decayMinutes = 180f,
        Battery = new BatteryProperties
        {
            StartCharge = 35f,
            Preset = BatteryItem.BatteryPreset.Medium
        },
        Light = new LightProperties
        {
            Intensity = 0.75f,
            Color = Color.white,
            PointLightOuterRadius = 7.5f,
            PointLightInnerRadius = 0f,
            PointLightOuterAngle = 360f,
            PointLightInnerAngle = 360f,
            LightType = CustomLightType.Point
        },
        SpawnFrequency = 1
    },
    AssetLoader.LoadEmbeddedSprite("Images.portablelamp.png")
);</code></pre>
    <img src="images/portable-lamp-ingame.png" alt="In-game screenshot of the portable lamp's light effect" class="screenshot" />
    </section>

    

    <section class="lesson-card">
      <h2>Non-gun weapons</h2>
      <p>For melee tools and small weapons, <span class="inline-code">ToolProperties</span> builds a vanilla <span class="inline-code">AttackInfo</span> and calls <span class="inline-code">Body.Attack</span>. This gives custom tools the same swing, raycast, hit sound, cooldown, damage-number, and block-damage path used by vanilla tools.</p>
      <pre><code>ItemRegistry.Register("glassshiv", new CustomItemInfo
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
    }
}, AssetLoader.LoadEmbeddedSprite("Images.glassshiv.png"), 1);</code></pre>
    <img src="images/glass-shiv-ingame.png" alt="long long knife" class="screenshot" />
    </section>

    <section class="lesson-card">
      <h2>Equippables</h2>
      <p>Wearables are regular items with wearable fields set. <span class="inline-code">wearSlotId</span> is the save/load and replacement key. <span class="inline-code">desiredWearLimb</span> points the primary visual/armor behavior at a body region, while <span class="inline-code">wearableArmor</span> and <span class="inline-code">wearableIsolation</span> affect protection and temperature. Use <span class="inline-code">MultiWornSprites</span> when the same wearable should also draw extra pieces on other limbs, and set <span class="inline-code">WearableSortingOrder</span> when you need that whole wearable stack to use a higher or lower draw priority than other equipment.</p>
      <p>If a wearable needs a custom script, use the same item-side <span class="inline-code">AddSpawnComponent&lt;T&gt;()</span> helper described below. There is no separate wearable-only script hook.</p>
      <pre><code> ItemRegistry.Register(
     "fieldpack",
     new CustomItemInfo
     {
         fullName = "Field Pack",
         description = "A versatile pack for carrying supplies.",
         category = "tool",
         wearable = true,
         wearableCanBeHeld = true,
         desiredWearLimb = "UpTorso",
         wearSlotId = "back",
         wearableArmor = 0.18f,
         wearableIsolation = 0.08f,
         wearableHitDurabilityLossMultiplier = 0.7f,
         weight = 1.1f,
         value = 14,
         tags = "cangetwet",
         rec = new Recognition(4),
         WornSprite = AssetLoader.LoadEmbeddedSprite("Images.fieldpack-worn.png"),
         WornSpriteOffset = new Vector2(0f, -0.04f),
         WearableSortingOrder = 206,
         MultiWornSprites = new Dictionary<string, Sprite>
         {
             ["DownTorso"] = AssetLoader.LoadEmbeddedSprite("Images.fieldpack-bedroll.png"),
             ["HandB"] = AssetLoader.LoadEmbeddedSprite("Images.fieldpack-strap.png")
         },
         Container = new ContainerProperties
         {
             Capacity = 8f,
             MaxWeightPerItem = 3f,
             EncumbranceReduction = 0.5f
         },
         SpawnFrequency = 1
     }
     .SetMultiWornSpriteOffset(new Vector2(0f, -0.02f), AssetLoader.LoadEmbeddedSprite("Images.fieldpack-bedroll.png"))
     .SetMultiWornSpriteOffset("HandB", new Vector2(-0.01f, 0.01f)),
     AssetLoader.LoadEmbeddedSprite("Images.fieldpack.png")
 );</code></pre>
 <img src="images/fieldpack-ingame.png" alt="Field Pack item sprite" class="screenshot" />

 <p>In this example, we combined the container tag, a primary <span class="inline-code">WornSprite</span>, and two <span class="inline-code">MultiWornSprites</span> entries to make one backpack draw across multiple limbs while still behaving like a normal wearable container. <p>
 <p>A lot of more unique items such as this can be made with combinations of tags. Give it a try!<p>
    </section>

    <section class="lesson-card">
      <h2>Decay bit flags</h2>
      <p><span class="inline-code">decayInfo</span> is a byte, but it represents bit flags from <span class="inline-code">ItemInfo.DecayType</span>. Combine flags with bitwise OR (|). The enums are: <span class="inline-code">NoDecayWithoutContainerItem (1)</span>, <span class="inline-code">NoDecayWhenNotWorn (2)</span>, <span class="inline-code">NoDecayWhenStill (4)</span>, and <span class="inline-code">BatteryDecay (16)</span>.</p>
      <pre><code>decayMinutes = 240f,
decayInfo = (byte)(
    ItemInfo.DecayType.NoDecayWhenNotWorn | ItemInfo.DecayType.NoDecayWhenStill
);</code></pre>
      <p><span class="inline-code">BatteryDecay (16)</span> changes decay from normal item condition loss into battery drain. Use it for battery-powered items that should consume charge over time instead of rotting or breaking down normally.</p>
      <p>Note: The item still needs <span class="inline-code">decayMinutes</span> for decay speed.</p>
    </section>

    <section class="lesson-card">
      <h2>SpawnComponents type names</h2>
      <p>For plugin-authored item scripts, prefer <span class="inline-code">AddSpawnComponent&lt;T&gt;()</span>. It stores the runtime-resolvable assembly-qualified name for you, so you do not have to hand-write <span class="inline-code">Type.GetType(...)</span> strings.</p>
      <pre><code>ItemRegistry.Register(
    "ToggleBlade",
    new CustomItemInfo
    {
        fullName = "Toggle blade",
        category = "weapon"
    }
    .AddSpawnComponent&lt;ToggleBladeScript&gt;(),
    icon
);</code></pre>
      <p><span class="inline-code">SpawnComponents</span> remains the backing list, so you can still fill it manually for uncommon reflection-driven cases. When you do, use the full type name plus the assembly name without the <span class="inline-code">.dll</span> extension. Example: <span class="inline-code">"TestModd.ToggleBladeScript, TestModd"</span>.</p>
      <p>Current runtime note: invalid raw entries are skipped and now log a warning with the item ID and failing entry.</p>
    </section>

    <section class="lesson-card">
      <h2>Custom data</h2>
      <p><span class="inline-code">CustomData</span> is for your own mod's item-side custom state defaults: tuning values, upgrade tiers, internal labels, or small constants you want to keep near the registration. CUCoreLib copies those values onto each spawned item instance so one item can change without rewriting every other item of the same type.</p>
      <pre><code>ItemRegistry.Register(
    "laserdrill",
    new CustomItemInfo
    {
        fullName = "Laser drill",
        category = "tool",
        CustomData =
        {
            ["beamRange"] = 8f,
            ["warmupSeconds"] = 0.35f,
            ["upgradeFamily"] = "laserdrill"
        }
    },
    AssetLoader.LoadEmbeddedSprite("Images.laserdrill.png")
);</code></pre>
      <p>Retrieve it from the item with <span class="inline-code">ItemRegistry.GetCustomData</span>. The generic type is intentional: the dictionary stores <span class="inline-code">object</span>, so callers should ask for the type they expect.</p>
      <pre><code>float beamRange = ItemRegistry.GetCustomData(item, "beamRange", 4f);
Logger.LogInfo($"Laser drill range: {beamRange}");</code></pre>
      <p>Consider adding a function to encapsulate the retrieval logic.</p>
      <p>If the data changes per spawned item, update it through <span class="inline-code">ItemRegistry.SetCustomData</span> rather than mutating the shared registered definition. Tile <span class="inline-code">CustomData</span> still remains registration metadata.</p>
    </section>
  `;
}

function recipePage(): string {
  return `
    <section class="lesson-card">
      <h2>RecipeRegistry.Register</h2>
      <p>CUCoreLib registers normal vanilla <span class="inline-code">Recipe</span> objects. The recipe uses the game's existing crafting visibility, ingredient matching, item consumption, and result spawning flow.</p>
      <p>Recipe visibility follows vanilla rules: if <span class="inline-code">specialKnown</span> is false, the recipe only appears when the player's INT is at least <span class="inline-code">INT - 3</span>. Keep early test recipes low, such as <span class="inline-code">INT = 10</span>, or they can look like they failed to register.</p>
      <pre><code>RecipeRegistry.Register(new Recipe
{
    INT = 6,
    result = new RecipeResult { id = "cinderstalkbag", amount = 1 },
    items = new List&lt;RecipeItem&gt;
    {
        new RecipeItem { specificId = "cinderstalk", minimumCondition = 0.9f },
        new RecipeItem { specificId = "cinderstalk", minimumCondition = 0.9f },
        new RecipeItem { specificId = "cinderstalk", minimumCondition = 0.9f },
        new RecipeItem { specificId = "rope", minimumCondition = 0f },
        new RecipeItem { specificId = "charredfern", minimumCondition = 0.9f },
        new RecipeItem { specificId = "plasticchunk", minimumCondition = 0.9f }
    },
    category = Recipes.RecipeCategory.Utilities
});</code></pre>
    <img src="images/cinderstalk-bag-craft-ingame.png" alt="Cinderstalk bag craft, with custom materials" class="screenshot" scale="0.6">
    </section>
    <section class="lesson-card">
      <h2>Ingredients</h2>
      <p>A <span class="inline-code">RecipeItem</span> can require a specific ID or a crafting quality. Specific IDs are best when you need exactly <span class="inline-code">water</span>, <span class="inline-code">rope</span>, or one of your custom item IDs. Qualities are best for reusable tools or broad food/material groups.</p>
      <p>For liquids, the constructor value is mL. For normal items, it is minimum condition.</p>
    </section>
    <details open>
      <summary>Result</summary>
      <div class="details-body form-grid">
        ${textInput("recipe-result", "Result Item ID", recipeState.resultId)}
        ${selectInput("recipe-category", "Category", ["Materials", "Tools", "Medicine", "Utilities", "Food"], recipeState.category)}
        ${rangeInput("recipe-int", "Intelligence Requirement", "0", "20", "1", recipeState.intRequirement)}
        ${rangeInput("recipe-amount", "Result Amount", "1", "10", "1", recipeState.resultAmount)}
        ${rangeInput("recipe-condition", "Result Condition", "0", recipeState.isLiquidResult ? "500" : "1", recipeState.isLiquidResult ? "1" : "0.05", recipeState.resultCondition, recipeState.isLiquidResult ? "Liquid results use mL." : "Item results use condition from 0 to 1.")}
        ${checkboxInput("recipe-liquid-result", "Result is liquid", recipeState.isLiquidResult)}
      </div>
    </details>
    <details open>
      <summary>Ingredients</summary>
      <div class="details-body">
        <div class="ingredient-list" id="ingredient-list">${ingredientCards()}</div>
        <div class="actions">
          <button class="action-button primary" type="button" id="add-ingredient">Add Ingredient</button>
          <button class="action-button" type="button" id="reset-ingredients">Reset</button>
        </div>
      </div>
    </details>
  `;
}

function assetPage(): string {
  return `
  <section class="lesson-card">
      
      <p>Great! We have a mod. </p>
      <p>Hm. Looks like it doesn't do much, though. A single line of debug dialogue is hardly enough for a mod, after all. </p>
      <p>Let's add some images that we'll be able to use now.</p>
    </section>
    <section class="lesson-card">
      <h2>Embedded assets</h2>
      <p>Use embedded assets for core art, default sounds, and data that should always ship with your mod. In Visual Studio, add the file to your project and set its Build Action to <span class="inline-code">Embedded Resource</span>.</p>
      <p>Tip: keep assets in folders such as <span class="inline-code">Images</span>, <span class="inline-code">Audio</span>, or <span class="inline-code">Data</span> so the manifest names stay predictable.</p>
      <p>Embedded images are now auto-warmed in memory while the main menu is open, so most first-use sprite loads after that reuse the already decoded texture data. If a sprite is requested before warmup finishes, CUCoreLib still falls back to the normal synchronous load path.</p>
      <ul>
        <li>To embed a file, add it to your project and open its properties.</li>
        <li>Set the build action to <span class="inline-code">Embedded Resource</span>.</li>
        <li>They are great for item icons, building hit sounds, loop audio, default text files, and required assets.</li>
      </ul>
      ${docsVideo(externalVideoUrls.embeddingImages, "/videos/embedding-images.mp4", "screenshot docs-video")}
      <p><span class="inline-code">AssetLoader.LoadEmbeddedSprite</span> and <span class="inline-code">LoadEmbeddedText</span> search the calling assembly by suffix, so you can usually pass only the unique tail of the manifest resource name.</p>
      <pre><code>Sprite icon = AssetLoader.LoadEmbeddedSprite("Images.sunpear.png");
string json = AssetLoader.LoadEmbeddedText("Data.default-loot.json");</code></pre>
    </section>
    
    <section class="lesson-card">
      <h2>Resource names</h2>
      <p>.NET names embedded resources using the project's default namespace plus the folder and file name. A file at <span class="inline-code">Images/sunpear.png</span> often becomes something like <span class="inline-code">MyMod.Images.sunpear.png</span>.</p>
      <p>CUCoreLib lets you pass the shorter suffix when it is unique. If two resources end the same way, use a longer suffix so the loader knows which one you mean.</p>
      <pre><code>// Usually enough:
Sprite icon = AssetLoader.LoadEmbeddedSprite("Images.sunpear.png");

// More explicit if needed:
Sprite icon = AssetLoader.LoadEmbeddedSprite("MyMod.Images.sunpear.png");
</code></pre>
    </section>
    <section class="lesson-card">
      <h2>Loose files</h2>
      <p>Use loose files when the asset should be user-editable, optional, or replaceable without recompiling the mod. For paths relative to your plugin DLL, use <span class="inline-code">LoadSpriteFromPluginFolder()</span>.</p>
      <p>Main-menu warmup only covers embedded image resources in v1. Loose plugin-folder images still load on demand when you first request them.</p>
      <p>Both embedded and loose sprites are converted to Unity sprites with point filtering and clamp wrapping, which is friendly for pixel-art item sprites.</p>
      <pre><code>Sprite icon = AssetLoader.LoadSpriteFromPluginFolder(this, "Images/sunpear.png");</code></pre>
      <p>In this example, the file would be located at <span class="inline-code">BepInEx/plugins/MyMod/Images/sunpear.png</span>.</p>
      </section>
    <section class="lesson-card">
      <h2>Shared asset cache</h2>
      <p>When more than one system needs to resolve the same loaded asset by ID later, cache it once and reuse it. CUCoreLib exposes cache helpers for shared sprites.</p>
      <pre><code>Sprite icon = AssetLoader.LoadEmbeddedSprite("Images.sunpear.png");

AssetLoader.CacheSprite("sunpear", icon);

Sprite cachedIcon = AssetLoader.GetCachedSprite("sunpear");</code></pre>
      <p>Audio loading, caching, and playback patterns now have their own dedicated page so they are easier to find when you are wiring loops, hit sounds, or sound packs.</p>
      </section>
    <section class="lesson-card">
      <h2>AssetBundles in v1</h2>
      <p>CUCoreLib's first AssetBundle pass is intentionally narrow: use bundles for minigame screen prefabs and body <span class="inline-code">AnimationCurve</span> data. BuildingEntity bundles are out of scope for this rollout, so do not treat this as a general prefab registry yet.</p>
      <p>Register the bundle once, then resolve assets from it by bundle ID plus asset name. Loose bundles are best when you want a replaceable <span class="inline-code">.bundle</span> file beside the plugin DLL; embedded bundles are best when the bundle itself should ship inside the mod assembly.</p>
      <pre><code>// Loose bundle next to your plugin DLL:
AssetLoader.RegisterBundleFromPluginFolder(this, "glassworks.minigames", "Bundles/glassworks-minigames");

// Embedded bundle packed into the DLL as an Embedded Resource:
AssetLoader.RegisterEmbeddedBundle("glassworks.default", "Bundles.glassworks-default");

// Generic resolution stays available for advanced mods:
if (AssetLoader.TryLoadBundleAsset("glassworks.minigames", "WireSpliceScreen", out GameObject screenPrefab))
{
    Logger.LogInfo("Loaded bundled screen prefab: " + screenPrefab.name);
}</code></pre>
      <p>Bundle asset names must match the names you exported in Unity. For minigame screens that usually means a prefab asset name such as <span class="inline-code">WireSpliceScreen</span>. For curves, CUCoreLib expects ScriptableObject wrapper assets described below, not raw <span class="inline-code">AnimationCurve</span> fields floating by themselves.</p>
    </section>
    <section class="lesson-card">
      <h2>Bundle invalidation hooks</h2>
      <p>CUCoreLib exposes explicit cache invalidation because bundle lifetimes are now separate from sprite/audio lifetimes. That lets your mod clear a bundle registration before re-registering it, or discard the loaded bundle handle without promising full live hot-reload semantics.</p>
      <pre><code>AssetLoader.InvalidateBundle("glassworks.minigames");   // unloads the bundle handle + clears resolved bundle-asset caches
AssetLoader.UnregisterBundle("glassworks.minigames");  // invalidates and removes the registration definition
AssetLoader.InvalidateAllBundles();                    // clears every registered bundle handle</code></pre>
      <p>v1 only guarantees these hooks clear CUCoreLib-owned caches. They are not a promise that every live scene instance or Unity reference updates itself automatically after the bundle changes.</p>
    </section>
    <section class="lesson-card">
      <h2>Unity / ThunderKit authoring</h2>
      <p>For minigame screens, build a normal Unity prefab with a root <span class="inline-code">GameObject</span> or <span class="inline-code">RectTransform</span> and export it into an AssetBundle through your usual ThunderKit or Unity build pipeline. CUCoreLib instantiates that prefab under the live minigame screen canvas, then applies the same sibling-index and <span class="inline-code">spawnedMiniGame</span> bookkeeping the vanilla runner uses.</p>
      <p>For body curves, create a CUCoreLib-owned <span class="inline-code">AnimationCurveAsset</span> ScriptableObject and assign the curve into its <span class="inline-code">Curve</span> field. If you want a reusable batch, create a <span class="inline-code">BodyAnimationCurveProfileAsset</span> and fill its array with <span class="inline-code">BodyAnimationCurveField</span> plus asset-name pairs.</p>
      <pre><code>// Unity authoring summary:
// 1. Create prefab asset: WireSpliceScreen
// 2. Create ScriptableObject asset: SprintStaminaCurve (AnimationCurveAsset)
// 3. Optional batch asset: WinterProfile (BodyAnimationCurveProfileAsset)
// 4. Export them into an AssetBundle with stable asset names</code></pre>
      <p>The profile stores asset-name strings on purpose so the same bundle can keep one reusable profile asset that points at multiple curve assets without forcing CUCoreLib into a separate registry layer.</p>
    </section>
    <details open>
      <summary>When to choose which</summary>
      <div class="details-body">
        <ul>
          <li>Embed required icons, shipped sounds, and bundled text or JSON files.</li>
          <li>Use loose files for texture packs, config-adjacent assets, or anything players may replace.</li>
          <li>Use AssetBundles in v1 only for minigame prefabs and body-curve data.</li>
          <li>See the Audio page for <span class="inline-code">AudioClip</span>-specific loading, caching, and playback guidance.</li>
        </ul>
      </div>
    </details>
  `;
}

function audioPage(): string {
  return `
    <section class="lesson-card">
      <h2>Embedded audio</h2>
      <p>Use embedded audio when the clip is required for your mod to function: hit sounds, machine loops, voice lines, or other shipped defaults. Set the audio file's Build Action to <span class="inline-code">Embedded Resource</span>, then load it by suffix from the calling assembly.</p>
      <pre><code>AudioClip centrifugeHit = AssetLoader.LoadEmbeddedAudio("Audio.centrifuge-hit.wav");</code></pre>
      <p><span class="inline-code">AssetLoader</span> caches embedded audio by assembly plus normalized resource path, so repeating the same load call returns the same clip instead of decoding it again.</p>
    </section>

    <section class="lesson-card">
      <h2>Loose audio files</h2>
      <p>Use loose files when players should be able to swap clips without rebuilding your DLL, such as sound packs or optional ambience. <span class="inline-code">LoadAudioFromPluginFolder()</span> resolves a path relative to your plugin DLL.</p>
      <pre><code>AudioClip loop = AssetLoader.LoadAudioFromPluginFolder(this, "Audio/centrifuge-loop.wav");</code></pre>
      <p>The plugin-folder example points at <span class="inline-code">BepInEx/plugins/MyMod/Audio/centrifuge-loop.wav</span>. Loose file loads are cached by full file path so later calls reuse the same clip object.</p>
    </section>

    <section class="lesson-card">
      <h2>Shared audio cache</h2>
      <p>Please cache your audio. Loading audio every time you want to use it is inefficient.</p>
      <p>Luckily, there is a built-in way:</p>
      <pre><code>AudioClip embeddedLoop = AssetLoader.LoadEmbeddedAudio("Audio.centrifuge-loop.wav");
AssetLoader.CacheAudioClip("modname.centrifuge.loop", embeddedLoop);
AudioClip cachedLoop = AssetLoader.GetCachedAudioClip("modname.centrifuge.loop");</code></pre>
    </section>

    <section class="lesson-card">
      <h2>Using audio in gameplay</h2>
      <p>Loaded clips are standard Unity <span class="inline-code">AudioClip</span> objects. You can pass them into CUCoreLib definitions that accept clips or play them directly with <span class="inline-code">CUCoreUtils.PlaySoundAt()</span>.</p>
      <pre><code>AudioClip centrifugeHit = AssetLoader.LoadEmbeddedAudio("Audio.centrifuge-hit.wav");

BuildingEntityRegistry.Register("centrifuge", new CustomBuildingEntityDefinition
{
    Name = "Centrifuge",
    Sprite = AssetLoader.LoadEmbeddedSprite("Images.centrifuge.png", 8f),
    HitSound = centrifugeHit // Plays the audio clip on claw hit
});</code></pre>
      <pre><code>CUCoreUtils.PlaySoundAt(
    AssetLoader.GetCachedAudioClip("yourmod.verycoolsound.effect"),
    volume: 0.7f,
    delay: 0.1f,
    position: (Vector2)transform.position,
    pitch: 1.1f
);</code></pre>
      <pre><code>CUCoreUtils.Talk("Hello there.");
CUCoreUtils.TalkElectronic("Beep boop.", item);</code></pre>
    </section>

    <details open>
      <summary>Supported formats and tips</summary>
      <div class="details-body">
        <ul>
          <li><span class="inline-code">AssetLoader</span> supports <span class="inline-code">.wav</span>, <span class="inline-code">.mp1</span>, <span class="inline-code">.mp2</span>, <span class="inline-code">.mp3</span>, <span class="inline-code">.cue</span>, <span class="inline-code">.aif</span>, and <span class="inline-code">.aiff</span>.</li>
          <li>Sorry, <span class="inline-code">.ogg</span> enjoyers.</li>
          <li>Like images, it is preferred to use embedded clips for required defaults and loose files for player-replaceable audio packs.</li>
        </ul>
      </div>
    </details>
  `;
}

function consolePage(): string {
  return `
    <section class="lesson-card">
      <h2>How the game console works</h2>
      <p>Vanilla <span class="inline-code">ConsoleScript</span> stores commands in a static <span class="inline-code">ConsoleScript.Commands</span> list. Each <span class="inline-code">Command</span> owns a name, description, action, optional autofill, and optional argument descriptions.</p>
      <p>CUCoreLib's <span class="inline-code">ConsoleCommandRegistry</span> adds commands after vanilla command registration, so dependent mods do not need their own console Harmony patch.</p>
      <pre><code>ConsoleCommandRegistry.Register(
    string name,
    string description,
    Command.Action action,
    Dictionary&lt;int, List&lt;string&gt;&gt; argAutofill = null,
    params (string shortDesc, string longDesc)[] argDescription
);</code></pre>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr>
              <th>Argument</th>
              <th>Type</th>
              <th>What it needs</th>
            </tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">name</span></td><td><span class="inline-code">string</span></td><td>The command text the player types, without spaces. Example: <span class="inline-code">whereami</span>.</td></tr>
            <tr><td><span class="inline-code">description</span></td><td><span class="inline-code">string</span></td><td>Help text shown by the console command list.</td></tr>
            <tr><td><span class="inline-code">action</span></td><td><span class="inline-code">Command.Action</span></td><td>The delegate that runs when the command is executed. It receives <span class="inline-code">string[] args</span>.</td></tr>
            <tr><td><span class="inline-code">argAutofill</span></td><td><span class="inline-code">Dictionary&lt;int, List&lt;string&gt;&gt;</span></td><td>Optional autocomplete suggestions per argument index. Index <span class="inline-code">0</span> means the first argument after the command name.</td></tr>
            <tr><td><span class="inline-code">argDescription</span></td><td><span class="inline-code">params (string, string)[]</span></td><td>Optional argument labels and descriptions. The first string is the short label shown in command usage; the second string explains it.</td></tr>
          </tbody>
        </table>
      </div>
      <p>Inside the action, <span class="inline-code">args[0]</span> is the command name. The first value the player typed after the command is <span class="inline-code">args[1]</span>.</p>
    </section>
    <section class="lesson-card">
      <h2>When to use a command</h2>
      <p>Use console commands for debugging, development tools, diagnostics, and explicit player/admin actions. Do not hide core gameplay behavior behind a console command if it should happen automatically in normal play.</p>
      <p>Descriptions and argument metadata matter: the vanilla console shows them while typing, and the first argument is <span class="inline-code">args[1]</span> because <span class="inline-code">args[0]</span> is the command name.</p>
    </section>
    <details open>
      <summary>Autofill and argument descriptions</summary>
      <div class="details-body">
        <p>The game's <span class="inline-code">Command</span> constructor auto-adds bool autofill for argument descriptions beginning with <span class="inline-code">bool</span>, and position autofill for descriptions beginning with <span class="inline-code">position</span>.</p>
        <pre><code>ConsoleCommandRegistry.Register(
    "teleportmarker",
    "Teleports the player to a saved marker.",
    args =>
    {
        CUCoreUtils.ConsoleCheckForWorld(ConsoleScript.instance);
        string marker = args[1];
        bool snapToGround = bool.Parse(args[2]);
    },
    new Dictionary&lt;int, List&lt;string&gt;&gt;
    {
        // Suggestions for args[1].
        { 0, new List&lt;string&gt; { "home", "camp", "exit" } }
    },
    ("string marker", "Saved marker name"),
    ("bool snapToGround", "Whether to place the player on the ground")
);</code></pre>
        <img src="images/teleportmarker-command-ingame.png" alt="In-game screenshot of console autofill suggestions" class="screenshot" />
        <p>Because the second argument description starts with <span class="inline-code">bool</span>, vanilla adds <span class="inline-code">true</span> and <span class="inline-code">false</span> suggestions automatically. Descriptions beginning with <span class="inline-code">position</span> get <span class="inline-code">cursor</span>, <span class="inline-code">player</span>, <span class="inline-code">random</span>, and <span class="inline-code">#.#</span>.</p>
      </div>
    </details>
    
  `;
}

function debugTestingPage(): string {
  return `
    <section class="lesson-card">
      <h2>Fast mod-testing loop</h2>
      <p>Most CUCoreLib iteration gets faster when you separate startup-only work from content registration. Keep console commands, Harmony patches, and other one-time setup in <span class="inline-code">Awake()</span>, but move reloadable content into a dedicated host class or explicit replay methods.</p>
      <p>That split still helps, but the default hot reload mode is now more forgiving. CUCoreLib reruns broad post-marker code, blocks known startup-only APIs during reload with warnings, and only asks you to restructure when replay still cannot safely discover the content work.</p>
    </section>

    <section class="lesson-card">
     
      
      <h2>Usage</h2>
      
      <p>The preferred entrypoint contract is a single marker call inside your plugin <span class="inline-code">Awake()</span>:</p>
      <pre><code>private void Awake()
{
    CacheMyAssets(); // startup-only work
    ContentReloadManager.EnableHotReload(GUID);
    RegisterReloadable();
    FantasyGameplayHooks.PatchAll(harmony, Logger);
}</code></pre>
      <p>Everything before <span class="inline-code">EnableHotReload(GUID)</span> is startup-only and never replayed. The marker call must use the plugin's literal GUID string constant so the scanner can verify it against <span class="inline-code">[BepInPlugin]</span>.</p>
      <p>In the default flexible guarded mode, CUCoreLib scans calls that appear after the marker, follows local helper-to-helper chains, discovers replayable registration leaves, and suppresses known startup-only APIs such as Harmony setup during replay with a warning instead of failing the whole reload.</p>
      <p>If you want stricter behavior, use the options overload and opt into <span class="inline-code">HotReloadMode.Strict</span>. Strict mode preserves the old fail-fast behavior for unsupported startup-only APIs.</p>
      ${docsVideo(externalVideoUrls.hotReload, "/videos/hot-reload.mp4", "screenshot docs-video")}

      <h2>Commands</h2>
      <pre><code>
      reloadcontent com.example.mymod
      autohotreload com.example.mymod true
      autohotreload com.example.mymod false
      </code></pre>
      <ul>
        <li><span class="inline-code">reloadcontent &lt;modGuid&gt;</span> re-reads the mod's configured hot reload source DLL, clears the mod's previously registered reloadable content, and replays only recognized content methods from that rebuilt assembly.</li>
        <li><span class="inline-code">autohotreload &lt;modGuid&gt; true</span> enables persistent watch mode for the same configured hot reload source DLL of a mod that previously called <span class="inline-code">EnableHotReload(GUID)</span>.</li>
        <li><span class="inline-code">autohotreload &lt;modGuid&gt; false</span> disables the saved watch mode for that mod.</li>
      </ul>
      <p>By default, the hot reload source is still the deployed plugin DLL that BepInEx loaded. If Windows refuses to overwrite that file while the game is open, set <span class="inline-code">[Hot Reload]</span> -&gt; <span class="inline-code">&lt;modGuid&gt;.overridePath</span> in <span class="inline-code">CUCoreLib.cfg</span> to an alternate rebuilt DLL path such as your project's <span class="inline-code">bin\\Debug</span> output.</p>
      <p>Watch mode polls the selected source DLL every few seconds, waits for the file hash to settle, then runs the same reload flow automatically. That means it can watch either the deployed plugin DLL or your configured override path.</p>

      <h2>Automatic discovery rules</h2>
      <ul>
        <li>Supported reload surfaces are asset loading, locale/text, liquids, items, basic building entities, and recipes.</li>
        <li>Harmony patch setup, console command registration, save hooks, mod options, multiplayer hooks, tiles, structures, and live scene mutation currently do not work and are blocked.</li>
      </ul>

       <h2>Limitations</h2>
      <p>CUCoreLib's built-in DLL reload path is still <strong>singleplayer only</strong> and only restores reloadable content owned by that mod after clearing the old registered entries.</p>
      </section>

    <section class="lesson-card">
      <h2>Testing in multiplayer</h2>
      <ul>
        <li>In <span class="inline-code">Casualties Unknown Demo\\CasualtiesUnknown_Data\\boot.config</span>, remove the <span class="inline-code">forceSingleInstance: 1</span> line.</li>
        <li>Open the multiplayer mod on two instances and swap from Steam to IP hosting.</li>
        <li>The name of the client and the host must be different.</li>
        <li>Connect on both, and you can test multiplayer interactions.</li>
      </ul>
    </section>

    <section class="lesson-card">
      <h2>Launch overrides and optimizations</h2>
      <p>For testing elements that are not covered by hot reload, CUCoreLib has a configuration for loading directly into a test world or tutorial.</p>
      <p>In BepInEx -> Config -> CUCoreLib.cfg, there are options to instantly load the debug world or tutorial on launch.</p>
      <img src="images/cucorelib-config.png" alt="CUCoreLib .cfg config" class="screenshot">
    </section>

    <section class="lesson-card">
      <h2>Debugwatch overlay</h2>
      <p>CUCoreLib also includes a built-in <span class="inline-code">debugwatch</span> command for runtime value watching and debugging.</p>
      <p>The command resolves supported static fields by reflected <span class="inline-code">Type.member</span> name, then shows watched values as white text in the top-right corner while a world is active.</p>
      <pre><code>debugwatch add MyNamespace.MyPlugin.healthRate
debugwatch add MyNamespace.MyPlugin.debugEnabled
debugwatch clear</code></pre>
      <p>Currently, debugwatch supports static fields only. Supported field types are simple values such as numbers, <span class="inline-code">bool</span>, <span class="inline-code">string</span>, enums, and <span class="inline-code">Vector2</span>/<span class="inline-code">Vector3</span>.</p>
    </section>


  `;
}

function utilsPage(): string {
  return `
    <section class="lesson-card">
      <h2>CUCoreUtils surface map</h2>
      <p>This page documents the current public surface in <span class="inline-code">Helpers/CUCoreUtils.cs</span>.</p>
      <p>Where CUCoreLib exposes both PascalCase and camelCase names, both are listed below.</p>
    </section>

    <section class="lesson-card">
      <h2>General runtime helpers</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">StartCoroutine</span></td><td><span class="inline-code">IEnumerator routine</span></td><td>Runs a coroutine from CUCoreLib's hidden persistent runner.</td></tr>
            <tr><td><span class="inline-code">StartCoroutine</span></td><td><span class="inline-code">Func&lt;IEnumerator&gt; routineFactory</span></td><td>Builds the coroutine lazily, then runs it through the same shared runner.</td></tr>
            <tr><td><span class="inline-code">DelayCall</span></td><td><span class="inline-code">float delaySeconds, Action action</span></td><td>Runs an action after a realtime delay.</td></tr>
            <tr><td><span class="inline-code">CallWhen</span></td><td><span class="inline-code">Func&lt;bool&gt; condition, Action action, float checkRepeatTimeSeconds = 0f</span></td><td>Polls until the condition is true, then runs the action.</td></tr>
            <tr><td><span class="inline-code">AwaitMainMenu</span> / <span class="inline-code">awaitMainMenu</span></td><td><span class="inline-code">float checkRepeatTimeSeconds = 0f</span></td><td>Coroutine wait helper for the main menu becoming available.</td></tr>
            <tr><td><span class="inline-code">AwaitWorldGeneration</span> / <span class="inline-code">awaitWorldGeneration</span></td><td><span class="inline-code">float checkRepeatTimeSeconds = 0f</span></td><td>Coroutine wait helper for the runtime world finishing generation.</td></tr>
            <tr><td><span class="inline-code">IsMainMenuReady</span></td><td>None</td><td>Returns whether the game is currently at a usable main-menu state.</td></tr>
            <tr><td><span class="inline-code">IsWorldGenerationReady</span></td><td>None</td><td>Returns whether the world exists and is no longer generating.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>PlayerPrefsUtils</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">GetBool</span> / <span class="inline-code">getBool</span></td><td><span class="inline-code">string key, bool defaultValue = false</span></td><td>Reads a boolean value from <span class="inline-code">PlayerPrefs</span>.</td></tr>
            <tr><td><span class="inline-code">SetBool</span> / <span class="inline-code">setBool</span></td><td><span class="inline-code">string key, bool value</span></td><td>Stores a boolean value in <span class="inline-code">PlayerPrefs</span>.</td></tr>
            <tr><td><span class="inline-code">GetFloat</span> / <span class="inline-code">getFloat</span></td><td><span class="inline-code">string key, float defaultValue = 0f</span></td><td>Reads a float value from <span class="inline-code">PlayerPrefs</span>.</td></tr>
            <tr><td><span class="inline-code">SetFloat</span> / <span class="inline-code">setFloat</span></td><td><span class="inline-code">string key, float value</span></td><td>Stores a float value in <span class="inline-code">PlayerPrefs</span>.</td></tr>
            <tr><td><span class="inline-code">GetString</span> / <span class="inline-code">getString</span></td><td><span class="inline-code">string key, string defaultValue = ""</span></td><td>Reads a string value from <span class="inline-code">PlayerPrefs</span>.</td></tr>
            <tr><td><span class="inline-code">SetString</span> / <span class="inline-code">setString</span></td><td><span class="inline-code">string key, string value</span></td><td>Stores a string value in <span class="inline-code">PlayerPrefs</span>.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>PlayerUtils</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">IsInWorld</span> / <span class="inline-code">isInWorld</span></td><td>None</td><td>Returns whether a usable runtime world and player body are available.</td></tr>
            <tr><td><span class="inline-code">TryGetBody</span> / <span class="inline-code">tryGetBody</span></td><td><span class="inline-code">out Body body</span></td><td>Attempts to get the active player body.</td></tr>
            <tr><td><span class="inline-code">TryGetCamera</span> / <span class="inline-code">tryGetCamera</span></td><td><span class="inline-code">out PlayerCamera camera</span></td><td>Attempts to get the active player camera.</td></tr>
            <tr><td><span class="inline-code">GetMousePosition</span> / <span class="inline-code">getMousePosition</span></td><td>None</td><td>Returns the current mouse or target-look position, depending on player state.</td></tr>
            <tr><td><span class="inline-code">ShowAlert</span> / <span class="inline-code">showAlert</span></td><td><span class="inline-code">string text, bool? important = false</span></td><td>Shows an on-screen alert (popup) immediately.</td></tr>
            <tr><td><span class="inline-code">Alert</span> / <span class="inline-code">alert</span></td><td><span class="inline-code">string text, bool important, float delay = 0f</span></td><td>Shows an on-screen alert (popup) immediately or after a delay.</td></tr>
            <tr><td><span class="inline-code">Talk</span> / <span class="inline-code">talk</span></td><td><span class="inline-code">string dialogue</span></td><td>Routes dialogue through the player body's talker.</td></tr>
            <tr><td><span class="inline-code">TalkElectronic</span> / <span class="inline-code">talkElectronic</span></td><td><span class="inline-code">string dialogue, Item item = null</span></td><td>Routes dialogue through an item's talker or a watch-style fallback proxy.</td></tr>
            <tr><td><span class="inline-code">PlaySoundAt</span> / <span class="inline-code">playSoundAt</span></td><td><span class="inline-code">AudioClip clip, Vector2? pos = null</span></td><td>Plays a clip at a position or at the player by default. Potentially see Sound.Play as an alternative.</td></tr>
            <tr><td><span class="inline-code">PlaySoundAt</span> / <span class="inline-code">playSoundAt</span></td><td><span class="inline-code">AudioClip clip, float? volume = null, float? delay = null, Vector2? position = null, float? pitch = null</span></td><td>Plays a clip with optional volume, delay, position, and pitch overrides.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>PlayerInventoryUtils</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">TryGetHeldItem</span> / <span class="inline-code">tryGetHeldItem</span></td><td><span class="inline-code">out Item item</span></td><td>Attempts to get the item currently held by the player.</td></tr>
            <tr><td><span class="inline-code">HasEquipped</span> / <span class="inline-code">hasEquipped</span></td><td><span class="inline-code">string id</span></td><td>Returns whether the active player currently has an item with that ID in any body slot.</td></tr>
            <tr><td><span class="inline-code">TryGetHoveredItem</span> / <span class="inline-code">tryGetHoveredItem</span></td><td><span class="inline-code">out Item item</span></td><td>Attempts to find the item currently under the mouse, either in UI or in the world.</td></tr>
            <tr><td><span class="inline-code">GiveItem</span> / <span class="inline-code">giveItem</span></td><td><span class="inline-code">string id, int count</span></td><td>Spawns item instances near the player and auto-picks them up.</td></tr>
            <tr><td><span class="inline-code">GiveItemSlot</span> / <span class="inline-code">giveItemSlot</span></td><td><span class="inline-code">string id, int slot, int count</span></td><td>Spawns item instances near the player and tries to put them into a specific slot.</td></tr>
            <tr><td><span class="inline-code">TryGetCustomItemInfo</span> / <span class="inline-code">tryGetCustomItemInfo</span></td><td><span class="inline-code">string id, out CustomItemInfo info</span></td><td>Looks up registered custom item metadata by ID.</td></tr>
            <tr><td><span class="inline-code">SetWornSprite</span> / <span class="inline-code">setWornSprite</span></td><td><span class="inline-code">string itemId, Sprite wornSprite</span></td><td>Updates a registered custom item's primary worn sprite and refreshes all live instances with that item ID.</td></tr>
            <tr><td><span class="inline-code">SetWornSprite</span> / <span class="inline-code">setWornSprite</span></td><td><span class="inline-code">Item item, Sprite wornSprite</span></td><td>Convenience overload that updates the registered custom item backing that live item instance, then refreshes matching live instances.</td></tr>
            <tr><td><span class="inline-code">IsModdedItem</span> / <span class="inline-code">isModdedItem</span></td><td><span class="inline-code">string itemId</span></td><td>Returns whether an item ID belongs to a recognized modded item path.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>PlayerLimbUtils</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">DoAmputate</span> / <span class="inline-code">doAmputate</span></td><td><span class="inline-code">Item item, Limb limb</span></td><td>Ow!</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="lesson-card">
      <h2>ReflectionUtils</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">GetMethod</span></td><td><span class="inline-code">object target, string methodName</span></td><td>Returns a reflected method from the target object or type.</td></tr>
            <tr><td><span class="inline-code">InvokeMethod</span></td><td><span class="inline-code">object target, string methodName, params object[] args</span></td><td>Looks up a method and invokes it with the supplied arguments.</td></tr>
          </tbody>
        </table>
      </div>
    </section>


    <section class="lesson-card">
      <h2>Console and misc helpers</h2>
      <div class="table-wrap">
        <table class="field-table">
          <thead>
            <tr><th>Method</th><th>Args</th><th>Description</th></tr>
          </thead>
          <tbody>
            <tr><td><span class="inline-code">ConsoleLog</span></td><td><span class="inline-code">ConsoleScript instance, string message</span></td><td>Writes a message into the in-game console through the game's internal logging path.</td></tr>
            <tr><td><span class="inline-code">ConsoleRunCommand</span></td><td><span class="inline-code">ConsoleScript instance, string commandString</span></td><td>Executes a command string through the in-game console.</td></tr>
            <tr><td><span class="inline-code">ConsoleCheckForWorld</span></td><td><span class="inline-code">ConsoleScript instance</span></td><td>Runs the game's console-side world check before world-dependent commands.</td></tr>
            <tr><td><span class="inline-code">LoadEmbeddedStream</span></td><td><span class="inline-code">string resourcePath, Assembly sourceAssembly = null</span></td><td>Opens a readable embedded-resource stream using the same suffix-based lookup as other CUCoreLib loaders. Dispose the returned stream when finished.</td></tr>
            <tr><td><span class="inline-code">CompressGZip</span></td><td><span class="inline-code">byte[] data</span></td><td>Compresses a byte array with GZip.</td></tr>
            <tr><td><span class="inline-code">DecompressGZip</span></td><td><span class="inline-code">byte[] compressedData</span></td><td>Decompresses a GZip byte array.</td></tr>
            <tr><td><span class="inline-code">CompressDeflate</span></td><td><span class="inline-code">byte[] data</span></td><td>Compresses a byte array with Deflate.</td></tr>
            <tr><td><span class="inline-code">DecompressDeflate</span></td><td><span class="inline-code">byte[] compressedData</span></td><td>Decompresses a Deflate byte array.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

  `;
}

function textInput(id: string, label: string, value: string, hint = "", full = false): string {
  return `<label class="${full ? "full" : ""}">${label}<input id="${id}" value="${escapeHtml(value)}">${hint ? `<span class="hint">${hint}</span>` : ""}</label>`;
}

function textareaInput(id: string, label: string, value: string): string {
  return `<label class="full">${label}<textarea id="${id}">${escapeHtml(value)}</textarea></label>`;
}

function selectInput(id: string, label: string, options: string[], value: string): string {
  const optionHtml = options.map((option) => `<option value="${escapeHtml(option)}"${option === value ? " selected" : ""}>${option || "none"}</option>`).join("");
  return `<label>${label}<select id="${id}">${optionHtml}</select></label>`;
}

function rangeInput(id: string, label: string, min: string, max: string, step: string, value: string, hint = ""): string {
  return `<label>${label}<span class="range-row"><input id="${id}" type="range" min="${min}" max="${max}" step="${step}" value="${value}"><span class="range-value" data-range-for="${id}">${value}</span></span>${hint ? `<span class="hint">${hint}</span>` : ""}</label>`;
}

function checkboxInput(id: string, label: string, checked: boolean, full = false): string {
  return `<label class="checkbox-row ${full ? "full" : ""}"><input id="${id}" type="checkbox"${checked ? " checked" : ""}>${label}</label>`;
}

function ingredientCards(): string {
  return ingredients
    .map(
      (ingredient, index) => `
        <section class="ingredient-card">
          <header>
            <h3>Ingredient ${index + 1}</h3>
            <button class="mini-button" type="button" data-remove-ingredient="${index}">Remove</button>
          </header>
          <div class="form-grid">
            <label>Match Type
              <select data-ingredient-field="mode" data-index="${index}">
                <option value="specific"${ingredient.mode === "specific" ? " selected" : ""}>Specific ID</option>
                <option value="quality"${ingredient.mode === "quality" ? " selected" : ""}>Crafting Quality</option>
              </select>
            </label>
            <label>ID Or Quality
              <input value="${escapeHtml(ingredient.id)}" data-ingredient-field="id" data-index="${index}">
            </label>
            <label>Amount / Minimum Condition
              <input type="number" min="0" step="0.1" value="${escapeHtml(ingredient.amount)}" data-ingredient-field="amount" data-index="${index}">
            </label>
            <label class="checkbox-row">
              <input type="checkbox" ${ingredient.isLiquid ? "checked" : ""} data-ingredient-field="isLiquid" data-index="${index}">
              Liquid ingredient
            </label>
            <label class="checkbox-row full">
              <input type="checkbox" ${ingredient.destroyItem ? "checked" : ""} data-ingredient-field="destroyItem" data-index="${index}">
              Consume or destroy this ingredient
            </label>
          </div>
        </section>
      `
    )
    .join("");
}
