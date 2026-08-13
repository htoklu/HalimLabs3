namespace HalimLabs.Localization;

public enum AppLanguage
{
    Turkish,
    English
}

public static class AppLanguageExtensions
{
    public static string ToCode(this AppLanguage language) =>
        language == AppLanguage.English ? "en" : "tr";

    public static AppLanguage FromCode(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.Turkish;
}
