using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ResourceDictionary
{
    public class ThingGroup : IExposable
    {
        public string thingKey;
        public string mainThingDefName;
        private ThingDef mainThingDef;
        public ThingDef MainThingDef
        {
            get
            {
                if (mainThingDef == null)
                {
                    mainThingDef = DefDatabase<ThingDef>.GetNamedSilentFail(mainThingDefName);
                }
                if (mainThingDef is null)
                {
                    mainThingDef = FirstDef;
                    if (mainThingDef != null)
                    {
                        mainThingDefName = mainThingDef.defName;
                    }
                }
                return mainThingDef;
            }
            
        }
        public List<string> thingDefs;
        public List<string> removedDefs;
        public bool deduplicationEnabled;
        public ThingDef FirstDef
        {
            get
            {
                foreach (var defName in thingDefs)
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (def != null)
                    {
                        return def;
                    }
                }
                return null;
            }
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref thingKey, "thingKey");
            Scribe_Values.Look(ref mainThingDefName, "mainThingDefName");
            Scribe_Values.Look(ref deduplicationEnabled, "deduplicationEnabled");
            Scribe_Collections.Look(ref thingDefs, "thingDefs");
            Scribe_Collections.Look(ref removedDefs, "removedDefs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (thingDefs is null)
                {
                    thingDefs = new List<string>();
                }
                if (removedDefs is null)
                {
                    removedDefs = new List<string>();
                }
            }
        }
    }
}

