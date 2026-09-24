using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGameLibrary.Presentation.ViewModels
{
    public partial class CalendarDayCell : ObservableObject
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        [ObservableProperty] private bool _isSelected;

        public List<UpcomingReleaseItem> Releases { get; set; } = new();
        public int ReleaseCount => Releases.Count;
        public int DayNumber => Date.Day;
    }
}
