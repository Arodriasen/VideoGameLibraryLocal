using System;
using System.Globalization;
using System.Windows.Data;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.Converters
{
    // Traduce DeletedDate a un texto de cuenta atrás ("Se elimina en X días") usando el
    // mismo periodo de retención que GameRepository.PurgeExpiredTrashAsync, para que la UI
    // de la papelera nunca se desincronice del valor real que borra los juegos.
    public class DeletedDateToDaysLeftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime deletedDate) return string.Empty;

            var daysLeft = GameRepository.TrashRetentionDays - (DateTime.Now.Date - deletedDate.Date).Days;

            if (daysLeft <= 0) return "se elimina hoy";
            if (daysLeft == 1) return "se elimina mañana";
            return $"se elimina en {daysLeft} días";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
