using System;
using System.Collections.Generic;
using System.Linq;

namespace Nexora.Services
{
    public sealed class PaginationState<T>
    {
        private IReadOnlyList<T> _items = new List<T>();

        public PaginationState(int pageSize)
        {
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
            PageSize = pageSize;
        }

        public int PageSize { get; private set; }
        public int CurrentPage { get; private set; } = 1;
        public int PageCount => Math.Max(1, (int)Math.Ceiling(_items.Count / (double)PageSize));
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < PageCount;
        public bool HasMultiplePages => _items.Count > PageSize;
        public string PageLabel => CurrentPage + " / " + PageCount;

        public void SetPageSize(int pageSize, bool resetToFirstPage = false)
        {
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (PageSize == pageSize && !resetToFirstPage) return;

            PageSize = pageSize;
            CurrentPage = resetToFirstPage ? 1 : Math.Min(CurrentPage, PageCount);
        }

        public void SetItems(IEnumerable<T> items, bool resetToFirstPage = false)
        {
            _items = (items ?? Enumerable.Empty<T>()).ToList();

            if (resetToFirstPage)
                CurrentPage = 1;
            else
                CurrentPage = Math.Min(CurrentPage, PageCount);
        }

        public IReadOnlyList<T> GetCurrentPageItems()
        {
            return _items.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        }

        public bool MovePrevious()
        {
            if (!HasPreviousPage) return false;
            CurrentPage--;
            return true;
        }

        public bool MoveNext()
        {
            if (!HasNextPage) return false;
            CurrentPage++;
            return true;
        }
    }
}
