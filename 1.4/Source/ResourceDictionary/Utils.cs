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

        public static Dictionary<string, List<ThingGroup>> thingGroupsByDefNames = new Dictionary<string, List<ThingGroup>>();
        public static Dictionary<ThingDef, List<ThingGroup>> thingGroupsByDefs = new Dictionary<ThingDef, List<ThingGroup>>();

        public static Dictionary<string, List<ThingGroup>> terrainGroupsByDefNames = new Dictionary<string, List<ThingGroup>>();
        public static Dictionary<TerrainDef, List<ThingGroup>> terrainGroupsByDefs = new Dictionary<TerrainDef, List<ThingGroup>>();

        public static Dictionary<ushort, ThingDef> thingDefsByShortHash = new Dictionary<ushort, ThingDef>();
        public static Dictionary<ushort, TerrainDef> terrainDefsByShortHash = new Dictionary<ushort, TerrainDef>();

        public static List<BuildableDef> defsToResolve = new List<BuildableDef>();
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
                foreach (var def in defsToProcess)
                {
                    if (def is TerrainDef terrainDef)
                    {
                        if (terrainDef != terrainDef.GetMainDef())
                        {
                            if (DefDatabase<TerrainDef>.AllDefsListForReading.Contains(terrainDef))
                            {
                                defsToResolve.Add(terrainDef);
                                DefDatabase<TerrainDef>.Remove(terrainDef);
                                DefDatabase<TerrainDef>.defsByName[terrainDef.defName] = terrainDef.GetMainDef();
                                terrainDef.shortHash = (ushort)(GenText.StableStringHash(terrainDef.defName) % 65535);
                                terrainDefsByShortHash[terrainDef.shortHash] = terrainDef;
                            }
                        }
                    }
                    else if (def is ThingDef thingDef)
                    {
                        if (thingDef != thingDef.GetMainDef())
                        {
                            if (DefDatabase<ThingDef>.AllDefsListForReading.Contains(thingDef))
                            {
                                defsToResolve.Add(thingDef);
                                DefDatabase<ThingDef>.Remove(thingDef);
                                DefDatabase<ThingDef>.defsByName[thingDef.defName] = thingDef.GetMainDef();
                                thingDef.shortHash = (ushort)(GenText.StableStringHash(thingDef.defName) % 65535);
                                thingDefsByShortHash[thingDef.shortHash] = thingDef;
                            }
                        }
                    }
                }
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
                }
            }
            ResourceDictionaryMod.settings.curThingGroups = ResourceDictionaryMod.settings.groups.Values
                .OrderByDescending(x => x.deduplicationEnabled)
                .ThenByDescending(x => x.defs.Count)
                .ThenBy(x => x.groupKey).ToList();

            var groupsWithDeduplication = ResourceDictionaryMod.settings.groups.Values.Where(x => x.deduplicationEnabled).ToList();
            thingGroupsByDefNames.Clear();
            thingGroupsByDefs.Clear();
            terrainGroupsByDefNames.Clear();
            terrainGroupsByDefs.Clear();
            foreach (var group in groupsWithDeduplication)
            {
                if (group.FirstDef is ThingDef)
                {
                    foreach (var defName in group.defs)
                    {
                        if (!thingGroupsByDefNames.TryGetValue(defName, out var list))
                        {
                            thingGroupsByDefNames[defName] = list = new List<ThingGroup>();
                        }
                        list.Add(group);
                        var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (thingDef != null)
                        {
                            if (!thingGroupsByDefs.TryGetValue(thingDef, out var list2))
                            {
                                thingGroupsByDefs[thingDef] = list2 = new List<ThingGroup>();
                            }
                            list2.Add(group);
                        }
                    }
                }
                if (group.FirstDef is TerrainDef)
                {
                    foreach (var defName in group.defs)
                    {
                        if (!terrainGroupsByDefNames.TryGetValue(defName, out var list))
                        {
                            terrainGroupsByDefNames[defName] = list = new List<ThingGroup>();
                        }
                        list.Add(group);
                        var terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
                        if (terrainDef != null)
                        {
                            if (!terrainGroupsByDefs.TryGetValue(terrainDef, out var list2))
                            {
                                terrainGroupsByDefs[terrainDef] = list2 = new List<ThingGroup>();
                            }
                            list2.Add(group);
                        }
                    }
                }

            }
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
            var processedRecipes = new HashSet<RecipeDef>();
            foreach (var originalRecipe in defs)
            {
                originalRecipe.ResolveReferences();
                if (processedRecipes.Any(x => x.label == originalRecipe.label && x.products.Count == 1 && originalRecipe.products.Count == 1
                    && x.ProducedThingDef == originalRecipe.ProducedThingDef && x.products[0].count == originalRecipe.products[0].count))
                {
                    DefDatabase<RecipeDef>.Remove(originalRecipe);
                    originalRecipe.ClearRemovedRecipesFromRecipeUsers();
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
        public static Def GetMainDef(Def __result)
        {
            if (__result is ThingDef thingDef)
            {
                return thingDef.GetMainDef();
            }
            else if (__result is TerrainDef terrainDef)
            {
                return terrainDef.GetMainDef();
            }
            return __result;
        }
        public static ThingDef GetMainDef(this ThingDef def)
        {
            if (def != null && thingGroupsByDefs.TryGetValue(def, out var list))
            {
                foreach (var group in list)
                {
                    if (group.MainThingDef != def)
                    {
                        return group.MainThingDef;
                    }
                }
            }
            return def;
        }

        public static TerrainDef GetMainDef(this TerrainDef def)
        {
            if (def != null && terrainGroupsByDefs.TryGetValue(def, out var list))
            {
                foreach (var group in list)
                {
                    if (group.MainTerrainDef != def)
                    {
                        return group.MainTerrainDef;
                    }
                }
            }
            return def;
        }

        public static string GetMainThingDefName(string defName)
        {
            if (defName != null && thingGroupsByDefNames.TryGetValue(defName, out var list))
            {
                foreach (var group in list)
                {
                    if (group.mainDefName != defName)
                    {
                        return group.mainDefName;
                    }
                }
            }
            return defName;
        }

        public static string GetMainTerrainDefName(string defName)
        {
            if (defName != null && terrainGroupsByDefNames.TryGetValue(defName, out var list))
            {
                foreach (var group in list)
                {
                    if (group.mainDefName != defName)
                    {
                        return group.mainDefName;
                    }
                }
            }
            return defName;
        }
    }
}
