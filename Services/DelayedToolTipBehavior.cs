using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WpfApp1.Services
{
    public static class DelayedToolTipBehavior
    {
        private const int DelayMilliseconds = 900;

        private sealed class State
        {
            public FrameworkElement Owner;
            public bool IsMouseOver;
        }

        private static readonly Dictionary<FrameworkElement, State> States = new Dictionary<FrameworkElement, State>();
        private static FrameworkElement _currentOwner;
        private static ToolTip _currentToolTip;
        private static State _pendingState;
        private static readonly DispatcherTimer PendingTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(DelayMilliseconds)
        };

        static DelayedToolTipBehavior()
        {
            PendingTimer.Tick += (_, __) => ShowPendingTooltip();
        }

        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DelayedToolTipBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is FrameworkElement owner)) return;
            if ((bool)e.NewValue)
            {
                if (States.ContainsKey(owner)) return;
                States[owner] = new State { Owner = owner };
                owner.MouseEnter += Owner_MouseEnter;
                owner.MouseLeave += Owner_MouseLeave;
                owner.Unloaded += Owner_Unloaded;
            }
            else
            {
                RemoveState(owner);
            }
        }

        private static void Owner_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!(sender is FrameworkElement owner) || owner.ToolTip == null || !owner.IsEnabled || !States.TryGetValue(owner, out var state)) return;
            state.IsMouseOver = true;
            PendingTimer.Stop();
            if (_currentOwner != null && !ReferenceEquals(_currentOwner, owner)) CloseCurrentTooltip();
            _pendingState = state;
            PendingTimer.Interval = TimeSpan.FromMilliseconds(DelayMilliseconds);
            PendingTimer.Start();
        }

        private static void Owner_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!(sender is FrameworkElement owner)) return;
            if (States.TryGetValue(owner, out var state)) state.IsMouseOver = false;
            if (_pendingState != null && ReferenceEquals(_pendingState.Owner, owner))
            {
                PendingTimer.Stop();
                _pendingState = null;
            }
            if (ReferenceEquals(_currentOwner, owner)) CloseCurrentTooltip();
        }

        private static void ShowPendingTooltip()
        {
            PendingTimer.Stop();
            var state = _pendingState;
            _pendingState = null;
            if (state == null || !state.IsMouseOver || !state.Owner.IsVisible || !state.Owner.IsEnabled || state.Owner.ToolTip == null) return;
            if (_currentOwner != null && !ReferenceEquals(_currentOwner, state.Owner)) CloseCurrentTooltip();

            var tooltip = state.Owner.ToolTip as ToolTip ?? new ToolTip { Content = state.Owner.ToolTip };
            tooltip.PlacementTarget = state.Owner;
            tooltip.StaysOpen = true;
            tooltip.IsOpen = true;
            _currentOwner = state.Owner;
            _currentToolTip = tooltip;
        }

        private static void CloseCurrentTooltip()
        {
            try
            {
                if (_currentToolTip != null) _currentToolTip.IsOpen = false;
            }
            catch
            {
            }
            _currentOwner = null;
            _currentToolTip = null;
        }

        private static void Owner_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement owner) RemoveState(owner);
        }

        private static void RemoveState(FrameworkElement owner)
        {
            if (States.TryGetValue(owner, out var state))
            {
                owner.MouseEnter -= Owner_MouseEnter;
                owner.MouseLeave -= Owner_MouseLeave;
                owner.Unloaded -= Owner_Unloaded;
                States.Remove(owner);
            }
            if (ReferenceEquals(_currentOwner, owner)) CloseCurrentTooltip();
        }
    }
}
