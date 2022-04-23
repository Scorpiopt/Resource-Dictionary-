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

namespace ResourceDictionary
{
    class ResourceDictionaryMod : Mod
    {
        public static ResourceDictionarySettings settings;
        public ResourceDictionaryMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<ResourceDictionarySettings>();
            new Harmony("ResourceDictionary.Mod").PatchAll();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        
        public override string SettingsCategory()
        {
            return this.Content.Name;
        }
    }
}