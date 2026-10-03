using DISPLAY_SCALER.ViewModels;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace DISPLAY_SCALER.Views
{
    public partial class AddCustomResolutionDialog : Window
    {
        private static readonly Regex NumberRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly CustomResViewModel _viewModel;

        public AddCustomResolutionDialog(CustomResViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            InitializeComponent();
            _viewModel.RequestClose += OnRequestClose;
            Closed += OnClosed;
        }

        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !NumberRegex.IsMatch(e.Text);
        }

        private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnRequestClose(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _viewModel.RequestClose -= OnRequestClose;
            Closed -= OnClosed;
        }
    }
}