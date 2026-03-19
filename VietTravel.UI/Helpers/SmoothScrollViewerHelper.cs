using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace VietTravel.UI.Helpers
{
    public static class SmoothScrollViewerHelper
    {
        private static bool _globalEnabled;
        private static readonly HashSet<ScrollViewer> ActiveScrollViewers = new();
        private static bool _isRenderingSubscribed;

        public static readonly DependencyProperty SmoothScrollProperty =
            DependencyProperty.RegisterAttached(
                "SmoothScroll",
                typeof(bool),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(false, OnSmoothScrollChanged));

        public static readonly DependencyProperty WheelStepProperty =
            DependencyProperty.RegisterAttached(
                "WheelStep",
                typeof(double),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(320d));

        public static readonly DependencyProperty DurationMsProperty =
            DependencyProperty.RegisterAttached(
                "DurationMs",
                typeof(double),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(180d));

        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedVerticalOffset",
                typeof(double),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        private static readonly DependencyProperty ScrollStateProperty =
            DependencyProperty.RegisterAttached(
                "ScrollState",
                typeof(ScrollState),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(null));

        public static bool GetSmoothScroll(DependencyObject obj)
        {
            return (bool)obj.GetValue(SmoothScrollProperty);
        }

        public static void SetSmoothScroll(DependencyObject obj, bool value)
        {
            obj.SetValue(SmoothScrollProperty, value);
        }

        public static double GetWheelStep(DependencyObject obj)
        {
            return (double)obj.GetValue(WheelStepProperty);
        }

        public static void SetWheelStep(DependencyObject obj, double value)
        {
            obj.SetValue(WheelStepProperty, value);
        }

        public static double GetDurationMs(DependencyObject obj)
        {
            return (double)obj.GetValue(DurationMsProperty);
        }

        public static void SetDurationMs(DependencyObject obj, double value)
        {
            obj.SetValue(DurationMsProperty, value);
        }

        public static double GetAnimatedVerticalOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(AnimatedVerticalOffsetProperty);
        }

        public static void SetAnimatedVerticalOffset(DependencyObject obj, double value)
        {
            obj.SetValue(AnimatedVerticalOffsetProperty, value);
        }

        public static void EnableGlobalSmoothScroll(double wheelStep = 320d, double durationMs = 85d)
        {
            if (_globalEnabled)
            {
                return;
            }

            _globalEnabled = true;
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) =>
                {
                    if (sender is not ScrollViewer scrollViewer || IsExcludedScrollViewer(scrollViewer))
                    {
                        return;
                    }

                    SetWheelStep(scrollViewer, wheelStep);
                    SetDurationMs(scrollViewer, durationMs);
                    SetSmoothScroll(scrollViewer, true);
                }));
        }

        private static void OnSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    SetAnimatedVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                    scrollViewer.Unloaded += ScrollViewer_Unloaded;
                    EnsureState(scrollViewer);
                }
                else
                {
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                    scrollViewer.Unloaded -= ScrollViewer_Unloaded;
                    StopAnimation(scrollViewer);
                    scrollViewer.ClearValue(ScrollStateProperty);
                }
            }
        }

        private static bool IsExcludedScrollViewer(ScrollViewer scrollViewer)
        {
            if (scrollViewer.Name == "PART_ContentHost")
            {
                return true;
            }

            return scrollViewer.TemplatedParent is TextBoxBase or PasswordBox;
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer || !GetSmoothScroll(scrollViewer))
            {
                return;
            }

            if (IsExcludedScrollViewer(scrollViewer))
            {
                return;
            }

            if (scrollViewer.ScrollableHeight <= 0)
            {
                return;
            }

            var state = EnsureState(scrollViewer);
            state.CurrentOffset = scrollViewer.VerticalOffset;

            var wheelStep = Math.Max(40, GetWheelStep(scrollViewer));
            var target = state.TargetOffset - (e.Delta / 120.0) * wheelStep;
            target = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, target));

            var atTopAndScrollingUp = scrollViewer.VerticalOffset <= 0 && e.Delta > 0;
            var atBottomAndScrollingDown = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight && e.Delta < 0;
            if (atTopAndScrollingUp || atBottomAndScrollingDown)
            {
                return;
            }

            e.Handled = true;
            state.TargetOffset = target;
            state.LastFrameTimestamp = DateTime.UtcNow;
            state.IsAnimating = true;
            StartAnimation(scrollViewer);
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer || !GetSmoothScroll(scrollViewer))
            {
                return;
            }

            var state = EnsureState(scrollViewer);
            var clampedOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset));

            if (state.IsApplyingOffset)
            {
                return;
            }

            // When animation is active, keep target untouched to avoid collapsing motion.
            if (state.IsAnimating)
            {
                return;
            }

            // Keep physics state in sync when user drags scrollbar or content size changes.
            state.CurrentOffset = clampedOffset;
            state.TargetOffset = clampedOffset;
        }

        private static void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            scrollViewer.Unloaded -= ScrollViewer_Unloaded;
            StopAnimation(scrollViewer);
            scrollViewer.ClearValue(ScrollStateProperty);
        }

        private static void StartAnimation(ScrollViewer scrollViewer)
        {
            ActiveScrollViewers.Add(scrollViewer);
            EnsureRenderingSubscription();
        }

        private static void StopAnimation(ScrollViewer scrollViewer)
        {
            ActiveScrollViewers.Remove(scrollViewer);
            var state = scrollViewer.GetValue(ScrollStateProperty) as ScrollState;
            if (state != null)
            {
                state.IsAnimating = false;
            }

            CleanupRenderingSubscription();
        }

        private static void EnsureRenderingSubscription()
        {
            if (_isRenderingSubscribed)
            {
                return;
            }

            CompositionTarget.Rendering += OnRendering;
            _isRenderingSubscribed = true;
        }

        private static void CleanupRenderingSubscription()
        {
            if (!_isRenderingSubscribed || ActiveScrollViewers.Count != 0)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingSubscribed = false;
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            if (ActiveScrollViewers.Count == 0)
            {
                CleanupRenderingSubscription();
                return;
            }

            var now = DateTime.UtcNow;
            var finished = new List<ScrollViewer>();

            foreach (var scrollViewer in new List<ScrollViewer>(ActiveScrollViewers))
            {
                if (!GetSmoothScroll(scrollViewer) || scrollViewer.ScrollableHeight <= 0)
                {
                    finished.Add(scrollViewer);
                    continue;
                }

                var state = EnsureState(scrollViewer);
                if (!state.IsAnimating)
                {
                    finished.Add(scrollViewer);
                    continue;
                }

                var target = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, state.TargetOffset));
                state.TargetOffset = target;

                if (state.LastFrameTimestamp == default)
                {
                    state.LastFrameTimestamp = now;
                }

                var dtSeconds = (now - state.LastFrameTimestamp).TotalSeconds;
                state.LastFrameTimestamp = now;

                dtSeconds = Math.Max(0.001, Math.Min(0.05, dtSeconds));
                var smoothTimeSeconds = Math.Max(0.08, GetDurationMs(scrollViewer) / 1000.0);
                var alpha = 1 - Math.Exp(-dtSeconds / smoothTimeSeconds);

                state.CurrentOffset += (target - state.CurrentOffset) * alpha;

                if (Math.Abs(target - state.CurrentOffset) < 0.15)
                {
                    state.CurrentOffset = target;
                    state.IsAnimating = false;
                    finished.Add(scrollViewer);
                }

                state.IsApplyingOffset = true;
                SetAnimatedVerticalOffset(scrollViewer, state.CurrentOffset);
                state.IsApplyingOffset = false;
            }

            foreach (var scrollViewer in finished)
            {
                ActiveScrollViewers.Remove(scrollViewer);
            }

            CleanupRenderingSubscription();
        }

        private static ScrollState EnsureState(ScrollViewer scrollViewer)
        {
            var state = scrollViewer.GetValue(ScrollStateProperty) as ScrollState;
            if (state != null)
            {
                return state;
            }

            state = new ScrollState
            {
                CurrentOffset = scrollViewer.VerticalOffset,
                TargetOffset = scrollViewer.VerticalOffset,
                LastFrameTimestamp = DateTime.UtcNow
            };
            scrollViewer.SetValue(ScrollStateProperty, state);
            return state;
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        private sealed class ScrollState
        {
            public double CurrentOffset { get; set; }
            public double TargetOffset { get; set; }
            public DateTime LastFrameTimestamp { get; set; }
            public bool IsAnimating { get; set; }
            public bool IsApplyingOffset { get; set; }
        }
    }
}
