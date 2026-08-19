using System.Runtime.CompilerServices;
using System.Windows;

// Permite que VideoGameLibrary.Tests vea los miembros "internal" (p.ej. GameApiService.ExtractYear)
// sin tener que hacerlos públicos de cara a quien use la app como librería.
[assembly: InternalsVisibleTo("VideoGameLibrary.Tests")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
