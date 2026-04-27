using System;
using System.Collections.Generic;
using System.Globalization;

namespace SpellsAndRooms.scripts.Turns
{
    /// @brief Utilidades compartidas para leer y parsear columnas CSV de forma segura.
    public static class TurnCsvUtils
    {
        /// @brief Busca el indice de una columna por nombre, ignorando mayusculas/minusculas.
        /// @param headers Lista de cabeceras CSV.
        /// @param columnName Nombre de la columna a buscar.
        /// @return Indice de la columna o -1 si no existe.
        public static int FindColumnIndex(List<string> headers, string columnName)
        {
            if (headers == null || headers.Count == 0 || string.IsNullOrWhiteSpace(columnName))
                return -1;

            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        /// @brief Parsea un entero usando cultura invariante.
        /// @param value Valor de entrada.
        /// @param fallback Valor por defecto si el parseo falla.
        /// @return Entero parseado o fallback.
        public static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        /// @brief Parsea un booleano en formato texto.
        /// @param value Valor de entrada.
        /// @return true solo cuando el parseo es valido y el valor es true.
        public static bool ParseBool(string value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }
    }
}

