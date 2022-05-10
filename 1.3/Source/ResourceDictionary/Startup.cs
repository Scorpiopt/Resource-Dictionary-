using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;

namespace ResourceDictionary
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            Utils.TryFormGroups();
            //Debug();
        }

        //public static void Debug()
        //{
        //    foreach (var recipe in DefDatabase<RecipeDef>.AllDefs)
        //    {
        //        if (recipe.ProducedThingDef != null)
        //        {
        //            var group = recipe.ProducedThingDef.GetGroup();
        //            if (group != null)
        //            {
        //                Log.Message("Group for " + recipe.ProducedThingDef + " - " + group.groupKey + " - " + string.Join(", ", group.defs));
        //                if (group.MainDef != recipe.ProducedThingDef)
        //                {
        //                    Log.Error(recipe + " has a produced thing def not replaced: " + recipe.ProducedThingDef + ", main thing def: " + group.MainDef);
        //                }
        //                else
        //                {
        //                    Log.Message(recipe + " works fine, has the right produced thingDef: " + recipe.ProducedThingDef);
        //                }
        //            }
        //        }
        //        if (recipe.defaultIngredientFilter != null)
        //        {
        //            DebugThingFilter(recipe, recipe.defaultIngredientFilter);
        //        }
        //        if (recipe.fixedIngredientFilter != null)
        //        {
        //            DebugThingFilter(recipe, recipe.fixedIngredientFilter);
        //        }
        //        if (recipe.ingredients != null)
        //        {
        //            foreach (var ingredient in recipe.ingredients)
        //            {
        //                DebugThingFilter(recipe, ingredient.filter);
        //            }
        //        }
        //    }
        //}
        //
        //private static void DebugThingFilter(RecipeDef recipe, ThingFilter thingFilter)
        //{
        //    foreach (var thingDef in (thingFilter.thingDefs ?? new List<ThingDef>()))
        //    {
        //        var group = thingDef.GetGroup();
        //        if (group != null)
        //        {
        //            Log.Message("Group for " + thingDef + " - " + group.groupKey + " - " + string.Join(", ", group.defs));
        //            if (group.MainDef != thingDef)
        //            {
        //                Log.Error(recipe + " has an allowed thing def not replaced: " + thingDef + ", main thing def: " + group.MainDef);
        //            }
        //            else
        //            {
        //                Log.Message(recipe + " works fine, has the right thingDef: " + thingDef);
        //            }
        //        }
        //    }
        //}
    }
}
