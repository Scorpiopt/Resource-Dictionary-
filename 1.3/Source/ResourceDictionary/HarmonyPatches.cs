using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Xml;
using Verse;
using static Verse.DirectXmlCrossRefLoader;
using System;
using System.Linq;

namespace ResourceDictionary
{
    [HarmonyPatch(typeof(Game), "FinalizeInit")]
    public class Game_FinalizeInit_Patch
    {
        public static void Postfix()
        {
            Utils.TryFormGroups();
            Utils.ProcessRecipes();
        }
    }

    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "ResolveAllWantedCrossReferences")]
    public static class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences
    {
        public static void Prefix()
        {
            Utils.TryFormGroups();
            foreach (var wanted in wantedRefs)
            {
                if (wanted is WantedRefForObject wantedRefForObject)
                {
                    Utils.TryModifyResult(wantedRefForObject.defName, ref wantedRefForObject.defName);
                }
                else
                {
                    var genericTypeDefinition = wanted.GetType().GetGenericTypeDefinition();
                    if (genericTypeDefinition == typeof(WantedRefForList<>))
                    {
                        var defType = wanted.GetType().GetGenericArguments()[0];
                        if (typeof(BuildableDef).IsAssignableFrom(defType))
                        {
                            var field = Traverse.Create(wanted).Field("defNames");
                            var defNames = field.GetValue() as List<string>;
                            var newDefNames = new List<string>();
                            for (var i = 0; i < defNames.Count; i++)
                            {
                                var defName = defNames[i];
                                Utils.TryModifyResult(defName, ref defName);
                                newDefNames.Add(defName);
                            }
                            field.SetValue(newDefNames);
                        }
                    }
                    else if (genericTypeDefinition == typeof(WantedRefForDictionary<,>))
                    {
                        var key = wanted.GetType().GetGenericArguments()[0];
                        var value = wanted.GetType().GetGenericArguments()[1];
                        bool keyIsDef = typeof(BuildableDef).IsAssignableFrom(key);
                        bool valueIsDef = typeof(BuildableDef).IsAssignableFrom(value);
                        if (keyIsDef || valueIsDef)
                        {
                            var field = Traverse.Create(wanted).Field("wantedDictRefs");
                            var wantedDictRefs = field.GetValue() as List<XmlNode>;
                            foreach (XmlNode wantedDictRef in wantedDictRefs)
                            {
                                if (keyIsDef)
                                {
                                    XmlNode xmlNode = wantedDictRef["key"];
                                    var text = xmlNode.InnerText;
                                    Log.Message("Replacing " + text);
                                    Utils.TryModifyResult(text, ref text);
                                    xmlNode.InnerText = text;
                                }
                                if (valueIsDef)
                                {
                                    XmlNode xmlNode2 = wantedDictRef["value"];
                                    var text = xmlNode2.InnerText;
                                    Log.Message("Replacing " + text);
                                    Utils.TryModifyResult(text, ref text);
                                    xmlNode2.InnerText = text;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingDef), "Named")]
    public static class ThingDef_Named
    {
        public static void Postfix(ref ThingDef __result)
        {
            if (__result != null)
            {
                var value = __result as Def;
                Utils.TryModifyResult(ref value);
                __result = value as ThingDef;
            }
        }
    }
    
    [HarmonyPatch(typeof(TerrainDef), "Named")]
    public static class TerrainDef_Named
    {
        public static void Postfix(ref TerrainDef __result)
        {
            if (__result != null)
            {
                var value = __result as Def;
                Utils.TryModifyResult(ref value);
                __result = value as TerrainDef;
            }
        }
    }

    [HarmonyPatch(typeof(GenDefDatabase), "GetDef")]
    public static class GenDefDatabase_GetDef
    {
        public static void Postfix(ref Def __result)
        {
            Utils.TryModifyResult(ref __result);
        }
    }
    
    [HarmonyPatch(typeof(GenDefDatabase), "GetDefSilentFail")]
    public static class GenDefDatabase_GetDefSilentFail
    {
        public static void Postfix(ref Def __result)
        {
            Utils.TryModifyResult(ref __result);
        }
    }
    
    [HarmonyPatch(typeof(ThingDefCountClass), MethodType.Constructor, new Type[] { typeof(ThingDef), typeof(int) })]
    public static class ThingDefCountClass_GetDefSilentFail
    {
        public static void Prefix(ref ThingDef thingDef)
        {
            var value = thingDef as Def;
            Utils.TryModifyResult(ref value);
            thingDef = value as ThingDef;
        }
    }

    [HarmonyPatch(typeof(ThingMaker), "MakeThing")]
    public static class ThingMaker_MakeThing
    {
        public static void Prefix(ref ThingDef def, ref ThingDef stuff)
        {
            if (def != null)
            {
                var value = def as Def;
                Utils.TryModifyResult(ref value);
                def = value as ThingDef;
            }
            if (stuff != null)
            {
                var value = stuff as Def;
                Utils.TryModifyResult(ref value);
                stuff = value as ThingDef;
            }
        }
    }
    
    [HarmonyPatch(typeof(BackCompatibility), "BackCompatibleDefName")]
    public static class BackCompatibility_BackCompatibleDefName
    {
        public static void Postfix(Type defType, string defName, ref string __result)
        {
            if (typeof(BuildableDef).IsAssignableFrom(defType))
            {
                Utils.TryModifyResult(defName, ref __result);
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
            if (typeof(BuildableDef).IsAssignableFrom(type))
            {
                Utils.TryModifyResult(targetDefName, ref targetDefName);
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
            if (typeof(BuildableDef).IsAssignableFrom(type))
            {
                Utils.TryModifyResult(targetDefName, ref targetDefName);
            }
        }
    }
    
    [HarmonyPatch(typeof(DirectXmlCrossRefLoader), "RegisterObjectWantsCrossRef",
    new Type[] { typeof(object), typeof(string), typeof(XmlNode), typeof(string), typeof(Type) })]
    public class DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchThree
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var getName = AccessTools.PropertyGetter(typeof(XmlNode), "Name");
            foreach (var code in codes)
            {
                yield return code;
                if (code.Calls(getName))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(DirectXmlCrossRefLoader_RegisterObjectWantsCrossRef_PatchThree), nameof(ReturnName)));
                }
            }
        }
    
        public static string ReturnName(string name, object wanter, string fieldName)
        {
            var type = wanter.GetType().GetField(fieldName, AccessTools.all).FieldType;
            if (typeof(BuildableDef).IsAssignableFrom(type))
            {
                Utils.TryModifyResult(name, ref name);
            }
            return name;
        }
    }
}
