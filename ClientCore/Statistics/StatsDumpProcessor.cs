using System;
using System.Text;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Processes parsed stats.dmp data and generates human-readable summaries.
    /// </summary>
    public static class StatsDumpProcessor
    {
        public static void Process(string dumpPath, MatchStatistics stats, ClientConfiguration config)
        {
            if (!config.EnableStatsDumpParser)
                return;

            try
            {
                var parser = new StatsDumpParser(dumpPath);
                StatsDumpData data = parser.Data;

                // Update average FPS if available
                if (stats.AverageFPS == 0 && data.AverageFPS > 0)
                {
                    stats.AverageFPS = data.AverageFPS;
                }

                // Build human-readable summary
                var sb = new StringBuilder();

                // Game-level information
                sb.AppendLine("=== GAME STATISTICS ===");
                if (!string.IsNullOrEmpty(data.MapName))
                    sb.AppendLine($"Map: {data.MapName}");
                if (data.GameDuration > 0)
                    sb.AppendLine($"Duration: {data.GameDuration} seconds");
                if (data.GameSpeed > 0)
                    sb.AppendLine($"Game Speed: {data.GameSpeed}");
                if (data.TechLevel > 0)
                    sb.AppendLine($"Tech Level: {data.TechLevel}");
                if (data.StartingCredits > 0)
                    sb.AppendLine($"Starting Credits: {data.StartingCredits}");
                sb.AppendLine($"Bases: {(data.BasesEnabled != 0 ? "ON" : "OFF")}");
                sb.AppendLine($"Ore Regeneration: {(data.OreRegenerates != 0 ? "ON" : "OFF")}");
                sb.AppendLine($"Crates: {(data.CratesEnabled != 0 ? "ON" : "OFF")}");
                if (data.AverageFPS > 0)
                    sb.AppendLine($"Average FPS: {data.AverageFPS}");

                sb.AppendLine();
                sb.AppendLine("=== PLAYER STATISTICS ===");

                // Per-player table
                for (int i = 0; i < 8; i++)
                {
                    if (data.PlayerNames[i] != null && !string.IsNullOrEmpty(data.PlayerNames[i]))
                    {
                        sb.AppendLine($"\nPlayer {i + 1}: {data.PlayerNames[i]}");
                        if (!string.IsNullOrEmpty(data.PlayerSides[i]))
                            sb.AppendLine($"  Side: {data.PlayerSides[i]}");

                        // Player state info
                        if (data.PlayerSpectatorStates[i] > 0)
                            sb.AppendLine($"  Status: Spectator");
                        else if (data.PlayerDeadStates[i] > 0)
                            sb.AppendLine($"  Status: Dead");
                        else if (data.PlayerResigned[i] > 0)
                            sb.AppendLine($"  Status: Resigned");
                        else if (data.PlayerConnectionLost[i] > 0)
                            sb.AppendLine($"  Status: Connection Lost");
                        else if (data.PlayerQuitStates[i] > 0)
                            sb.AppendLine($"  Status: Quit");

                        // Economic info
                        if (data.PlayerMoneyHarvested[i] >= 0)
                            sb.AppendLine($"  Money Harvested: {data.PlayerMoneyHarvested[i]}");
                        if (data.PlayerCredits[i] >= 0)
                            sb.AppendLine($"  Remaining Credits: {data.PlayerCredits[i]}");

                        // Unit statistics
                        int totalVehiclesKilled = SumVehicles(data.PlayerVehiclesKilled[i]);
                        int totalInfantryKilled = SumInfantry(data.PlayerInfantryKilled[i]);
                        int totalPlanesKilled = SumPlanes(data.PlayerPlanesKilled[i]);
                        int totalVesselsKilled = SumVessels(data.PlayerVesselsKilled[i]);
                        int totalBuildingsKilled = SumBuildings(data.PlayerBuildingsKilled[i]);

                        int totalUnitsKilled = totalVehiclesKilled + totalInfantryKilled + totalPlanesKilled + totalVesselsKilled + totalBuildingsKilled;

                        if (totalUnitsKilled > 0)
                        {
                            sb.AppendLine($"  Total Units Killed: {totalUnitsKilled}");
                            if (totalVehiclesKilled > 0)
                                sb.AppendLine($"    Vehicles: {totalVehiclesKilled}");
                            if (totalInfantryKilled > 0)
                                sb.AppendLine($"    Infantry: {totalInfantryKilled}");
                            if (totalPlanesKilled > 0)
                                sb.AppendLine($"    Aircraft: {totalPlanesKilled}");
                            if (totalVesselsKilled > 0)
                                sb.AppendLine($"    Vessels: {totalVesselsKilled}");
                            if (totalBuildingsKilled > 0)
                                sb.AppendLine($"    Buildings: {totalBuildingsKilled}");
                        }

                        // Crates info
                        int totalCrates = data.PlayerCratesCollected[i].Heal + data.PlayerCratesCollected[i].Cloak +
                                         data.PlayerCratesCollected[i].Armor + data.PlayerCratesCollected[i].Speed +
                                         data.PlayerCratesCollected[i].FirePower + data.PlayerCratesCollected[i].Money;
                        if (totalCrates > 0)
                        {
                            sb.AppendLine($"  Total Crates: {totalCrates}");
                        }
                    }
                }

                stats.DmpSummary = sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing stats dump: {ex.Message}");
            }
        }

        private static int SumVehicles(VehiclesData data)
        {
            return data.MediumTank + data.HarvesterUnit + data.APCUnit + data.Ranger +
                   data.ReconBike + data.FlameThrower + data.Orca + data.Chinook;
        }

        private static int SumInfantry(InfantryData data)
        {
            return data.Soldier + data.Ranger + data.CivilianMale + data.CivilianFemale +
                   data.Engineer + data.Commando + data.FlameThrower + data.Medic +
                   data.GeneralUnit + data.WestSoldier;
        }

        private static int SumPlanes(PlanesData data)
        {
            return data.A10Tank + data.Orca + data.Chinook;
        }

        private static int SumVessels(VesselsData data)
        {
            return data.Gunboat + data.LST + data.Destroyer;
        }

        private static int SumBuildings(BuildingsData data)
        {
            return data.PowerPlant + data.Barracks + data.Refinery + data.Radar +
                   data.GATurret + data.NSTurret + data.Fence + data.AirStrip +
                   data.Shipyard + data.SubPen + data.Sandbag + data.ConcretWall;
        }
    }
}