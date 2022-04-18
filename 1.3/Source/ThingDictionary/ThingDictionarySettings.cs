using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deduplicator
{
    class ThingDictionarySettings : ModSettings
    {
        public ThingDictionarySettings()
        {
            thingSettings = new Dictionary<string, ThingGroupExposable>();
        }
        public Dictionary<string, ThingGroupExposable> thingSettings = new Dictionary<string, ThingGroupExposable>();
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref thingSettings, "resourceSettings", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (thingSettings is null)
                {
                    thingSettings = new Dictionary<string, ThingGroupExposable>();
                }
            }
        }

        string searchKey;
        public void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            string title = "RD.ThingGroups".Translate();
            Text.Font = GameFont.Medium;
            var titleWidth = Text.CalcSize(title);
            var titleRect = new Rect(rect.center.x - titleWidth.x, rect.y, titleWidth.x, titleWidth.y);
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            var searchLabel = new Rect(rect.x + 5, titleRect.yMax + 5, 60, 24);
            Widgets.Label(searchLabel, "RD.Search".Translate());
            var searchRect = new Rect(searchLabel.xMax + 5, searchLabel.y, 200, 24f);
            searchKey = Widgets.TextField(searchRect, searchKey);
            Text.Anchor = TextAnchor.UpperLeft;

            var thingGroups = (searchKey.NullOrEmpty() ? 
                Core.thingGroupsByKeys.Values :
                Core.thingGroupsByKeys.Values.Where(x => x.thingKey.ToLower().Contains(searchKey.ToLower()))).Where(x => x.thingDefs.Count > 1).ToList();
            var height = GetScrollHeight(thingGroups);

            Rect outerRect = new Rect(rect.x, searchRect.yMax + 10, rect.width, rect.height - 70);
            Rect viewArea = new Rect(rect.x, outerRect.y, rect.width - 16, height);
            Widgets.BeginScrollView(outerRect, ref scrollPosition, viewArea, true);

            Vector2 outerPos = new Vector2(rect.x + 5, outerRect.y);
            foreach (var thingGroup in thingGroups)
            {
                var labelRect = new Rect(outerPos.x, outerPos.y, 200, 30f);
                Text.Font = GameFont.Medium;
                Widgets.Label(labelRect, Core.GetThingKeyBase(thingGroup.thingDefs.First()).CapitalizeFirst());
                Text.Font = GameFont.Small;
                var innerPos = new Vector2(outerPos.x + 10, labelRect.yMax);
                foreach (var thingDef in thingGroup.thingDefs.ToList())
                {
                    if (Widgets.RadioButton(new Vector2(innerPos.x, innerPos.y), thingGroup.mainThingDef == thingDef))
                    {
                        thingGroup.mainThingDef = thingDef;
                    }
                    var name = thingDef.defName + " - " + thingDef.LabelCap + " [" + (thingDef.modContentPack?.Name ?? "RD.UnknownMod".Translate()) + "]";
                    var labelRect2 = new Rect(innerPos.x + 30, innerPos.y, Text.CalcSize(name).x + 10, 24f);
                    Widgets.Label(labelRect2, name);
                    innerPos.y += 24;
                    outerPos.y += 24;
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

            Widgets.EndScrollView();
            base.Write();
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
                    num += 24f;
                }
            }
            return num;
        }
        private static Vector2 scrollPosition = Vector2.zero;

    }
}

