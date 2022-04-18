using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Deduplicator
{
    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "ResolveAllWantedCrossReferences")]
    public static class Core
    {
        public static void Prefix()
        {
            TryFormThingGroups();
        }

        public static Dictionary<string, ThingGroup> thingGroupsByKeys = new Dictionary<string, ThingGroup>();
        public static bool formedThingGroups;
        public static void TryFormThingGroups()
        {
            if (!formedThingGroups && UnityData.IsInMainThread)
            {
                thingGroupsByKeys.Clear();
                Log.Message("[Deduplicator] Forming duplicate groups...");
                foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
                {
                    if (DebugThingPlaceHelper.IsDebugSpawnable(thingDef, true))
                    {
                        if (thingDef.IsBuildingArtificial && thingDef.designationCategory is null || thingDef.category == ThingCategory.Ethereal)
                        {
                            continue;
                        }
                        var thingKey = GetThingKey(thingDef);
                        if (!thingKey.NullOrEmpty())
                        {
                            if (!thingGroupsByKeys.TryGetValue(thingKey, out var group))
                            {
                                thingGroupsByKeys[thingKey] = group = new ThingGroup(thingKey);
                            }
                            group.thingDefs.Add(thingDef);
                        }
                    }
                }
                
                foreach (var group in thingGroupsByKeys.Values)
                {
                    if (ThingDictionaryMod.settings.thingSettings.TryGetValue(group.thingKey, out var settings)
                        && !settings.mainThingDef.NullOrEmpty())
                    {
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(settings.mainThingDef);
                        if (def != null)
                        {
                            group.mainThingDef = def;
                        }
                        else
                        {
                            group.mainThingDef = group.thingDefs.First();
                        }
                    }
                    else
                    {
                        group.mainThingDef = group.thingDefs.First();
                    }
                }
                formedThingGroups = true;
            }
        }

        public static string GetThingKey(this ThingDef thingDef)
        {
            return GetThingKeyBase(thingDef) + "_" + thingDef.category.ToString().ToLower();
        }

        public static string GetThingKeyBase(ThingDef thingDef)
        {
            return thingDef.stuffProps != null ? thingDef.LabelAsStuff.ToLower() : thingDef.label?.ToLower();
        }

        public static ThingGroup GetGroup(this ThingDef thingDef)
        {
            if (thingDef.stuffProps != null && thingGroupsByKeys.TryGetValue(thingDef.GetThingKey(), out var resourceGroup))
            {
                return resourceGroup;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(GenDefDatabase), "GetDef")]
    public static class GenDefDatabase_GetDef
    {  
        public static void Postfix(ref Def __result)
        {
            TryModifyResult(ref __result);
        }
    
        public static void TryModifyResult(ref Def __result)
        {
            if (__result is ThingDef thingDef)
            {
                var group = thingDef.GetGroup();
                if (group != null && group.mainThingDef != thingDef)
                {
                    Log.Message("Replacing def: " + thingDef + " with " + group.mainThingDef);
                    Log.ResetMessageCount();
                    __result = group.mainThingDef;
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(GenDefDatabase), "GetDefSilentFail")]
    public static class GenDefDatabase_GetDefSilentFail
    {
        public static void Postfix(ref Def __result)
        {
            GenDefDatabase_GetDef.TryModifyResult(ref __result);
        }
    }
    
    [HarmonyPatch(typeof(BackCompatibility), "BackCompatibleDefName")]
    public static class BackCompatibility_BackCompatibleDefName
    {
        public static void Postfix(Type defType, string defName, ref string __result)
        {
            if (typeof(ThingDef).IsAssignableFrom(defType))
            {
                var thingDef = GenDefDatabase.GetDefSilentFail(defType, defName) as ThingDef;
                if (thingDef != null)
                {
                    var group = thingDef.GetGroup();
                    if (group != null && group.mainThingDef != thingDef)
                    {
                        Log.Message("Replacing def: " + thingDef + " with " + group.mainThingDef);
                        Log.ResetMessageCount();
                        __result = group.mainThingDef.defName;
                    }
                }
            }
        }
    
    }
}
