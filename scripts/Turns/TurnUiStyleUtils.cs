using Godot;

namespace SpellsAndRooms.scripts.Turns
{
    /// @brief Reutiliza estilos y utilidades UI entre escenas de turnos.
    public static class TurnUiStyleUtils
    {
        /// @brief Crea el estilo visual base de cartas para tienda/cofres.
        /// @return StyleBox listo para asignar al panel principal de la carta.
        public static StyleBoxFlat CreateCardStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.10f, 0.14f, 0.92f),
                BorderColor = new Color(0.83f, 0.71f, 0.41f, 1.0f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 12,
                CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12,
                CornerRadiusBottomRight = 12,
                ContentMarginLeft = 12,
                ContentMarginTop = 12,
                ContentMarginRight = 12,
                ContentMarginBottom = 12
            };
        }

        /// @brief Crea el estilo del marco de imagen dentro de una carta.
        /// @return StyleBox listo para asignar al contenedor de textura.
        public static StyleBoxFlat CreateImageFrameStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.06f, 0.09f, 0.90f),
                BorderColor = new Color(0.44f, 0.50f, 0.62f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 6,
                ContentMarginTop = 6,
                ContentMarginRight = 6,
                ContentMarginBottom = 6
            };
        }

        /// @brief Elimina todos los nodos hijos de un contenedor.
        /// @param container Contenedor a limpiar.
        public static void ClearContainer(Container container)
        {
            if (container == null)
                return;

            foreach (Node child in container.GetChildren())
                child.QueueFree();
        }
    }
}


