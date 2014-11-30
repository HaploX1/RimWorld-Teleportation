using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using VerseBase;
using Verse;
using Verse.AI;
//using Verse.Sound;
using RimWorld;
//using RimWorld.Planet;
//using RimWorld.SquadAI;


namespace ModCommon
{
    /// <summary>
    /// Various helping functions
    /// </summary>
    /// <author>Haplo</author>
    /// <permission>Please check the provided license info for granted permissions.</permission>
    class Helper
    {
        /// <summary>
        /// Find the thing, that is nearest to the position
        /// </summary>
        /// <param name="things"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static Thing FindNearestThing(IEnumerable<Thing> things, IntVec3 pos)
        {
            return FindNearestThing(new List<Thing>(things), pos);
        }
        /// <summary>
        /// Find the thing, that is nearest to the position
        /// </summary>
        /// <param name="things"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static Thing FindNearestThing(List<Thing> things, IntVec3 pos)
        {
            double nearestDistance = 99999.0d;
            Thing foundThing = null;

            foreach (Thing t in things)
            {
                double dist = GetDistance(t.Position, pos);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    foundThing = t;
                }
            }

            return foundThing;
        }


        /// <summary>
        /// Find Pawns next or at the provided position
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public static IEnumerable<Pawn> FindAllAdjacentPawnsToPosition(IntVec3 pos)
        {
            IEnumerable<IntVec3> positions = GenAdj.AdjacentCellsCardinal_InBounds(pos);
            IEnumerable<Pawn> pawns = Find.ListerPawns.AllPawns;

            foreach (IntVec3 position1 in positions)
            {
                foreach (Pawn pawn1 in pawns)
                {
                    if (pawn1.Position == position1 || pawn1.Position == pos)
                        yield return pawn1;
                }
            }
        }



        /// <summary>
        /// Get the distance between two points
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static double GetDistance(IntVec3 p1, IntVec3 p2)
        {
            int X = Math.Abs(p1.x - p2.x);
            int Y = Math.Abs(p1.y - p2.y);
            int Z = Math.Abs(p1.z - p2.z);

            return Math.Sqrt(X * X + Y * Y + Z * Z);

        }


        /// <summary>
        /// Get the difference in the position of two objects
        /// </summary>
        /// <param name="sourcePos"></param>
        /// <param name="targetPos"></param>
        /// <returns></returns>
        public static IntVec3 GetPositionDifference(IntVec3 sourcePos, IntVec3 targetPos)
        {
            return sourcePos - targetPos;
        }

    }
}
