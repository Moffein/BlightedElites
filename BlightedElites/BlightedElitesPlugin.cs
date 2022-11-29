using BepInEx;
using R2API.Utils;
using UnityEngine;
using R2API;
using RoR2;
using System.Reflection;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using BlightedElites.Components;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.Linq;
using RoR2.ContentManagement;

namespace BlightedElites
{
    [BepInDependency("com.Moffein.EliteReworks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Moffein.BlightedElites", "Blighted Elites", "1.1.3")]
    [R2API.Utils.R2APISubmoduleDependency(nameof(PrefabAPI), nameof(EliteAPI), nameof(SoundAPI))]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class BlightedElitesPlugin : BaseUnityPlugin
    {
        public static Color AffixBlightedColor = new Color(0.1f, 0.1f, 0.1f);
        public static Color AffixBlightedLightColor = new Color(93f / 255f, 18f / 255f, 14f/255f);

        public static EquipmentDef AffixBlightedEquipment;
        public static BuffDef AffixBlightedBuff;
        public static EliteDef AffixBlightedElite;

        public static float healthMult = 12f;
        public static float damageMult = 3.5f;
        public static float affixDropChance = 0.00025f;
        public static bool allowT2Affixes = false;

        public static AssetBundle assetBundle;
        public static PluginInfo pluginInfo;

        private void ReadConfig()
        {
            healthMult = Config.Bind<float>("Stats", "Health Multiplier", 12f, "Elite HP Multiplier. Malachite is 18.").Value;
            damageMult = Config.Bind<float>("Stats", "Damage Multiplier", 3.5f, "Elite damage Multiplier. Malachite is 6.").Value;
            affixDropChance = Config.Bind<float>("Stats", "Affix Drop Chance", 0.025f, "Chance to drop affix on death. Max is 100 (guaranteed drop).").Value;
            allowT2Affixes = Config.Bind<bool>("Stats", "Allow T2 Affixes", false, "Blighted Elites can use T2 affixes like Celestine and Malachite.").Value;
        }


        public void Awake()
        {
            ContentManager.collectContentPackProviders += ContentManager_collectContentPackProviders;
            pluginInfo = Info;
            new LanguageTokens();
            SetupAssetBundle();

            ReadConfig();


            SetupBuff();
            SetupEquipment();
            SetupElite();
        }

        private void ContentManager_collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(new BlightedElitesContentPack());
        }

        private void SetupAssetBundle()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BlightedElites.blightedelitebundle"))
            {
                assetBundle = AssetBundle.LoadFromStream(stream);
            }

            using (var bankStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BlightedElites.BlightedElitesSoundbank.bnk"))
            {
                var bytes = new byte[bankStream.Length];
                bankStream.Read(bytes, 0, bytes.Length);
                R2API.SoundAPI.SoundBanks.Add(bytes);
            }
        }

