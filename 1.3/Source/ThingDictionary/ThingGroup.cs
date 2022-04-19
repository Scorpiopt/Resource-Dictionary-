using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Deduplicator
{
    public class ThingGroup : IExposable
    {
        public string thingKey;
        public string mainThingDefName;
        public ThingDef mainThingDef;
        public List<string> thingDefs;
        public List<string> removedDefs;
        public bool deduplicationEnabled;
        public ThingDef FirstDef
        {
            get
            {
                foreach (var defName in thingDefs)
                {
                    var def = DefDatabase<ThingDef>.GetNamed(defName);
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
            Scribe_Values.Look(ref thingKey, "stuffKey");
            Scribe_Values.Look(ref mainThingDefName, "mainThing");
            Scribe_Values.Look(ref deduplicationEnabled, "deduplicationEnabled");
            Scribe_Collections.Look(ref thingDefs, "thingDefs");
            Scribe_Collections.Look(ref removedDefs, "removedDefs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                thingDefs = new List<string>();
                removedDefs = new List<string>();
            }
        }
    }
}

