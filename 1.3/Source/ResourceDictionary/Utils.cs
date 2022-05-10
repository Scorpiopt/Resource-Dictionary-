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
using System.Xml;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ResourceDictionary
{
    public static class Utils
    {
        public static HashSet<BuildableDef> processedDefs = new HashSet<BuildableDef>();
        public static void TryFormGroups()
        {
            var defsToProcess = DefDatabase<BuildableDef>.AllDefs.Where(x => !processedDefs.Contains(x) && x.IsSpawnable()).ToList();
            if (defsToProcess.Any())
            {
                if (ResourceDictionaryMod.settings.groups is null)
                {
                    ResourceDictionaryMod.settings.groups = new Dictionary<string, ThingGroup>();
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
            foreach (var group in ResourceDictionaryMod.settings.groups.Values)
            {
                if (group.defs.Any())
                {
                    if (!group.mainDefName.NullOrEmpty())
                    {
                        var def = DefDatabase<BuildableDef>.GetNamedSilentFail(group.mainDefName);
                        if (def != null)
                        {
                            group.mainDefName = def.defName;
                        }
                    }
                    else
                    {
                        group.mainDefName = group.defs.First();
                    }
                    //if (group.thingDefs.Count > 1 && group.deduplicationEnabled)
                    //{
                    //    Log.Message("Main thing def: " + group.mainThingDefName + ", will replace following things: " + String.Join(", ", group.thingDefs.Where(x => x != group.mainThingDefName)));
                    //}
                }
            }
            ResourceDictionaryMod.settings.curThingGroups = ResourceDictionaryMod.settings.groups.Values
                .OrderByDescending(x => x.deduplicationEnabled)
                .ThenByDescending(x => x.defs.Count)
                .ThenBy(x => x.groupKey).ToList();
        }
        public static void ProcessDef(BuildableDef def)
        {
            var thingKey = GetDefKey(def);
            if (!thingKey.NullOrEmpty())
            {
                if (!ResourceDictionaryMod.settings.groups.TryGetValue(thingKey, out var group))
                {
                    ResourceDictionaryMod.settings.groups[thingKey] = group = new ThingGroup
                    {
                        groupKey = thingKey,
                        defs = new List<string>(),
                        removedDefs = new List<string>(),
                    };
                }
                if (!group.removedDefs.Contains(def.defName) && !group.defs.Contains(def.defName))
                {
                    group.defs.Add(def.defName);
                }
            }
            processedDefs.Add(def);
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

        public static Dictionary<BuildableDef, bool> cachedSpawnableResults = new Dictionary<BuildableDef, bool>();
        public static bool IsSpawnable(this BuildableDef def)
        {
            if (!cachedSpawnableResults.TryGetValue(def, out bool result))
            {
                cachedSpawnableResults[def] = result = IsSpawnableInt(def);
            }
            return result;
        }

        public static bool IsSpawnableInt(this BuildableDef def)
        {
            if (def is TerrainDef terrainDef)
            {
                return true;
            }
            else if (def is ThingDef thingDef)
            {
                return IsSpawnableInt(thingDef);
            }
            return false;
        }

        public static bool IsSpawnableInt(ThingDef def)
        {
            try
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
            }
            catch (Exception ex)
            {
                Log.Error("Caught error processing " + def + ": " + ex.ToString());
            }
            return false;
        }
        public static bool DerivedFrom(this ThingDef thingDef, Type type)
        {
            return type.IsAssignableFrom(thingDef.thingClass);
        }
        public static string GetDefKey(this BuildableDef buildableDef)
        {
            if (buildableDef is ThingDef thingDef)
            {
                return GetThingDefKey(thingDef);
            }
            else if (buildableDef is TerrainDef terrainDef)
            {
                return GetTerrainDefKey(terrainDef);
            }
            return null;
        }
        public static string GetThingDefKey(this ThingDef def)
        {
            if (!def.label.NullOrEmpty())
            {
                var result = GetDefKeyBase(def) + "_" + def.category.ToString().ToLower();
                return result;
            }
            return null;
        }

        public static string GetTerrainDefKey(this TerrainDef terrainDef)
        {
            if (!terrainDef.label.NullOrEmpty())
            {
                var result = GetDefKeyBase(terrainDef) + "_terrain";
                return result;
            }
            return null;
        }
        public static string GetDefKeyBase(BuildableDef def)
        {
            return def.label?.ToLower();
        }
        public static ThingGroup GetGroup(this BuildableDef def)
        {
            if (def != null)
            {
                return ResourceDictionaryMod.settings.groups.Values
                        .Where(x => x.deduplicationEnabled && x.defs.Count > 1 && x.defs.Contains(def.defName)).FirstOrDefault();
            }
            return null;
        }
        public static void TryModifyResult(ref Def __result)
        {
            if (__result is BuildableDef def)
            {
                TryModifyResult(ref def);
            }
        }

        public static void TryModifyResult(ref BuildableDef def)
        {
            var group = def.GetGroup();
            if (group is null)
            {
                if (!processedDefs.Contains(def) && def.IsSpawnable())
                {
                    try
                    {
                        processedDefs.Add(def);
                        ProcessDef(def);
                        ProcessGroups();
                        group = def.GetGroup();
                    }
                    catch
                    {
                        Log.Error("Failed to process " + def);
                    }
                }
            }

            if (group != null && group.MainDef != def)
            {
                def = group.MainDef;
            }
        }

        public static void TryModifyResult(string defName, ref string result)
        {
            var group = ResourceDictionaryMod.settings.groups.Values.Where(x => x.deduplicationEnabled 
                && x.defs.Contains(defName) && x.mainDefName != defName).FirstOrDefault();
            if (group != null && result != group.mainDefName)
            {
                result = group.mainDefName;
            }
        }
    }
}
