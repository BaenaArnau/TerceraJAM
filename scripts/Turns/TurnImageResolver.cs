using Godot;

namespace SpellsAndRooms.scripts.Turns
{
    /// @brief Resuelve arte de ofertas/recompensas desde CSV con fallback automatico.
    public static class TurnImageResolver
    {
        /// @brief Resuelve la textura de un consumible.
        /// @param def Definicion cargada desde CSV.
        /// @return Textura valida o null si no hay recurso disponible.
        public static Texture2D ResolveConsumableTexture(ItemDatabase.ConsumableDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            string name = Normalize(def.Name);
            string subtype = Normalize(def.Subtype);

            if (name.Contains("health") || subtype.Contains("healing"))
                return LoadFirstExistingTexture("res://assets/Items/Consumable/Poti.png");

            if (name.Contains("mana") || subtype.Contains("mana"))
                return LoadFirstExistingTexture("res://assets/Items/Consumable/PotiManai.png");

            return LoadFirstExistingTexture(
                $"res://assets/Items/Consumable/{def.Name}.png",
                "res://assets/Items/Consumable/Poti.png",
                "res://assets/Items/Consumable/PotiManai.png");
        }

        /// @brief Resuelve la textura de un pasivo.
        /// @param def Definicion cargada desde CSV.
        /// @return Textura valida o null si no hay recurso disponible.
        public static Texture2D ResolvePassiveTexture(ItemDatabase.PassiveDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            return LoadFirstExistingTexture(
                $"res://assets/Items/Passive/{def.Name}.png",
                "res://assets/Items/Passive/pecheGris.png");
        }

        /// @brief Resuelve la textura de una skill.
        /// @param def Definicion cargada desde CSV.
        /// @return Textura valida o null si no hay recurso disponible.
        public static Texture2D ResolveSkillTexture(SkillDatabase.SkillDefinition def)
        {
            Texture2D csvTexture = TryLoadTexture(def.ImagePath);
            if (csvTexture != null)
                return csvTexture;

            string name = Normalize(def.Name);

            if (name.Contains("pyro"))
                return LoadFirstExistingTexture("res://assets/Characters/Enemy/BlackGoblin.png");
            if (name.Contains("aqua"))
                return LoadFirstExistingTexture("res://assets/Characters/Player/MagoAzul.png");
            if (name.Contains("earth"))
                return LoadFirstExistingTexture("res://assets/Characters/Enemy/Esqueleto.png");

            return LoadFirstExistingTexture(
                "res://assets/Characters/Enemy/Slime.png",
                "res://assets/Characters/Player/CaballeroNegro.png");
        }

        /// @brief Devuelve el path final de textura para consumibles.
        public static string ResolveConsumablePath(ItemDatabase.ConsumableDefinition def)
        {
            Texture2D texture = ResolveConsumableTexture(def);
            return texture?.ResourcePath ?? string.Empty;
        }

        /// @brief Devuelve el path final de textura para pasivos.
        public static string ResolvePassivePath(ItemDatabase.PassiveDefinition def)
        {
            Texture2D texture = ResolvePassiveTexture(def);
            return texture?.ResourcePath ?? string.Empty;
        }

        /// @brief Devuelve el path final de textura para skills.
        public static string ResolveSkillPath(SkillDatabase.SkillDefinition def)
        {
            Texture2D texture = ResolveSkillTexture(def);
            return texture?.ResourcePath ?? string.Empty;
        }

        /// @brief Carga una textura desde path de recurso si existe.
        /// @param path Ruta en formato `res://`.
        /// @return Textura cargada o null si no existe.
        public static Texture2D TryLoadTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                return null;

            return GD.Load<Texture2D>(path);
        }

        private static Texture2D LoadFirstExistingTexture(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && ResourceLoader.Exists(candidate))
                    return GD.Load<Texture2D>(candidate);
            }

            return null;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}


