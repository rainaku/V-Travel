using System.Windows;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI.Views
{
    public partial class VerificationWindow : Window
    {
        public VerificationWindow(VerificationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Đóng window khi DialogResult thay đổi (thông qua property binding)
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(VerificationViewModel.DialogResult) && viewModel.DialogResult.HasValue)
                {
                    this.DialogResult = viewModel.DialogResult.Value;
                    this.Close();
                }
            };
        }
    }
}
