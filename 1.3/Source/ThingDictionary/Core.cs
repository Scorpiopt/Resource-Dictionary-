using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Deduplicator
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            TryFormThingGroups();
        }

        public static bool formedThingGroups;
        public static List<ThingDef> allSpawnableDefs;
        public static void TryFormThingGroups()
        {
            if (!formedThingGroups)
            {
                if (ThingDictionaryMod.settings.thingSettings is null)
                {
                    ThingDictionaryMod.settings.thingSettings = new Dictionary<string, ThingGroup>();
                }
                allSpawnableDefs = DefDatabase<ThingDef>.AllDefs.Where(x => x.IsSpawnable()).ToList();
                Log.Message("[Deduplicator] Forming duplicate groups, processing " + allSpawnableDefs.Count + " defs.");
                foreach (var thingDef in allSpawnableDefs)
                {
                    var thingKey = GetThingKey(thingDef);
                    if (!thingKey.NullOrEmpty())
                    {
                        if (!ThingDictionaryMod.settings.thingSettings.TryGetValue(thingKey, out var group))
                        {
                            ThingDictionaryMod.settings.thingSettings[thingKey] = group = new ThingGroup
                            {
                                thingKey = thingKey,
                                thingDefs = new List<string>(),
                                removedDefs = new List<string>(),
                            };
                        }
                        if (!group.removedDefs.Contains(thingDef.defName))
                        {
                            group.thingDefs.Add(thingDef.defName);
                        }
                    }
                }
                
                foreach (var group in ThingDictionaryMod.settings.thingSettings.Values)
                {
                    if (group.thingDefs.Any())
                    {
                        if (!group.mainThingDefName.NullOrEmpty())
                        {
                            var def = DefDatabase<ThingDef>.GetNamedSilentFail(group.mainThingDefName);
                            if (def != null)
                            {
                                group.mainThingDefName = def.defName;
                            }
                            else
                            {
                                group.mainThingDefName = group.thingDefs.First();
                            }
                        }
                        else
                        {
                            group.mainThingDefName = group.thingDefs.First();
                        }
                        group.mainThingDef = DefDatabase<ThingDef>.GetNamed(group.mainThingDefName);

                        //if (group.thingDefs.Count > 1)
                        //{
                        //    Log.Message("Main thing def: " + group.mainThingDef + ", will replace following things: " + String.Join(", ", group.thingDefs.Where(x => x != group.mainThingDef)));
                        //}
                    }
                }
                formedThingGroups = true;

                if (ThingDictionaryMod.settings.curThingGroups is null)
                {
                    ThingDictionaryMod.settings.curThingGroups = ThingDictionaryMod.settings.thingSettings.Values
                        .OrderByDescending(x => x.thingDefs.Count).ThenBy(x => x.thingKey).ToList();
                }
            }
        }

        public static bool IsSpawnable(this ThingDef def)
        {
            if (def.forceDebugSpawnable)
            {
                return true;
            }
            if (def.DerivedFrom(typeof(Corpse)) || def.IsBlueprint || def.IsFrame || def.DerivedFrom(typeof(ActiveDropPod))
                || def.DerivedFrom(typeof(MinifiedThing)) || def.DerivedFrom(typeof(MinifiedTree)) || def.DerivedFrom(typeof(UnfinishedThing)) 
                || def.DerivedFrom(typeof(SignalAction)) || def.destroyOnDrop)
            {
                return false;
            }
            if (def.IsBuildingArtificial && def.designationCategory is null)
            {
                return false;
            }
            if (def.category == ThingCategory.Item || def.category == ThingCategory.Plant || def.category == ThingCategory.Pawn)
            {
                return true;
            }
            if (def.category == ThingCategory.Building && def.building.isNaturalRock)
            {
                return true;
            }
            if (def.category == ThingCategory.Building && !def.BuildableByPlayer)
            {
                return true;
            }
            if (def.category == ThingCategory.Building && def.BuildableByPlayer)
            {
                return true;
            }
            return false;
        }

        public static bool DerivedFrom(this ThingDef thingDef, Type type)
        {
            return type.IsAssignableFrom(thingDef.thingClass);
        }
        public static string GetThingKey(this ThingDef thingDef)
        {
            return GetThingKeyBase(thingDef) + "_" + thingDef.category.ToString().ToLower();
        }

        public static string GetThingKeyBase(ThingDef thingDef)
        {
            return thingDef.label?.ToLower();
        }
        public static ThingGroup GetGroup(this ThingDef thingDef)
        {
            if (ThingDictionaryMod.settings.thingSettings.TryGetValue(thingDef.GetThingKey(), out var resourceGroup) 
                && resourceGroup.deduplicationEnabled)
            {
                return resourceGroup;
            }
            return null;
        }
    }
    
    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "ResolveAllWantedCrossReferences")]
    public static class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences
    {
        public static void Prefix()
        {
            Core.TryFormThingGroups();
        }
    }
    
    [HarmonyPatch(typeof(ThingDef), "Named")]
    public static class ThingDef_Named
    {
        public static void Postfix(ref ThingDef __result)
        {
            if (__result != null)
            {
                var group = __result.GetGroup();
                if (group != null && group.mainThingDef != __result)
                {
                    Log.Message("1 Replacing def: " + __result + " with " + group.mainThingDefName);
                    Log.ResetMessageCount();
                    __result = group.mainThingDef;
                }
            }
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
                    Log.Message("2 Replacing def: " + thingDef + " with " + group.mainThingDefName);
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
                    if (group != null && group.mainThingDefName != thingDef.defName)
                    {
                        Log.Message("3 Replacing def: " + thingDef + " with " + group.mainThingDefName);
                        Log.ResetMessageCount();
                        __result = group.mainThingDefName;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(StatsReportUtility), "Reset")]
    public static class StatsReportUtility_Reset_Patch
    {
        public static bool Prefix()
        {
            if (Current.Game?.World?.factionManager is null)
            {
                Reset();
                return false;
            }
            return true;
        }

        public static void Reset()
        {
            StatsReportUtility.scrollPosition = default(Vector2);
            StatsReportUtility.scrollPositionRightPanel = default(Vector2);
            StatsReportUtility.selectedEntry = null;
            StatsReportUtility.scrollPositioner.Arm(armed: false);
            StatsReportUtility.mousedOverEntry = null;
            StatsReportUtility.cachedDrawEntries.Clear();
            StatsReportUtility.quickSearchWidget.Reset();
            PermitsCardUtility.selectedPermit = null;
            PermitsCardUtility.selectedFaction = null;
        }
    }
}
