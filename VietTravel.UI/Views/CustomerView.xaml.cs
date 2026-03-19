using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using VietTravel.UI.ViewModels;

namespace VietTravel.UI.Views
{
    public partial class CustomerView : UserControl
    {
        private const double TourLoadMoreThreshold = 260;
        private const string SourceUiVideoDirectory = @"D:\CodeShii\Viet-Travel\VietTravel.UI\UI";
        private bool _isDraggingDetailsCard;
        private Point _dragStartPoint;
        private double _dragStartOffsetY;
        private DateTime _lastSampleTime;
        private double _lastSampleY;
        private double _velocityY;
        private readonly Random _videoRandom = new();
        private readonly List<string> _heroVideoSources = new();
        private readonly Queue<string> _heroVideoQueue = new();
        private bool _isSwitchingHeroVideo;
        private bool _hasPlayedHeroVideo;

        public CustomerView()
        {
            InitializeComponent();
            Loaded += CustomerView_OnLoaded;
            Unloaded += CustomerView_OnUnloaded;
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
            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

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

        private void TourDetailsOverlay_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not bool isVisible)
            {
                return;
            }

            if (isVisible)
            {
                AnimateBackgroundBlur(toRadius: 10, durationMs: 220);
                return;
            }

            AnimateBackgroundBlur(toRadius: 0, durationMs: 180);

            if (TourDetailsHero.IsMouseCaptured)
            {
                TourDetailsHero.ReleaseMouseCapture();
            }

            _isDraggingDetailsCard = false;
            ResetModalVisualState();
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

        private void AnimateBackgroundBlur(double toRadius, int durationMs)
        {
            if (CustomerMainContentBlurEffect == null)
            {
                return;
            }

            if (durationMs <= 0)
            {
                CustomerMainContentBlurEffect.Radius = toRadius;
                return;
            }

            CustomerMainContentBlurEffect.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation
                {
                    To = toRadius,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });
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

        private async void CustomerView_OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureHeroVideoSources();
            await PlayNextHeroVideoAsync(useFade: false);
        }

        private void ExploreHeroCardContent_OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateExploreHeroCardClip();
        }

        private void ExploreHeroCardContent_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateExploreHeroCardClip();
        }

        private void UpdateExploreHeroCardClip()
        {
            if (ExploreHeroCardContent == null)
            {
                return;
            }

            var width = ExploreHeroCardContent.ActualWidth;
            var height = ExploreHeroCardContent.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double cornerRadius = 20;
            ExploreHeroCardContent.Clip = new RectangleGeometry(
                new Rect(0, 0, width, height),
                cornerRadius,
                cornerRadius);
        }

        private void CustomerView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopHeroVideo();
        }

        private void ExploreHeroVideo_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _ = PlayNextHeroVideoAsync(useFade: true);
        }

        private void ExploreHeroVideo_OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _ = PlayNextHeroVideoAsync(useFade: true);
        }

        private void EnsureHeroVideoSources()
        {
            if (_heroVideoSources.Count > 0)
            {
                return;
            }

            var candidateDirectories = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "UI"),
                SourceUiVideoDirectory
            };

            var distinctDirectories = candidateDirectories
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 1; i <= 3; i++)
            {
                var fileName = $"{i}.mp4";
                var candidatePath = distinctDirectories
                    .Select(dir => Path.Combine(dir, fileName))
                    .FirstOrDefault(File.Exists);

                if (!string.IsNullOrWhiteSpace(candidatePath))
                {
                    _heroVideoSources.Add(candidatePath);
                }
            }
        }

        private void RefillHeroVideoQueue()
        {
            if (_heroVideoSources.Count == 0)
            {
                return;
            }

            var shuffled = _heroVideoSources
                .OrderBy(_ => _videoRandom.Next())
                .ToList();

            foreach (var source in shuffled)
            {
                _heroVideoQueue.Enqueue(source);
            }
        }

        private string? DequeueNextHeroVideoPath()
        {
            var attemptCount = 0;
            while (attemptCount < _heroVideoSources.Count)
            {
                if (_heroVideoQueue.Count == 0)
                {
                    RefillHeroVideoQueue();
                }

                if (_heroVideoQueue.Count == 0)
                {
                    return null;
                }

                var nextVideoPath = _heroVideoQueue.Dequeue();
                attemptCount++;
                if (!File.Exists(nextVideoPath))
                {
                    continue;
                }

                return nextVideoPath;
            }

            return null;
        }

        private async Task PlayNextHeroVideoAsync(bool useFade)
        {
            if (ExploreHeroVideo == null || _isSwitchingHeroVideo)
            {
                return;
            }

            EnsureHeroVideoSources();
            if (_heroVideoSources.Count == 0)
            {
                return;
            }

            var nextVideoPath = DequeueNextHeroVideoPath();
            if (string.IsNullOrWhiteSpace(nextVideoPath))
            {
                return;
            }

            _isSwitchingHeroVideo = true;
            try
            {
                if (useFade && _hasPlayedHeroVideo)
                {
                    await AnimateOpacityAsync(ExploreHeroVideo, toOpacity: 0, durationMs: 260);
                }
                else
                {
                    ExploreHeroVideo.Opacity = 0;
                }

                ExploreHeroVideo.Stop();
                ExploreHeroVideo.Source = new Uri(nextVideoPath, UriKind.Absolute);
                ExploreHeroVideo.Position = TimeSpan.Zero;
                ExploreHeroVideo.Play();

                await AnimateOpacityAsync(ExploreHeroVideo, toOpacity: 1, durationMs: 420);
                _hasPlayedHeroVideo = true;
            }
            catch
            {
                // Ignore broken frame and continue with next clip on next loop.
            }
            finally
            {
                _isSwitchingHeroVideo = false;
            }
        }

        private static Task AnimateOpacityAsync(UIElement element, double toOpacity, int durationMs)
        {
            if (durationMs <= 0)
            {
                element.Opacity = toOpacity;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var animation = new DoubleAnimation
            {
                To = toOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            animation.Completed += (_, _) => tcs.TrySetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, animation);
            return tcs.Task;
        }

        private void StopHeroVideo()
        {
            if (ExploreHeroVideo == null)
            {
                return;
            }

            ExploreHeroVideo.Stop();
            ExploreHeroVideo.Source = null;
            ExploreHeroVideo.Opacity = 1;
            _isSwitchingHeroVideo = false;
            _hasPlayedHeroVideo = false;
        }

        private static bool IsInteractiveElement(DependencyObject? originalSource)
        {
            while (originalSource != null)
            {
                if (originalSource is ButtonBase ||
                    originalSource is ScrollBar ||
                    originalSource is Slider ||
                    originalSource is TextBox ||
                    originalSource is PasswordBox)
                {
                    return true;
                }

                originalSource = VisualTreeHelper.GetParent(originalSource);
            }

            return false;
        }
    }
}
