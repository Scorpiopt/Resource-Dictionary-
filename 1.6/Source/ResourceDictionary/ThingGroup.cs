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
        private ThingDef mainThingDef;
        public ThingDef MainThingDef
        {
            get
            {
                if (mainThingDef == null)
                {
                    mainThingDef = DefDatabase<ThingDef>.GetNamedSilentFail(mainDefName);
                }
                if (mainThingDef is null)
                {
                    mainThingDef = FirstDef as ThingDef;
                    if (mainThingDef != null)
                    {
                        mainDefName = mainThingDef.defName;
                    }
                }
                return mainThingDef;
            }
        }

        private TerrainDef mainTerrainDef;
        public TerrainDef MainTerrainDef
        {
            get
            {
                if (mainTerrainDef == null)
                {
                    mainTerrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(mainDefName);
                }
                if (mainTerrainDef is null)
                {
                    mainTerrainDef = FirstDef as TerrainDef;
                    if (mainTerrainDef != null)
                    {
                        mainDefName = mainTerrainDef.defName;
                    }
                }
                return mainTerrainDef;
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

