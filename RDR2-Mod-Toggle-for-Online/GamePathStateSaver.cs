using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace RDR2_Mod_Toggle_for_Online
{
    public static class GamePathStateSaver
    {
        private const string GameStateFilePath = "gameState.json";

        public static void SaveGamePathState(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                throw new DirectoryNotFoundException("The specified game path does not exist.");
            }

            var gameState = new List<GameFileItem>();
            SaveDirectoryState(gamePath, gameState, gamePath);

            File.WriteAllText(GameStateFilePath, JsonConvert.SerializeObject(gameState, Formatting.Indented));
            Console.WriteLine("Game path state saved successfully.");
        }

        private static void SaveDirectoryState(string directoryPath, List<GameFileItem> gameState, string basePath)
        {
            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                var relativePath = directory.Substring(basePath.Length + 1);
                gameState.Add(new GameFileItem { Path = relativePath, IsDirectory = true });
                SaveDirectoryState(directory, gameState, basePath);
            }

            foreach (var file in Directory.GetFiles(directoryPath))
            {
                var relativePath = file.Substring(basePath.Length + 1);
                gameState.Add(new GameFileItem { Path = relativePath, IsDirectory = false });
            }
        }

        public static bool IsBaseGameFile(string relativePath)
        {
            var gameStateJson = File.ReadAllText(GameStateFilePath);
            var gameState = JsonConvert.DeserializeObject<List<GameFileItem>>(gameStateJson);
            return gameState.Any(item => item.Path.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class GameFileItem
    {
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
    }
}
