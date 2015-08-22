using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
//using Verse.Sound;
using RimWorld;
//using RimWorld.Planet;
//using RimWorld.SquadAI;

using ModCommon;

namespace Teleportation
{
    public class Building_Teleporter : Building, ISlotGroupParent
    {
        #region Variables

        // These variables are needed to setup the storage field
        public SlotGroup slotGroup;
        public StorageSettings settingsStorage;
        private List<IntVec3> cachedOccupiedCells;

        // UI graphics
        public static string UI_SendingPath = "UI/Commands/Transporter/UI_ButtonSending";
        public static string UI_ReceivingPath = "UI/Commands/Transporter/UI_ButtonReceiving";
        public static Texture2D UI_ButtonSending;
        public static Texture2D UI_ButtonReceiving;

        private string txtPortalState = "Teleporter_PortalState";
        private string txtPortalState_send = "Teleporter_PortalState_Send";
        private string txtPortalState_receive = "Teleporter_PortalState_Receive";
        private string txtPortalState_off = "Teleporter_PortalState_Off";

        private string txtCountdownTeleporting = "Teleporter_Countdown";
        private string txtNoTeleporterConnected = "Teleporter_NoTeleporterConnected";
        private string txtSwitchStateToSending = "Teleporter_ButtonSwitchToSending";
        private string txtSwitchStateToReceiving = "Teleporter_ButtonSwitchToReceiving";


