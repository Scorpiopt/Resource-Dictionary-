using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace ResourceDictionary
{
    class ResourceDictionarySettings : ModSettings
    {
        public ResourceDictionarySettings()
        {
            groups = new Dictionary<string, ThingGroup>();
        }
        public Dictionary<string, ThingGroup> groups = new Dictionary<string, ThingGroup>();
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref groups, "resourceSettings", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (groups is null)
                {
                    groups = new Dictionary<string, ThingGroup>();
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
            var explanationTitleRect = new Rect(searchRect.xMax + 15, searchRect.y, inRect.width - (searchLabel.width + searchRect.width + 35), 54f);
            Widgets.Label(explanationTitleRect, "RD.ExplanationTitle".Translate());
            var thingGroups = (searchKey.NullOrEmpty() ? curThingGroups : curThingGroups.Where(x => x.groupKey.ToLower().Contains(searchKey.ToLower())))
                .Where(x => x.FirstDef != null).ToList();
            foreach (var thingGroup in thingGroups)
            {
                foreach (var defName in thingGroup.defs.ListFullCopy())
                {
                    var def = DefDatabase<BuildableDef>.GetNamedSilentFail(defName);
                    if (def is null)
                    {
                        thingGroup.defs.Remove(defName);
                    }
                }
                foreach (var defName in thingGroup.removedDefs.ListFullCopy())
                {
                    var def = DefDatabase<BuildableDef>.GetNamedSilentFail(defName);
                    if (def is null)
                    {
                        thingGroup.removedDefs.Remove(defName);
                    }
                }
            }
            thingGroups.RemoveAll(x => x.defs.Count() == 0 && x.removedDefs.Count() == 0);

            var resetRect = new Rect(searchLabel.x, searchLabel.yMax + 5, 265, 24f);
            if (Widgets.ButtonText(resetRect, "RD.ResetModSettingsToDefault".Translate()))
            {
                groups = new Dictionary<string, ThingGroup>();
                Utils.processedDefs.Clear();
                Utils.TryFormGroups();
                curThingGroups = groups.Values.OrderByDescending(x => x.defs.Count).ThenBy(x => x.groupKey).ToList();
            }
            var height = GetScrollHeight(thingGroups);
            Rect outerRect = new Rect(rect.x, searchRect.yMax + 35, rect.width, rect.height - 70);
            Rect viewArea = new Rect(rect.x, outerRect.y, rect.width - 16, height);
            Widgets.BeginScrollView(outerRect, ref scrollPosition, viewArea, true);
            Vector2 outerPos = new Vector2(rect.x + 5, outerRect.y);
            float num = 0;
            var entryHeight = 200;
            foreach (var thingGroup in thingGroups)
            {
                bool canDrawGroup = num >= scrollPosition.y - entryHeight && num <= (scrollPosition.y + outerRect.height);
                var curNum = outerPos.y;
                var sectionRect = new Rect(outerPos.x - 5, outerPos.y, viewArea.width, 5 + 35f + 24 + (thingGroup.defs.ToList().Count * 28));
                Widgets.DrawMenuSection(sectionRect);
                var labelRect = new Rect(outerPos.x + 5, outerPos.y + 5, viewArea.width - 20, 35f);
                if (canDrawGroup)
                {
                    Widgets.Label(labelRect, Utils.GetDefKeyBase(thingGroup.FirstDef).CapitalizeFirst());
                    var pos = new Vector2(viewArea.width - 220, labelRect.y);
                    Widgets.Checkbox(pos, ref thingGroup.deduplicationEnabled);
                    var activateGroupRect = new Rect(pos.x + 24 + 10, pos.y, 200, 24);
                    Widgets.Label(activateGroupRect, "RD.ActivateDeduplication".Translate());
                }
                outerPos.y += 35f;
                var innerPos = new Vector2(outerPos.x + 10, outerPos.y);
                var toRemove = "";
                foreach (var defName in thingGroup.defs.ToList())
                {
                    if (canDrawGroup)
                    {
                        var def = DefDatabase<BuildableDef>.GetNamed(defName);
                        if (Widgets.RadioButton(new Vector2(innerPos.x, innerPos.y), thingGroup.mainDefName == defName))
                        {
                            thingGroup.mainDefName = defName;
                        }
                        var iconRect = new Rect(innerPos.x + 30, innerPos.y, 24, 24);
                        Widgets.InfoCardButton(iconRect, def);
                        if (def is ThingDef thingDef)
                        {
                            iconRect.x += 24;
                            try
                            {
                                Widgets.ThingIcon(iconRect, thingDef);
                            }
                            catch
                            {

                            }
                        }

                        var name = defName + " - " + def.LabelCap + " [" + (def.modContentPack?.Name ?? "RD.UnknownMod".Translate()) + "]";
                        var labelRect2 = new Rect(iconRect.xMax + 15, innerPos.y, viewArea.width - 350, 24f);
                        Widgets.Label(labelRect2, name);
                        var removeRect = new Rect(viewArea.width - 220, labelRect2.y, 200, 24);
                        if (Widgets.ButtonText(removeRect, "RD.RemoveFromThisGroup".Translate()))
                        {
                            toRemove = defName;
                        }
                        innerPos.y += 28;
                    }
                    outerPos.y += 28;
                }
                outerPos.y += 24;
                outerPos.y += 12f; // separates sections
                if (!toRemove.NullOrEmpty())
                {
                    thingGroup.defs.Remove(toRemove);
                    thingGroup.removedDefs.Add(toRemove);
                    if (thingGroup.mainDefName == toRemove)
                    {
                        thingGroup.mainDefName = thingGroup.FirstDef?.defName;
                    }
                }
                if (canDrawGroup)
                {
                    var addNewStuff = new Rect(innerPos.x, innerPos.y, 200, 24f);
                    if (Widgets.ButtonText(addNewStuff, "RD.AddNewThing".Translate()))
                    {
                        var window = new Dialog_AddNewThing(thingGroup);
                        Find.WindowStack.Add(window);
                    }
                }
                num += (outerPos.y - curNum);
            }
            
            Widgets.EndScrollView();
        }

        private float GetScrollHeight(List<ThingGroup> thingGroups)
        {
            float num = 0;
            foreach (var group in thingGroups)
            {
                num += 35;
                num += 24;
                foreach (var thingDef in group.defs)
                {
                    num += 28f;
                }
                num += 12f;
            }
            return num;
        }
        private static Vector2 scrollPosition = Vector2.zero;

    }
}

