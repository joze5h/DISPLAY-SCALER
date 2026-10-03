using DISPLAY_SCALER.Models;
using System.Windows;
using System.Windows.Input;

namespace DISPLAY_SCALER.Views
{
    public partial class DuplicateResolutionDialog : Window
    {
        public DuplicateResolutionDialog(ModeDuplicateInfo info)
        {
            ResultAction = DuplicateModeAction.Ignore;
            DataContext = info;
            InitializeComponent();
        }

        public DuplicateModeAction ResultAction { get; private set; }

        private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void AddMarker_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = DuplicateModeAction.AddMarker;
            DialogResult = true;
            Close();
        }

        private void Ignore_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = DuplicateModeAction.Ignore;
            DialogResult = false;
            Close();
        }
    }
}