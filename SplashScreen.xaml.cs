using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace PicSerial
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += SplashScreen_Loaded;
        }

        private async void SplashScreen_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade in logo and text
            FadeIn(SplashImage, 1.0, 800);
            FadeIn(LoadingText, 1.0, 800);
            AnimateDots();

            // Keep splash visible for 2.5 seconds
            await Task.Delay(2500);

            // Fade out
            FadeOut(SplashImage, 0.0, 600);
            FadeOut(LoadingText, 0.0, 600);

            await Task.Delay(700);

            // Open main window
            var main = new MainWindow();
            main.Show();
            Close();
        }

        private void FadeIn(UIElement element, double to, int duration)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(duration));
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void FadeOut(UIElement element, double to, int duration)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(duration));
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private async void AnimateDots()
        {
            var dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5 };

            while (IsVisible)
            {
                foreach (var dot in dots)
                {
                    var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
                    dot.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    await Task.Delay(200);

                    var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
                    dot.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            }
        }

    }
}
