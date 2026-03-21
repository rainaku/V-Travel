using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VietTravel.UI.ViewModels
{
    public abstract partial class PaginatedListViewModelBase<T> : ObservableObject
    {
        private List<T> _pagedSource = new();
        private ObservableCollection<T>? _pagedTarget;

        protected const int DefaultPageSize = 12;

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _totalPages = 1;
        [ObservableProperty] private int _totalFilteredItems;

        public bool CanGoToPreviousPage => CurrentPage > 1;
        public bool CanGoToNextPage => CurrentPage < TotalPages;
        public bool ShowPagination => TotalFilteredItems > DefaultPageSize;
        public string PaginationSummary => TotalFilteredItems == 0
            ? "0 mục"
            : $"Trang {CurrentPage}/{TotalPages} • {TotalFilteredItems} mục";

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void PreviousPage()
        {
            if (!CanGoToPreviousPage)
            {
                return;
            }

            CurrentPage--;
        }

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void NextPage()
        {
            if (!CanGoToNextPage)
            {
                return;
            }

            CurrentPage++;
        }

        protected void SetPagedItems(IEnumerable<T> source, ObservableCollection<T> target, bool resetPage = true)
        {
            _pagedSource = source.ToList();
            _pagedTarget = target;

            var resolvedTotalPages = Math.Max(1, (int)Math.Ceiling(_pagedSource.Count / (double)DefaultPageSize));
            TotalFilteredItems = _pagedSource.Count;
            TotalPages = resolvedTotalPages;

            if (resetPage)
            {
                CurrentPage = 1;
            }
            else
            {
                var clampedPage = Math.Clamp(CurrentPage, 1, TotalPages);
                if (clampedPage != CurrentPage)
                {
                    CurrentPage = clampedPage;
                }
            }

            RefreshPagedItems();
        }

        partial void OnCurrentPageChanged(int value)
        {
            RefreshPagedItems();
        }

        private void RefreshPagedItems()
        {
            if (_pagedTarget != null)
            {
                _pagedTarget.Clear();
                foreach (var item in _pagedSource
                             .Skip((Math.Max(CurrentPage, 1) - 1) * DefaultPageSize)
                             .Take(DefaultPageSize))
                {
                    _pagedTarget.Add(item);
                }
            }

            NotifyPaginationStateChanged();
        }

        private void NotifyPaginationStateChanged()
        {
            OnPropertyChanged(nameof(CanGoToPreviousPage));
            OnPropertyChanged(nameof(CanGoToNextPage));
            OnPropertyChanged(nameof(ShowPagination));
            OnPropertyChanged(nameof(PaginationSummary));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }
}
