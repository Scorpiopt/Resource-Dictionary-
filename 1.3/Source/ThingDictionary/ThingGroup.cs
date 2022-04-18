using System.Collections.Generic;
using Verse;

namespace Deduplicator
{
    public class ThingGroup
    {
        public ThingGroup(string stuffKey)
        {
            this.thingDefs = new List<ThingDef>();
            this.thingKey = stuffKey;
        }
        public ThingDef mainThingDef;
        public List<ThingDef> thingDefs;
        public string thingKey;
    }
}
