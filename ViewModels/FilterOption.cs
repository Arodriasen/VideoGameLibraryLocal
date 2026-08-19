using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGameLibrary.ViewModels
{
    public partial class FilterOption : ObservableObject
    {
        public string Value { get; set; } = string.Empty;
        [ObservableProperty] private bool _isSelected;

        public Action? OnChanged { get; set; }

        partial void OnIsSelectedChanged(bool value) => OnChanged?.Invoke();
    }
}
