namespace dboot.Builder.Options
{
    public class InstallOptions
    {
        public string AppName { get; set; }
        public Guid UninstallGuid { get; set; }
        public string Publisher { get; set; }
        public string FormattedUninstallGuid => $"{{{UninstallGuid.ToString().ToUpper()}}}";
        public string UninstallRegistryPath => $"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{FormattedUninstallGuid}";
        public string AppRegistryPath => $"SOFTWARE\\{AppName}";
        public string DisplayIcon { get; set; } = "Assets\\dolus.ico";
        public string HelpLink { get; set; } = "https://dolus.app/";
        public string URLInfoAbout { get; set; } = "https://dolus.app/about";
        public InstallOptions()
        {
            // Default constructor
        }
        public InstallOptions(string appName, Guid uninstallGuid, string publisher, string displayIcon, string helpLink, string urlInfoAbout)
           : this(appName, uninstallGuid, publisher, "")
        {
            DisplayIcon = displayIcon;
            HelpLink = helpLink;
            URLInfoAbout = urlInfoAbout;
        }

        public InstallOptions(string appName, Guid uninstallGuid, string publisher, string directory)
        {
            AppName = appName;
            UninstallGuid = uninstallGuid;
            Publisher = publisher;
        }

        // Optional: Override Equals and GetHashCode for value equality comparisons
        public override bool Equals(object? obj)
        {
            return obj is InstallOptions options &&
                   AppName == options.AppName &&
                   UninstallGuid.Equals(options.UninstallGuid) &&
                   Publisher == options.Publisher;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AppName, UninstallGuid, Publisher);
        }

        // Optional: Override ToString for easy debugging
        public override string ToString()
        {
            return $"InstallOptions {{ AppName = {AppName}, UninstallGuid = {UninstallGuid}, Publisher = {Publisher} }}";
        }
    }
}