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

namespace ResourceDictionary
{
    [HarmonyPatch(typeof(Game), "FinalizeInit")]
    public class Game_FinalizeInit_Patch
    {
        public static void Postfix()
        {
            Core.TryFormThingGroups();
            Core.ProcessRecipes();
        }
    }
    
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            TryFormThingGroups();
        }

        public static HashSet<ThingDef> processedDefs = new HashSet<ThingDef>();
        public static void TryFormThingGroups()
        {
            Log.Message("Forming thing groups");
            var defsToProcess = DefDatabase<ThingDef>.AllDefs.Where(x => !processedDefs.Contains(x) && x.IsSpawnable()).ToList();
            if (defsToProcess.Any())
            {
                if (ResourceDictionaryMod.settings.thingSettings is null)
                {
                    ResourceDictionaryMod.settings.thingSettings = new Dictionary<string, ThingGroup>();
                }

                foreach (var def in defsToProcess)
                {
                    ProcessDef(def);
                }
                ProcessGroups();
            }
            

        }
        public static void ProcessGroups()
        {
            foreach (var group in ResourceDictionaryMod.settings.thingSettings.Values)
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
                    }
                    else
                    {
                        group.mainThingDefName = group.thingDefs.First();
                    }

                    //if (group.thingDefs.Count > 1)
                    //{
                    //    Log.Message("Main thing def: " + group.mainThingDef + ", will replace following things: " + String.Join(", ", group.thingDefs.Where(x => x != group.mainThingDef)));
                    //}
                }
            }
            ResourceDictionaryMod.settings.curThingGroups = ResourceDictionaryMod.settings.thingSettings.Values
                .OrderByDescending(x => x.thingDefs.Count).ThenBy(x => x.thingKey).ToList();
        }
        public static void ProcessDef(ThingDef thingDef)
        {
            var thingKey = GetThingKey(thingDef);
            if (!thingKey.NullOrEmpty())
            {
                if (!ResourceDictionaryMod.settings.thingSettings.TryGetValue(thingKey, out var group))
                {
                    ResourceDictionaryMod.settings.thingSettings[thingKey] = group = new ThingGroup
                    {
                        thingKey = thingKey,
                        thingDefs = new List<string>(),
                        removedDefs = new List<string>(),
                    };
                }
                if (!group.removedDefs.Contains(thingDef.defName) && !group.thingDefs.Contains(thingDef.defName))
                {
                    group.thingDefs.Add(thingDef.defName);
                }
            }
            processedDefs.Add(thingDef);
        }

        public static void ProcessRecipes()
        {
            var defs = DefDatabase<RecipeDef>.AllDefsListForReading.ListFullCopy();
            //Log.Message("[Resource Dictionary] Processing " + defs.Count + " recipes.");
            var processedRecipes = new HashSet<RecipeDef>();
            foreach (var originalRecipe in defs)
            {
                originalRecipe.ResolveReferences();
                if (processedRecipes.Any(x => x.label == originalRecipe.label && x.products.Count == 1 && originalRecipe.products.Count == 1
                    && x.ProducedThingDef == originalRecipe.ProducedThingDef && x.products[0].count == originalRecipe.products[0].count))
                {
                    DefDatabase<RecipeDef>.Remove(originalRecipe);
                    originalRecipe.ClearRemovedRecipesFromRecipeUsers();
                    //Log.Message("[Resource Dictionary] Removed duplicate recipe " + originalRecipe.label);
                }
                processedRecipes.Add(originalRecipe);
            }
        }

        public static void ClearRemovedRecipesFromRecipeUsers(this RecipeDef recipeDef)
        {
            if (recipeDef.recipeUsers != null)
            {
                foreach (var recipeUser in recipeDef.recipeUsers)
                {
                    if (recipeUser.allRecipesCached != null)
                    {
                        for (int i = recipeUser.allRecipesCached.Count - 1; i >= 0; i--)
                        {
                            if (recipeUser.allRecipesCached[i] == recipeDef)
                            {
                                recipeUser.allRecipesCached.RemoveAt(i);
                            }
                        }
                    }
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
            if (def.category == ThingCategory.Item || def.category == ThingCategory.Plant || def.category == ThingCategory.Pawn 
                || def.category == ThingCategory.Building)
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
            if (ResourceDictionaryMod.settings.thingSettings.TryGetValue(thingDef.GetThingKey(), out var resourceGroup) 
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
                if (group is null)
                {
                    if (__result.IsSpawnable() && !Core.processedDefs.Contains(__result))
                    {
                        //Log.Message("Missing group for " + __result);
                        try
                        {
                            Core.processedDefs.Add(__result);
                            Core.ProcessDef(__result);
                            Core.ProcessGroups();
                            group = __result.GetGroup();
                        }
                        catch
                        {
                            //Log.Message("Failed to process " + __result);
                        }
                    }
                }
                if (group != null && group.MainThingDef != __result)
                {
                    __result = group.MainThingDef;
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
                if (group is null)
                {
                    if (thingDef.IsSpawnable() && !Core.processedDefs.Contains(thingDef))
                    {
                        //Log.Message("Missing group for " + thingDef);
                        try
                        {
                            Core.processedDefs.Add(thingDef);
                            Core.ProcessDef(thingDef);
                            Core.ProcessGroups();
                            group = thingDef.GetGroup();
                        }
                        catch
                        {
                            //Log.Message("Failed to process " + thingDef);
                        }
                    }
                }
                if (group != null && group.MainThingDef != thingDef)
                {
                    __result = group.MainThingDef;
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
                var thingDef = GenDefDatabase.GetDefSilentFail(defType, __result) as ThingDef;
                if (thingDef != null)
                {
                    var group = thingDef.GetGroup();
                    if (group is null)
                    {
                        if (thingDef.IsSpawnable() && !Core.processedDefs.Contains(thingDef))
                        {
                            //Log.Message("Missing group for " + thingDef);
                            try
                            {
                                Core.processedDefs.Add(thingDef);
                                Core.ProcessDef(thingDef);
                                Core.ProcessGroups();
                                group = thingDef.GetGroup();
                            }
                            catch
                            {
                                //Log.Message("Failed to process " + thingDef);
                            }
                        }
                    }
                    if (group != null && group.mainThingDefName != __result)
                    {
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
