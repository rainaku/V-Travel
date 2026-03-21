using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VietTravel.UI.Views
{
    public partial class AdminPaginationBar : UserControl
    {
        public static readonly DependencyProperty SummaryTextProperty =
            DependencyProperty.Register(
                nameof(SummaryText),
                typeof(string),
                typeof(AdminPaginationBar),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CanGoPreviousProperty =
            DependencyProperty.Register(
                nameof(CanGoPrevious),
                typeof(bool),
                typeof(AdminPaginationBar),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CanGoNextProperty =
            DependencyProperty.Register(
                nameof(CanGoNext),
                typeof(bool),
                typeof(AdminPaginationBar),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowPaginationProperty =
            DependencyProperty.Register(
                nameof(ShowPagination),
                typeof(bool),
                typeof(AdminPaginationBar),
                new PropertyMetadata(false));

        public static readonly DependencyProperty PreviousCommandProperty =
            DependencyProperty.Register(
                nameof(PreviousCommand),
                typeof(ICommand),
                typeof(AdminPaginationBar),
                new PropertyMetadata(null));

        public static readonly DependencyProperty NextCommandProperty =
            DependencyProperty.Register(
                nameof(NextCommand),
                typeof(ICommand),
                typeof(AdminPaginationBar),
                new PropertyMetadata(null));

        public string SummaryText
        {
            get => (string)GetValue(SummaryTextProperty);
            set => SetValue(SummaryTextProperty, value);
        }

        public bool CanGoPrevious
        {
            get => (bool)GetValue(CanGoPreviousProperty);
            set => SetValue(CanGoPreviousProperty, value);
        }

        public bool CanGoNext
        {
            get => (bool)GetValue(CanGoNextProperty);
            set => SetValue(CanGoNextProperty, value);
        }

        public bool ShowPagination
        {
            get => (bool)GetValue(ShowPaginationProperty);
            set => SetValue(ShowPaginationProperty, value);
        }

        public ICommand? PreviousCommand
        {
            get => (ICommand?)GetValue(PreviousCommandProperty);
            set => SetValue(PreviousCommandProperty, value);
        }

        public ICommand? NextCommand
        {
            get => (ICommand?)GetValue(NextCommandProperty);
            set => SetValue(NextCommandProperty, value);
        }

        public AdminPaginationBar()
        {
            InitializeComponent();
        }
    }
}
