using System.Collections.Generic;

namespace Hexwex.Core
{
    public sealed class Biome
    {
        public readonly BiomeId Id;
        /// <summary>The spec's spelling, which also names the source art (<c>biomes/forrest.webp</c>).</summary>
        public readonly string Key;
        public readonly string Label;
        public readonly string Description;
        /// <summary>Fill of the hex, as the prototype's <c>#rrggbb</c>.</summary>
        public readonly string Color;
        /// <summary>Darker rim of the hex.</summary>
        public readonly string EdgeColor;

        public Biome(BiomeId id, string key, string label, string description, string color, string edgeColor)
        {
            Id = id;
            Key = key;
            Label = label;
            Description = description;
            Color = color;
            EdgeColor = edgeColor;
        }
    }

    /// <summary>The 16 biomes of the spec (<c>core/biomes.ts</c>), in the spec's order.</summary>
    public static class Biomes
    {
        /// <summary>The order is the order of <see cref="BiomeId"/>, and island generation picks by it.</summary>
        public static readonly IReadOnlyList<Biome> All = new[]
        {
            new Biome(BiomeId.Grassland, "grassland", "Умеренные луга", "Жирная трава по пояс и ровная почва. Здесь растёт всё, и людям тут хорошо живётся.", "#6fae4f", "#3f6b2c"),
            new Biome(BiomeId.Plains, "plains", "Равнина", "Сухая ровная степь. Родит скупо, зато на ней легко строить что угодно.", "#a8bd5e", "#6d7a35"),
            new Biome(BiomeId.Forrest, "forrest", "Лес", "Прямые стволы в два обхвата. Главный источник дерева на острове.", "#3f7d43", "#24512a"),
            new Biome(BiomeId.Savanna, "savanna", "Саванна", "Редкие зонтичные деревья на выжженной траве. Дерева мало, но оно рядом.", "#c2a24c", "#83682a"),
            new Biome(BiomeId.Rainforest, "rainforest", "Джунгли", "Душная зелёная стена. Дерева больше, чем где бы то ни было, и гниёт оно так же быстро.", "#2f7d5c", "#18503a"),
            new Biome(BiomeId.Taiga, "taiga", "Хвойный лес", "Смолистые ели на вечной мерзлоте. Ровный, надёжный лесоповал.", "#356b55", "#1d4436"),
            new Biome(BiomeId.Tundra, "tundra", "Тундра", "Мох, лишайник и ветер. Людям тут тяжело, но жить можно.", "#8fa79b", "#5c6f66"),
            new Biome(BiomeId.Desert, "desert", "Пустыня", "Песок и камень до горизонта. Еды нет, зато никто не мешает.", "#dcc179", "#a08a4a"),
            new Biome(BiomeId.PolarDesert, "polar_desert", "Ледяная пустыня", "Голый лёд. Тут выживают только упрямые.", "#c9dbe4", "#8ea6b2"),
            new Biome(BiomeId.Swamp, "swamp", "Болото", "Тёплая жижа, мошка и пузыри газа. Отдаёт много и травит сильнее всего.", "#5c6b3a", "#38421f"),
            new Biome(BiomeId.Badlands, "badlands", "Бесплодные земли", "Растрескавшаяся отравленная глина. Селиться тут — плохая идея.", "#a2603f", "#6b3a23"),
            new Biome(BiomeId.Crater, "crater", "Кратер", "Воронка от чего-то очень старого. По краям обнажилась порода.", "#7a6f66", "#4b433d"),
            new Biome(BiomeId.Volcano, "volcano", "Вулкан", "Дышит серой и даёт больше камня, чем любая гора. И травит соразмерно.", "#6b3733", "#41201d"),
            new Biome(BiomeId.Hills, "hills", "Холмы", "Пологие склоны с хорошей землёй в распадках.", "#8a9a4e", "#5a672f"),
            new Biome(BiomeId.Mountains, "mountains", "Горы", "Серый камень и снег на макушках. Лучшая порода на острове.", "#8d8f96", "#585b62"),
            new Biome(BiomeId.Cliffs, "cliffs", "Утёсы", "Обрыв по краю острова. Камень рядом, места мало.", "#7e8894", "#4e565f"),
        };

        public static Biome Get(BiomeId id)
        {
            return All[(int)id];
        }
    }
}
