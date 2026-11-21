using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// A set of generated islands on disk, as JSON. The file keeps the settings it
    /// was made from, so the generator can open it again and change them, and the
    /// islands themselves, hex by hex, so the game can use them as they are.
    /// Biomes and buildings go by their keys ("forrest", "farm"), not by number,
    /// so a file stays readable and survives a reordering of the enums.
    ///
    /// The generator saves into <c>Assets/Resources/Islands</c> in the Editor, where
    /// the game finds a set with <see cref="LoadResource"/>; a built player saves
    /// into its persistent data folder.
    /// </summary>
    public static class IslandSetFile
    {
        public const int Version = 1;
        /// <summary>The folder under <c>Resources</c> the sets live in.</summary>
        public const string ResourceFolder = "Islands";

        [Serializable]
        public sealed class FileData
        {
            public int version = Version;
            public string name;
            public string created;
            public SpecData settings = new SpecData();
            public List<IslandData> islands = new List<IslandData>();
        }

        [Serializable]
        public sealed class SpecData
        {
            public string seed;
            public List<RecipeData> recipes = new List<RecipeData>();
        }

        [Serializable]
        public sealed class RecipeData
        {
            public string name;
            public int count;
            public int size;
            public float compactness;
            public float clumping;
            /// <summary>"share" or "count".</summary>
            public string mode;
            public List<BiomeAmountData> biomes = new List<BiomeAmountData>();
            public bool requireBuildable;
            public int toxicityMin;
            public int toxicityMax;
            public float buildingChance;
        }

        [Serializable]
        public sealed class BiomeAmountData
        {
            public string biome;
            public int amount;
        }

        [Serializable]
        public sealed class IslandData
        {
            public string name;
            public string recipe;
            public string seed;
            public List<HexData> hexes = new List<HexData>();
        }

        [Serializable]
        public sealed class HexData
        {
            public int q;
            public int r;
            public string biome;
            public int toxicity;
            /// <summary>The building's key, or empty for none.</summary>
            public string building;
        }

        /// <summary>The folder a set is saved into: the project's Resources in the Editor, the player's data folder in a build.</summary>
        public static string Folder
        {
            get
            {
                return Application.isEditor
                    ? Path.Combine(Application.dataPath, "Resources", ResourceFolder)
                    : Path.Combine(Application.persistentDataPath, ResourceFolder);
            }
        }

        public static string ToJson(string name, IslandSetSpec spec, List<ForgedIsland> islands)
        {
            FileData data = new FileData
            {
                name = name,
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                settings = new SpecData { seed = spec.Seed, recipes = spec.Recipes.Select(WriteRecipe).ToList() },
                islands = islands.Select(island => new IslandData
                {
                    name = island.Name,
                    recipe = island.Recipe,
                    seed = island.Seed,
                    hexes = island.Hexes.Select(hex => new HexData
                    {
                        q = hex.Q,
                        r = hex.R,
                        biome = Biomes.Get(hex.Biome).Key,
                        toxicity = hex.Toxicity,
                        building = hex.Building.HasValue ? Buildings.Get(hex.Building.Value).Key : "",
                    }).ToList(),
                }).ToList(),
            };

            return JsonUtility.ToJson(data, true);
        }

        /// <summary>Reads a set. Throws <see cref="InvalidDataException"/> on text that is not one.</summary>
        public static void FromJson(string json, out IslandSetSpec spec, out List<ForgedIsland> islands)
        {
            FileData data;
            try
            {
                data = JsonUtility.FromJson<FileData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Не JSON набора островов", exception);
            }

            if (data == null || data.settings == null)
            {
                throw new InvalidDataException("Не набор островов");
            }

            if (data.version > Version)
            {
                throw new InvalidDataException("Файл новее генератора: версия " + data.version);
            }

            spec = new IslandSetSpec { Seed = data.settings.seed, Recipes = data.settings.recipes.Select(ReadRecipe).ToList() };
            islands = (data.islands ?? new List<IslandData>()).Select(island => new ForgedIsland
            {
                Name = island.name,
                Recipe = island.recipe,
                Seed = island.seed,
                Hexes = (island.hexes ?? new List<HexData>()).Select(ReadHex).ToList(),
            }).ToList();
        }

        /// <summary>Writes a set into <see cref="Folder"/>. Returns the file's path.</summary>
        public static string Save(string name, IslandSetSpec spec, List<ForgedIsland> islands)
        {
            string safe = SafeName(name);
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, safe + ".json");
            File.WriteAllText(path, ToJson(safe, spec, islands));

            return path;
        }

        /// <summary>Reads a set from <see cref="Folder"/> by its name.</summary>
        public static void Load(string name, out IslandSetSpec spec, out List<ForgedIsland> islands)
        {
            FromJson(File.ReadAllText(Path.Combine(Folder, SafeName(name) + ".json")), out spec, out islands);
        }

        /// <summary>The names of the sets in <see cref="Folder"/>, without the extension.</summary>
        public static List<string> List()
        {
            if (!Directory.Exists(Folder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(Folder, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(name => name).ToList();
        }

        /// <summary>
        /// The islands of a set the game ships with, from <c>Resources/Islands/&lt;name&gt;</c>,
        /// or <c>null</c> when there is no such set. This is how the game uses a set.
        /// </summary>
        public static List<ForgedIsland> LoadResource(string name)
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceFolder + "/" + name);
            if (asset == null)
            {
                return null;
            }

            FromJson(asset.text, out _, out List<ForgedIsland> islands);

            return islands;
        }

        /// <summary>A file name made of the name's letters, digits, dashes and underscores.</summary>
        public static string SafeName(string name)
        {
            string trimmed = (name ?? "").Trim();
            char[] letters = trimmed.Select(letter => char.IsLetterOrDigit(letter) || letter == '-' || letter == '_' ? letter : '_').ToArray();
            string safe = new string(letters).Trim('_');

            return safe.Length > 0 ? safe : "islands";
        }

        private static RecipeData WriteRecipe(IslandRecipe recipe)
        {
            return new RecipeData
            {
                name = recipe.Name,
                count = recipe.Count,
                size = recipe.Size,
                compactness = (float)recipe.Compactness,
                clumping = (float)recipe.Clumping,
                mode = recipe.Mode == BiomeAmountMode.Count ? "count" : "share",
                biomes = Biomes.All.Where(biome => recipe.Biomes[(int)biome.Id] > 0)
                    .Select(biome => new BiomeAmountData { biome = biome.Key, amount = recipe.Biomes[(int)biome.Id] }).ToList(),
                requireBuildable = recipe.RequireBuildable,
                toxicityMin = recipe.ToxicityMin,
                toxicityMax = recipe.ToxicityMax,
                buildingChance = (float)recipe.BuildingChance,
            };
        }

        private static IslandRecipe ReadRecipe(RecipeData data)
        {
            IslandRecipe recipe = new IslandRecipe
            {
                Name = data.name,
                Count = Math.Max(0, data.count),
                Size = data.size,
                Compactness = data.compactness,
                Clumping = data.clumping,
                Mode = data.mode == "count" ? BiomeAmountMode.Count : BiomeAmountMode.Share,
                Biomes = new int[Biomes.All.Count],
                RequireBuildable = data.requireBuildable,
                ToxicityMin = data.toxicityMin,
                ToxicityMax = data.toxicityMax,
                BuildingChance = data.buildingChance,
            };
            foreach (BiomeAmountData amount in data.biomes ?? new List<BiomeAmountData>())
            {
                if (TryBiome(amount.biome, out BiomeId biome))
                {
                    recipe.Biomes[(int)biome] = Math.Max(0, amount.amount);
                }
            }

            return recipe;
        }

        private static HexTile ReadHex(HexData data)
        {
            if (!TryBiome(data.biome, out BiomeId biome))
            {
                throw new InvalidDataException("Неизвестный биом: " + data.biome);
            }

            HexTile hex = new HexTile { Id = HexMath.HexId(data.q, data.r), Q = data.q, R = data.r, Biome = biome, Toxicity = Mathf.Clamp(data.toxicity, 0, Tax.DeadToxicityPct) };
            if (!string.IsNullOrEmpty(data.building))
            {
                Building building = Buildings.All.FirstOrDefault(candidate => candidate.Key == data.building);
                if (building == null)
                {
                    throw new InvalidDataException("Неизвестное здание: " + data.building);
                }

                hex.Building = building.Id;
                hex.Workers = Population.WorkersPerBuilding;
            }

            return hex;
        }

        private static bool TryBiome(string key, out BiomeId biome)
        {
            Biome found = Biomes.All.FirstOrDefault(candidate => candidate.Key == key);
            biome = found != null ? found.Id : default;

            return found != null;
        }
    }
}
