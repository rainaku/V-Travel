using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI.Views
{
    public partial class CustomerView : UserControl
    {
        private const double TourLoadMoreThreshold = 260;
        private bool _isDraggingDetailsCard;
        private Point _dragStartPoint;
        private double _dragStartOffsetY;
        private DateTime _lastSampleTime;
        private double _lastSampleY;
        private double _velocityY;

        public CustomerView()
        {
            InitializeComponent();
        }

        private void ExploreScrollViewer_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                UpdateExploreFadeOverlays(scrollViewer);
            }
        }

        private void ExploreScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            UpdateExploreFadeOverlays(scrollViewer);

            // Load next chunk only when the user actively scrolls downward.
            if (e.VerticalChange > 0)
            {
                TryLoadMoreTours(scrollViewer);
            }
        }

        private void TourDetailsHero_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingDetailsCard = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartOffsetY = TourDetailsTranslateTransform.Y;
            _lastSampleTime = DateTime.UtcNow;
            _lastSampleY = _dragStartOffsetY;
            _velocityY = 0;

            StopModalAnimations();
            TourDetailsHero.CaptureMouse();
            e.Handled = true;
        }

        private void TourDetailsHero_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingDetailsCard || !TourDetailsHero.IsMouseCaptured)
            {
                return;
            }

            var current = e.GetPosition(this);
            var deltaY = current.Y - _dragStartPoint.Y;

            // Add resistance for upward drag so motion stays controlled and subtle.
            if (deltaY < 0)
            {
                deltaY *= 0.35;
            }

            var nextY = _dragStartOffsetY + deltaY;
            TourDetailsTranslateTransform.Y = nextY;

            var absY = Math.Abs(nextY);
            var scale = Math.Max(0.965, 1 - (absY / 1800));
            TourDetailsScaleTransform.ScaleX = scale;
            TourDetailsScaleTransform.ScaleY = scale;
            TourDetailsOverlay.Opacity = Math.Max(0.55, 1 - (absY / 380));

            var now = DateTime.UtcNow;
            var dt = (now - _lastSampleTime).TotalSeconds;
            if (dt > 0.0001)
            {
                _velocityY = (nextY - _lastSampleY) / dt;
                _lastSampleTime = now;
                _lastSampleY = nextY;
            }

            e.Handled = true;
        }

        private void TourDetailsHero_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingDetailsCard)
            {
                return;
            }

            TourDetailsHero.ReleaseMouseCapture();
            FinishDragInteraction();
            e.Handled = true;
        }

        private void TourDetailsHero_OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDraggingDetailsCard)
            {
                FinishDragInteraction();
            }
        }

        private void FinishDragInteraction()
        {
            _isDraggingDetailsCard = false;

            var shouldDismiss = TourDetailsTranslateTransform.Y > 120 || _velocityY > 900;
            if (shouldDismiss)
            {
                AnimateDismissWithVelocity();
                return;
            }

            AnimateRestoreWithSpring();
        }

        private void AnimateRestoreWithSpring()
        {
            var speed = Math.Min(Math.Abs(_velocityY), 2400);
            var durationMs = Math.Max(180, 320 - (speed / 20));
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var spring = new BackEase { Amplitude = 0.08, EasingMode = EasingMode.EaseOut };

            TourDetailsTranslateTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = spring
                });

            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation
                {
                    To = 1,
                    Duration = duration,
                    EasingFunction = spring
                });

            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation
                {
                    To = 1,
                    Duration = duration,
                    EasingFunction = spring
                });

            TourDetailsOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation
                {
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(durationMs * 0.9),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void AnimateDismissWithVelocity()
        {
            var currentY = TourDetailsTranslateTransform.Y;
            var projected = currentY + Math.Max(220, _velocityY * 0.12);
            var targetY = Math.Max(projected, 420);
            var speed = Math.Min(Math.Abs(_velocityY), 2400);
            var durationMs = Math.Max(140, 240 - (speed / 20));
            var duration = TimeSpan.FromMilliseconds(durationMs);

            var dismissAnimation = new DoubleAnimation
            {
                To = targetY,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            dismissAnimation.Completed += (_, _) =>
            {
                if (DataContext is CustomerViewModel vm && vm.CloseTourDetailsCommand.CanExecute(null))
                {
                    vm.CloseTourDetailsCommand.Execute(null);
                }

                ResetModalVisualState();
            };

            TourDetailsTranslateTransform.BeginAnimation(TranslateTransform.YProperty, dismissAnimation);
            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation
                {
                    To = 0.96,
                    Duration = duration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation
                {
                    To = 0.96,
                    Duration = duration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            TourDetailsOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(durationMs * 0.9),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
        }

        private void StopModalAnimations()
        {
            TourDetailsTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TourDetailsScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TourDetailsOverlay.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private void ResetModalVisualState()
        {
            StopModalAnimations();
            TourDetailsTranslateTransform.Y = 10;
            TourDetailsScaleTransform.ScaleX = 0.975;
            TourDetailsScaleTransform.ScaleY = 0.975;
            TourDetailsOverlay.Opacity = 0;
        }

        private void TryLoadMoreTours(ScrollViewer scrollViewer)
        {
            if (DataContext is not CustomerViewModel vm)
            {
                return;
            }

            var remainingDistance = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
            if (remainingDistance <= TourLoadMoreThreshold && vm.LoadMoreToursCommand.CanExecute(null))
            {
                vm.LoadMoreToursCommand.Execute(null);
            }
        }

        private void UpdateExploreFadeOverlays(ScrollViewer scrollViewer)
        {
            if (ExploreTopFadeOverlay == null || ExploreBottomFadeOverlay == null)
            {
                return;
            }

            if (scrollViewer.ScrollableHeight <= 1)
            {
                ExploreTopFadeOverlay.Opacity = 0;
                ExploreBottomFadeOverlay.Opacity = 0;
                return;
            }

            var topOpacity = Math.Min(scrollViewer.VerticalOffset / 36d, 1d);
            var bottomDistance = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
            var bottomOpacity = Math.Min(bottomDistance / 44d, 1d);

            ExploreTopFadeOverlay.Opacity = topOpacity;
            ExploreBottomFadeOverlay.Opacity = bottomOpacity;
        }
    }
}
