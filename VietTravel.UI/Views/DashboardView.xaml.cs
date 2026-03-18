using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void QuickAction_Tours(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.NavigateToToursCommand.Execute(null);
        }

        private void QuickAction_Departures(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.NavigateToDeparturesCommand.Execute(null);
        }

        private void QuickAction_Bookings(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.NavigateToBookingsCommand.Execute(null);
        }

        private void QuickAction_Customers(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.NavigateToCustomersCommand.Execute(null);
        }
    }
}
