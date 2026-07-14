// Decompiled game code for all current items in the v7-pre demo version.
// You can copy almost anything inside the ItemInfo{} brackets and it will work with CUCoreLib
/*

E.g. you can copy the stats for a steak item into your own mod as a base

ItemRegistry.Register("someCoolFoodName", new CustomItemInfo
	{
		
		// === COPY START ===
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(15f, 1.6f);
			body.happiness += 2f;
			item.condition -= 0.334f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(0)
		// === COPY END ===

	},
	AssetLoader.LoadEmbeddedSprite("Images.someSweetFoodPixelArt.png")
);

And it will work fine.

Note that Liquids, Containers, Batteries, Banages, Syringes, Tools, and inbuilt Lighting require the advanced item API fields to work well, due to being set via unity's inspector when created.
As such, these examples should only really be for basic items (e.g. foods/drinks) or for reference and info about item fields. 

*/

public static void SetupItems()
{
	GlobalItems = new Dictionary<string, ItemInfo>();
	GlobalItems.Add("bandage", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.9f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, Color.white, scaleLimb: true);
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 12f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 30f;
				limb.bandageSlowAmount += num2 * 45f;
				limb.pain -= num2 * 60f;
				limb.boneHealTimer -= num2 * 20f;
				limb.dislocationTimer -= num2 * 20f;
			}, Color.white, limb), item);
		},
		tags = "dressing,medicine",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("dressing")
		},
		value = 10,
		rec = new Recognition(1)
	});
	GlobalItems.Add("rippeddressing", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 8f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 8f;
				limb.bandageSlowAmount += num2 * 18f;
				limb.pain -= num2 * 40f;
				limb.boneHealTimer -= num2 * 5f;
				limb.dislocationTimer -= num2 * 5f;
			}, new Color(0.9f, 0.9f, 0.9f), limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, Color.white, scaleLimb: true);
		},
		value = 4,
		tags = "medicine",
		rec = new Recognition(1)
	});
	GlobalItems.Add("bruisekit", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.9f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 10f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 100f;
				CoUtils.instance.DoTimedOp("bruisekit" + limb.name, delegate
				{
					limb.muscleHealth += 1f;
				}, num2 * 140f);
				limb.pain -= num2 * 80f;
				limb.dislocationTimer -= num2 * 80f;
			}, Color.green, limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, Color.green, scaleLimb: true);
		},
		value = 12,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("medicalsuture", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			limb.body.DoGoreSound();
			limb.pain += 12.5f;
			limb.skinHealAmount += 25f;
			CoUtils.instance.DoTimedOp("suture" + limb.name, delegate
			{
				limb.bleedAmount -= 4.5f;
			}, 10f);
			item.condition -= 0.51f;
		},
		value = 8,
		tags = "dressing,medicine",
		rec = new Recognition(7)
	});
	GlobalItems.Add("tourniquet", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		weight = 0.2f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (!limb.isVital && limb != limb.body.limbs[2] && !limb.GetComponent<TourniquetScript>())
			{
				item.condition -= 0.125f;
				limb.AddComponent<TourniquetScript>().condition = item.condition;
				UnityEngine.Object.Destroy(item.gameObject);
			}
		},
		value = 10,
		tags = "medicine",
		rec = new Recognition(4)
	});
	GlobalItems.Add("adhesivebandage", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.25f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			limb.skinHealAmount += 3f;
			limb.bandageSlowAmount += 6f;
			limb.pain *= 0.9f;
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/ashSprite"), 0f, Color.white);
			item.condition -= 0.16f;
		},
		value = 6,
		tags = "dressing,medicine",
		rec = new Recognition(1)
	});
	GlobalItems.Add("analgesicgauze", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 15f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 20f;
				limb.bandageSlowAmount += num2 * 50f;
				limb.pain -= num2 * 300f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += num2 * 28f;
			}, new Color32(158, 167, 194, byte.MaxValue), limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/alginateWrap"), 0f, new Color32(158, 167, 194, byte.MaxValue), scaleLimb: true);
		},
		tags = "dressing,medicine",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("dressing")
		},
		value = 10,
		rec = new Recognition(3)
	});
	GlobalItems.Add("alginate", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 15f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 125f;
				limb.bandageSlowAmount += num2 * 72.5f;
				limb.pain -= num2 * 80f;
				limb.SetDisinfect(limb.disinfectionTime + num2 * 800f);
			}, Color.white, limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/alginateWrap"), 0f, Color.white, scaleLimb: true);
		},
		tags = "dressing,medicine",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("dressing")
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("rag", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 0.05f;
			body.wetness *= 0.5f;
			Sound.Play("splint", item.transform.position);
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 8f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 8f;
				limb.bandageSlowAmount += num2 * 10f;
				limb.pain -= num2 * 25f;
				limb.boneHealTimer -= num2 * 5f;
				limb.dislocationTimer -= num2 * 5f;
			}, new Color32(143, 126, 139, byte.MaxValue), limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, new Color32(143, 126, 139, byte.MaxValue), scaleLimb: true);
		},
		tags = "dressing,medicine",
		value = 4,
		rec = new Recognition(0)
	});
	GlobalItems.Add("sterilizedbandage", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.9f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 12f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 30f;
				limb.bandageSlowAmount += num2 * 45f;
				limb.pain -= num2 * 60f;
				limb.boneHealTimer -= num2 * 20f;
				limb.dislocationTimer -= num2 * 20f;
				limb.SetDisinfect(limb.disinfectionTime + num2 * 900f);
			}, Color.gray, limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, new Color32(128, 128, 128, byte.MaxValue), scaleLimb: true);
		},
		tags = "dressing,medicine",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("dressing")
		},
		value = 15,
		rec = new Recognition(1)
	});
	GlobalItems.Add("plasticbandage", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 12f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 60f;
				limb.bandageSlowAmount += num2 * 72f;
				limb.pain -= num2 * 100f;
				limb.boneHealTimer -= num2 * 30f;
				limb.dislocationTimer -= num2 * 30f;
			}, new Color32(97, 150, 204, byte.MaxValue), limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/bandageWrap"), 0f, new Color32(97, 150, 204, byte.MaxValue), scaleLimb: true);
		},
		tags = "dressing,medicine",
		value = 20,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("dressing")
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("musharm", new ItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		decayMinutes = 20f,
		combineable = true,
		weight = 0.8f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			Sound.Play("goo", limb.transform.position);
			MinigameBase.main.StartMinigame(new BandageMinigame(delegate(float normalAngle)
			{
				float num = 2.5f;
				float num2 = normalAngle / num;
				item.condition -= num2;
				limb.skinHealAmount += num2 * 8f;
				limb.bandageSlowAmount += num2 * 10f;
			}, new Color32(226, 136, 151, byte.MaxValue), limb), item);
			limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/musharmwrap"), 0f, Color.white, scaleLimb: true);
		},
		tags = "cangetwet",
		value = 1,
		rec = new Recognition(4),
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("foliage")
		}
	});
	GlobalItems.Add("paincream", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		autoFill = false,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("reliefcream", 100f)
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			item.GetComponent<WaterContainerItem>().ApplyToLimb(limb, 10f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("woundglue", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		autoFill = false,
		capacity = 80f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("woundglue", 80f)
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			item.GetComponent<WaterContainerItem>().ApplyToLimb(limb, 20f);
		},
		value = 1,
		tags = "dressing,medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("boneweldingtool", new ItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0f)
			{
				Sound.Play("boneweld", limb.body.transform.position);
				limb.skinHealth -= 25f;
				limb.muscleHealth -= 26f;
				limb.bleedAmount += 5f;
				limb.pain += 30f;
				limb.body.bloodViscosity += 2f;
				limb.boneHealTimer *= 0.25f;
				item.condition -= 0.5f;
			}
		},
		value = 20,
		tags = "medicine",
		rec = new Recognition(12)
	});
	GlobalItems.Add("morphine", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("morphine", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 10,
		tags = "medicine",
		rec = new Recognition(8)
	});
	GlobalItems.Add("syringe", new LiquidItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.25f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>(),
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 6,
		tags = "medicine",
		rec = new Recognition(4)
	});
	GlobalItems.Add("makeshiftlrd", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		capacity = 50f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("lrdserum", 50f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem component = item.GetComponent<WaterContainerItem>();
			WoundView.view.AddImageToLimb(limb, Resources.Load<Sprite>("Special/injectionWound"), flip: false);
			Sound.Play("syringe", limb.body.transform.position);
			if (component.AmountOf("lrdserum") >= 24f)
			{
				component.Drain(component.CalculateDrainSingleLiquid("lrdserum", 25f));
				limb.muscleHealth += 50f;
				limb.body.caffeinated += 60f;
				limb.infectionAmount -= 10f * limb.infectionSpeedMult;
				limb.SetDisinfect(400f);
				limb.body.adrenaline += 60f;
				limb.body.venomTotal = Mathf.MoveTowards(limb.body.venomTotal, 0f, 12f);
				Limb[] connectedLimbs = limb.connectedLimbs;
				foreach (Limb limb2 in connectedLimbs)
				{
					limb2.muscleHealth += 40f;
					limb2.infectionAmount -= 5f * limb2.infectionSpeedMult;
					limb2.SetDisinfect(300f);
				}
				if (limb == limb.body.limbs[1])
				{
					limb.body.internalBleeding *= 0.75f;
				}
			}
		},
		value = 5,
		tags = "medicine",
		rec = new Recognition(9)
	});
	GlobalItems.Add("lrd", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		ignoreDepression = true,
		weight = 0.6f,
		scaleWeightWithCondition = true,
		capacity = 75f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("lrdserum", 75f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem component = item.GetComponent<WaterContainerItem>();
			WoundView.view.AddImageToLimb(limb, Resources.Load<Sprite>("Special/injectionWound"), flip: false);
			Sound.Play("syringe", limb.body.transform.position);
			if (component.AmountOf("lrdserum") >= 24f)
			{
				component.Drain(component.CalculateDrainSingleLiquid("lrdserum", 25f));
				limb.muscleHealth += 50f;
				limb.infectionAmount -= 10f * limb.infectionSpeedMult;
				limb.SetDisinfect(600f);
				limb.body.caffeinated += 60f;
				limb.body.adrenaline += 90f;
				limb.bleedAmount *= 0.7f;
				limb.body.GetOrAddComponent<Painkillers>().opiateAmount += 10f;
				limb.body.venomTotal = Mathf.MoveTowards(limb.body.venomTotal, 0f, 12f);
				Limb[] connectedLimbs = limb.connectedLimbs;
				foreach (Limb limb2 in connectedLimbs)
				{
					limb2.muscleHealth += 40f;
					limb2.infectionAmount -= 5f * limb2.infectionSpeedMult;
					limb2.SetDisinfect(300f);
					limb.bleedAmount *= 0.85f;
				}
				if (limb == limb.body.limbs[1])
				{
					limb.body.internalBleeding *= 0.45f;
				}
			}
		},
		value = 12,
		tags = "medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("opium", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.25f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("opium", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(7)
	});
	GlobalItems.Add("heroin", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 150f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("heroin", 100f),
			new LiquidStack("water", 50f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 150f);
			}, limb, wat.AverageColor()), item);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("naloxone", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("naloxone", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("naltrexone", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("naltrexone", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 20f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("sodiumnitroprusside", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("sodiumnitroprusside", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 50f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(10)
	});
	GlobalItems.Add("vasopressin", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("vasopressin", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 50f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(10)
	});
	GlobalItems.Add("amiodarone", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("amiodarone", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 50f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("fentanyl", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("water", 90f),
			new LiquidStack("fentanyl", 10f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(10)
	});
	GlobalItems.Add("ceftriaxone", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("ceftriaxone", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 100f);
			}, limb, wat.AverageColor()), item);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(10)
	});
	GlobalItems.Add("painkillers", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("painkillers", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 10f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(4)
	});
	GlobalItems.Add("keratinbooster", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		autoFill = false,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("keratinbooster", 100f)
		},
		capacity = 100f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 50f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("braingrow", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("braingrow", 100f)
		},
		capacity = 100f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 20f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(8)
	});
	GlobalItems.Add("neuralbooster", new ItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f)
			{
				if (body.usedNeuralBooster)
				{
					body.sicknessAmount += 100f;
					body.brainHealth -= 30f;
					body.internalBleeding += 100f;
					body.shock = 10f;
					body.Ragdoll();
					body.vomiter.Vomit();
					body.temperature += 5f;
					body.happiness -= 10f;
					body.RemoveEye();
					body.RemoveEye();
					Limb[] limbs = body.limbs;
					for (int i = 0; i < limbs.Length; i++)
					{
						limbs[i].muscleHealth *= 0.2f;
					}
				}
				body.usedNeuralBooster = true;
				body.maxSpeed *= 1.25f;
				body.moveForce *= 1.25f;
				body.jumpSpeed *= 1.2f;
				body.brainHealth += 10f;
				body.strokeAmount = 0f;
				body.happiness += 20f;
				if (body.brainGrowSickness > 0f)
				{
					body.shock = 10f;
					body.Ragdoll();
					if (!body.GetComponent<MindwipeScript>())
					{
						body.gameObject.AddComponent<MindwipeScript>();
					}
				}
				body.brainGrowSickness = 1200f;
				body.talker.EatGood();
				body.caffeinated += 600f;
				Sound.Play("pills", body.transform.position);
				item.condition = 0f;
			}
		},
		value = 50,
		rec = new Recognition(18)
	});
	GlobalItems.Add("autopump", new ItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 1f,
		wearable = true,
		wearableCanBeHeld = true,
		wearableHitDurabilityLossMultiplier = 0f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "outertorso",
		wearableVisualOffset = 9,
		value = 20,
		tags = "medicine",
		rec = new Recognition(9)
	});
	GlobalItems.Add("aed", new ItemInfo
	{
		category = "utility",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		weight = 1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			float num = 0.02f;
			if (item.battery.GetCharge() > num * 100f)
			{
				MinigameBase.main.StartMinigame(new AEDMinigame(limb), item);
			}
		},
		value = 30,
		tags = "medicine",
		rec = new Recognition(3)
	});
	GlobalItems.Add("manualdefibrillator", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		weight = 1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.battery.hasCharge)
			{
				MinigameBase.main.StartMinigame(new ManualDefibMinigame(limb), item);
			}
		},
		value = 20,
		tags = "medicine",
		rec = new Recognition(8)
	});
	GlobalItems.Add("antidepressants", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		ignoreDepression = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		capacity = 140f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("antidepressants", 140f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 20f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("antibiotics", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("antibiotics", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 20f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(7)
	});
	GlobalItems.Add("mindwipe", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		ignoreDepression = true,
		capacity = 60f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("mindwipe", 30f),
			new LiquidStack("morphine", 30f)
		},
		useAction = delegate(Body body, Item item)
		{
			WaterContainerItem component = item.GetComponent<WaterContainerItem>();
			if (component.HasLiquid("mindwipe") && body.totalHappiness > -50f && body.brainHealth > 90f && body.strokeAmount < 5f)
			{
				body.talker.Talk("...");
			}
			else
			{
				component.Drink(body, 60f);
			}
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(0)
	});
	GlobalItems.Add("antiserum", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("antiserum", 100f)
		},
		capacity = 100f,
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			Sound.Play("syringe", limb.body.transform.position);
			item.GetComponent<WaterContainerItem>().Inject(limb, 50f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("antirad", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("antirad", 100f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 20f, "pills");
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(10)
	});
	GlobalItems.Add("sleepingpills", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.4f,
		scaleWeightWithCondition = true,
		capacity = 25f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("sleepingpills", 25f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body, 5f, "pills");
		},
		value = 0,
		tags = "medicine",
		rec = new Recognition(2)
	});
	GlobalItems.Add("rosepod", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 1.2f,
		useAction = delegate(Body body, Item item)
		{
			item.condition = 0f;
			int slot = body.SlotOf(item);
			body.DropItem(item);
			body.PickUpItem(UnityEngine.Object.Instantiate(Resources.Load("roselight"), item.transform.position, item.transform.rotation).GetComponent<Item>(), slot);
			Sound.Play("goo", item.transform.position);
		},
		value = 1,
		rec = new Recognition(0)
	});
	GlobalItems.Add("roselight", new ItemInfo
	{
		category = "custom",
		decayMinutes = 5f,
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		weight = 0.9f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			item.condition = 0f;
			if (limb.infected)
			{
				PlayerCamera.main.showInfection[Array.IndexOf(limb.body.limbs, limb)] = true;
				Sound.Play("goo", limb.transform.position);
			}
		},
		value = 1,
		rec = new Recognition(0)
	});
	GlobalItems.Add("splint", new ItemInfo
	{
		category = "medical",
		slotRotation = 90f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		weight = 1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (!limb.isHead && !limb.isVital && !limb.GetComponent<SplintLimb>())
			{
				Sound.Play("splint", limb.body.transform.position);
				SplintLimb splintLimb = limb.AddComponent<SplintLimb>();
				splintLimb.conditionLossMinute = 0.015f;
				splintLimb.condition = item.condition;
				splintLimb.item = "splint";
				limb.CreateTemporarySprite(item.GetComponent<SpriteRenderer>().sprite, -90f, null, scaleLimb: false, 1000f, (Limb x) => !x.GetComponent<SplintLimb>());
				UnityEngine.Object.Destroy(item.gameObject);
			}
		},
		value = 15,
		tags = "medicine",
		rec = new Recognition(4)
	});
	GlobalItems.Add("carcasssplint", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		weight = 1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (!limb.isHead && !limb.isVital && !limb.GetComponent<SplintLimb>())
			{
				Sound.Play("splint", limb.body.transform.position);
				SplintLimb splintLimb = limb.AddComponent<SplintLimb>();
				splintLimb.conditionLossMinute = 0.036f;
				splintLimb.condition = item.condition;
				splintLimb.item = "carcasssplint";
				limb.CreateTemporarySprite(item.GetComponent<SpriteRenderer>().sprite, 0f, null, scaleLimb: false, 1000f, (Limb x) => !x.GetComponent<SplintLimb>());
				UnityEngine.Object.Destroy(item.gameObject);
			}
		},
		value = 12,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("bloodcoagulant", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("procoagulant", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			Sound.Play("syringe", limb.body.transform.position);
			item.GetComponent<WaterContainerItem>().Inject(limb, 33.334f);
		},
		value = 1,
		tags = "dressing,medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("combatpen", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.15f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("highgradestimulant", 60f),
			new LiquidStack("epinephrine", 15f),
			new LiquidStack("oxyline", 25f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			Sound.Play("syringe", limb.body.transform.position);
			item.GetComponent<WaterContainerItem>().Inject(limb);
		},
		value = 10,
		tags = "medicine",
		rec = new Recognition(6)
	});
	GlobalItems.Add("clottingmush", new ItemInfo
	{
		category = "drug",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0f)
			{
				item.condition -= 0.34f;
				limb.body.bloodViscosity += 10f;
				limb.bleedAmount *= 0.6f;
				Sound.Play("goo", limb.body.transform.position);
			}
		},
		value = 5,
		tags = "medicine",
		rec = new Recognition(3)
	});
	GlobalItems.Add("chestdrain", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		weight = 0.3f,
		rotSpeed = -0.4f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0.99f && limb == limb.body.limbs[1])
			{
				WoundView.view.AddImageToLimb(limb, Resources.Load<Sprite>("Special/injectionWound"), flip: false);
				limb.bleedAmount += 2f;
				item.condition = 0f;
				limb.body.hemothorax -= 35f;
				Sound.Play("syringe", limb.body.transform.position);
			}
		},
		value = 10,
		tags = "medicine",
		rec = new Recognition(12)
	});
	GlobalItems.Add("icepack", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		weight = 0.9f,
		rotSpeed = -0.9f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition >= 0.5f)
			{
				item.condition -= 0.5f;
				limb.body.temperature -= 1f;
				ChilledLimb orAddComponent = limb.gameObject.GetOrAddComponent<ChilledLimb>();
				orAddComponent.timeLeft = 150f;
				orAddComponent.maxTime = 150f;
			}
		},
		value = 16,
		tags = "medicine",
		rec = new Recognition(4)
	});
	GlobalItems.Add("spacedrain", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		destroyAtZeroCondition = false,
		weight = 0.5f,
		rotSpeed = -0.52f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0.1f)
			{
				item.condition -= 0.1f;
				Sound.Play("drainuse", item.transform.position);
				Vector2Int vector2Int = WorldGeneration.world.WorldToBlockPos(body.transform.position);
				FluidManager.main.fluid[vector2Int.x, vector2Int.y] = 0;
				FluidManager.main.fluid[vector2Int.x + 1, vector2Int.y] = 0;
				FluidManager.main.fluid[vector2Int.x - 1, vector2Int.y] = 0;
				FluidManager.main.fluid[vector2Int.x - 2, vector2Int.y] = 0;
				FluidManager.main.fluid[vector2Int.x + 2, vector2Int.y] = 0;
				FluidManager.main.fluid[vector2Int.x, vector2Int.y - 1] = 0;
				FluidManager.main.fluid[vector2Int.x + 1, vector2Int.y - 1] = 0;
				FluidManager.main.fluid[vector2Int.x - 1, vector2Int.y - 1] = 0;
				FluidManager.main.fluid[vector2Int.x - 2, vector2Int.y - 1] = 0;
				FluidManager.main.fluid[vector2Int.x + 2, vector2Int.y - 1] = 0;
				FluidManager.main.fluid[vector2Int.x, vector2Int.y + 1] = 0;
				FluidManager.main.fluid[vector2Int.x + 1, vector2Int.y + 1] = 0;
				FluidManager.main.fluid[vector2Int.x - 1, vector2Int.y + 1] = 0;
				FluidManager.main.fluid[vector2Int.x - 2, vector2Int.y + 1] = 0;
				FluidManager.main.fluid[vector2Int.x + 2, vector2Int.y + 1] = 0;
				FluidManager.main.fluid[vector2Int.x, vector2Int.y + 2] = 0;
				FluidManager.main.fluid[vector2Int.x + 1, vector2Int.y + 2] = 0;
				FluidManager.main.fluid[vector2Int.x - 1, vector2Int.y + 2] = 0;
				FluidManager.main.fluid[vector2Int.x, vector2Int.y - 2] = 0;
				FluidManager.main.fluid[vector2Int.x + 1, vector2Int.y - 2] = 0;
				FluidManager.main.fluid[vector2Int.x - 1, vector2Int.y - 2] = 0;
			}
		},
		value = 13,
		rec = new Recognition(5)
	});
	GlobalItems.Add("tweezers", new ItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		weight = 0.1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (limb.hasShrapnel)
			{
				MinigameBase.main.StartMinigame(new ShrapnelMinigame(limb, tweezers: true), item);
				item.condition -= 0.01f;
				Sound.Play("tweezeruse", limb.body.transform.position);
			}
		},
		value = 10,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("lockpickingkit", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.15f,
		value = 16,
		rec = new Recognition(8)
	});
	GlobalItems.Add("streptokinase", new LiquidItemInfo
	{
		category = "drug",
		slotRotation = 0f,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		capacity = 100f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("streptokinase", 100f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			Sound.Play("syringe", limb.body.transform.position);
			item.GetComponent<WaterContainerItem>().Inject(limb, 33.334f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(11)
	});
	GlobalItems.Add("bloodbag", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = true,
		combineable = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		capacity = 750f,
		autoFill = false,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("blood", 750f)
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			WoundView.view.AddImageToLimb(limb, Resources.Load<Sprite>("Special/injectionWound"), flip: false);
			item.GetComponent<WaterContainerItem>().Inject(limb, 375f);
			Sound.Play("syringe", limb.body.transform.position);
		},
		useAction = delegate(Body body, Item item)
		{
			DrawBlood(item.GetComponent<WaterContainerItem>(), body.limbs[1]);
		},
		value = 3,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("bloodsac", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.3f,
		decayMinutes = 30f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			body.Drink(7.5f);
			body.Eat(7f, 1f);
			body.sicknessAmount += 15f;
			body.happiness -= 1f;
			body.talker.EatMediocre();
			Sound.Play("eatFlesh", body.transform.position);
		},
		value = 3
	});
	GlobalItems.Add("venomgland", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.1f,
		decayMinutes = 60f,
		scaleWeightWithCondition = false,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			body.Drink(2f);
			body.Eat(2f, 0.2f);
			body.sicknessAmount += 15f;
			body.happiness -= 3f;
			body.talker.EatBad();
			body.venomTotal += 30f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		value = 3
	});
	GlobalItems.Add("bunchunk", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.3f,
		decayMinutes = 120f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			body.sicknessAmount += 10f;
			body.septicShock += 15f;
			body.internalBleeding *= 0.85f;
			body.bloodViscosity += 10f;
			body.happiness -= 1f;
			body.talker.EatMediocre();
			Sound.Play("eatFlesh", body.transform.position);
		},
		value = 5,
		rec = new Recognition(0)
	});
	GlobalItems.Add("saline", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.1f,
		scaleWeightWithCondition = true,
		capacity = 750f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("saline", 750f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 80f);
			}, limb, wat.AverageColor()), item);
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(7)
	});
	GlobalItems.Add("ringersolution", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.1f,
		scaleWeightWithCondition = true,
		capacity = 700f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("ringersolution", 700f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem wat = item.GetComponent<WaterContainerItem>();
			MinigameBase.main.StartMinigame(new SyringeMinigame(delegate(float mult)
			{
				wat.Inject(limb, mult * 80f);
			}, limb, wat.AverageColor()), item);
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(8)
	});
	GlobalItems.Add("bloodbaghuman", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = true,
		combineable = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		capacity = 750f,
		autoFill = false,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("redblood", 750f)
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			WoundView.view.AddImageToLimb(limb, Resources.Load<Sprite>("Special/injectionWound"), flip: false);
			item.GetComponent<WaterContainerItem>().Inject(limb, 375f);
			Sound.Play("syringe", limb.body.transform.position);
		},
		useAction = delegate(Body body, Item item)
		{
			DrawBlood(item.GetComponent<WaterContainerItem>(), body.limbs[1]);
		},
		value = 3,
		tags = "medicine",
		rec = new Recognition(9)
	});
	GlobalItems.Add("trashbag", new ItemInfo
	{
		category = "container",
		slotRotation = -35f,
		usable = false,
		usableOnLimb = false,
		weight = 0.25f,
		decayMinutes = 60f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 15
	});
	GlobalItems.Add("plasticbag", new ItemInfo
	{
		category = "container",
		slotRotation = -35f,
		usable = false,
		usableOnLimb = false,
		weight = 0.2f,
		decayMinutes = 45f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 12
	});
	GlobalItems.Add("foliagebag", new ItemInfo
	{
		category = "custom",
		slotRotation = -35f,
		usable = false,
		usableOnLimb = false,
		weight = 0.75f,
		decayMinutes = 25f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 2
	});
	GlobalItems.Add("gravbag", new ItemInfo
	{
		category = "container",
		slotRotation = -35f,
		usable = false,
		usableOnLimb = false,
		weight = 1f,
		destroyAtZeroCondition = false,
		decayMinutes = 100f,
		decayInfo = 17,
		value = 25,
		rec = new Recognition(10)
	});
	GlobalItems.Add("purse", new ItemInfo
	{
		category = "container",
		slotRotation = -90f,
		usable = false,
		usableOnLimb = false,
		weight = 0.9f,
		decayMinutes = 120f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		value = 16
	});
	GlobalItems.Add("pouch", new ItemInfo
	{
		category = "container",
		slotRotation = -90f,
		usable = false,
		usableOnLimb = false,
		weight = 0.2f,
		destroyAtZeroCondition = true,
		decayMinutes = 90f,
		decayInfo = 5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 16
	});
	GlobalItems.Add("brokenbag", new ItemInfo
	{
		category = "container",
		slotRotation = -10f,
		usable = false,
		usableOnLimb = false,
		weight = 1.25f,
		decayMinutes = 60f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		value = 20
	});
	GlobalItems.Add("box", new ItemInfo
	{
		category = "container",
		slotRotation = -90f,
		usable = false,
		usableOnLimb = false,
		weight = 0.3f,
		decayMinutes = 30f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		value = 10
	});
	GlobalItems.Add("toolbox", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		onlyHoldInHands = true,
		decayMinutes = 480f,
		decayInfo = 5,
		jumpHeightMultChange = -0.1f,
		weight = 4f,
		value = 30,
		rec = new Recognition(5)
	});
	GlobalItems.Add("medkit", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		decayMinutes = 240f,
		decayInfo = 5,
		weight = 1.2f,
		value = 15,
		rec = new Recognition(4)
	});
	GlobalItems.Add("disinfectant", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		capacity = 200f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("disinfectant", 200f)
		},
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem component = item.GetComponent<WaterContainerItem>();
			Sound.Play("spray", limb.transform.position);
			component.ApplyToLimb(limb, 10f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("spraybottle", new LiquidItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		capacity = 200f,
		defaultContents = new List<LiquidStack>(),
		autoFill = false,
		useLimbAction = delegate(Limb limb, Item item)
		{
			WaterContainerItem component = item.GetComponent<WaterContainerItem>();
			Sound.Play("spray", limb.transform.position);
			component.ApplyToLimb(limb, 10f);
		},
		value = 1,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("scrapmetal", new ItemInfo
	{
		category = "trash",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (body.canPlaceBlock && !(item.condition < 0.24f))
			{
				Vector2 vector = body.transform.position + (body.targetLookPos - body.transform.position).normalized * 5f;
				RaycastHit2D raycastHit2D = Physics2D.Linecast(body.transform.position, vector, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					vector = raycastHit2D.point + raycastHit2D.normal * 0.2f;
				}
				if (WorldGeneration.world.GetBlock(WorldGeneration.world.WorldToBlockPos(vector)) <= 0)
				{
					WorldGeneration.world.SetBlock(WorldGeneration.world.WorldToBlockPos(vector), 3);
					item.condition -= 0.25f;
					if (item.condition <= 0f)
					{
						body.attackCooldown = 0.5f;
					}
					body.armsAnimator.Play("ArmsSwing", -1, 0f);
					Sound.Play("scrapmetal", vector);
				}
			}
		},
		value = 5,
		tags = "placeable",
		rec = new Recognition(0)
	});
	GlobalItems.Add("climbingrope", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.6f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (body.canPlaceBlock)
			{
				Vector2 vector = body.transform.position + (body.targetLookPos - body.transform.position).normalized * 5f;
				RaycastHit2D raycastHit2D = Physics2D.Linecast(body.transform.position, vector, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					vector = raycastHit2D.point + raycastHit2D.normal * 0.2f;
				}
				RaycastHit2D raycastHit2D2 = Physics2D.Linecast(vector, vector + Vector2.down * 25f, LayerMask.GetMask("Ground"));
				Vector2 vector2 = (raycastHit2D2 ? raycastHit2D2.point : (vector + Vector2.down * 25f));
				GameObject obj = Utils.Create("climbingropeextended", vector, 0f);
				Climbable component = obj.GetComponent<Climbable>();
				component.points.Add(vector2);
				component.points.Add(vector);
				obj.transform.GetChild(0).localScale = new Vector2(1f, Vector2.Distance(vector, vector2) / 25f);
				item.condition -= 0.501f;
				if (item.condition <= 0f)
				{
					body.attackCooldown = 0.5f;
				}
				body.armsAnimator.Play("ArmsSwing", -1, 0f);
				Sound.Play("ropeplace", vector);
			}
		},
		value = 10,
		tags = "placeable",
		rec = new Recognition(4)
	});
	GlobalItems.Add("scaffoldingpack", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 2f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (body.canPlaceBlock)
			{
				Vector2 vector = body.transform.position + (body.targetLookPos - body.transform.position).normalized * 5f;
				RaycastHit2D raycastHit2D = Physics2D.Linecast(body.transform.position, vector, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					vector = raycastHit2D.point + raycastHit2D.normal * 0.2f;
				}
				if (WorldGeneration.world.GetBlock(WorldGeneration.world.WorldToBlockPos(vector)) <= 0)
				{
					WorldGeneration.world.SetBlock(WorldGeneration.world.WorldToBlockPos(vector), 21);
					item.condition -= 0.01f;
					if (item.condition <= 0f)
					{
						body.attackCooldown = 0.5f;
					}
					body.armsAnimator.Play("ArmsSwing", -1, 0f);
					Sound.Play("scrapmetal", vector);
				}
			}
		},
		value = 20,
		tags = "placeable",
		rec = new Recognition(10)
	});
	GlobalItems.Add("filterstraw", new ItemInfo
	{
		category = "utility",
		slotRotation = -30f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.3f,
		scaleWeightWithCondition = true,
		decayMinutes = 240f,
		value = 16,
		rec = new Recognition(4)
	});
	GlobalItems.Add("bloodcrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		value = 10,
		rec = new Recognition(10)
	});
	GlobalItems.Add("soothingcrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		value = 10,
		rec = new Recognition(10)
	});
	GlobalItems.Add("reliefcrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		value = 10,
		rec = new Recognition(10)
	});
	GlobalItems.Add("turbulentcrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		jumpHeightMultChange = 0.44f,
		value = 15,
		rec = new Recognition(10)
	});
	GlobalItems.Add("oxygencrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		value = 10,
		rec = new Recognition(10)
	});
	GlobalItems.Add("emissivecrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 30f,
		value = 10,
		rec = new Recognition(10)
	});
	GlobalItems.Add("digestioncrystalshard", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		decayMinutes = 20f,
		value = 8,
		rec = new Recognition(10)
	});
	GlobalItems.Add("geofruit", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 12f,
		destroyAtZeroCondition = true,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(3.5f, 0.1f);
			body.Drink(5f);
			body.happiness += 0.5f;
			item.condition -= 0.5f;
			item.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("geofruitBitten");
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 1,
		rec = new Recognition(3)
	});
	GlobalItems.Add("bread", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Drink(2f);
			body.Eat(9f, 0.5f);
			item.condition -= 0.34f;
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(1)
	});
	GlobalItems.Add("cake", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		weight = 1.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(8f, 1.25f);
			body.happiness += 0.8f;
			body.talker.EatGood();
			item.condition -= 0.1f;
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 20,
		rec = new Recognition(4)
	});
	GlobalItems.Add("frigiantfruit", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 5f,
		destroyAtZeroCondition = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(2.5f, 0.05f);
			body.Drink(3.5f);
			body.temperature -= 2.5f;
			body.snowAmount += 20f;
			body.happiness += 0.5f;
			body.talker.EatGood();
			item.condition -= 0.5f;
			item.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("frigiantfruitbitten");
			Sound.Play("glass", body.transform.position);
		},
		tags = "cangetwet",
		value = 1,
		rec = new Recognition(9)
	});
	GlobalItems.Add("popfruit", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(4f, 0.05f);
			body.Drink(2.5f);
			body.happiness += 0.35f;
			body.radiationSickness += UnityEngine.Random.Range(-7.5f, 7.5f);
			item.condition -= 0.5f;
			item.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("geigefruitbit");
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
			WorldGeneration.world.WorldToBlockPos(item.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 2,
		rec = new Recognition(8)
	});
	GlobalItems.Add("helluce", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		weight = 0.1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.temperature += 2f;
			body.sicknessAmount -= 11f;
			body.hearingLoss -= 8f;
			body.caffeinated += 30f;
			body.internalBleeding *= 0.8f;
			item.condition -= 0.5f;
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 2,
		rec = new Recognition(12)
	});
	GlobalItems.Add("bulbskin", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = true,
		decayMinutes = 20f,
		destroyAtZeroCondition = true,
		weight = 0.1f,
		useAction = delegate(Body body, Item item)
		{
			body.Drink(4.5f);
			if (UnityEngine.Random.value < 0.5f)
			{
				body.brainHealth -= UnityEngine.Random.Range(0.25f, 0.7f);
			}
			body.adrenaline = 100f;
			item.condition -= 0.34f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0f)
			{
				item.condition -= 1f;
				limb.bandageSlowAmount += 8f;
				limb.pain *= 0.1f;
				limb.body.adrenaline += 50f;
				Sound.Play("goo", limb.transform.position);
				limb.CreateTemporarySprite(Resources.Load<Sprite>("Special/musharmwrap"), 0f, Color.yellow, scaleLimb: true);
			}
		},
		tags = "cangetwet",
		value = 2,
		rec = new Recognition(10)
	});
	GlobalItems.Add("xalorissponge", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = true,
		decayMinutes = 15f,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		useAction = delegate(Body body, Item item)
		{
			item.condition = 0f;
			body.septicShock += 5f;
			body.Eat(8f, 0.15f);
			body.sicknessAmount += 2f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0f)
			{
				item.condition = 0f;
				limb.pain += 5f;
				limb.body.septicShock += 8f;
				limb.infectionAmount -= 5f * limb.infectionSpeedMult;
				limb.SetDisinfect(280f);
				Sound.Play("goo", limb.transform.position);
			}
		},
		tags = "cangetwet",
		value = 4,
		rec = new Recognition(12)
	});
	GlobalItems.Add("stonefruitclosed", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (body.stamina > 30f && !body.disfigured)
			{
				body.stamina -= 30f;
				body.energy -= 5f;
				body.limbs[0].pain += 10f;
				body.DropItem(2);
				Sound.Play("drop", body.transform.position);
				body.eatTime = 0.5f;
				int slot = body.SlotOf(item);
				item.condition = 0f;
				body.DropItem(item);
				body.PickUpItem(UnityEngine.Object.Instantiate(Resources.Load("stonefruitopen"), item.transform.position, item.transform.rotation).GetComponent<Item>(), slot);
			}
		},
		value = 1,
		rec = new Recognition(7)
	});
	GlobalItems.Add("stonefruitopen", new ItemInfo
	{
		category = "custom",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 140f,
		destroyAtZeroCondition = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(8f, 0.3f);
			body.thirst -= 5f;
			item.condition -= 1f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 1,
		rec = new Recognition(6)
	});
	GlobalItems.Add("banana", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(9f, 0.1f);
			body.Drink(4f);
			body.happiness += 1f;
			body.radiationSickness += 1f;
			item.condition -= 0.5f;
			item.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("bananabitten");
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 8,
		rec = new Recognition(10)
	});
	GlobalItems.Add("foliagemeal", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 10f,
		destroyAtZeroCondition = true,
		weight = 0.7f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(18f, 0f);
			body.Drink(2f);
			body.sicknessAmount += 3f;
			item.condition -= 0.5f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		value = 10,
		rec = new Recognition(5)
	});
	GlobalItems.Add("burger", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(14f, 1.2f);
			body.happiness += 1.5f;
			item.condition -= 0.334f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("pancake", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(10f, 1.2f);
			body.happiness += 1.25f;
			body.sicknessAmount += 1.5f;
			item.condition -= 0.251f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 12,
		rec = new Recognition(5)
	});
	GlobalItems.Add("pizzaslice", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 100f,
		destroyAtZeroCondition = true,
		weight = 1.2f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(12f, 1.2f);
			body.happiness += 1.5f;
			item.condition -= 0.334f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 10,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(7)
	});
	GlobalItems.Add("bucketofchicken", new ItemInfo
	{
		category = "food",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		onlyHoldInHands = true,
		combineable = true,
		weight = 2.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(10f, 2f);
			body.happiness += 1.5f;
			item.condition -= 0.21f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
			if (item.condition <= 0f)
			{
				int slot = body.SlotOf(item);
				body.DropItem(item);
				body.PickUpItem(UnityEngine.Object.Instantiate(Resources.Load("bucketofnochicken"), item.transform.position, item.transform.rotation).GetComponent<Item>(), slot);
			}
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		value = 16,
		rec = new Recognition(5)
	});
	GlobalItems.Add("bucketofnochicken", new ItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 20f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		weight = 1f,
		value = 4
	});
	GlobalItems.Add("popcorn", new ItemInfo
	{
		category = "food",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(9f, 2f);
			body.happiness += 1f;
			item.condition -= 0.21f;
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
			if (item.condition <= 0f)
			{
				int slot = body.SlotOf(item);
				body.DropItem(item);
				body.PickUpItem(UnityEngine.Object.Instantiate(Resources.Load("nopopcorn"), item.transform.position, item.transform.rotation).GetComponent<Item>(), slot);
			}
		},
		tags = "cangetwet",
		value = 10,
		rec = new Recognition(7)
	});
	GlobalItems.Add("nopopcorn", new ItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 20f,
		decayInfo = 5,
		destroyAtZeroCondition = true,
		weight = 0.2f,
		value = 4
	});
	GlobalItems.Add("steak", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(15f, 1.6f);
			body.happiness += 2f;
			item.condition -= 0.334f;
			Sound.Play("eatFlesh", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(0)
	});
	GlobalItems.Add("pemmican", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 600f,
		destroyAtZeroCondition = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(12f, 1.35f);
			item.condition -= 0.25f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(0)
	});
	GlobalItems.Add("cookies", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 240f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.2f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(3f, 0.7f);
			body.sicknessAmount += 3f;
			body.happiness += 0.85f;
			item.condition -= 0.1f;
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(3)
	});
	GlobalItems.Add("chips", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(3f, 0.9f);
			body.sicknessAmount += 3f;
			body.happiness += 0.7f;
			item.condition -= 0.1f;
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(6)
	});
	GlobalItems.Add("cereal", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.4f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(6f, 0.45f);
			body.thirst -= 2.5f;
			item.condition -= 0.2f;
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(4)
	});
	GlobalItems.Add("dogfood", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 200f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(6f, 0.4f);
			item.condition -= 0.2f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(2)
	});
	GlobalItems.Add("hardcandy", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 500f,
		destroyAtZeroCondition = true,
		combineable = true,
		weight = 0.1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(1f, 0.7f);
			body.sicknessAmount += 3f;
			body.happiness += 1f;
			item.condition -= 0.2f;
			Sound.Play("eatCrunch", body.transform.position);
			body.talker.EatGood();
		},
		tags = "cangetwet",
		value = 5,
		rec = new Recognition(4)
	});
	GlobalItems.Add("fleshchunk", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		onlyHoldInHands = true,
		weight = 3f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(14f, 1.2f);
			body.happiness -= 0.1f;
			item.condition -= 0.2f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		value = 15,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(0)
	});
	GlobalItems.Add("candybar", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 450f,
		destroyAtZeroCondition = true,
		weight = 0.1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(6f, 0.8f);
			body.happiness += 2.5f;
			item.condition -= 1f;
			body.sicknessAmount += 5f;
			body.talker.EatGood();
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 2,
		rec = new Recognition(1)
	});
	GlobalItems.Add("chocolatebar", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 450f,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(7f, 0.8f);
			body.happiness += 2.5f;
			item.condition -= 0.34f;
			body.sicknessAmount += 20f;
			body.talker.EatGood();
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 8,
		rec = new Recognition(2)
	});
	GlobalItems.Add("paprikash", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 240f,
		destroyAtZeroCondition = false,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f)
			{
				body.Eat(12f, 0.5f);
				body.happiness += 1f;
				item.condition -= 0.5f;
				body.sicknessAmount += 2f;
				body.talker.EatGood();
				Sound.Play("eatFlesh", body.transform.position);
			}
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		value = 8,
		rec = new Recognition(7)
	});
	GlobalItems.Add("nondescriptcan", new ItemInfo
	{
		category = "food",
		slotRotation = 45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<NonDescriptCan>().Eat(body);
		},
		value = 5,
		rec = new Recognition(4)
	});
	GlobalItems.Add("waterbottle", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		capacity = 500f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("water", 500f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 2,
		rec = new Recognition(2)
	});
	GlobalItems.Add("minibarrel", new LiquidItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 12f,
		capacity = 10000f,
		defaultContents = new List<LiquidStack>(),
		autoFill = false,
		onlyHoldInHands = true,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 0,
		rec = new Recognition(5)
	});
	GlobalItems.Add("milk", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.5f,
		scaleWeightWithCondition = true,
		capacity = 1000f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("milk", 1000f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 3,
		rec = new Recognition(5)
	});
	GlobalItems.Add("bowlofcereal", new LiquidItemInfo
	{
		category = "custom",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		capacity = 500f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("cereal", 500f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		tags = "cangetwet",
		value = 4,
		rec = new Recognition(4)
	});
	GlobalItems.Add("chocolatemilk", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.6f,
		scaleWeightWithCondition = true,
		capacity = 1000f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("chocolatemilk", 1000f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 3,
		rec = new Recognition(5)
	});
	GlobalItems.Add("ketchup", new LiquidItemInfo
	{
		category = "food",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		capacity = 300f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("ketchup", 300f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		rec = new Recognition(3)
	});
	GlobalItems.Add("waterjug", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		onlyHoldInHands = true,
		combineable = true,
		weight = 5f,
		scaleWeightWithCondition = true,
		capacity = 3000f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("water", 3000f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 4
	});
	GlobalItems.Add("bleach", new LiquidItemInfo
	{
		category = "medical",
		slotRotation = -45f,
		usable = true,
		destroyAtZeroCondition = false,
		onlyHoldInHands = true,
		combineable = true,
		weight = 3f,
		scaleWeightWithCondition = true,
		capacity = 1500f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("bleach", 1500f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 3,
		rec = new Recognition(8)
	});
	GlobalItems.Add("energydrink", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		capacity = 250f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("energydrink", 250f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		rec = new Recognition(3)
	});
	GlobalItems.Add("coffee", new LiquidItemInfo
	{
		category = "water",
		usable = true,
		usableOnLimb = false,
		slotRotation = -45f,
		combineable = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		destroyAtZeroCondition = false,
		capacity = 250f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("coffee", 250f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		rec = new Recognition(7)
	});
	GlobalItems.Add("soup", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		capacity = 300f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("soup", 300f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 2,
		rec = new Recognition(3)
	});
	GlobalItems.Add("sodabottle", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 2.3f,
		scaleWeightWithCondition = true,
		capacity = 1000f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("soda", 1000f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 3,
		rec = new Recognition(3)
	});
	GlobalItems.Add("applejuice", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.65f,
		scaleWeightWithCondition = true,
		capacity = 800f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("applejuice", 800f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 2,
		rec = new Recognition(3)
	});
	GlobalItems.Add("lemonade", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.65f,
		scaleWeightWithCondition = true,
		capacity = 750f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("lemonade", 750f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 2,
		rec = new Recognition(5)
	});
	GlobalItems.Add("icetea", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1.25f,
		scaleWeightWithCondition = true,
		capacity = 500f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("icetea", 500f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 2,
		rec = new Recognition(4)
	});
	GlobalItems.Add("sodacan", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		capacity = 250f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("soda", 250f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		value = 1,
		rec = new Recognition(3)
	});
	GlobalItems.Add("alcohol", new LiquidItemInfo
	{
		category = "water",
		slotRotation = -45f,
		usable = true,
		destroyAtZeroCondition = false,
		combineable = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		capacity = 400f,
		defaultContents = new List<LiquidStack>
		{
			new LiquidStack("alcohol", 400f)
		},
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		tags = "antiseptic,water",
		value = 8,
		rec = new Recognition(7)
	});
	GlobalItems.Add("foliage", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 15f,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(5f, 0f);
			body.happiness -= 2f;
			item.condition -= 1f;
			Sound.Play("eatFlesh", body.transform.position);
			if (UnityEngine.Random.Range(0f, 1f) < 0.66f)
			{
				body.sicknessAmount += UnityEngine.Random.Range(10f, 30f);
			}
			body.talker.EatMediocre();
		},
		tags = "cangetwet",
		rec = new Recognition(1),
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("foliage")
		}
	});
	GlobalItems.Add("rope", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 45f,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		rec = new Recognition(1)
	});
	GlobalItems.Add("exposedcore", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 4f,
		destroyAtZeroCondition = false,
		weight = 1.5f,
		rec = new Recognition(12),
		useAction = delegate(Body body, Item item)
		{
			body.Eat(12f, 1f);
			body.Drink(15f);
			body.energy += 10f;
			body.caffeinated += 90f;
			body.stimulantMultiplier += 0.35f;
			body.happiness += 1f;
			body.talker.EatGood();
			UnityEngine.Object.Destroy(item.gameObject);
			Sound.Play("glass", body.transform.position);
			Sound.Play("crystalenemylaugh", body.transform.position);
		}
	});
	GlobalItems.Add("flammablepowder", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		combineable = true,
		destroyAtZeroCondition = true,
		tags = "cangetwet",
		weight = 0.2f,
		rec = new Recognition(10)
	});
	GlobalItems.Add("browncap", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 40f,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			Sound.Play("eatFlesh", body.transform.position);
			float num = UnityEngine.Random.value * 100f;
			if (num < 20f)
			{
				body.Eat(10f, UnityEngine.Random.Range(-0.5f, 1f));
			}
			else if (num < 35f)
			{
				body.Eat(10f, UnityEngine.Random.Range(-0.5f, 1f));
				body.talker.EatGood();
				body.happiness += 2.5f;
			}
			else if (num < 55f)
			{
				body.Eat(10f, UnityEngine.Random.Range(-0.5f, 1f));
				body.talker.EatBad();
				body.happiness -= 2.5f;
				body.sicknessAmount += UnityEngine.Random.Range(0f, 50f);
				body.antibioticImmunityTime = 500f;
				Limb[] limbs = body.limbs;
				for (int i = 0; i < limbs.Length; i++)
				{
					limbs[i].infectionAmount -= 20f;
				}
			}
			else if (num < 65f)
			{
				body.Eat(7f, UnityEngine.Random.Range(-0.5f, 1f));
				body.vomiter.Vomit();
			}
			else if (num < 75f)
			{
				body.Eat(7f, UnityEngine.Random.Range(-0.5f, 1f));
				body.GetOrAddComponent<Painkillers>().opiateAmount += 35f;
			}
			else if (num < 99.6f)
			{
				body.Eat(7f, UnityEngine.Random.Range(-0.5f, 1f));
				body.happiness -= 1f;
				body.energy += 50f;
				body.talker.EatMediocre();
			}
			else
			{
				body.Eat(7f, UnityEngine.Random.Range(-0.5f, 1f));
				body.talker.EatBad();
				body.strokeAmount = 0.1f;
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.consciousness = 0f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.radiationSickness += 15f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.energy = 0f;
			}
			if (UnityEngine.Random.value < 0.06f)
			{
				body.respiratoryRate = 0f;
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.thirst += 20f;
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.thirst -= 20f;
			}
			if (UnityEngine.Random.value < 0.08f)
			{
				body.temperature += 4.5f;
			}
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 2,
		rec = new Recognition(9)
	});
	GlobalItems.Add("funguschunk", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 0.4f,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			Sound.Play("eatFlesh", body.transform.position);
			_ = UnityEngine.Random.value;
			body.Eat(10f, 0.25f);
			if (UnityEngine.Random.value < 0.1f)
			{
				body.consciousness -= 50f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.adrenaline = 100f;
			}
			if (UnityEngine.Random.value < 0.025f)
			{
				body.attackCooldown = 15f;
				PlayerCamera.main.lastAttackCool = 15f;
			}
			if (UnityEngine.Random.value < 0.04f)
			{
				body.caffeinated += 300f;
			}
			if (UnityEngine.Random.value < 0.04f)
			{
				body.antibioticImmunityTime += 240f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.radiationSickness += 10f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.radiationSickness -= 10f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.energy = 0f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.temporarySlowdown = 0.95f;
			}
			if (UnityEngine.Random.value < 0.035f)
			{
				body.energy = 100f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.respiratoryRate = 0f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.thirst += 20f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.thirst -= 20f;
			}
			if (UnityEngine.Random.value < 0.08f)
			{
				body.temperature += 4.5f;
			}
			if (UnityEngine.Random.value < 0.08f)
			{
				body.temperature -= 4.5f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.sicknessAmount += 20f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.sicknessAmount -= 20f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.septicShock += 10f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.dirtyness += 50f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.dirtyness -= 50f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.bloodOxygen -= 20f;
			}
			if (UnityEngine.Random.value < 0.02f)
			{
				body.bloodVolume += 10f;
			}
			if (UnityEngine.Random.value < 0.02f)
			{
				body.bloodVolume -= 10f;
			}
			if (UnityEngine.Random.value < 0.01f)
			{
				body.brainHealth -= 1f;
			}
			if (UnityEngine.Random.value < 0.01f)
			{
				body.brainHealth += 2f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.weightOffset += 2f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.weightOffset -= 2f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.stamina -= 40f;
			}
			if (UnityEngine.Random.value < 0.05f)
			{
				body.shock += 5f;
			}
			if (UnityEngine.Random.value < 0.025f)
			{
				body.bloodViscosity += 25f;
			}
			if (UnityEngine.Random.value < 0.025f)
			{
				body.bloodViscosity -= 25f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.caffeinated += 120f;
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.stamina += 40f;
			}
			if (UnityEngine.Random.value < 0.015f)
			{
				body.vomiter.VomitBlood();
			}
			if (UnityEngine.Random.value < 0.015f)
			{
				body.vomiter.Vomit();
			}
			if (UnityEngine.Random.value < 0.04f)
			{
				body.Ragdoll();
			}
			if (UnityEngine.Random.value < 0.035f)
			{
				body.hunger += UnityEngine.Random.Range(-30f, 30f);
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.GetOrAddComponent<Painkillers>().opiateAmount += 30f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.GetOrAddComponent<Painkillers>().antagonistAmount += 30f;
			}
			if (UnityEngine.Random.value < 0.025f)
			{
				body.limbs.PickRandom().pain += 60f;
			}
			if (UnityEngine.Random.value < 0.03f)
			{
				body.StartCoroutine("BrainControlReverse");
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.happiness += 2.5f;
				body.talker.EatGood();
			}
			if (UnityEngine.Random.value < 0.1f)
			{
				body.happiness -= 1.5f;
				body.talker.EatBad();
			}
		},
		tags = "cangetwet",
		value = 2,
		rec = new Recognition(10)
	});
	GlobalItems.Add("dryfoliage", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 120f,
		destroyAtZeroCondition = true,
		weight = 0.2f,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(6f, 0f);
			body.happiness -= 1f;
			item.condition -= 1f;
			Sound.Play("eatFlesh", body.transform.position);
			if (UnityEngine.Random.Range(0f, 1f) < 0.33f)
			{
				body.sicknessAmount += UnityEngine.Random.Range(5f, 20f);
			}
			body.talker.EatMediocre();
		},
		tags = "cangetwet",
		rec = new Recognition(1),
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("foliage")
		}
	});
	GlobalItems.Add("glowplantfruit", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 1f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			limb.skinHealth -= 1f;
			limb.muscleHealth -= 4f;
			limb.pain += 10f;
			limb.SetDisinfect(220f);
			Limb[] connectedLimbs = limb.connectedLimbs;
			foreach (Limb obj in connectedLimbs)
			{
				obj.SetDisinfect(100f);
				obj.muscleHealth -= 3f;
			}
			item.condition -= 1f;
			Sound.Play("goo", limb.transform.position);
		},
		tags = "cangetwet",
		value = 1,
		rec = new Recognition(4)
	});
	GlobalItems.Add("antisepticmush", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = true,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 0.8f,
		useLimbAction = delegate(Limb limb, Item item)
		{
			limb.pain += 10f;
			limb.SetDisinfect(600f);
			Limb[] connectedLimbs = limb.connectedLimbs;
			for (int i = 0; i < connectedLimbs.Length; i++)
			{
				connectedLimbs[i].SetDisinfect(500f);
			}
			item.condition -= 1f;
			Sound.Play("goo", limb.transform.position);
		},
		value = 3,
		tags = "medicine",
		rec = new Recognition(5)
	});
	GlobalItems.Add("flashlight", new ItemInfo
	{
		category = "utility",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		decayMinutes = 66f,
		decayInfo = 16,
		weight = 0.8f,
		useAction = delegate(Body body, Item item)
		{
			CustomItemBehaviour component = item.GetComponent<CustomItemBehaviour>();
			component.state++;
			if (component.state > 3)
			{
				component.state = 0;
			}
			Sound.Play("flashlighttoggle", item.transform.position);
		},
		tags = "belttool,backflip",
		value = 25,
		rec = new Recognition(7)
	});
	GlobalItems.Add("emergencylight", new ItemInfo
	{
		category = "utility",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		decayMinutes = 10f,
		weight = 0.5f,
		useAction = delegate(Body body, Item item)
		{
			CustomItemBehaviour component = item.GetComponent<CustomItemBehaviour>();
			component.state++;
			if (component.state > 1)
			{
				component.state = 0;
			}
			Sound.Play("flashlighttoggle", item.transform.position);
		},
		tags = "belttool,backflip",
		value = 8,
		rec = new Recognition(7)
	});
	GlobalItems.Add("pistol", new ItemInfo
	{
		category = "utility",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		autoAttack = true,
		destroyAtZeroCondition = true,
		weight = 0.8f,
		tags = "cangetwet,gun",
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<GunScript>().triggerPressed = true;
		},
		value = 30,
		rec = new Recognition(10)
	});
	GlobalItems.Add("smallmagazine", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.3f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<AmmoScript>().UnloadRound();
		},
		value = 22,
		tags = "belttool",
		rec = new Recognition(9)
	});
	GlobalItems.Add("boxof12gauge", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.4f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<AmmoScript>().UnloadRound();
		},
		value = 22,
		tags = "belttool",
		rec = new Recognition(9)
	});
	GlobalItems.Add("magazinebase", new ItemInfo
	{
		category = "custom",
		slotRotation = -0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		value = 4,
		rec = new Recognition(10)
	});
	GlobalItems.Add("9mmround", new ItemInfo
	{
		category = "custom",
		slotRotation = -0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.03f,
		value = 0,
		tags = "bullet",
		rec = new Recognition(10)
	});
	GlobalItems.Add("rifle", new ItemInfo
	{
		category = "utility",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		autoAttack = true,
		destroyAtZeroCondition = true,
		weight = 1.75f,
		tags = "cangetwet,gun",
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<GunScript>().triggerPressed = true;
		},
		value = 40,
		rec = new Recognition(10)
	});
	GlobalItems.Add("makeshiftrifle", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		autoAttack = true,
		destroyAtZeroCondition = true,
		weight = 1.75f,
		tags = "cangetwet,gun",
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<GunScript>().triggerPressed = true;
		},
		value = 25,
		rec = new Recognition(10)
	});
	GlobalItems.Add("12gauge", new ItemInfo
	{
		category = "custom",
		slotRotation = -0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.05f,
		value = 0,
		tags = "bullet",
		rec = new Recognition(9)
	});
	GlobalItems.Add("shotgun", new ItemInfo
	{
		category = "utility",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		autoAttack = true,
		destroyAtZeroCondition = true,
		weight = 1.25f,
		tags = "cangetwet,gun",
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<GunScript>().triggerPressed = true;
		},
		value = 40,
		rec = new Recognition(11)
	});
	GlobalItems.Add("riflemagazine", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<AmmoScript>().UnloadRound();
		},
		value = 32,
		tags = "belttool",
		rec = new Recognition(9)
	});
	GlobalItems.Add("556round", new ItemInfo
	{
		category = "custom",
		slotRotation = -0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.05f,
		value = 0,
		tags = "bullet",
		rec = new Recognition(10)
	});
	GlobalItems.Add("casing", new ItemInfo
	{
		category = "custom",
		slotRotation = -0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.02f,
		value = 0,
		tags = "bullet",
		rec = new Recognition(8)
	});
	GlobalItems.Add("plushie", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.15f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<PlushScript>().Squeak();
		},
		value = 5,
		tags = "belttool",
		rec = new Recognition(6)
	});
	GlobalItems.Add("smallbattery", new BatteryInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.1f,
		maxCharge = 50f,
		tags = "battery,bullet",
		value = 6,
		rec = new Recognition(3)
	});
	GlobalItems.Add("mediumbattery", new BatteryInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.2f,
		maxCharge = 100f,
		tags = "battery",
		value = 12,
		rec = new Recognition(5)
	});
	GlobalItems.Add("largebattery", new BatteryInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.5f,
		maxCharge = 300f,
		tags = "battery",
		value = 20,
		rec = new Recognition(6)
	});
	GlobalItems.Add("blueprint", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.2f,
		useAction = delegate(Body body, Item item)
		{
			item.condition = 0f;
			body.skills.AddExp(2, 25f);
			Recipe recipe = Recipes.recipes[item.GetComponent<BlueprintScript>().recipeIndex];
			recipe.INT = 0;
			string other = Locale.GetOther("learnedrecipe");
			other = other.Replace("r1", Locale.GetItem(recipe.simpleName));
			PlayerCamera.main.DoAlert(other);
			PlayerCamera.main.selectedRecipe = recipe.index;
			if (!PlayerCamera.main.craftingPanel.activeSelf)
			{
				PlayerCamera.main.OpenCraftScreen();
			}
			else
			{
				PlayerCamera.main.RefreshRecipeList();
			}
			Sound.Play("combine", item.transform.position);
		},
		value = 6,
		rec = new Recognition(0)
	});
	GlobalItems.Add("lantern", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		weight = 1.35f,
		value = 10,
		tags = "belttool",
		rec = new Recognition(2)
	});
	GlobalItems.Add("lightbulb", new ItemInfo
	{
		category = "utility",
		slotRotation = 30f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 10f,
		decayInfo = 16,
		weight = 0.4f,
		value = 14,
		tags = "noautopickup",
		rec = new Recognition(4)
	});
	GlobalItems.Add("nutrientbar", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(12.5f, 0.4f);
			item.condition -= 0.34f;
			Sound.Play("eatCrunch", body.transform.position);
		},
		tags = "cangetwet",
		value = 10,
		rec = new Recognition(6)
	});
	GlobalItems.Add("primitivediggingtool", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.75f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 50f,
				structuralDamage = 75f,
				attackCooldownMult = 0.75f,
				distance = 5.2f,
				knockBack = 270f,
				cooldown = 0.4f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1.65f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 15.5f
			}, 0))
			{
				item.condition -= 0.0016666667f;
			}
		},
		tags = "tool,backflip",
		value = 40,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 20f)
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("makeshiftdiggingtool", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.8f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 32f,
				structuralDamage = 65f,
				attackCooldownMult = 0.82f,
				distance = 5.2f,
				knockBack = 270f,
				cooldown = 0.4f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1.65f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 15.5f
			}, 0))
			{
				item.condition -= 0.0025f;
			}
		},
		tags = "tool,backflip",
		value = 30,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 10f),
			new CraftingQuality("cutting", 5f)
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("shovel", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 2f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 27f,
				structuralDamage = 45f,
				attackCooldownMult = 0.8f,
				distance = 5.7f,
				knockBack = 100f,
				cooldown = 0.26f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 11.5f
			}, 0))
			{
				item.condition -= 0.0018181818f;
			}
		},
		tags = "tool,backflip",
		value = 35,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 10f)
		},
		rec = new Recognition(2)
	});
	GlobalItems.Add("woodshovel", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 20f,
				structuralDamage = 40f,
				attackCooldownMult = 0.75f,
				distance = 5.7f,
				knockBack = 80f,
				cooldown = 0.25f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.75f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 10f
			}, 0))
			{
				item.condition -= 0.0033333334f;
			}
		},
		tags = "tool,backflip",
		value = 25,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 8f)
		},
		rec = new Recognition(2)
	});
	GlobalItems.Add("pitchfork", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.85f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 50f,
				structuralDamage = 40f,
				attackCooldownMult = 0.7f,
				distance = 5.88f,
				knockBack = 150f,
				cooldown = 0.35f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.87f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 13.5f
			}, 0))
			{
				item.condition -= 0.002631579f;
			}
		},
		tags = "tool,backflip",
		value = 35,
		rec = new Recognition(3)
	});
	GlobalItems.Add("woodpitchfork", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.25f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 43f,
				structuralDamage = 35f,
				attackCooldownMult = 0.8f,
				distance = 5.88f,
				knockBack = 150f,
				cooldown = 0.32f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.8f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 13.5f
			}, 0))
			{
				item.condition -= 0.0045454544f;
			}
		},
		tags = "tool,backflip",
		value = 25,
		rec = new Recognition(3)
	});
	GlobalItems.Add("minilaserdrill", new ItemInfo
	{
		category = "tool",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		autoAttack = true,
		weight = 1f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f && body.Attack(new AttackInfo
			{
				damage = 15f,
				structuralDamage = 25.1f,
				attackCooldownMult = 0.5f,
				distance = 5.5f,
				knockBack = 0f,
				cooldown = 0.16f,
				attackAnim = Resources.Load<GameObject>("LaserAnim"),
				staminaUse = 0f,
				piercing = false,
				swingSounds = new string[1] { "laser" },
				volume = 0.8f,
				physicalSwing = false,
				rotateAmount = 3f,
				doAttackAnim = false
			}, 0))
			{
				item.battery.DrainCharge(0.0014285714f);
			}
		},
		tags = "tool,backflip",
		value = 40,
		rec = new Recognition(10)
	});
	GlobalItems.Add("plasmacutter", new ItemInfo
	{
		category = "tool",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		onlyHoldInHands = true,
		destroyAtZeroCondition = false,
		autoAttack = true,
		weight = 3f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f && body.Attack(new AttackInfo
			{
				damage = 10f,
				structuralDamage = 10f,
				attackCooldownMult = 0.5f,
				distance = 5.5f,
				knockBack = 0f,
				cooldown = 0.1f,
				attackAnim = Resources.Load<GameObject>("LaserAnim"),
				staminaUse = 0f,
				piercing = false,
				swingSounds = new string[1] { "laser" },
				volume = 0.8f,
				physicalSwing = false,
				rotateAmount = 2f,
				doAttackAnim = false,
				metalMoreDamage = true
			}, 0))
			{
				item.battery.DrainCharge(0.001f);
			}
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (item.condition > 0f)
			{
				item.battery.DrainCharge(0.001f);
				limb.skinHealth -= 12f;
				limb.bleedAmount *= 0.2f;
				limb.pain += 45f;
				Sound.Play("laser", limb.transform.position);
			}
		},
		tags = "tool",
		value = 40,
		rec = new Recognition(12)
	});
	GlobalItems.Add("heavydrill", new ItemInfo
	{
		category = "tool",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		autoAttack = true,
		onlyHoldInHands = true,
		weight = 5f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f && body.Attack(new AttackInfo
			{
				damage = 50f,
				structuralDamage = 60f,
				attackCooldownMult = 0.4f,
				distance = 4f,
				knockBack = 180f,
				cooldown = 0.16f,
				attackAnim = null,
				staminaUse = 0f,
				piercing = false,
				swingSounds = new string[1],
				volume = 0.8f,
				physicalSwing = false,
				rotateAmount = 12f,
				doAttackAnim = false
			}, 0))
			{
				item.battery.DrainCharge(0.0023809525f);
			}
		},
		tags = "tool",
		value = 50,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 100f)
		},
		rec = new Recognition(12)
	});
	GlobalItems.Add("pickaxe", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		onlyHoldInHands = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 2f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 60f,
				structuralDamage = 110f,
				attackCooldownMult = 0.65f,
				distance = 5.2f,
				knockBack = 300f,
				cooldown = 0.55f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1.75f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 19f
			}, 0))
			{
				item.condition -= 0.0018181818f;
			}
		},
		tags = "tool",
		value = 45,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 20f)
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("titaniumpickaxe", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		onlyHoldInHands = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 2f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 70f,
				structuralDamage = 140f,
				attackCooldownMult = 0.65f,
				distance = 5.2f,
				knockBack = 300f,
				cooldown = 0.48f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1.7f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 19f
			}, 0))
			{
				item.condition -= 0.00033333333f;
			}
		},
		tags = "tool",
		value = 70,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 80f)
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("sledgehammer", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		onlyHoldInHands = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 2.5f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 60f,
				structuralDamage = 90f,
				attackCooldownMult = 0.72f,
				distance = 5.9f,
				knockBack = 700f,
				cooldown = 0.66f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 3f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 45f
			}, 0))
			{
				item.condition -= 0.00125f;
			}
		},
		tags = "tool",
		value = 30,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 1000f)
		},
		rec = new Recognition(4)
	});
	GlobalItems.Add("wrench", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.65f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 15f,
				structuralDamage = 25f,
				distance = 4.75f,
				knockBack = 70f,
				cooldown = 0.2f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 10f
			}, 0))
			{
				item.condition -= 0.00125f;
			}
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			if (limb.dislocated)
			{
				MinigameBase.main.StartMinigame(new DislocationMinigame(limb, wrench: true), item);
				Sound.Play("wrenchhit", item.transform.position);
			}
		},
		tags = "tool,backflip",
		value = 15,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 150f)
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("makeshiftwrench", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.6f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 14f,
				structuralDamage = 20f,
				distance = 5f,
				knockBack = 70f,
				cooldown = 0.2f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 10f
			}, 0))
			{
				item.condition -= 0.0033333334f;
			}
		},
		tags = "tool,backflip",
		value = 12,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 25f)
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("woodpickaxe", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		onlyHoldInHands = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.45f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 55f,
				structuralDamage = 105f,
				attackCooldownMult = 0.75f,
				distance = 5.2f,
				knockBack = 280f,
				cooldown = 0.49f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1.5f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 17.5f
			}, 0))
			{
				item.condition -= 1f / 140f;
			}
		},
		tags = "tool",
		value = 25,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 15f)
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("wooddiggingtool", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.25f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 38f,
				structuralDamage = 49f,
				attackCooldownMult = 0.8f,
				distance = 5.2f,
				knockBack = 220f,
				cooldown = 0.33f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 1f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 15.5f
			}, 0))
			{
				item.condition -= 0.0033333334f;
			}
		},
		tags = "tool,backflip",
		value = 15,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 10f)
		},
		rec = new Recognition(4)
	});
	GlobalItems.Add("machete", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.5f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 50f,
				structuralDamage = 38f,
				attackCooldownMult = 0.66f,
				distance = 5.75f,
				knockBack = 270f,
				cooldown = 0.35f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.75f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 15.5f
			}, 0))
			{
				item.condition -= 0.002631579f;
			}
		},
		tags = "tool,backflip",
		value = 25,
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 200f),
			new CraftingQuality("hammering", 15f)
		},
		rec = new Recognition(3)
	});
	GlobalItems.Add("titaniummachete", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.5f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 80f,
				structuralDamage = 50f,
				attackCooldownMult = 0.55f,
				distance = 5.75f,
				knockBack = 300f,
				cooldown = 0.32f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.7f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 15.5f
			}, 0))
			{
				item.condition -= 0.00033333333f;
			}
		},
		tags = "tool,backflip",
		value = 50,
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 500f),
			new CraftingQuality("hammering", 40f)
		},
		rec = new Recognition(5)
	});
	GlobalItems.Add("crudecleaver", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.8f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 25f,
				structuralDamage = 20f,
				attackCooldownMult = 0.66f,
				distance = 5f,
				knockBack = 270f,
				cooldown = 0.35f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.5f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 10.5f
			}, 0))
			{
				item.condition -= 0.004f;
			}
		},
		tags = "tool,backflip",
		value = 10,
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 40f)
		},
		rec = new Recognition(3)
	});
	GlobalItems.Add("sickle", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.75f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 40f,
				structuralDamage = 35f,
				attackCooldownMult = 0.75f,
				distance = 5.2f,
				knockBack = 270f,
				cooldown = 0.3f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.7f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 14.5f
			}, 0))
			{
				item.condition -= 0.002631579f;
			}
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		tags = "tool,backflip",
		value = 30,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 25f)
		},
		rec = new Recognition(3)
	});
	GlobalItems.Add("woodsickle", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.25f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 38f,
				structuralDamage = 32f,
				attackCooldownMult = 0.75f,
				distance = 5.2f,
				knockBack = 270f,
				cooldown = 0.3f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.7f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 14.5f
			}, 0))
			{
				item.condition -= 0.005f;
			}
		},
		tags = "tool,backflip",
		value = 20,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 20f)
		},
		rec = new Recognition(3)
	});
	GlobalItems.Add("rake", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 1.25f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 30f,
				structuralDamage = 35f,
				attackCooldownMult = 0.8f,
				distance = 5.2f,
				knockBack = 250f,
				cooldown = 0.35f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.88f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 14.5f
			}, 0))
			{
				item.condition -= 0.0023809525f;
			}
		},
		tags = "tool,backflip",
		value = 22,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 5f)
		},
		rec = new Recognition(1)
	});
	GlobalItems.Add("claws", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.9f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 35f,
				structuralDamage = 24f,
				attackCooldownMult = 0.9f,
				distance = 5f,
				knockBack = 170f,
				cooldown = 0.15f,
				attackAnim = Resources.Load<GameObject>("ClawAnim"),
				staminaUse = 0.5f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 9.5f
			}, 0))
			{
				item.condition -= 0.0014285714f;
			}
		},
		tags = "tool",
		value = 25,
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 100f)
		},
		rec = new Recognition(4)
	});
	GlobalItems.Add("trowel", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.45f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 16f,
				structuralDamage = 32f,
				attackCooldownMult = 0.88f,
				distance = 5f,
				knockBack = 185f,
				cooldown = 0.25f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.6f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 12f
			}, 0))
			{
				item.condition -= 0.0025f;
			}
		},
		tags = "tool,backflip",
		value = 20,
		rec = new Recognition(2)
	});
	GlobalItems.Add("titaniummultitool", new ItemInfo
	{
		category = "custom",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.3f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 20f,
				structuralDamage = 25f,
				attackCooldownMult = 0.88f,
				distance = 5f,
				knockBack = 185f,
				cooldown = 0.25f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.6f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 12f
			}, 0))
			{
				item.condition -= 0.0005f;
			}
		},
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		tags = "tool,backflip",
		value = 20,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 300f),
			new CraftingQuality("hammering", 300f)
		},
		rec = new Recognition(2)
	});
	GlobalItems.Add("woodtrowel", new ItemInfo
	{
		category = "tool",
		slotRotation = -90f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		autoAttack = true,
		weight = 0.3f,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 15f,
				structuralDamage = 28f,
				attackCooldownMult = 0.88f,
				distance = 5f,
				knockBack = 185f,
				cooldown = 0.24f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.5f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 12f
			}, 0))
			{
				item.condition -= 0.0038461538f;
			}
		},
		tags = "tool,backflip",
		value = 10,
		rec = new Recognition(2)
	});
	GlobalItems.Add("experimentflesh", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 1.2f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(12.5f, 1.5f);
			item.condition -= 1f;
			body.happiness -= 6f;
			body.talker.EatBad();
			body.sicknessAmount += 16f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		value = 1,
		rec = new Recognition(0)
	});
	GlobalItems.Add("animalflesh", new ItemInfo
	{
		category = "food",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 0.75f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(7.5f, 1f);
			item.condition -= 1f;
			body.happiness -= 0.75f;
			body.talker.EatMediocre();
			body.sicknessAmount += 4f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		value = 3,
		rec = new Recognition(0)
	});
	GlobalItems.Add("internalorgans", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 20f,
		destroyAtZeroCondition = true,
		onlyHoldInHands = true,
		weight = 2.8f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(15f, 3f);
			item.condition -= 0.34f;
			body.happiness -= 18f;
			body.sicknessAmount += 32f;
			body.talker.EatBad();
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		value = 1,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		rec = new Recognition(7)
	});
	GlobalItems.Add("blobflesh", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		weight = 1f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(6f, 0.6f);
			body.happiness += 1f;
			item.condition -= 1f;
			body.sicknessAmount += 5f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("meat")
		},
		value = 1,
		rec = new Recognition(1)
	});
	GlobalItems.Add("cactusflesh", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 40f,
		destroyAtZeroCondition = true,
		weight = 0.9f,
		scaleWeightWithCondition = true,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(4f, 0.6f);
			body.thirst += 4f;
			body.happiness += 1f;
			item.condition -= 1f;
			body.limbs[0].pain += 10f;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "cangetwet",
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("produce")
		},
		value = 1,
		rec = new Recognition(6)
	});
	GlobalItems.Add("watch", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		decayInfo = 16,
		destroyAtZeroCondition = false,
		weight = 0.5f,
		useAction = delegate(Body body, Item item)
		{
			if (item.battery.hasCharge)
			{
				TimeSpan timeSpan = TimeSpan.FromSeconds(SaveSystem.savedRunTime + Time.timeSinceLevelLoad);
				string text = Locale.GetOther("watchruntime") + timeSpan.ToString("hh\\:mm\\:ss") + "\n";
				text = text + Locale.GetOther("watchtemperature") + Mathf.Round(WorldGeneration.world.ambientTemperature * 10f) * 0.1f + "\"C";
				item.GetComponent<Talker>().Talk(text);
			}
		},
		value = 20,
		tags = "belttool",
		rec = new Recognition(7)
	});
	GlobalItems.Add("terrainscanner", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.6f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0.01f)
			{
				if ((bool)ScannerScript.main)
				{
					UnityEngine.Object.Destroy(ScannerScript.main.gameObject);
				}
				(UnityEngine.Object.Instantiate(Resources.Load("Special/ScannerUI"), PlayerCamera.main.mainView.transform) as GameObject).GetComponent<ScannerScript>().pos = item.transform.position;
				item.battery.DrainCharge(0.02f);
			}
		},
		value = 25,
		rec = new Recognition(10)
	});
	GlobalItems.Add("geigercounter", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.3f,
		decayMinutes = 60f,
		decayInfo = 16,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f)
			{
				item.GetComponent<GeigerCounterAudio>().active = !item.GetComponent<GeigerCounterAudio>().active;
				item.decayMultiplier = (item.GetComponent<GeigerCounterAudio>().active ? 1f : 0f);
			}
		},
		value = 10,
		tags = "belttool",
		rec = new Recognition(10)
	});
	GlobalItems.Add("handcrank", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.6f,
		useAction = delegate(Body body, Item item)
		{
			MinigameBase.main.StartMinigame(new HandCrankMinigame(), item);
		},
		value = 25,
		rec = new Recognition(9)
	});
	GlobalItems.Add("sleepingbag", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 1f,
		useAction = delegate(Body body, Item item)
		{
			if (body.canTakeNap && !body.usingSleepingBag)
			{
				item.condition -= 0.0501f;
				body.TakeANap();
				body.usingSleepingBag = true;
				RaycastHit2D raycastHit2D = Physics2D.Raycast(body.transform.position, Vector2.down, 100f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					Utils.Create("Special/sleepingbaguse", raycastHit2D.point + Vector2.up, 0f);
				}
			}
			else
			{
				PlayerCamera.main.PlayUISound(PlayerCamera.UISoundType.Deny);
			}
		},
		value = 20,
		rec = new Recognition(5)
	});
	GlobalItems.Add("liquidcentrifuge", new LiquidItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.9f,
		capacity = 1000f,
		autoFill = false,
		useAction = delegate(Body body, Item item)
		{
			CustomItemBehaviour component = item.GetComponent<CustomItemBehaviour>();
			if ((float)component.data[0] > 0f)
			{
				Sound.Play("error", item.transform.position, twoDimensional: false, pitchShift: false);
			}
			else
			{
				WaterContainerItem component2 = item.GetComponent<WaterContainerItem>();
				if (!(component2.CurrentTotal <= 0f))
				{
					foreach (LiquidStack item in component2.stack)
					{
						GameObject gameObject = Utils.Create("craftingbottle", item.transform.position, 0f);
						gameObject.GetComponent<WaterContainerItem>().AddLiquid(item.liquidId, item.amount * 0.6666f);
						body.AutoPickUpItem(gameObject.GetComponent<Item>());
					}
					component2.DrainAll();
					component.data[0] = 60f;
					Sound.Play("centrifuge", item.transform.position, twoDimensional: false, pitchShift: false, item.transform);
				}
			}
		},
		value = 28,
		rec = new Recognition(11)
	});
	GlobalItems.Add("epda", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.1f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f)
			{
				item.battery.DrainCharge(0.126f);
				item.GetComponent<EPdaScript>().Use();
			}
		},
		value = 6,
		rec = new Recognition(4)
	});
	GlobalItems.Add("rangefinder", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.4f,
		useAction = delegate(Body body, Item item)
		{
			if (item.condition > 0f)
			{
				RaycastHit2D raycastHit2D = Physics2D.Raycast(item.transform.position, item.transform.right, 9999f, LayerMask.GetMask("Ground"));
				if ((bool)raycastHit2D)
				{
					item.GetComponent<Talker>().Talk($"{raycastHit2D.distance * 0.3f:0.00}m");
				}
				else
				{
					item.GetComponent<Talker>().Talk("???");
				}
				item.battery.DrainCharge(0.0025f);
			}
		},
		value = 18,
		tags = "belttool",
		rec = new Recognition(3)
	});
	GlobalItems.Add("carcass", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.25f,
		value = 2,
		rec = new Recognition(2)
	});
	GlobalItems.Add("circuitboard", new ItemInfo
	{
		category = "trash",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.1f,
		value = 5,
		rec = new Recognition(5)
	});
	GlobalItems.Add("largecarcass", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		value = 5,
		rec = new Recognition(2)
	});
	GlobalItems.Add("mp3player", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		weight = 0.1f,
		useAction = delegate(Body body, Item item)
		{
			if (!(item.condition <= 0f))
			{
				Utils.Create("Special/MP3SongSelect", PlayerCamera.main.mainCanvas.transform);
				item.battery.DrainCharge(0.005f);
				if (PlayerCamera.main.radialOpen)
				{
					PlayerCamera.main.radialOpen = false;
				}
			}
		},
		value = 10,
		rec = new Recognition(7)
	});
	GlobalItems.Add("mushpear", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 15f,
		destroyAtZeroCondition = true,
		weight = 1f,
		useAction = delegate(Body body, Item item)
		{
			body.Eat(5f, -2.5f);
			body.Drink(4f);
			body.happiness -= 0.8f;
			item.condition -= 1f;
			body.sicknessAmount += 7f;
			body.GetOrAddComponent<Painkillers>().opiateAmount += 10f;
			body.talker.EatMediocre();
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "fruit",
		value = 1,
		rec = new Recognition(4)
	});
	GlobalItems.Add("mushtail", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 15f,
		destroyAtZeroCondition = true,
		weight = 1f,
		useAction = delegate(Body body, Item item)
		{
			item.condition -= 1f;
			body.energy = 0f;
			body.consciousness = 0f;
			body.happiness += 12f;
			body.GetOrAddComponent<SleepingPills>().amount += 150f;
			body.sleeping = true;
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "fruit",
		value = 2,
		rec = new Recognition(8)
	});
	GlobalItems.Add("aquapple", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 10f,
		destroyAtZeroCondition = true,
		weight = 0.5f,
		useAction = delegate(Body body, Item item)
		{
			body.Drink(7f);
			body.happiness -= 1f;
			item.condition -= 1f;
			body.talker.EatMediocre();
			Sound.Play("eatFlesh", body.transform.position);
		},
		tags = "fruit,cangetwet",
		rec = new Recognition(4)
	});
	GlobalItems.Add("droppings", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		weight = 0.4f,
		useAction = delegate(Body body, Item item)
		{
			body.talker.RefuseEat();
		},
		rec = new Recognition(0)
	});
	GlobalItems.Add("smallpack", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "UpTorso",
		wearSlotId = "back",
		decayMinutes = 210f,
		decayInfo = 7,
		weight = 0.8f,
		wearableIsolation = 0.02f,
		wearableVisualOffset = 5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 20
	});
	GlobalItems.Add("duffelbag", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "UpTorso",
		wearSlotId = "back",
		decayMinutes = 135f,
		decayInfo = 7,
		weight = 0.9f,
		wearableIsolation = 0.05f,
		wearableVisualOffset = 5,
		value = 30,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		rec = new Recognition(4)
	});
	GlobalItems.Add("slingbag", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "UpTorso",
		decayMinutes = 50f,
		decayInfo = 7,
		wearSlotId = "back",
		weight = 0.75f,
		wearableVisualOffset = 5,
		value = 8,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		rec = new Recognition(6)
	});
	GlobalItems.Add("bigpack", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "UpTorso",
		decayMinutes = 90f,
		decayInfo = 7,
		wearSlotId = "back",
		weight = 1f,
		wearableIsolation = 0.07f,
		wearableVisualOffset = 5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 4f)
		},
		value = 40,
		rec = new Recognition(3)
	});
	GlobalItems.Add("legpouch", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "ThighF",
		decayMinutes = 180f,
		decayInfo = 7,
		wearSlotId = "thigh",
		weight = 0.5f,
		wearableVisualOffset = 5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 25,
		rec = new Recognition(4)
	});
	GlobalItems.Add("liquidpouch", new LiquidItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		wearable = true,
		scaleWeightWithCondition = true,
		desiredWearLimb = "DownTorso",
		wearSlotId = "torsofront",
		weight = 1.6f,
		capacity = 1000f,
		autoFill = false,
		wearableVisualOffset = 101,
		value = 6,
		rec = new Recognition(4)
	});
	GlobalItems.Add("materialpouch", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		decayMinutes = 300f,
		decayInfo = 7,
		wearable = true,
		desiredWearLimb = "ThighB",
		wearSlotId = "thighback",
		weight = 0.75f,
		wearableVisualOffset = -5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 20,
		rec = new Recognition(7)
	});
	GlobalItems.Add("bikehelmet", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "hat",
		wearableArmor = 1f,
		wearableHitDurabilityLossMultiplier = 0.8f,
		weight = 0.8f,
		wearableIsolation = 0.08f,
		wearableVisualOffset = 8,
		value = 15,
		rec = new Recognition(6)
	});
	GlobalItems.Add("riothelmet", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "hat",
		wearableArmor = 2f,
		wearableHitDurabilityLossMultiplier = 0.3f,
		weight = 0.8f,
		wearableIsolation = 0.2f,
		wearableVisualOffset = 8,
		value = 50,
		rec = new Recognition(10)
	});
	GlobalItems.Add("makeshifthelmet", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "hat",
		wearableArmor = 0.75f,
		wearableHitDurabilityLossMultiplier = 1.15f,
		weight = 0.8f,
		wearableIsolation = 0.04f,
		wearableVisualOffset = 8,
		value = 5,
		rec = new Recognition(7)
	});
	GlobalItems.Add("headlamp", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 60f,
		decayInfo = 16,
		wearable = true,
		desiredWearLimb = "Head",
		weight = 0.1f,
		wearSlotId = "hat",
		wearableVisualOffset = 4,
		value = 25,
		rec = new Recognition(8)
	});
	GlobalItems.Add("makeshiftheadlamp", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 30f,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		weight = 1.25f,
		wearSlotId = "hat",
		wearableVisualOffset = 7,
		value = 5,
		rec = new Recognition(1)
	});
	GlobalItems.Add("dustmask", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "mouth",
		weight = 0.1f,
		wearableArmor = 0.1f,
		wearableHitDurabilityLossMultiplier = 0.35f,
		wearableIsolation = 0.08f,
		wearableVisualOffset = 3,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 10,
		rec = new Recognition(3)
	});
	GlobalItems.Add("safetyglasses", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "eyes",
		weight = 0.1f,
		wearableArmor = 0.25f,
		wearableHitDurabilityLossMultiplier = 0.45f,
		wearableVisualOffset = 5,
		value = 8,
		rec = new Recognition(3)
	});
	GlobalItems.Add("autozoomgoggles", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 120f,
		decayInfo = 18,
		wearable = true,
		weight = 0.2f,
		desiredWearLimb = "Head",
		wearSlotId = "eyes",
		wearableVisualOffset = 5,
		value = 20,
		rec = new Recognition(8)
	});
	GlobalItems.Add("blindfold", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "blindfold",
		wearableArmor = 0.1f,
		weight = 0.1f,
		wearableHitDurabilityLossMultiplier = 0.3f,
		wearableIsolation = 0.1f,
		wearableVisualOffset = 4,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 8,
		rec = new Recognition(1)
	});
	GlobalItems.Add("balaclava", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "balaclava",
		wearableArmor = 0.25f,
		weight = 0.1f,
		wearableHitDurabilityLossMultiplier = 0.35f,
		wearableIsolation = 0.2f,
		wearableVisualOffset = 1,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		value = 10
	});
	GlobalItems.Add("holidayhat", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "Head",
		wearSlotId = "hat",
		wearableArmor = 0.1f,
		weight = 0.1f,
		decayMinutes = 180f,
		wearableHitDurabilityLossMultiplier = 0f,
		wearableIsolation = 0.2f,
		wearableVisualOffset = 3,
		decayInfo = 6,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		value = 4
	});
	GlobalItems.Add("scarf", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 360f,
		destroyAtZeroCondition = true,
		decayInfo = 6,
		wearable = true,
		desiredWearLimb = "UpTorso",
		wearSlotId = "neck",
		weight = 0.1f,
		wearableIsolation = 0.15f,
		wearableVisualOffset = 8,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		value = 10
	});
	GlobalItems.Add("latexgloves", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 300f,
		destroyAtZeroCondition = true,
		decayInfo = 6,
		wearable = true,
		weight = 0.1f,
		desiredWearLimb = "HandF",
		wearSlotId = "hands",
		wearableIsolation = 0.08f,
		wearableHitDurabilityLossMultiplier = 0.5f,
		wearableVisualOffset = 4,
		value = 8
	});
	GlobalItems.Add("tacticalgloves", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 600f,
		destroyAtZeroCondition = true,
		decayInfo = 6,
		wearable = true,
		weight = 0.2f,
		desiredWearLimb = "HandF",
		wearSlotId = "hands",
		wearableIsolation = 0.15f,
		wearableHitDurabilityLossMultiplier = 0.15f,
		wearableArmor = 1f,
		wearableVisualOffset = 4,
		value = 35
	});
	GlobalItems.Add("armwarmers", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 360f,
		destroyAtZeroCondition = true,
		decayInfo = 6,
		wearable = true,
		weight = 0.1f,
		desiredWearLimb = "DownArmF",
		wearSlotId = "arms",
		wearableIsolation = 0.1f,
		wearableHitDurabilityLossMultiplier = 0.4f,
		wearableArmor = 0.25f,
		wearableVisualOffset = 3,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 12
	});
	GlobalItems.Add("limbwraps", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 300f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.1f,
		desiredWearLimb = "DownArmF",
		wearSlotId = "wraps",
		wearableIsolation = 0.07f,
		wearableHitDurabilityLossMultiplier = 0.35f,
		wearableArmor = 0.25f,
		wearableVisualOffset = 2,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 12,
		rec = new Recognition(5)
	});
	GlobalItems.Add("tornshirt", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 180f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.3f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "torso",
		wearableIsolation = 0.1f,
		wearableArmor = 0.25f,
		wearableHitDurabilityLossMultiplier = 0.4f,
		wearableVisualOffset = 2,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 10
	});
	GlobalItems.Add("scubadivinggear", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 180f,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 1.5f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "back",
		wearableVisualOffset = 8,
		wearableArmor = 0.8f,
		value = 30,
		rec = new Recognition(9)
	});
	GlobalItems.Add("sneakers", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 300f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.6f,
		desiredWearLimb = "FootF",
		wearSlotId = "feet",
		wearableIsolation = 0.07f,
		wearableArmor = 1f,
		wearableVisualOffset = 5,
		wearableHitDurabilityLossMultiplier = 0.6f,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 15
	});
	GlobalItems.Add("tacticalboots", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 400f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.8f,
		desiredWearLimb = "FootF",
		wearSlotId = "feet",
		wearableIsolation = 0.3f,
		wearableArmor = 2f,
		wearableVisualOffset = 5,
		wearableHitDurabilityLossMultiplier = 0.1f,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 2f)
		},
		value = 35
	});
	GlobalItems.Add("woodsandals", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 60f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.9f,
		desiredWearLimb = "FootF",
		wearSlotId = "feet",
		wearableHitDurabilityLossMultiplier = 4.5f,
		value = 4
	});
	GlobalItems.Add("hoodie", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 480f,
		destroyAtZeroCondition = true,
		decayInfo = 6,
		wearable = true,
		weight = 0.8f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "outertorso",
		wearableIsolation = 0.6f,
		wearableHitDurabilityLossMultiplier = 0.25f,
		wearableArmor = 0.25f,
		wearableVisualOffset = 8,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 4f)
		},
		value = 14
	});
	GlobalItems.Add("striderpelt", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 60f,
		decayInfo = 6,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.8f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "outertorso",
		wearableIsolation = 0.4f,
		wearableHitDurabilityLossMultiplier = 0.35f,
		wearableArmor = 0.4f,
		wearableVisualOffset = 6,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 10,
		rec = new Recognition(4)
	});
	GlobalItems.Add("climbingclaws", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.6f,
		desiredWearLimb = "HandF",
		wearSlotId = "hands",
		wearableVisualOffset = 8,
		value = 28,
		rec = new Recognition(9)
	});
	GlobalItems.Add("jetpack", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = false,
		wearable = true,
		desiredWearLimb = "UpTorso",
		wearSlotId = "back",
		weight = 2.5f,
		wearableIsolation = 0.05f,
		wearableVisualOffset = 4,
		tags = "cangetwet",
		value = 28,
		rec = new Recognition(10)
	});
	GlobalItems.Add("fannypack", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		decayMinutes = 150f,
		decayInfo = 7,
		wearable = true,
		weight = 0.15f,
		desiredWearLimb = "DownTorso",
		wearSlotId = "torsofront",
		wearableVisualOffset = 5,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 20,
		rec = new Recognition(3)
	});
	GlobalItems.Add("bandolier", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		decayMinutes = 300f,
		decayInfo = 7,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.2f,
		desiredWearLimb = "UpTorso",
		wearSlotId = "bandolier",
		wearableVisualOffset = 8,
		value = 16,
		rec = new Recognition(8)
	});
	GlobalItems.Add("bellyarmor", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "DownTorso",
		wearSlotId = "torsofront",
		wearableArmor = 1f,
		weight = 0.8f,
		wearableHitDurabilityLossMultiplier = 0.75f,
		wearableIsolation = 0.1f,
		wearableVisualOffset = 5,
		value = 15,
		rec = new Recognition(5)
	});
	GlobalItems.Add("kneepads", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "CrusF",
		wearSlotId = "knees",
		wearableArmor = 1f,
		weight = 0.4f,
		wearableHitDurabilityLossMultiplier = 0.75f,
		wearableIsolation = 0.05f,
		wearableVisualOffset = 7,
		value = 12,
		rec = new Recognition(5)
	});
	GlobalItems.Add("carapace", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		desiredWearLimb = "UpTorso",
		wearSlotId = "outertorso",
		wearableArmor = 1f,
		weight = 1f,
		wearableHitDurabilityLossMultiplier = 0.5f,
		wearableIsolation = 0.15f,
		wearableVisualOffset = 5,
		value = 20,
		rec = new Recognition(7)
	});
	GlobalItems.Add("belt", new ItemInfo
	{
		category = "container",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.2f,
		desiredWearLimb = "DownTorso",
		wearSlotId = "belt",
		wearableVisualOffset = 2,
		decayMinutes = 1440f,
		decayInfo = 7,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 14,
		rec = new Recognition(3)
	});
	GlobalItems.Add("traumarig", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = false,
		usableOnLimb = false,
		destroyAtZeroCondition = true,
		wearable = true,
		weight = 0.2f,
		desiredWearLimb = "DownTorso",
		wearSlotId = "torsofront",
		wearableVisualOffset = 4,
		decayMinutes = 1440f,
		wearableArmor = 0.8f,
		wearableHitDurabilityLossMultiplier = 0.1f,
		decayInfo = 7,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable", 1f)
		},
		value = 50,
		rec = new Recognition(12)
	});
	GlobalItems.Add("dynamite", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		decayMinutes = 300f,
		weight = 1f,
		destroyAtZeroCondition = true,
		useAction = delegate(Body body, Item item)
		{
			CustomItemBehaviour component = item.GetComponent<CustomItemBehaviour>();
			if (component.data == null || component.data.Length == 0 || !(bool)component.data[0])
			{
				item.transform.GetChild(0).GetComponent<SpriteRenderer>().enabled = true;
				item.GetComponent<AudioSource>().Play();
				component.Invoke("DynamiteExplode", 5f);
				component.data = new object[1] { true };
			}
		},
		value = 15,
		rec = new Recognition(8)
	});
	GlobalItems.Add("present", new ItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		weight = 0.6f,
		destroyAtZeroCondition = true,
		useAction = delegate(Body body, Item item)
		{
			item.condition = 0f;
			PlayerCamera.main.body.AutoPickUpItem(Utils.Create("holidayhat", item.transform.position, 0f).GetComponent<Item>());
			PlayerCamera.main.body.AutoPickUpItem(Utils.Create("plushie", item.transform.position, 0f).GetComponent<Item>());
			Sound.Play("combine", item.transform.position);
		},
		value = 5,
		rec = new Recognition(4)
	});
	GlobalItems.Add("grapplinghook", new ItemInfo
	{
		category = "utility",
		slotRotation = 0f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = false,
		weight = 2.2f,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<GrapplingHook>().Use(body);
		},
		tags = "backflip,belttool",
		value = 32,
		rec = new Recognition(10)
	});
	GlobalItems.Add("craftingbottle", new LiquidItemInfo
	{
		category = "unobtainable",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		weight = 1f,
		value = 0,
		capacity = 1000f,
		autoFill = false,
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		defaultContents = new List<LiquidStack>(),
		scaleWeightWithCondition = true,
		rec = new Recognition(0)
	});
	GlobalItems.Add("canteen", new LiquidItemInfo
	{
		category = "custom",
		slotRotation = 0f,
		usable = true,
		usableOnLimb = false,
		weight = 0.6f,
		value = 0,
		capacity = 300f,
		defaultContents = new List<LiquidStack>(),
		useAction = delegate(Body body, Item item)
		{
			item.GetComponent<WaterContainerItem>().Drink(body);
		},
		rec = new Recognition(1)
	});
	GlobalItems.Add("scrapcube", new ItemInfo
	{
		category = "custom",
		weight = 0.5f,
		rec = new Recognition(0),
		value = 6,
		destroyAtZeroCondition = true
	});
	GlobalItems.Add("scrappanel", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(0),
		value = 1,
		destroyAtZeroCondition = true
	});
	GlobalItems.Add("scraptube", new ItemInfo
	{
		category = "custom",
		weight = 0.2f,
		rec = new Recognition(0),
		value = 3,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 2f)
		}
	});
	GlobalItems.Add("woodscraps", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(0),
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("flammable")
		},
		value = 0
	});
	GlobalItems.Add("woodcube", new ItemInfo
	{
		category = "custom",
		weight = 0.3f,
		rec = new Recognition(0),
		value = 2,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("flammable")
		}
	});
	GlobalItems.Add("woodpanel", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(0),
		value = 0,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("flammable")
		}
	});
	GlobalItems.Add("stick", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(0),
		value = 1,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 1f),
			new CraftingQuality("flammable")
		}
	});
	GlobalItems.Add("charcoal", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(0),
		value = 1,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("flammable")
		}
	});
	GlobalItems.Add("canvas", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(1),
		value = 1,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("rippable")
		}
	});
	GlobalItems.Add("nails", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(2),
		value = 0,
		destroyAtZeroCondition = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("nails", 5f)
		}
	});
	GlobalItems.Add("string", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(1),
		value = 0,
		destroyAtZeroCondition = true
	});
	GlobalItems.Add("ilmenitechunk", new ItemInfo
	{
		category = "custom",
		weight = 1.5f,
		rec = new Recognition(10),
		destroyAtZeroCondition = true,
		value = 2
	});
	GlobalItems.Add("titaniumslab", new ItemInfo
	{
		category = "custom",
		weight = 1f,
		rec = new Recognition(10),
		destroyAtZeroCondition = true,
		value = 8
	});
	GlobalItems.Add("titaniumsheet", new ItemInfo
	{
		category = "custom",
		weight = 0.2f,
		rec = new Recognition(10),
		destroyAtZeroCondition = true,
		value = 2
	});
	GlobalItems.Add("titaniumrod", new ItemInfo
	{
		category = "custom",
		weight = 0.4f,
		rec = new Recognition(10),
		destroyAtZeroCondition = true,
		value = 4,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("hammering", 8f)
		}
	});
	GlobalItems.Add("rawcopper", new ItemInfo
	{
		category = "custom",
		weight = 1.2f,
		rec = new Recognition(6),
		destroyAtZeroCondition = true,
		value = 1
	});
	GlobalItems.Add("processedcopper", new ItemInfo
	{
		category = "custom",
		weight = 0.8f,
		rec = new Recognition(6),
		destroyAtZeroCondition = true,
		value = 5
	});
	GlobalItems.Add("plasticchunk", new ItemInfo
	{
		category = "custom",
		weight = 0.3f,
		rec = new Recognition(1),
		destroyAtZeroCondition = true,
		value = 0
	});
	GlobalItems.Add("bundleofwires", new ItemInfo
	{
		category = "custom",
		weight = 0.03f,
		rec = new Recognition(6),
		destroyAtZeroCondition = true,
		value = 1
	});
	GlobalItems.Add("lcdscreen", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(7),
		destroyAtZeroCondition = true,
		value = 2
	});
	GlobalItems.Add("autoinjector", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(9),
		destroyAtZeroCondition = true,
		value = 4
	});
	GlobalItems.Add("flexiglass", new ItemInfo
	{
		category = "custom",
		weight = 0.1f,
		rec = new Recognition(4),
		destroyAtZeroCondition = true,
		value = 1
	});
	GlobalItems.Add("drillrepairkit", new ItemInfo
	{
		category = "custom",
		weight = 0.8f,
		rec = new Recognition(8),
		destroyAtZeroCondition = true,
		value = 10
	});
	GlobalItems.Add("firestarter", new ItemInfo
	{
		category = "custom",
		weight = 0.3f,
		rec = new Recognition(8),
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("firestarter", 3f)
		},
		destroyAtZeroCondition = true,
		value = 5
	});
	GlobalItems.Add("lighter", new ItemInfo
	{
		category = "utility",
		weight = 0.1f,
		rec = new Recognition(5),
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("firestarter", 100f)
		},
		destroyAtZeroCondition = true,
		value = 12
	});
	GlobalItems.Add("ryebulb", new ItemInfo
	{
		category = "custom",
		weight = 1f,
		decayMinutes = 30f,
		tags = "cangetwet",
		rec = new Recognition(5),
		destroyAtZeroCondition = true,
		value = 0
	});
	GlobalItems.Add("ryeflour", new ItemInfo
	{
		category = "custom",
		weight = 0.5f,
		decayMinutes = 300f,
		tags = "cangetwet",
		rec = new Recognition(7),
		qualities = new List<CraftingQuality> { "flour" },
		destroyAtZeroCondition = true,
		value = 2
	});
	GlobalItems.Add("campfire", new ItemInfo
	{
		category = "custom",
		weight = 10f,
		decayMinutes = 15f,
		rec = new Recognition(3),
		destroyAtZeroCondition = true,
		onlyHoldInHands = true,
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("heatsource", 20f)
		},
		tags = "cangetwet",
		value = 0
	});
	GlobalItems.Add("torch", new ItemInfo
	{
		category = "utility",
		weight = 0.8f,
		slotRotation = -90f,
		decayMinutes = 20f,
		rec = new Recognition(3),
		tags = "cangetwet,backflip",
		value = 6
	});
	GlobalItems.Add("flimsyknife", new ItemInfo
	{
		category = "custom",
		weight = 0.3f,
		usable = true,
		usableWithLMB = true,
		usableOnLimb = true,
		autoAttack = true,
		destroyAtZeroCondition = true,
		useAction = delegate(Body body, Item item)
		{
			if (body.Attack(new AttackInfo
			{
				damage = 16f,
				structuralDamage = 16f,
				attackCooldownMult = 0.75f,
				distance = 5f,
				knockBack = 50f,
				cooldown = 0.2f,
				attackAnim = Resources.Load<GameObject>("SwingAnim"),
				staminaUse = 0.4f,
				piercing = false,
				swingSounds = new string[4] { "BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4" },
				volume = 0.5f,
				rotateAmount = 4f
			}, 0))
			{
				item.condition -= 0.01f;
			}
		},
		rec = new Recognition(2),
		value = 0,
		tags = "tool,backflip",
		useLimbAction = delegate(Limb limb, Item item)
		{
			DoAmputate(item, limb);
		},
		qualities = new List<CraftingQuality>
		{
			new CraftingQuality("cutting", 8f)
		}
	});
	foreach (KeyValuePair<string, ItemInfo> globalItem in GlobalItems)
	{
		globalItem.Value.fullName = Locale.GetItem(globalItem.Key);
		globalItem.Value.description = Locale.GetItem(globalItem.Key + "dsc");
		globalItem.Value.SetTags();
		if (globalItem.Value.decayMinutes > 0f)
		{
			globalItem.Value.rotSpeed = 1.666f / globalItem.Value.decayMinutes;
		}
	}
	ItemLootPool.InitializePool();
}