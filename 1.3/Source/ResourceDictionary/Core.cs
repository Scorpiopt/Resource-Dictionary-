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
            if (thingDef != null)
            {
                var group = ResourceDictionaryMod.settings.thingSettings.Values.Where(x => x.thingDefs.Contains(thingDef.defName)).FirstOrDefault();
                if (group != null && group.deduplicationEnabled)
                {
                    return group;
                }
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
        public static void TryModifyResult(ref ThingDef thingDef)
        {
            var group = thingDef.GetGroup();
            if (group is null)
            {
                if (thingDef.IsSpawnable() && !processedDefs.Contains(thingDef))
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
                thingDef = group.MainThingDef;
            }
        }

        public static void TryModifyResult(string defName, ref string result)
        {
            var group = ResourceDictionaryMod.settings.thingSettings.Values.Where(x => x.thingDefs.Contains(defName)).FirstOrDefault();
            if (group != null)
            {
                if (group.deduplicationEnabled)
                {
                    if (group.mainThingDefName != result)
                    {
                        result = group.mainThingDefName;
                    }
                }
            }
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
                Core.TryModifyResult(ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(GenDefDatabase), "GetDef")]
    public static class GenDefDatabase_GetDef
    {
        public static void Postfix(ref Def __result)
        {
            Core.TryModifyResult(ref __result);
        }
    }

    [HarmonyPatch(typeof(GenDefDatabase), "GetDefSilentFail")]
    public static class GenDefDatabase_GetDefSilentFail
    {
        public static void Postfix(ref Def __result)
        {
            Core.TryModifyResult(ref __result);
        }
    }

    [HarmonyPatch(typeof(ThingDefCountClass), MethodType.Constructor, new Type[] { typeof(ThingDef), typeof(int) })]
    public static class ThingDefCountClass_GetDefSilentFail
    {
        public static void Prefix(ref ThingDef thingDef)
        {
            Core.TryModifyResult(ref thingDef);
        }
    }

    [HarmonyPatch(typeof(ThingMaker), "MakeThing")]
    public static class ThingMaker_MakeThing
    {
        public static void Prefix(ref ThingDef def, ref ThingDef stuff)
        {
            if (def != null)
            {
                Core.TryModifyResult(ref def);
            }
            if (stuff != null)
            {
                Core.TryModifyResult(ref stuff);
            }
        }
    }

    [HarmonyPatch(typeof(BackCompatibility), "BackCompatibleDefName")]
    public static class BackCompatibility_BackCompatibleDefName
    {
        public static void Postfix(Type defType, string defName, ref string __result)
        {
            if (typeof(ThingDef).IsAssignableFrom(defType))
            {
                Core.TryModifyResult(defName, ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
        new Type[] { typeof(object), typeof(FieldInfo), typeof(string), typeof(string), typeof(Type) })]
    public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchOne
    {
        public static void Prefix(object wanter, FieldInfo fi, ref string targetDefName, string mayRequireMod = null, Type assumeFieldType = null)
        {
            var type = fi.FieldType;
            if (typeof(ThingDef).IsAssignableFrom(type))
            {
                Core.TryModifyResult(targetDefName, ref targetDefName);
            }
        }
    }

    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
        new Type[] { typeof(object), typeof(string), typeof(string), typeof(string), typeof(Type) })]
    public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchTwo
    {
        public static void Prefix(object wanter, string fieldName, ref string targetDefName, string mayRequireMod = null, Type overrideFieldType = null)
        {
            var type = wanter.GetType().GetField(fieldName, AccessTools.all).FieldType;
            if (typeof(ThingDef).IsAssignableFrom(type))
            {
                Core.TryModifyResult(targetDefName, ref targetDefName);
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
