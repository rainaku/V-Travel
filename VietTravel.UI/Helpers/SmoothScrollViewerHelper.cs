using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace VietTravel.UI.Helpers
{
    public static class SmoothScrollViewerHelper
    {
        private static bool _globalEnabled;

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
                new PropertyMetadata(84d));

        public static readonly DependencyProperty DurationMsProperty =
            DependencyProperty.RegisterAttached(
                "DurationMs",
                typeof(double),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(220d));

        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedVerticalOffset",
                typeof(double),
                typeof(SmoothScrollViewerHelper),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

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

        public static void EnableGlobalSmoothScroll(double wheelStep = 88d, double durationMs = 220d)
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
                }
                else
                {
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
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

            var current = GetAnimatedVerticalOffset(scrollViewer);
            if (Math.Abs(current - scrollViewer.VerticalOffset) > 2)
            {
                current = scrollViewer.VerticalOffset;
                SetAnimatedVerticalOffset(scrollViewer, current);
            }

            var wheelStep = Math.Max(24, GetWheelStep(scrollViewer));
            var target = current - (e.Delta / 120.0) * wheelStep;
            target = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, target));

            var atTopAndScrollingUp = scrollViewer.VerticalOffset <= 0 && e.Delta > 0;
            var atBottomAndScrollingDown = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight && e.Delta < 0;
            if (atTopAndScrollingUp || atBottomAndScrollingDown)
            {
                return;
            }

            e.Handled = true;
            scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);

            var animation = new DoubleAnimation
            {
                From = current,
                To = target,
                Duration = TimeSpan.FromMilliseconds(Math.Max(120, GetDurationMs(scrollViewer))),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, animation, HandoffBehavior.Compose);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }
}
