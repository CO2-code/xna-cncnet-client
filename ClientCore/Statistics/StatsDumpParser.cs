using System;
using System.IO;
using System.Text;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Parses RA1 stats.dmp binary files into structured data.
    /// Ported from the standalone RA1 stats dump parser.
    /// </summary>
    public class StatsDumpParser
    {
        private BigEndianReader _bin;
        private int _pos;
        private int _dumpSize;
        private int _quitPlayerNumHelper = -1;

        public StatsDumpData Data { get; private set; }

        public StatsDumpParser(string fileName)
        {
            Data = new StatsDumpData();

            try
            {
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    _bin = new BigEndianReader(br);
                    _dumpSize = (int)fs.Length;
                    _pos = 0;

                    // Read header: 2-byte ReportedSize (reserved), 2-byte padding
                    _bin.ReadInt16();
                    _pos += 2;
                    _bin.ReadInt16();
                    _pos += 2;

                    // Main parsing loop
                    while (_pos < _dumpSize)
                    {
                        byte[] idBytes = _bin.ReadBytes(4);
                        _pos += 4;
                        string id = Encoding.ASCII.GetString(idBytes);

                        // Route by record ID
                        switch (id)
                        {
                            case "NAME":
                                ParseNameInfo();
                                break;
                            case "SIDE":
                                ParseSideInfo();
                                break;
                            case "COLR":
                                ParseColorInfo();
                                break;
                            case "ALLY":
                                ParseAlliancesInfo();
                                break;
                            case "CRAT":
                                ParseCratesCollectedInfo();
                                break;
                            case "SCEN":
                                ParseScenarioInfo();
                                break;
                            case "QUIT":
                                ParseQuitState();
                                break;
                            case "CONN":
                                ParseConnectionLostInfo();
                                break;
                            case "RESO":
                                ParseResignedInfo();
                                break;
                            case "SPEC":
                                ParseSpectatorStateInfo();
                                break;
                            case "DEAD":
                                ParseDeadStateInfo();
                                break;
                            case "LOCA":
                                ParseSpawnLocationInfo();
                                break;
                            case "VHCL":
                                ParseVehiclesStuff();
                                break;
                            case "INFN":
                                ParseInfantryStuff();
                                break;
                            case "AIRP":
                                ParsePlanesStuff();
                                break;
                            case "VESS":
                                ParseVesselsStuff();
                                break;
                            case "BLDG":
                                ParseBuildingsStuff();
                                break;
                            case "MONY":
                                ParseMoneyHarvestedInfo();
                                break;
                            case "CRED":
                                ParseCreditsInfo();
                                break;
                            case "DATE":
                                ParseDateInfo();
                                break;
                            case "GSPD":
                                Data.GameSpeed = ReadByte();
                                break;
                            case "NUMP":
                                Data.NumberOfPlayers = ReadByte();
                                break;
                            case "NAID":
                                Data.NumberOfAIPlayers = ReadByte();
                                break;
                            case "NREM":
                                Data.NumberOfRemainingPlayers = ReadByte();
                                break;
                            case "TCTF":
                                Data.CTFEnabled = ReadOnOrOff();
                                break;
                            case "TTRN":
                                Data.IsTournamentGame = ReadOnOrOff();
                                break;
                            case "TBASE":
                                Data.BasesEnabled = ReadOnOrOff();
                                break;
                            case "TORE":
                                Data.OreRegenerates = ReadOnOrOff();
                                break;
                            case "TCRT":
                                Data.CratesEnabled = ReadOnOrOff();
                                break;
                            case "TGROW":
                                Data.ShroudRegrows = ReadOnOrOff();
                                break;
                            case "TTECH":
                                Data.TechLevel = ReadByte();
                                break;
                            case "UNIT":
                                Data.StartingUnits = ReadByte();
                                break;
                            case "CRED_START":
                                Data.StartingCredits = Read32Bits();
                                break;
                            case "FRMS":
                                Data.GameDuration = Read32Bits();
                                break;
                            case "AFPS":
                                Data.AverageFPS = ReadByte();
                                break;
                            case "TIME":
                                Data.StartTime = Read32Bits();
                                break;
                            case "PROC":
                                Data.ProcessorType = ReadByte();
                                break;
                            case "SMEM":
                                Data.SystemMemory = ReadUnsigned32Bits();
                                break;
                            case "VMEM":
                                Data.VideoMemory = ReadUnsigned32Bits();
                                break;
                            case "VER":
                                Data.Version = ParseString();
                                break;
                            case "SDFX":
                                Data.SDFX = Read32Bits();
                                break;
                            case "GNUM":
                                Data.GameNumber = Read32Bits();
                                break;
                            case "MAP":
                                Data.MapName = ParseString();
                                break;
                            case "IP1":
                                Data.IPAddress1 = ParseString();
                                break;
                            case "IP2":
                                Data.IPAddress2 = ParseString();
                                break;
                            case "PING":
                                Data.Ping = ParseString();
                                break;
                            case "COMP":
                                Data.CompletionType = ReadByte();
                                break;
                            case "EXEDATE":
                                long ticks = Read32Bits();
                                if (ticks > 0)
                                {
                                    Data.GameExeLastWriteTimeUTC = new DateTime(ticks, DateTimeKind.Utc);
                                }
                                break;
                            default:
                                // Unknown record, skip it
                                ReadGarbage();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing stats dump: {ex.Message}");
            }
        }

        private void ParseNameInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerNames[i] = ParseShortString();
            }
        }

        private void ParseSideInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                int side = ReadByte();
                Data.PlayerSides[i] = side == 0 ? "GDI" : side == 1 ? "Nod" : "Unknown";
            }
            _quitPlayerNumHelper = ReadByte();
        }

        private void ParseColorInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerColors[i] = ReadByte();
            }
        }

        private void ParseAlliancesInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerAlliancesBitFields[i] = ReadByte();
            }
        }

        private void ParseCratesCollectedInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerCratesCollected[i].Heal = ReadByte();
                Data.PlayerCratesCollected[i].Cloak = ReadByte();
                Data.PlayerCratesCollected[i].Armor = ReadByte();
                Data.PlayerCratesCollected[i].Speed = ReadByte();
                Data.PlayerCratesCollected[i].FirePower = ReadByte();
                Data.PlayerCratesCollected[i].Money = ReadByte();
            }
        }

        private void ParseScenarioInfo()
        {
            ReadGarbage();
        }

        private void ParseQuitState()
        {
            if (_quitPlayerNumHelper >= 0 && _quitPlayerNumHelper < 8)
            {
                Data.PlayerQuitStates[_quitPlayerNumHelper] = ReadOnOrOff();
            }
            else
            {
                ReadByte();
            }
        }

        private void ParseConnectionLostInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerConnectionLost[i] = ReadOnOrOff();
            }
        }

        private void ParseResignedInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerResigned[i] = ReadOnOrOff();
            }
        }

        private void ParseSpectatorStateInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerSpectatorStates[i] = ReadOnOrOff();
            }
        }

        private void ParseDeadStateInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerDeadStates[i] = ReadOnOrOff();
            }
        }

        private void ParseSpawnLocationInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerSpawnLocations[i] = ReadByte();
            }
        }

        private void ParseVehiclesStuff()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVehiclesKilled[i].MediumTank = ReadByte();
                Data.PlayerVehiclesKilled[i].HarvesterUnit = ReadByte();
                Data.PlayerVehiclesKilled[i].APCUnit = ReadByte();
                Data.PlayerVehiclesKilled[i].Ranger = ReadByte();
                Data.PlayerVehiclesKilled[i].ReconBike = ReadByte();
                Data.PlayerVehiclesKilled[i].FlameThrower = ReadByte();
                Data.PlayerVehiclesKilled[i].Orca = ReadByte();
                Data.PlayerVehiclesKilled[i].Chinook = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVehiclesBought[i].MediumTank = ReadByte();
                Data.PlayerVehiclesBought[i].HarvesterUnit = ReadByte();
                Data.PlayerVehiclesBought[i].APCUnit = ReadByte();
                Data.PlayerVehiclesBought[i].Ranger = ReadByte();
                Data.PlayerVehiclesBought[i].ReconBike = ReadByte();
                Data.PlayerVehiclesBought[i].FlameThrower = ReadByte();
                Data.PlayerVehiclesBought[i].Orca = ReadByte();
                Data.PlayerVehiclesBought[i].Chinook = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVehiclesLeft[i].MediumTank = ReadByte();
                Data.PlayerVehiclesLeft[i].HarvesterUnit = ReadByte();
                Data.PlayerVehiclesLeft[i].APCUnit = ReadByte();
                Data.PlayerVehiclesLeft[i].Ranger = ReadByte();
                Data.PlayerVehiclesLeft[i].ReconBike = ReadByte();
                Data.PlayerVehiclesLeft[i].FlameThrower = ReadByte();
                Data.PlayerVehiclesLeft[i].Orca = ReadByte();
                Data.PlayerVehiclesLeft[i].Chinook = ReadByte();
            }
        }

        private void ParseInfantryStuff()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerInfantryKilled[i].Soldier = ReadByte();
                Data.PlayerInfantryKilled[i].Ranger = ReadByte();
                Data.PlayerInfantryKilled[i].CivilianMale = ReadByte();
                Data.PlayerInfantryKilled[i].CivilianFemale = ReadByte();
                Data.PlayerInfantryKilled[i].Engineer = ReadByte();
                Data.PlayerInfantryKilled[i].Commando = ReadByte();
                Data.PlayerInfantryKilled[i].FlameThrower = ReadByte();
                Data.PlayerInfantryKilled[i].Medic = ReadByte();
                Data.PlayerInfantryKilled[i].GeneralUnit = ReadByte();
                Data.PlayerInfantryKilled[i].WestSoldier = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerInfantryBought[i].Soldier = ReadByte();
                Data.PlayerInfantryBought[i].Ranger = ReadByte();
                Data.PlayerInfantryBought[i].CivilianMale = ReadByte();
                Data.PlayerInfantryBought[i].CivilianFemale = ReadByte();
                Data.PlayerInfantryBought[i].Engineer = ReadByte();
                Data.PlayerInfantryBought[i].Commando = ReadByte();
                Data.PlayerInfantryBought[i].FlameThrower = ReadByte();
                Data.PlayerInfantryBought[i].Medic = ReadByte();
                Data.PlayerInfantryBought[i].GeneralUnit = ReadByte();
                Data.PlayerInfantryBought[i].WestSoldier = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerInfantryLeft[i].Soldier = ReadByte();
                Data.PlayerInfantryLeft[i].Ranger = ReadByte();
                Data.PlayerInfantryLeft[i].CivilianMale = ReadByte();
                Data.PlayerInfantryLeft[i].CivilianFemale = ReadByte();
                Data.PlayerInfantryLeft[i].Engineer = ReadByte();
                Data.PlayerInfantryLeft[i].Commando = ReadByte();
                Data.PlayerInfantryLeft[i].FlameThrower = ReadByte();
                Data.PlayerInfantryLeft[i].Medic = ReadByte();
                Data.PlayerInfantryLeft[i].GeneralUnit = ReadByte();
                Data.PlayerInfantryLeft[i].WestSoldier = ReadByte();
            }
        }

        private void ParsePlanesStuff()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerPlanesKilled[i].A10Tank = ReadByte();
                Data.PlayerPlanesKilled[i].Orca = ReadByte();
                Data.PlayerPlanesKilled[i].Chinook = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerPlanesBought[i].A10Tank = ReadByte();
                Data.PlayerPlanesBought[i].Orca = ReadByte();
                Data.PlayerPlanesBought[i].Chinook = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerPlanesLeft[i].A10Tank = ReadByte();
                Data.PlayerPlanesLeft[i].Orca = ReadByte();
                Data.PlayerPlanesLeft[i].Chinook = ReadByte();
            }
        }

        private void ParseVesselsStuff()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVesselsKilled[i].Gunboat = ReadByte();
                Data.PlayerVesselsKilled[i].LST = ReadByte();
                Data.PlayerVesselsKilled[i].Destroyer = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVesselsBought[i].Gunboat = ReadByte();
                Data.PlayerVesselsBought[i].LST = ReadByte();
                Data.PlayerVesselsBought[i].Destroyer = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerVesselsLeft[i].Gunboat = ReadByte();
                Data.PlayerVesselsLeft[i].LST = ReadByte();
                Data.PlayerVesselsLeft[i].Destroyer = ReadByte();
            }
        }

        private void ParseBuildingsStuff()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerBuildingsKilled[i].PowerPlant = ReadByte();
                Data.PlayerBuildingsKilled[i].Barracks = ReadByte();
                Data.PlayerBuildingsKilled[i].Refinery = ReadByte();
                Data.PlayerBuildingsKilled[i].Radar = ReadByte();
                Data.PlayerBuildingsKilled[i].GATurret = ReadByte();
                Data.PlayerBuildingsKilled[i].NSTurret = ReadByte();
                Data.PlayerBuildingsKilled[i].Fence = ReadByte();
                Data.PlayerBuildingsKilled[i].AirStrip = ReadByte();
                Data.PlayerBuildingsKilled[i].Shipyard = ReadByte();
                Data.PlayerBuildingsKilled[i].SubPen = ReadByte();
                Data.PlayerBuildingsKilled[i].Sandbag = ReadByte();
                Data.PlayerBuildingsKilled[i].ConcretWall = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerBuildingsBought[i].PowerPlant = ReadByte();
                Data.PlayerBuildingsBought[i].Barracks = ReadByte();
                Data.PlayerBuildingsBought[i].Refinery = ReadByte();
                Data.PlayerBuildingsBought[i].Radar = ReadByte();
                Data.PlayerBuildingsBought[i].GATurret = ReadByte();
                Data.PlayerBuildingsBought[i].NSTurret = ReadByte();
                Data.PlayerBuildingsBought[i].Fence = ReadByte();
                Data.PlayerBuildingsBought[i].AirStrip = ReadByte();
                Data.PlayerBuildingsBought[i].Shipyard = ReadByte();
                Data.PlayerBuildingsBought[i].SubPen = ReadByte();
                Data.PlayerBuildingsBought[i].Sandbag = ReadByte();
                Data.PlayerBuildingsBought[i].ConcretWall = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerBuildingsLeft[i].PowerPlant = ReadByte();
                Data.PlayerBuildingsLeft[i].Barracks = ReadByte();
                Data.PlayerBuildingsLeft[i].Refinery = ReadByte();
                Data.PlayerBuildingsLeft[i].Radar = ReadByte();
                Data.PlayerBuildingsLeft[i].GATurret = ReadByte();
                Data.PlayerBuildingsLeft[i].NSTurret = ReadByte();
                Data.PlayerBuildingsLeft[i].Fence = ReadByte();
                Data.PlayerBuildingsLeft[i].AirStrip = ReadByte();
                Data.PlayerBuildingsLeft[i].Shipyard = ReadByte();
                Data.PlayerBuildingsLeft[i].SubPen = ReadByte();
                Data.PlayerBuildingsLeft[i].Sandbag = ReadByte();
                Data.PlayerBuildingsLeft[i].ConcretWall = ReadByte();
            }

            for (int i = 0; i < 8; i++)
            {
                Data.PlayerBuildingsCaptured[i].PowerPlant = ReadByte();
                Data.PlayerBuildingsCaptured[i].Barracks = ReadByte();
                Data.PlayerBuildingsCaptured[i].Refinery = ReadByte();
                Data.PlayerBuildingsCaptured[i].Radar = ReadByte();
                Data.PlayerBuildingsCaptured[i].GATurret = ReadByte();
                Data.PlayerBuildingsCaptured[i].NSTurret = ReadByte();
                Data.PlayerBuildingsCaptured[i].Fence = ReadByte();
                Data.PlayerBuildingsCaptured[i].AirStrip = ReadByte();
                Data.PlayerBuildingsCaptured[i].Shipyard = ReadByte();
                Data.PlayerBuildingsCaptured[i].SubPen = ReadByte();
                Data.PlayerBuildingsCaptured[i].Sandbag = ReadByte();
                Data.PlayerBuildingsCaptured[i].ConcretWall = ReadByte();
            }
        }

        private void ParseMoneyHarvestedInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerMoneyHarvested[i] = Read32Bits();
            }
        }

        private void ParseCreditsInfo()
        {
            for (int i = 0; i < 8; i++)
            {
                Data.PlayerCredits[i] = Read32Bits();
            }
        }

        private void ParseDateInfo()
        {
            ReadGarbage();
        }

        private void ReadGarbage()
        {
            _bin.ReadBytes(4);
            _pos += 4;
        }

        private int Read32Bits()
        {
            int value = _bin.ReadInt32();
            _pos += 4;
            return value;
        }

        private uint ReadUnsigned32Bits()
        {
            uint value = _bin.ReadUInt32();
            _pos += 4;
            return value;
        }

        private int ReadByte()
        {
            int value = _bin.ReadByteAs4();
            _pos += 4;
            return value;
        }

        private int ReadOnOrOff()
        {
            int value = ReadByte();
            return value != 0 ? 1 : 0;
        }

        private string ParseString()
        {
            int length = _bin.ReadInt16();
            _pos += 2;
            if (length <= 0 || length > 512)
                return string.Empty;

            byte[] data = _bin.ReadBytes(length);
            _pos += length;
            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        }

        private string ParseShortString()
        {
            int length = _bin.ReadByte();
            _pos += 1;
            if (length <= 0 || length > 128)
                return string.Empty;

            byte[] data = _bin.ReadBytes(length);
            _pos += length;
            return Encoding.ASCII.GetString(data).TrimEnd('\0');
        }
    }
}