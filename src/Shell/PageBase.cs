using System.Windows;
using System.Windows.Controls;
using Whittle.Core;

namespace Whittle.Shell
{
    /// <summary>A page builds its view once and refreshes parts of it when the shared state changes.</summary>
    public abstract class PageBase
    {
        protected readonly MainWindow Win;
        FrameworkElement view;

        protected PageBase(MainWindow win)
        {
            Win = win;
        }

        protected AppState State { get { return AppState.Current; } }

        public FrameworkElement View
        {
            get
            {
                if (view == null) view = Build();
                return view;
            }
        }

        public bool IsBuilt { get { return view != null; } }

        protected abstract FrameworkElement Build();

        /// <summary>Called every time the page becomes visible.</summary>
        public virtual void OnShown() { }

        /// <summary>Wraps page content in the standard scrolling container with page margins.</summary>
        protected static ScrollViewer Scroller(UIElement content)
        {
            // Fills the width up to 1060 px and stays left-aligned beyond that, like Windows Settings.
            Grid wrap = new Grid();
            wrap.Margin = new Thickness(40, 30, 40, 48);
            ColumnDefinition col = new ColumnDefinition();
            col.Width = new GridLength(1, GridUnitType.Star);
            col.MaxWidth = 1060;
            wrap.ColumnDefinitions.Add(col);
            wrap.Children.Add(content);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.CanContentScroll = false;
            sv.Focusable = false;
            sv.Content = wrap;
            return sv;
        }
    }
}
