using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace Deduplicator
{
    class ThingDictionarySettings : ModSettings
    {
        public ThingDictionarySettings()
        {
            thingSettings = new Dictionary<string, ThingGroup>();
        }
        public Dictionary<string, ThingGroup> thingSettings = new Dictionary<string, ThingGroup>();
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref thingSettings, "resourceSettings", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (thingSettings is null)
                {
                    thingSettings = new Dictionary<string, ThingGroup>();
                }
            }
        }

        string searchKey;

        public List<ThingGroup> curThingGroups;
        public void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            var searchLabel = new Rect(rect.x + 5, rect.y, 60, 24);
            Widgets.Label(searchLabel, "RD.Search".Translate());
            var searchRect = new Rect(searchLabel.xMax + 5, searchLabel.y, 200, 24f);
            searchKey = Widgets.TextField(searchRect, searchKey);
            Text.Anchor = TextAnchor.UpperLeft;

            var thingGroups = (searchKey.NullOrEmpty() ?
                curThingGroups :
                curThingGroups.Where(x => x.thingKey.ToLower().Contains(searchKey.ToLower()))).Where(x => x.FirstDef != null)
                .ToList();

            var height = GetScrollHeight(thingGroups);
            Rect outerRect = new Rect(rect.x, searchRect.yMax + 10, rect.width, rect.height - 70);
            Rect viewArea = new Rect(rect.x, outerRect.y, rect.width - 16, height);
            Widgets.BeginScrollView(outerRect, ref scrollPosition, viewArea, true);
            Vector2 outerPos = new Vector2(rect.x + 5, outerRect.y);
            float num = 0;
            foreach (var thingGroup in thingGroups)
            {
                num += (thingGroup.thingDefs.Count * 28f) + (24 + 32 + 30);
                //if (num >= scrollPosition.y && num <= (scrollPosition.y + 2000))
                {
                    var labelRect = new Rect(outerPos.x, outerPos.y, 200, 30f);
                    Widgets.Label(labelRect, Core.GetThingKeyBase(thingGroup.FirstDef).CapitalizeFirst());
                    Widgets.Checkbox(new Vector2(labelRect.xMax, labelRect.y), ref thingGroup.deduplicationEnabled);
                    var innerPos = new Vector2(outerPos.x + 10, labelRect.yMax);
                    var toRemove = "";
                    foreach (var defName in thingGroup.thingDefs.ToList())
                    {
                        var def = DefDatabase<ThingDef>.GetNamed(defName);
                        if (Widgets.RadioButton(new Vector2(innerPos.x, innerPos.y), thingGroup.mainThingDefName == defName))
                        {
                            thingGroup.mainThingDefName = defName;
                        }
                        var iconRect = new Rect(innerPos.x + 30, innerPos.y, 24, 24);
                        Widgets.InfoCardButton(iconRect, def);
                        iconRect.x += 24;
                        Widgets.ThingIcon(iconRect, def);
                        var name = defName + " - " + def.LabelCap + " [" + (def.modContentPack?.Name ?? "RD.UnknownMod".Translate()) + "]";
                        var labelRect2 = new Rect(iconRect.xMax + 15, innerPos.y, Text.CalcSize(name).x + 10, 24f);
                        Widgets.Label(labelRect2, name);
                        var removeRect = new Rect(labelRect2.xMax + 5, labelRect2.y, 20, 21f);
                        if (Widgets.ButtonImage(removeRect, TexButton.DeleteX))
                        {
                            toRemove = defName;
                        }
                        innerPos.y += 28;
                        outerPos.y += 28;
                    }

                    if (!toRemove.NullOrEmpty())
                    {
                        thingGroup.thingDefs.Remove(toRemove);
                        thingGroup.removedDefs.Add(toRemove);
                    }

                    var addNewStuff = new Rect(innerPos.x, innerPos.y, 200, 24f);
                    if (Widgets.ButtonText(addNewStuff, "RD.AddNewThing".Translate()))
                    {
                        var window = new Dialog_AddNewThing(thingGroup);
                        Find.WindowStack.Add(window);
                    }
                    outerPos.y += 24;
                    outerPos.y += 32;
                }
            }
            Widgets.EndScrollView();
        }

        private float GetScrollHeight(List<ThingGroup> thingGroups)
        {
            float num = 0;
            foreach (var group in thingGroups)
            {
                num += 32;
                num += 24;
                foreach (var thingDef in group.thingDefs)
                {
                    num += 28f;
                }
            }
            return num;
        }
        private static Vector2 scrollPosition = Vector2.zero;

    }
}

