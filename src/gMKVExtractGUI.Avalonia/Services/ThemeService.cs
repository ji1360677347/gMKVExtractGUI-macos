using Avalonia;
using Avalonia.Styling;

namespace gMKVToolNix.UI.Services;

public static class ThemeService
{
    public static void ApplyCurrentTheme()
    {
        Apply(SettingsService.Instance.Current.UseDarkTheme);
    }

    public static void Apply(bool useDarkTheme)
    {
        if (Application.Current is null) return;

        Application.Current.RequestedThemeVariant = useDarkTheme
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }
}
