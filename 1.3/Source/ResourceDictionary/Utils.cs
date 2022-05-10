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
using static Verse.DirectXmlCrossRefLoader;

namespace ResourceDictionary
{
    public static class Utils
    {
        public static HashSet<ThingDef> processedDefs = new HashSet<ThingDef>();
        public static void TryFormThingGroups()
        {
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
                    //if (group.thingDefs.Count > 1 && group.deduplicationEnabled)
                    //{
                    //    Log.Message("Main thing def: " + group.mainThingDefName + ", will replace following things: " + String.Join(", ", group.thingDefs.Where(x => x != group.mainThingDefName)));
                    //}
                }
            }
            ResourceDictionaryMod.settings.curThingGroups = ResourceDictionaryMod.settings.thingSettings.Values
                .OrderByDescending(x => x.deduplicationEnabled)
                .ThenByDescending(x => x.thingDefs.Count)
                .ThenBy(x => x.thingKey).ToList();
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

        public static Dictionary<ThingDef, bool> cachedSpawnableResults = new Dictionary<ThingDef, bool>();
        public static bool IsSpawnable(this ThingDef def)
        {
            if (!cachedSpawnableResults.TryGetValue(def, out bool result))
            {
                cachedSpawnableResults[def] = result = IsSpawnableInt(def);
            }
            return result;
        }

        public static bool IsSpawnableInt(this ThingDef def)
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
        public static string GetThingKey(this ThingDef thingDef)
        {
            if (!thingDef.label.NullOrEmpty())
            {
                var result = GetThingKeyBase(thingDef) + "_" + thingDef.category.ToString().ToLower();
                return result;
            }
            return null;
        }
        public static string GetThingKeyBase(ThingDef thingDef)
        {
            return thingDef.label?.ToLower();
        }
        public static ThingGroup GetGroup(this ThingDef thingDef)
        {
            if (thingDef != null)
            {
                return ResourceDictionaryMod.settings.thingSettings.Values
                        .Where(x => x.deduplicationEnabled && x.thingDefs.Count > 1 && x.thingDefs.Contains(thingDef.defName)).FirstOrDefault();
            }
            return null;
        }
        public static void TryModifyResult(ref Def __result)
        {
            if (__result is ThingDef thingDef)
            {
                TryModifyResult(ref thingDef);
            }
        }

        public static int replaceCount;
        public static void TryModifyResult(ref ThingDef thingDef)
        {
            var group = thingDef.GetGroup();
            if (group is null)
            {
                if (!processedDefs.Contains(thingDef) && thingDef.IsSpawnable())
                {
                    try
                    {
                        processedDefs.Add(thingDef);
                        ProcessDef(thingDef);
                        ProcessGroups();
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
                replaceCount++;
                Log.Message(replaceCount + " - Replacing " + thingDef + " with " + group.MainThingDef);
                thingDef = group.MainThingDef;
            }
        }

        public static void TryModifyResult(string defName, ref string result)
        {
            var group = ResourceDictionaryMod.settings.thingSettings.Values.Where(x => x.deduplicationEnabled 
                && x.thingDefs.Contains(defName) && x.mainThingDefName != defName).FirstOrDefault();
            if (group != null && result != group.mainThingDefName)
            {
                replaceCount++;
                Log.Message(replaceCount + " - 2 Replacing " + result + " with " + group.mainThingDefName);
                result = group.mainThingDefName;
            }
        }
    }

}
