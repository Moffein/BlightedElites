using RoR2;
using UnityEngine;
using System;
using System.Collections.Generic;
using R2API;
using UnityEngine.Networking;
using System.Linq;
using UnityEngine.AddressableAssets;

namespace BlightedElites.Components
{
    public class AffixBlightedComponent : MonoBehaviour
    {
        public static NetworkSoundEventDef rerollSound;

        public static List<EliteDef> tier1Affixes = new List<EliteDef>()
        {
            Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/EliteFire/edFire.asset").WaitForCompletion(),
            Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/EliteIce/edIce.asset").WaitForCompletion(),
            Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/EliteLightning/edLightning.asset").WaitForCompletion(),
            Addressables.LoadAssetAsync<EliteDef>("RoR2/DLC1/EliteEarth/edEarth.asset").WaitForCompletion(),
        };
        public static List<EliteDef> tier2Affixes = new List<EliteDef>()
        {
            Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/EliteHaunted/edHaunted.asset").WaitForCompletion(),
            Addressables.LoadAssetAsync<EliteDef>("RoR2/Base/ElitePoison/edPoison.asset").WaitForCompletion(),
        };

        public BuffDef buff1;
        public BuffDef buff2;

        public CharacterBody characterBody;

        public bool active = false;

        public static float soundCooldown = 3f;
        private float soundCooldownStopwatch = 0f;

        public void Reroll(Xoroshiro128Plus rng)
        {
            if (!NetworkServer.active) return;
            Deactivate();
            Activate(rng);
        }

        public void Activate(Xoroshiro128Plus rng)
        {
            if (!NetworkServer.active) return;
            if (!characterBody) characterBody = base.GetComponent<CharacterBody>();

            if (soundCooldownStopwatch > 0f) soundCooldownStopwatch -= Time.fixedDeltaTime;

            //Reroll affixes whenever Blighted is freshly activated.
            if (!active)
            {
                buff1 = null;
                buff2 = null;

                //Only roll 1 t2 elite affix
                EliteDef elite1 = GetRandomElite(tier1Affixes);
                if (BlightedElitesPlugin.allowT2Affixes && UnityEngine.Random.Range(0,6) == 0)
                {
                    EliteDef overrideElite = GetRandomElite(tier2Affixes);
                    if (overrideElite != null) elite1 = overrideElite;
                }
                if (elite1) buff1 = GetEliteBuff(elite1);

                EliteDef elite2 = GetRandomElite(tier1Affixes.Where(ed => ed != elite1).ToList());
                if (elite2) buff2 = GetEliteBuff(elite2);

                if (buff1 || buff2)
                {
                    active = true;
                    if (buff1) characterBody.AddBuff(buff1);
                    if (buff2) characterBody.AddBuff(buff2);

                    if (rerollSound && soundCooldownStopwatch <= 0f)
                    {
                        soundCooldownStopwatch = AffixBlightedComponent.soundCooldown;
                        EffectManager.SimpleSoundEffect(rerollSound.index, base.transform.position, true);
                    }
                }
            }
        }

        //Make sure this doesnt interact weirdly with Wake of Vultures
        public void Deactivate()
        {
            if (!NetworkServer.active) return;
            if (buff1 && characterBody.HasBuff(buff1))
            {
                characterBody.RemoveBuff(buff1);
            }
            if (buff2 && characterBody.HasBuff(buff2))
            {
                characterBody.RemoveBuff(buff2);
            }

            buff1 = null;
            buff2 = null;
            active = false;
        }

        public void FixedUpdate()
        {
            if (characterBody)
            {
                if (active)
                {
                    if (!characterBody.HasBuff(BlightedElitesPlugin.AffixBlightedBuff)) Deactivate();
                }
                else
                {
                    if (characterBody.HasBuff(BlightedElitesPlugin.AffixBlightedBuff) && Run.instance) Activate(Run.instance.spawnRng);
                }
            }
        }

        public static BuffDef GetEliteBuff(EliteDef eliteDef)
        {
            if (eliteDef.eliteEquipmentDef && eliteDef.eliteEquipmentDef.passiveBuffDef) return eliteDef.eliteEquipmentDef.passiveBuffDef;
            return null;
        }

        public static EliteDef GetRandomElite(List<EliteDef> eliteDefs)
        {
            List<EliteDef> trimmed = eliteDefs.Where(ed => ed.IsAvailable()).ToList();
            if (trimmed.Count <= 0)
            {
                Debug.LogError("BlightedElites: GetRandomElite returned null.");
                return null;
            }

            return trimmed[UnityEngine.Random.Range(0, trimmed.Count)];
        }
    }
}
