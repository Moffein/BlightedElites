using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BlightedElites
{
    public class BlightedElitesContentPack : IContentPackProvider
    {
        public static ContentPack content = new ContentPack();
        public string identifier => "MoffeinBlightedElites.content";

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(content, args.output);
            yield break;
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            //Add EquipmentDef through ItemAPI due to the displayrule thing
            content.eliteDefs.Add(new RoR2.EliteDef[] { BlightedElitesPlugin.AffixBlightedElite });
            content.buffDefs.Add(new RoR2.BuffDef[] { BlightedElitesPlugin.AffixBlightedBuff });
            content.networkSoundEventDefs.Add(new RoR2.NetworkSoundEventDef[] { Components.AffixBlightedComponent.rerollSound });
            content.equipmentDefs.Add(new RoR2.EquipmentDef[] { BlightedElitesPlugin.AffixBlightedEquipment });
            yield break;
        }
    }
}
