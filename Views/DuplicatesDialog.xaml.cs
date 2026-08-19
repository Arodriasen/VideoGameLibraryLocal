using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VideoGameLibrary.Models;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class DuplicatesDialog : Window
    {
        private readonly List<DuplicateCandidateItem> _items;

        public List<int> SelectedIds { get; private set; } = new();

        // groups: cada lista es un conjunto de juegos que probablemente son el mismo,
        // ya ordenados por fecha de alta (el más antiguo primero) por ImportService.FindDuplicateGroups.
        public DuplicatesDialog(List<List<Game>> groups)
        {
            InitializeComponent();

            _items = groups.SelectMany(group => group.Select((game, index) =>
                new DuplicateCandidateItem(game, isSelected: index > 0, isGroupStart: index == 0, groupCount: group.Count)))
                .ToList();

            ItemsList.ItemsSource = _items;

            var totalCopies = _items.Count;
            TxtSummary.Text = $"{groups.Count} grupo(s) de posibles duplicados, {totalCopies} juego(s) en total.";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            SelectedIds = _items.Where(i => i.IsSelected).Select(i => i.Game.Id).ToList();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
