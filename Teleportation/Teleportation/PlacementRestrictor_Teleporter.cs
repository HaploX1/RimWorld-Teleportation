using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine; // Always needed
using RimWorld; // Needed
using Verse; // Needed
using Verse.AI; // Needed when you do something with the AI
//using Verse.Sound; // Needed when you do something with the Sound


namespace Teleportation
{
    class PlaceWorker_Teleporter : PlaceWorker
    {
        private string txtErrorMessage = "Teleportation_PlaceWorker_MaxCountOfTeleportersReached";

        private const string defNameOfTeleporter = "TeleportationStation";

        public override AcceptanceReport AllowsPlacing(EntityDef def, IntVec3 center, IntRot rot)
        {
            
            AcceptanceReport acceptanceReport;

            //IEnumerable<Building_Teleporter> foundBuildings = Find.ListerBuildings.AllBuildingsColonistOfClass<Building_Teleporter>();

            IEnumerable<Thing> allThingsHelper = Find.ListerThings.AllThings;
            List<Thing> allThings = new List<Thing>();
            allThings.AddRange(allThingsHelper);

            int validThings = 0;

            for (int i = 0; i < allThings.Count; i++)
            {
                Thing t = allThings[i];
                if (t.def.defName == defNameOfTeleporter || t.def.defName == defNameOfTeleporter + "_Blueprint" || t.def.defName == defNameOfTeleporter + "_Frame")
                {
                    validThings++;
                }
            }

            //foreach(Thing t in allThings)
            //{
            //    if (t.def.defName == defNameOfTeleporter || t.def.defName == defNameOfTeleporter + "_Blueprint" || t.def.defName == defNameOfTeleporter + "_Frame")
            //    {
            //        validThings++;
            //    }
            //}

                // count of found buildings 0 or 1 => ok / 2 or more => not ok
                if ((validThings >= 0) && (validThings < 2))
                {
                    acceptanceReport = true;
                    return acceptanceReport;
                }

            return txtErrorMessage.Translate();
        }
    }
}
