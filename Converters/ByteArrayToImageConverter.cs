using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace VideoGameLibrary.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        // ConverterParameter (opcional): ancho máximo de decodificación en píxeles. Sin él, se
        // decodifica a resolución original (portada grande en ficha/edición). Con él, la imagen
        // se decodifica ya al tamaño real de la miniatura en vez de a tamaño completo + reescalado
        // por WPF en cada fotograma — evita el scroll a trompicones en listas con muchas portadas.
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                var image = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (parameter != null && int.TryParse(parameter.ToString(), out var decodeWidth) && decodeWidth > 0)
                    image.DecodePixelWidth = decodeWidth;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
