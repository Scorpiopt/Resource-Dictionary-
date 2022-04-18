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
    class ThingGroupExposable : IExposable
    {
        public string stuffKey;
        public string mainThingDef;
        public List<string> thingDefs;

        public void ExposeData()
        {
            Scribe_Values.Look(ref stuffKey, "stuffKey");
            Scribe_Values.Look(ref mainThingDef, "mainThing");
            Scribe_Collections.Look(ref thingDefs, "resources");
        }
    }
}

