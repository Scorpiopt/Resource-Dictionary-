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
        public string groupKey;
        public string mainDefName;
        private BuildableDef mainDef;
        public BuildableDef MainDef
        {
            get
            {
                if (mainDef == null)
                {
                    mainDef = DefDatabase<BuildableDef>.GetNamedSilentFail(mainDefName);
                }
                if (mainDef is null)
                {
                    mainDef = FirstDef;
                    if (mainDef != null)
                    {
                        mainDefName = mainDef.defName;
                    }
                }
                return mainDef;
            }
            
        }
        public List<string> defs;
        public List<string> removedDefs;
        public bool deduplicationEnabled;
        public BuildableDef FirstDef
        {
            get
            {
                foreach (var defName in defs)
                {
                    var def = DefDatabase<BuildableDef>.GetNamedSilentFail(defName);
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
            Scribe_Values.Look(ref groupKey, "thingKey");
            Scribe_Values.Look(ref mainDefName, "mainThingDefName");
            Scribe_Values.Look(ref deduplicationEnabled, "deduplicationEnabled");
            Scribe_Collections.Look(ref defs, "thingDefs");
            Scribe_Collections.Look(ref removedDefs, "removedDefs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (defs is null)
                {
                    defs = new List<string>();
                }
                if (removedDefs is null)
                {
                    removedDefs = new List<string>();
                }
            }
        }
    }
}

