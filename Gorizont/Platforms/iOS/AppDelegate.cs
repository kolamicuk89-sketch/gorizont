using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Gorizont.Platforms.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
