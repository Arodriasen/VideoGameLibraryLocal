# Mi Colección de Juegos

![Build](https://github.com/Arodriasen/VideoGameLibrary/actions/workflows/build.yml/badge.svg)

Aplicación de escritorio para Windows que permite catalogar tu colección personal de videojuegos escaneando el código de barras (UPC/EAN) de la caja. Busca automáticamente título, plataforma, género, año y portada, y guarda todo en una base de datos local (SQLite) en tu propio equipo.

## Características

- Alta rapidez escaneando el código de barras (con lector USB o escribiéndolo a mano), incluyendo un modo de escaneo rápido que añade el juego sin abrir ningún diálogo.
- Búsqueda manual por nombre si el código de barras no encuentra el juego, con selección entre varios resultados candidatos.
- Ficha con portada, plataforma, editorial, género, año y notas.
- Etiquetas libres (favorito, para vender, edición coleccionista...), mostradas como chips en las tarjetas, la lista y la ficha.
- Puntuación por estrellas (1 a 5), marcar como jugado, y lista de deseos (wishlist) con un botón para pasar el juego a la colección cuando lo compras.
- Filtros por plataforma, género, etiqueta, año, puntuación y estado (jugado / falta por jugar), y ordenación de la colección.
- Papelera: los juegos eliminados se conservan 7 días antes de borrarse para siempre, con opción de restaurarlos mientras tanto.
- Estadísticas de la colección (icono de gráfico en la barra inferior): totales, por plataforma, por género, por etiqueta y progreso de altas por año. La ventana se puede redimensionar.
- Importación desde Excel/CSV (columnas identificadas por nombre, no por posición), con vista previa antes de importar que avisa de duplicados. Exportación de la colección a Excel (.xlsx) o CSV.
- Copia de seguridad de la base de datos con un clic desde Ajustes, mantenimiento (compactar/VACUUM) y un detector de posibles duplicados en la colección.
- Tema claro/oscuro.
- Registro de errores dentro de la app (icono ⚠ en la barra de herramientas) para diagnosticar problemas sin depurador.
- Aviso automático dentro de la app cuando hay una versión más nueva disponible en GitHub, con acceso directo a la descarga.
- Todos los datos se guardan localmente: no hay cuentas ni servidores propios.

## Requisitos

- Windows 10/11 de 64 bits.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para compilar el proyecto (no hace falta si solo vas a ejecutar un `.exe` ya publicado).

## Descargar

Si solo quieres usar la app (sin tocar el código), descarga el `.exe` ya compilado desde la [última release](https://github.com/Arodriasen/VideoGameLibrary/releases/latest). No necesita instalación ni tener .NET instalado.

La propia app te avisará dentro de la interfaz cuando salga una versión más nueva, con un acceso directo para descargarla.

## Compilar y ejecutar

Si prefieres compilarlo tú mismo desde el código fuente:

1. Clona el repositorio.
2. Para probarlo directamente sin generar un ejecutable:
   ```
   dotnet run --project VideoGameLibrary.csproj
   ```
3. Para generar un `.exe` autocontenido (no necesita tener .NET instalado en el equipo donde se ejecute):
   ```
   dotnet publish VideoGameLibrary.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
   ```
   El ejecutable quedará en la carpeta `publish\VideoGameLibrary.exe`.

También puedes abrir `VideoGameLibrary.sln` directamente con Visual Studio 2022.

> **Nota:** al no estar firmado digitalmente, Windows SmartScreen puede mostrar un aviso la primera vez que ejecutes el `.exe` ("Más información" → "Ejecutar de todas formas").

## Primer uso

Al arrancar por primera vez, la app te pedirá elegir o crear el archivo de base de datos (`.db`) donde se guardará tu colección, y te abrirá la ventana de Ajustes para introducir claves de API (todas opcionales, ver más abajo). Puedes omitir este paso y añadirlas más tarde desde el icono de engranaje de la barra de herramientas.

## Claves de API (opcionales)

La app funciona sin ninguna clave: el escaneo usará únicamente UPCitemdb, que no requiere registro. Añadir claves mejora la tasa de acierto y la calidad de los datos (portadas, género, plataforma):

| Servicio | Para qué se usa | Dónde conseguirla |
|---|---|---|
| [ScanDex](https://scandex.gamery.app/) | Resolución del código de barras específica de videojuegos | Web de ScanDex |
| [IGDB](https://api-docs.igdb.com/) | Enriquecimiento por nombre (portada, género, plataforma) | Client ID y Secret desde una app registrada en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps) |
| [RAWG](https://rawg.io/apidocs) | Enriquecimiento por nombre, segunda fuente | Clave gratuita en rawg.io |
| [TheGamesDB](https://thegamesdb.net/) | Portada como último recurso | Clave gratuita solicitándola en su foro |

Ninguna clave se sube a ningún sitio: se guardan solo en tu equipo, en `%AppData%\VideoGameLibrary\config.json`, cifradas con la protección de datos de Windows (DPAPI) ligada a tu usuario. Solo se pueden descifrar iniciando sesión con ese mismo usuario de Windows en este equipo; si compartes el equipo pero cada persona tiene su propia cuenta de Windows, las claves de una no son legibles desde la otra.

## Tecnologías

WPF (.NET 8), Entity Framework Core + SQLite, CommunityToolkit.Mvvm, MaterialDesignInXAML, ClosedXML.

## Licencia

Este proyecto está publicado bajo la licencia [MIT](LICENSE): puedes usarlo, modificarlo y distribuirlo libremente, incluso en proyectos privados o comerciales, siempre citando la licencia original.
