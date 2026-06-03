using System;
using System.IO;
using System.Text;
using Rampastring.Tools;

namespace ClientCore.Statistics
{
    /// <summary>
    /// Coordinates parsing the stats.dmp file at the end of a game and storing its text output in the MatchStatistics object.
    /// </summary>
    public static class StatsDumpProcessor
    {
        public static void Process(string dumpPath, MatchStatistics stats, ClientConfiguration config)
        {
            if (!config.EnableStatsDumpParser)
                return;

            try
            {
                // Choose the correct rules file based on the game type
                string game = config.GetString("Game", "RA1");
                string rulesFile = game.Equals("RA2", StringComparison.OrdinalIgnoreCase)
                    ? "RA2Rules.ini"
                    : config.StatsDumpRulesIni;

                string rulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rulesFile);

                if (!File.Exists(dumpPath))
                    return;

                var parser = new StatsDumpParser(dumpPath);
                
                IniFile ini = null;
                if (File.Exists(rulesPath))
                {
                    ini = new IniFile(rulesPath);
                }

                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                {
                    var original = Console.Out;
                    Console.SetOut(sw);
                    
                    parser.PrintParsedData();
                    if (ini != null)
                    {
                        parser.PrintPlayerData(ini);
                    }
                    
                    Console.SetOut(original);
                }

                stats.DmpSummary = sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing stats dump: {ex.Message}");
            }
        }
    }
}
