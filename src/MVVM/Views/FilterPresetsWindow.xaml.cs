using System.Windows;
using System.Windows.Input;
using LogViewer.MVVM.ViewModels.Log;

namespace LogViewer.MVVM.Views
{
    public partial class FilterPresetsWindow : Window
    {
        public FilterPresetsWindow(FilterPresetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += CloseFromApply;
            Closed += (sender, args) => viewModel.CloseRequested -= CloseFromApply;
        }

        private void CloseFromApply()
        {
            Close();
        }

        private void PresetsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as FilterPresetsViewModel;
            if (vm?.Selected != null)
                vm.ApplyCommand.Execute(null);
        }
    }
}
