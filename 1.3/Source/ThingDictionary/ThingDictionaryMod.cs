using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Deduplicator
{
    class ThingDictionaryMod : Mod
    {
        public static ThingDictionarySettings settings;
        public ThingDictionaryMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<ThingDictionarySettings>();
            new Harmony("ThingDictionary.Mod").PatchAll();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            settings.curThingGroups = null;
        }
        public override string SettingsCategory()
        {
            return this.Content.Name;
        }
    }
}