using System;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Container for all parsed data from a stats.dmp file.
    /// Ported from the standalone RA1 stats dump parser.
    /// </summary>
    public class StatsDumpData
    {
        // Scalar game-level fields
        public int SDFX { get; set; }
        public int GameNumber { get; set; }
        public int NumberOfPlayers { get; set; }
        public int NumberOfRemainingPlayers { get; set; }
        public int IsTournamentGame { get; set; }
        public int StartingCredits { get; set; }
        public int BasesEnabled { get; set; }
        public int OreRegenerates { get; set; }
        public int CratesEnabled { get; set; }
        public int NumberOfAIPlayers { get; set; }
        public int ShroudRegrows { get; set; }
        public int CTFEnabled { get; set; }
        public int StartingUnits { get; set; }
        public int TechLevel { get; set; }
        public string MapName { get; set; }
        public string IPAddress1 { get; set; }
        public string IPAddress2 { get; set; }
        public string Ping { get; set; }
        public int CompletionType { get; set; }
        public int GameDuration { get; set; }
        public int StartTime { get; set; }
        public int ProcessorType { get; set; }
        public int AverageFPS { get; set; }
        public uint SystemMemory { get; set; }
        public uint VideoMemory { get; set; }
        public int GameSpeed { get; set; }
        public string Version { get; set; }
        public DateTime? GameExeLastWriteTimeUTC { get; set; }

        // Per-player arrays (size 8)
        public int[] PlayerMoneyHarvested { get; set; }
        public int[] PlayerCredits { get; set; }
        public int[] PlayerQuitStates { get; set; }
        public int[] PlayerColors { get; set; }
        public int[] PlayerAlliancesBitFields { get; set; }
        public int[] PlayerSpectatorStates { get; set; }
        public int[] PlayerDeadStates { get; set; }
        public int[] PlayerSpawnLocations { get; set; }
        public int[] PlayerConnectionLost { get; set; }
        public int[] PlayerResigned { get; set; }
        public string[] PlayerNames { get; set; }
        public string[] PlayerSides { get; set; }

        // Per-player struct arrays (size 8)
        public CratesData[] PlayerCratesCollected { get; set; }
        public VehiclesData[] PlayerVehiclesKilled { get; set; }
        public VehiclesData[] PlayerVehiclesBought { get; set; }
        public VehiclesData[] PlayerVehiclesLeft { get; set; }
        public InfantryData[] PlayerInfantryKilled { get; set; }
        public InfantryData[] PlayerInfantryBought { get; set; }
        public InfantryData[] PlayerInfantryLeft { get; set; }
        public PlanesData[] PlayerPlanesKilled { get; set; }
        public PlanesData[] PlayerPlanesBought { get; set; }
        public PlanesData[] PlayerPlanesLeft { get; set; }
        public VesselsData[] PlayerVesselsKilled { get; set; }
        public VesselsData[] PlayerVesselsBought { get; set; }
        public VesselsData[] PlayerVesselsLeft { get; set; }
        public BuildingsData[] PlayerBuildingsKilled { get; set; }
        public BuildingsData[] PlayerBuildingsBought { get; set; }
        public BuildingsData[] PlayerBuildingsLeft { get; set; }
        public BuildingsData[] PlayerBuildingsCaptured { get; set; }

        public StatsDumpData()
        {
            // Initialize int arrays to -1
            PlayerMoneyHarvested = new int[8];
            PlayerCredits = new int[8];
            PlayerQuitStates = new int[8];
            PlayerColors = new int[8];
            PlayerAlliancesBitFields = new int[8];
            PlayerSpectatorStates = new int[8];
            PlayerDeadStates = new int[8];
            PlayerSpawnLocations = new int[8];
            PlayerConnectionLost = new int[8];
            PlayerResigned = new int[8];

            for (int i = 0; i < 8; i++)
            {
                PlayerMoneyHarvested[i] = -1;
                PlayerCredits[i] = -1;
                PlayerQuitStates[i] = -1;
                PlayerColors[i] = -1;
                PlayerAlliancesBitFields[i] = -1;
                PlayerSpectatorStates[i] = -1;
                PlayerDeadStates[i] = -1;
                PlayerSpawnLocations[i] = -1;
                PlayerConnectionLost[i] = -1;
                PlayerResigned[i] = -1;
            }

            // Initialize string arrays to null
            PlayerNames = new string[8];
            PlayerSides = new string[8];

            // Initialize struct arrays
            PlayerCratesCollected = new CratesData[8];
            PlayerVehiclesKilled = new VehiclesData[8];
            PlayerVehiclesBought = new VehiclesData[8];
            PlayerVehiclesLeft = new VehiclesData[8];
            PlayerInfantryKilled = new InfantryData[8];
            PlayerInfantryBought = new InfantryData[8];
            PlayerInfantryLeft = new InfantryData[8];
            PlayerPlanesKilled = new PlanesData[8];
            PlayerPlanesBought = new PlanesData[8];
            PlayerPlanesLeft = new PlanesData[8];
            PlayerVesselsKilled = new VesselsData[8];
            PlayerVesselsBought = new VesselsData[8];
            PlayerVesselsLeft = new VesselsData[8];
            PlayerBuildingsKilled = new BuildingsData[8];
            PlayerBuildingsBought = new BuildingsData[8];
            PlayerBuildingsLeft = new BuildingsData[8];
            PlayerBuildingsCaptured = new BuildingsData[8];

            for (int i = 0; i < 8; i++)
            {
                PlayerCratesCollected[i] = new CratesData();
                PlayerVehiclesKilled[i] = new VehiclesData();
                PlayerVehiclesBought[i] = new VehiclesData();
                PlayerVehiclesLeft[i] = new VehiclesData();
                PlayerInfantryKilled[i] = new InfantryData();
                PlayerInfantryBought[i] = new InfantryData();
                PlayerInfantryLeft[i] = new InfantryData();
                PlayerPlanesKilled[i] = new PlanesData();
                PlayerPlanesBought[i] = new PlanesData();
                PlayerPlanesLeft[i] = new PlanesData();
                PlayerVesselsKilled[i] = new VesselsData();
                PlayerVesselsBought[i] = new VesselsData();
                PlayerVesselsLeft[i] = new VesselsData();
                PlayerBuildingsKilled[i] = new BuildingsData();
                PlayerBuildingsBought[i] = new BuildingsData();
                PlayerBuildingsLeft[i] = new BuildingsData();
                PlayerBuildingsCaptured[i] = new BuildingsData();
            }
        }
    }

    public class CratesData
    {
        public int Heal { get; set; }
        public int Cloak { get; set; }
        public int Armor { get; set; }
        public int Speed { get; set; }
        public int FirePower { get; set; }
        public int Money { get; set; }
    }

    public class VehiclesData
    {
        public int MediumTank { get; set; }
        public int HarvesterUnit { get; set; }
        public int APCUnit { get; set; }
        public int Ranger { get; set; }
        public int ReconBike { get; set; }
        public int FlameThrower { get; set; }
        public int Orca { get; set; }
        public int Chinook { get; set; }
    }

    public class InfantryData
    {
        public int Soldier { get; set; }
        public int Ranger { get; set; }
        public int CivilianMale { get; set; }
        public int CivilianFemale { get; set; }
        public int Engineer { get; set; }
        public int Commando { get; set; }
        public int FlameThrower { get; set; }
        public int Medic { get; set; }
        public int GeneralUnit { get; set; }
        public int WestSoldier { get; set; }
    }

    public class PlanesData
    {
        public int A10Tank { get; set; }
        public int Orca { get; set; }
        public int Chinook { get; set; }
    }

    public class VesselsData
    {
        public int Gunboat { get; set; }
        public int LST { get; set; }
        public int Destroyer { get; set; }
    }

    public class BuildingsData
    {
        public int PowerPlant { get; set; }
        public int Barracks { get; set; }
        public int Refinery { get; set; }
        public int Radar { get; set; }
        public int GATurret { get; set; }
        public int NSTurret { get; set; }
        public int Fence { get; set; }
        public int AirStrip { get; set; }
        public int Shipyard { get; set; }
        public int SubPen { get; set; }
        public int Sandbag { get; set; }
        public int ConcretWall { get; set; }
    }
}