        private void SetupBuff()
        {
            AffixBlightedBuff = ScriptableObject.CreateInstance<BuffDef>();
            AffixBlightedBuff.name = name;
            AffixBlightedBuff.canStack = false;
            AffixBlightedBuff.isCooldown = false;
            AffixBlightedBuff.isDebuff = false;
            AffixBlightedBuff.buffColor = AffixBlightedColor;
            AffixBlightedBuff.iconSprite = assetBundle.LoadAsset<Sprite>("texIconBuffAffixBlight.png"); //from LiT
            (AffixBlightedBuff as UnityEngine.Object).name = AffixBlightedBuff.name;

            On.RoR2.CharacterBody.GetSubtitle += (orig, self) =>
            {
                if (self.equipmentSlot && self.equipmentSlot.equipmentIndex == AffixBlightedEquipment.equipmentIndex)
                {
                    return Language.GetString("ELITE_MODIFIER_BLIGHTED_MOFFEIN_SUBTITLENAMETOKEN");
                }
                return orig(self);
            };

            On.RoR2.CharacterBody.OnClientBuffsChanged += UpdateBlightedBuff;

            AffixBlightedComponent.rerollSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            AffixBlightedComponent.rerollSound.eventName = "Play_MoffeinBlighted_Spawn";

            //Blighted overrides all other Elite names
            IL.RoR2.Util.GetBestBodyName += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c = c.GotoNext(MoveType.After,
                    x => x.MatchCallvirt<CharacterBody>("HasBuff"));
                c.Emit(OpCodes.Ldloc, 6);//BuffIndex
                c.Emit(OpCodes.Ldloc_0);//CharacterBody
                c.EmitDelegate<Func<bool, BuffIndex, CharacterBody, bool>>((hasBuff, buffIndex, cb) =>
                {
                    if (hasBuff && buffIndex != AffixBlightedBuff.buffIndex)
                    {
                        if (cb.HasBuff(AffixBlightedBuff)) hasBuff = false;
                    }
                    return hasBuff;
                });
            };
        }

        private void SetupEquipment()
        {
            AffixBlightedEquipment = ScriptableObject.CreateInstance<EquipmentDef>();
            AffixBlightedEquipment.appearsInMultiPlayer = true;
            AffixBlightedEquipment.appearsInSinglePlayer = true;
            AffixBlightedEquipment.canBeRandomlyTriggered = false;
            AffixBlightedEquipment.canDrop = false;
            AffixBlightedEquipment.colorIndex = ColorCatalog.ColorIndex.Equipment;
            AffixBlightedEquipment.cooldown = 45f;  //Equipment use = reroll buffs
            AffixBlightedEquipment.isLunar = false;
            AffixBlightedEquipment.isBoss = false;
            AffixBlightedEquipment.passiveBuffDef = AffixBlightedBuff;
            AffixBlightedEquipment.pickupIconSprite = assetBundle.LoadAsset<Sprite>("texIconPickupAffixBlight.png"); //from LiT
            AffixBlightedEquipment.dropOnDeathChance = BlightedElitesPlugin.affixDropChance/100f;
            AffixBlightedEquipment.enigmaCompatible = false;

            //Based off of Spikestrip code.
            AffixBlightedEquipment.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/EliteFire/PickupEliteFire.prefab").WaitForCompletion().InstantiateClone("PickupAffixBlightedMoffein", false);
            Material material = UnityEngine.Object.Instantiate<Material>(AffixBlightedEquipment.pickupModelPrefab.GetComponentInChildren<MeshRenderer>().material);
            material.color = Color.black;//AffixBlightedColor;
            //Material material = Addressables.LoadAssetAsync<Material>("RoR2/DLC1/VoidSurvivor/matVoidSurvivorCorruptOverlay.mat").WaitForCompletion();//RoR2/Base/InvadingDoppelganger/matDoppelganger.mat
            foreach (Renderer renderer in AffixBlightedEquipment.pickupModelPrefab.GetComponentsInChildren<Renderer>())
            {
                renderer.material = material;
            }

            AffixBlightedEquipment.pickupToken = "EQUIPMENT_AFFIXBLIGHTED_MOFFEIN_PICKUP";
            AffixBlightedEquipment.descriptionToken = "EQUIPMENT_AFFIXBLIGHTED_MOFFEIN_DESC";
            AffixBlightedEquipment.nameToken = "EQUIPMENT_AFFIXBLIGHTED_MOFFEIN_NAME";
            AffixBlightedEquipment.loreToken = "";

            AffixBlightedEquipment.name = "AffixBlightedMoffein";
            (AffixBlightedEquipment as ScriptableObject).name = "AffixBlightedMoffein";

            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
        }

        private bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            bool result = orig(self, equipmentDef);
            if (!result)
            {
                if (equipmentDef == AffixBlightedEquipment)
                {
                    CharacterBody cb = self.characterBody;
                    if (cb && Run.instance)
                    {
                        Xoroshiro128Plus rng = Run.instance.spawnRng;
                        AffixBlightedComponent abc = cb.GetComponent<AffixBlightedComponent>();
                        if (abc)
                        {
                            abc.Reroll(rng);
                        }
                        else
                        {
                            abc = cb.gameObject.AddComponent<AffixBlightedComponent>();
                            abc.characterBody = cb;
                            abc.Activate(rng);
                        }
                    }
                    result = true;
                }
            }
            return result;
        }

        private void SetupElite()
        {
            //EliteDef malachiteDef = Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/ElitePoison/edPoison.asset").WaitForCompletion();

            AffixBlightedElite = ScriptableObject.CreateInstance<EliteDef>();
            AffixBlightedElite.color = AffixBlightedColor;
            AffixBlightedElite.eliteEquipmentDef = AffixBlightedEquipment;

            AffixBlightedElite.modifierToken = "ELITE_MODIFIER_BLIGHTED_MOFFEIN";

            AffixBlightedElite.name = "EliteBlightedMoffein";
            (AffixBlightedElite as ScriptableObject).name = "EliteBlightedMoffein";

            AffixBlightedElite.healthBoostCoefficient = BlightedElitesPlugin.healthMult;
            AffixBlightedElite.damageBoostCoefficient = BlightedElitesPlugin.damageMult;

            AffixBlightedBuff.eliteDef = AffixBlightedElite;

            On.RoR2.CombatDirector.Init += (orig) =>
            {
                orig();

                if (EliteAPI.VanillaEliteTiers.Length > 3)
                {
                    CombatDirector.EliteTierDef targetTier = EliteAPI.VanillaEliteTiers[3];
                    List<EliteDef> elites = targetTier.eliteTypes.ToList();
                    elites.Add(AffixBlightedElite);
                    targetTier.eliteTypes = elites.ToArray();
                }
            };

            //Main Texture
            IL.RoR2.CharacterModel.UpdateOverlays += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(
                     x => x.MatchLdsfld(typeof(RoR2Content.Items), "InvadingDoppelganger")
                    );
                c.Index += 2;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, CharacterModel, int>>((vengeanceCount, self) =>
                {
                    if (self.myEliteIndex == AffixBlightedElite.eliteIndex)
                    {
                        vengeanceCount++;
                    }
                    return vengeanceCount;
                });
            };

            //Disable shield visuals on Blighted since it makes them look like Overloading
            IL.RoR2.CharacterModel.UpdateOverlays += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(MoveType.After,
                     x => x.MatchLdfld(typeof(HealthComponent), "shield")
                    );
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, CharacterModel, float>>((shieldAmount, self) =>
                {
                    if (self.myEliteIndex == AffixBlightedElite.eliteIndex)
                    {
                        return 0f;
                    }
                    return shieldAmount;
                });
            };
        }

        private static void UpdateBlightedBuff(On.RoR2.CharacterBody.orig_OnClientBuffsChanged orig, CharacterBody self)
        {
            orig(self);

            if (NetworkServer.active && self.HasBuff(AffixBlightedBuff))
            {
                AffixBlightedComponent component = self.GetComponent<AffixBlightedComponent>();
                if (!component)
                {
                    component = self.gameObject.AddComponent<AffixBlightedComponent>();
                    component.characterBody = self;
                }
                if (Run.instance)
                {
                    component.Activate(Run.instance.spawnRng);
                }
            }
        }
    }
}
