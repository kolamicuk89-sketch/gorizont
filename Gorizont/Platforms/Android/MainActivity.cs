using Android.App;
using Android.Content.PM;

namespace Gorizont.Platforms.Android;

// ConfigurationChanges — чтобы при повороте экрана активити не пересоздавалась,
// а MAUI просто присылал новый размер (наш адаптивный интерфейс)
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