        // state of the teleporter
        public enum TeleporterState
        {
            off,
            send,
            receive
        }
        private TeleporterState state;
        public TeleporterState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
            }
        }

        private Building_Teleporter connectedTeleporter = null;

        private bool countdownTeleportingActive;
        private int countdownTeleporting;
        private const int countdownTeleportingMax = 600;

        private int counterNextCheck;
        private const int counterNextCheckMax = 60;

        private CompPowerTrader powerComp;

        // Is this powered?
        public bool PowerOk
        {
            get
            {
                if (powerComp != null)
                    return powerComp.PowerOn;
                else
                    return true;
            }
        }

        public Building_Teleporter ConnectedTeleporter
        {
            get
            {
                return connectedTeleporter;
            }
        }

        public bool StorageTabVisible
        {
            get
            {
                return true;
            }
        }

        public void TrySetConnectedTeleporter(Building_Teleporter teleporter)
        {
            if (teleporter != null && teleporter != this)
                this.connectedTeleporter = teleporter;
        }

        #endregion



        #region Create / Destroy

        /// <summary>
        /// Do something after the object is initialized, but before it is spawned
        /// </summary>
        public override void PostMake()
        {
            base.PostMake();

            settingsStorage = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
            {
                settingsStorage.CopyFrom(def.building.defaultStorageSettings);
            }

        }

        /// <summary>
        /// This is called when the building is spawned into the world
        /// </summary>
        public override void SpawnSetup()
        {
            base.SpawnSetup();

            powerComp = GetComp<CompPowerTrader>();

            // Prepare storage info data
            cachedOccupiedCells = AllSlotCells().ToList(); // new List<IntVec3>(AllSlotCells());
            //cachedOccupiedCells.Add(Position);

            // Create the new collection position (storage zone)
            //slotGroup = new SlotGroup(this);

            // Load graphics for the UI elements
            UI_ButtonSending = ContentFinder<Texture2D>.Get(UI_SendingPath, true);
            UI_ButtonReceiving = ContentFinder<Texture2D>.Get(UI_ReceivingPath, true);
        }


        /// <summary>
        /// To write and read data (savegame)
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Save and load the storage settings
            Scribe_Deep.LookDeep<StorageSettings>(ref settingsStorage, "settingsStorage", this);
            Scribe_Values.LookValue<TeleporterState>(ref state, "state", TeleporterState.off);
            Scribe_Values.LookValue<int>(ref counterNextCheck, "counterChecking", 60);
        }


        /// <summary>
        /// Destroy and do cleanup
        /// </summary>
        /// <param name="mode"></param>
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            //Check if the slotGroup is active >> deregister it
            if (slotGroup != null)
            {
                slotGroup.Notify_ParentDestroying();
            }

            base.Destroy(mode);
        }

        #endregion


        #region Ticks

        /// <summary>
        /// This is used, when the Ticker is changed from Normal to Rare
        /// This is a tick thats done once every 250Ticks
        /// </summary>
        public override void TickRare()
        {
            base.TickRare();

            DoTickWork(250);
        }

        /// <summary>
        /// This is used, when the Ticker is Normal
        /// This is a tick thats done 60 times per second
        /// </summary>
        public override void Tick()
        {
            base.Tick();

            DoTickWork(1);
        }

        // Do ticker work
        private void DoTickWork(int ticks)
        {
            // not off, not connected => try to find other teleporter 
            if (ConnectedTeleporter == null)
            {
                TrySetConnectedTeleporter( TryFindOtherTeleporter() );

                // nothing found, set to off
                if (ConnectedTeleporter == null)
                    TrySwitchStateToOff();
            }

            if (!PowerOk)
            {
                TrySwitchStateToOff();
                return;
            }

            // State: not sending => Do nothing more
            if (State != TeleporterState.send)
            {
                if (counterNextCheck != counterNextCheckMax)
                    counterNextCheck = counterNextCheckMax;

                return;
            }

            // Send items only after x ticks
            if (countdownTeleportingActive && countdownTeleporting > 0)
            {
                countdownTeleporting -= ticks;
                return;
            }

            // Check for new items only every x ticks
            if (!countdownTeleportingActive && counterNextCheck > 0)
            {
                counterNextCheck -= ticks;
                return;
            }
            else
            {
                counterNextCheck = counterNextCheckMax;
            }

            // State connected building: not receiving
            Building_Teleporter target = ConnectedTeleporter;
            if (target == null || target.State != TeleporterState.receive)
                return;

            // Check if there is an item on my slots, send if possible to target
            List<IntVec3> allCells = AllSlotCellsList();
            List<IntVec3> targetCells = target.AllSlotCellsList();

            for (int i = 0; i < allCells.Count; i++)
            {
                IntVec3 sourceCell = allCells[i];
                IntVec3 targetCell = targetCells[i];


                // Check if resource is here and check if no resource is at target position
                if (Find.ThingGrid.CellContains(sourceCell, ThingCategory.Item))
                {
                    // Check if target cell is empty
                    if (Find.ThingGrid.CellContains(targetCell, ThingCategory.Item))
                        continue;

                    // Start sending countdown
                    if (!countdownTeleportingActive)
                    {
                        countdownTeleporting = countdownTeleportingMax;
                        countdownTeleportingActive = true;
                        break;
                    }
                    else
                    {

                        // Send to target position
                        IEnumerable<Thing> sourceCellThings = Find.ThingGrid.ThingsAt(sourceCell);
                        foreach (Thing t in sourceCellThings)
                        {
                            if (t.def.category == ThingCategory.Item)
                            {
                                // Create identical item at target
                                Thing targetThing = ThingMaker.MakeThing(t.def, t.Stuff);
                                targetThing = GenSpawn.Spawn(targetThing, targetCell);
                                targetThing.stackCount = t.stackCount;

                                // Destroy source
                                t.Destroy(DestroyMode.Vanish);
                                break;
                            }
                        }
                    }
                }
            }

            if (countdownTeleportingActive && countdownTeleporting <= 0)
                countdownTeleportingActive = false;

        }

        #endregion


        #region GUI

        /// <summary>
        /// This string will be shown when the object is selected (focus)
        /// </summary>
        /// <returns></returns>
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());

            stringBuilder.AppendLine();
            stringBuilder.Append(txtPortalState.Translate() + " ");

            if (State == TeleporterState.off)
                stringBuilder.Append(txtPortalState_off.Translate());
            if (State == TeleporterState.receive)
                stringBuilder.Append(txtPortalState_receive.Translate());
            if (State == TeleporterState.send)
                stringBuilder.Append(txtPortalState_send.Translate());

            // DEBUG ONLY
            //if (State == TeleporterState.send)
            //{
            //    stringBuilder.AppendLine();
            //    stringBuilder.Append("Next check in: " + counterNextCheck.ToString());
            //}

            if (State == TeleporterState.send)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append(txtCountdownTeleporting.Translate() + " " + GenDate.TickstoSecondsString(countdownTeleporting));
            }

            return stringBuilder.ToString();
        }


        /// <summary>
        /// This creates new selection buttons with a new graphic
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo cbase in base.GetGizmos())
                yield return cbase;

            if (!PowerOk)
                yield break;

            int groupBase = 313676300;

            // Key-Binding F - Start Production
            Command_Action optF;
            optF = new Command_Action();
            optF.disabled = ConnectedTeleporter == null;
            optF.disabledReason = txtNoTeleporterConnected.Translate();
            if (State == TeleporterState.off || State == TeleporterState.receive)
            {
                optF.defaultDesc = txtSwitchStateToSending.Translate();
                optF.icon = UI_ButtonReceiving;
            }
            else
            {
                optF.defaultDesc = txtSwitchStateToReceiving.Translate();
                optF.icon = UI_ButtonSending;
            }
            optF.hotKey = KeyBindingDefOf.Misc1; //KeyCode.F;
            optF.activateSound = SoundDef.Named("Click");
            optF.action = ButtonSwitchState;
            optF.groupKey = groupBase + 1;

            yield return optF;

        }

        #endregion


        #region Storage Settings

        /// <summary>
        /// Don't know what this does...
        /// </summary>
        /// <returns></returns>
        public string SlotYielderLabel()
        {
            return this.Label;
        }

        /// <summary>
        /// Base storage settings (from xml)
        /// </summary>
        /// <returns></returns>
        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        /// <summary>
        /// Active storage settings (from xml or base)
        /// </summary>
        /// <returns></returns>
        public StorageSettings GetStoreSettings()
        {
            return settingsStorage;
        }

        /// <summary>
        /// Returns the occupied slot list
        /// </summary>
        /// <returns></returns>
        public List<IntVec3> AllSlotCellsList()
        {
            if (this.cachedOccupiedCells == null)
            {
                this.cachedOccupiedCells = this.AllSlotCells().ToList<IntVec3>();
            }
            return this.cachedOccupiedCells;
        }

        /// <summary>
        /// Fill resources position == my position
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IntVec3> AllSlotCells()
        {
            return GenAdj.CellsOccupiedBy(Position, Rotation, def.size);
        }

        /// <summary>
        /// Returns the slotgroup
        /// </summary>
        /// <returns></returns>
        public SlotGroup GetSlotGroup()
        {
            return this.slotGroup;
        }

        /// <summary>
        /// Not used
        /// </summary>
        /// <param name="newItem"></param>
        public void Notify_LostThing(Thing newItem)
        {
        }

        /// <summary>
        /// I received something => add stackCount to wood and destroy it
        /// </summary>
        /// <param name="newItem"></param>
        public void Notify_ReceivedThing(Thing newItem)
        {

        }

        #endregion



        #region Functions

        private void ButtonSwitchState()
        {
            switch (State) 
            {
                case TeleporterState.off:
                    TrySwitchStateToReceive();
                    break;
                case TeleporterState.receive:
                    TrySwitchStateToSend();
                    break;
                case TeleporterState.send:
                    TrySwitchStateToReceive();
                    break;
                default:
                    TrySwitchStateToOff();
                    break;
            }
        }

        public void TrySwitchStateToReceive()
        {
            if (State != TeleporterState.receive)
                State = TeleporterState.receive;

            if (connectedTeleporter != null)
            {
                if (connectedTeleporter.ConnectedTeleporter != this)
                    connectedTeleporter.TrySetConnectedTeleporter(this);
            }

            if (slotGroup != null)
            {
                slotGroup.Notify_ParentDestroying();
                slotGroup = null;
            }
        }

        public void TrySwitchStateToSend()
        {
            if (State != TeleporterState.send && connectedTeleporter != null)
            {
                if (connectedTeleporter.ConnectedTeleporter != this)
                    connectedTeleporter.TrySetConnectedTeleporter(this);

                connectedTeleporter.TrySwitchStateToReceive();

                if (connectedTeleporter.State == TeleporterState.receive)
                    State = TeleporterState.send;
                else
                    TrySwitchStateToReceive();
            }

            if (State == TeleporterState.send)
            {
                // Create storage field
                if (slotGroup == null)
                {
                    if (cachedOccupiedCells == null || cachedOccupiedCells.Count == 0)
                        cachedOccupiedCells = AllSlotCells().ToList();

                    slotGroup = new SlotGroup(this);
                }
            }
        }

        public void TrySwitchStateToOff(bool switchOtherTeleporter = true)
        {
            if (State != TeleporterState.off)
                State = TeleporterState.off;

            if (!switchOtherTeleporter)
                return;

            if (connectedTeleporter != null && connectedTeleporter.ConnectedTeleporter == this && connectedTeleporter.State != TeleporterState.off)
                connectedTeleporter.TrySwitchStateToOff(false);

            if (slotGroup != null)
            {
                slotGroup.Notify_ParentDestroying();
                slotGroup = null;
            }
        }

        private Building_Teleporter TryFindOtherTeleporter()
        {
            // Find all teleporter
            IEnumerable<Building_Teleporter> buildings = Find.ListerBuildings.AllBuildingsColonistOfClass<Building_Teleporter>();
            if (buildings == null || buildings.Count() == 0)
            {
                return null;
            }

            // remove this teleporter
            IEnumerable<Building_Teleporter> buildings1 = buildings.Where(b => b != this);
            if (buildings1 == null || buildings1.Count() == 0)
            {
                return null;
            }

            Building_Teleporter building = buildings.RandomElement();
            return building;
        }


        #endregion


    }
}
