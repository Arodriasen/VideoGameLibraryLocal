using System;
using System.Collections.Generic;

namespace VideoGameLibrary.Application.Platforms
{
    // Reglas puras sin I/O -- por eso vive en Application y no en Infrastructure/ExternalApis
    // (que sí habla de verdad con IGDB por red), aunque el nombre recuerde a IGDB.
    // Traduce el texto de Game.Platform (libre, tal y como lo devuelven ScanDex/IGDB/RAWG) al ID
    // numérico de plataforma de IGDB, necesario para filtrar el calendario de lanzamientos por
    // plataforma. Todos los IDs verificados uno a uno contra IGDB /platforms el 2026-08-21 (no
    // memorizados a ciegas). Deliberadamente no es el catálogo completo de IGDB (cientos de
    // entradas, muchas retro/regionales) — solo las plataformas de la colección del usuario más
    // las más habituales, para que el filtro del calendario sea manejable. CanonicalPlatforms fija
    // qué aparece en el filtro (una entrada por plataforma, sin duplicar por alias); Map añade
    // alias adicionales (p.ej. "PS5", "Switch") solo para reconocer el texto libre de Game.Platform.
    public static class IgdbPlatformMap
    {
        // Orden de aparición en el filtro del calendario: primero las plataformas más recientes.
        public static readonly IReadOnlyList<(string Name, int Id)> CanonicalPlatforms = new (string Name, int Id)[]
        {
            ("Nintendo Switch 2", 508),
            ("Nintendo Switch", 130),
            ("Nintendo 3DS", 37),
            ("Wii U", 41),
            ("PlayStation 5", 167),
            ("PlayStation 4", 48),
            ("PlayStation 3", 9),
            ("PlayStation Vita", 46),
            ("Xbox Series X|S", 169),
            ("Xbox One", 49),
            ("Xbox 360", 12),
            ("PC (Microsoft Windows)", 6),
            ("Mac", 14),
            ("Linux", 3),
            ("iOS", 39),
            ("Android", 34),
            ("Arcade", 52),
        };

        private static readonly Dictionary<string, int> Map = BuildMap();

        private static Dictionary<string, int> BuildMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, id) in CanonicalPlatforms)
                map[name] = id;

            // Alias adicionales — variantes de texto que pueden venir de ScanDex/IGDB/RAWG o de
            // ediciones anteriores de la app, pero que no interesa mostrar como entradas separadas
            // en el filtro (todas apuntan al mismo ID que su forma canónica de arriba).
            map["Switch"] = 130;
            map["Switch 2"] = 508;
            map["PS5"] = 167;
            map["PS4"] = 48;
            map["PS3"] = 9;
            map["PS Vita"] = 46;
            map["Vita"] = 46;
            map["Xbox Series X"] = 169;
            map["Xbox Series S"] = 169;
            map["PC"] = 6;
            map["Windows"] = 6;
            map["Mac OS"] = 14;
            map["macOS"] = 14;

            return map;
        }

        public static bool TryGetId(string platformName, out int id) => Map.TryGetValue(platformName.Trim(), out id);
    }
}
