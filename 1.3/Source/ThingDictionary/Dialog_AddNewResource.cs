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
using Verse.Sound;

namespace Deduplicator
{
    public class Dialog_AddNewThing : Window
    {
		private Vector2 scrollPosition;
		public override Vector2 InitialSize => new Vector2(620f, 500f);

		public List<ThingDef> stuffThingDefs;

		public ThingGroup thingGroup;
		public Dialog_AddNewThing(ThingGroup thingGroup)
		{
			doCloseButton = true;
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
			this.thingGroup = thingGroup;
			stuffThingDefs = DefDatabase<ThingDef>.AllDefs.Where(x => x.IsStuff && !thingGroup.thingDefs.Contains(x)).ToList();
		}

		string searchKey;
		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Small;

			Text.Anchor = TextAnchor.MiddleLeft;
			var searchLabel = new Rect(inRect.x, inRect.y, 60, 24);
			Widgets.Label(searchLabel, "RD.Search".Translate());
			var searchRect = new Rect(searchLabel.xMax + 5, searchLabel.y, 200, 24f);
			searchKey = Widgets.TextField(searchRect, searchKey);
			Text.Anchor = TextAnchor.UpperLeft;

			Rect outRect = new Rect(inRect);
			outRect.y = searchRect.yMax + 5;
			outRect.yMax -= 70f;
			outRect.width -= 16f;

			var thingDefs = searchKey.NullOrEmpty() ? stuffThingDefs.ToList() : stuffThingDefs.Where(x => x.label.ToLower().Contains(searchKey.ToLower())).ToList();

			Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, (float)thingDefs.Count() * 35f);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			try
			{
				float num = 0f;
				foreach (ThingDef thingDef in thingDefs)
				{
					Rect rect = new Rect(0f, num, viewRect.width * 0.7f, 32f);
					Widgets.Label(rect, thingDef.defName + " - " + thingDef.label);
					rect.x = rect.xMax;
					rect.width = viewRect.width * 0.3f;
					if (Widgets.ButtonText(rect, "RD.Add".Translate()))
					{
						thingGroup.thingDefs.Add(thingDef);
						if (!ThingDictionaryMod.settings.thingSettings.TryGetValue(thingGroup.thingKey, out var resourceGroup))
                        {
							ThingDictionaryMod.settings.thingSettings[thingGroup.thingKey] = resourceGroup = new ThingGroupExposable();
						}
						resourceGroup.thingDefs.Add(thingDef.defName);
						SoundDefOf.Click.PlayOneShotOnCamera();
						this.Close();
					}
					num += 35f;
				}
			}
			finally
			{
				Widgets.EndScrollView();
			}
		}
	}
}

