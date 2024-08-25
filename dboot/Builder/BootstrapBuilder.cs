using dboot.Builder.Options;
using dboot.Core;
using dboot.Core.Http;
using dboot.SubSystem;
using Serilog;
using Serilog.Events;

namespace dboot.Builder
{
    public class BootstrapBuilder
    {
        private DialogOptions _dialogOptions;
        private readonly List<StepFunction> _installSteps;
        private readonly List<StepFunction> _uninstallSteps;
        // Components for BootContext
        private UpdateClient? _updateClient;
        private InstallOptions? _installOptions;
        private readonly BootstrapperOptions _bootstrapperOptions;
        private Func<ValueTask>? _postInstallCallback;

        private Func<ValueTask>? _postUnInstallCallback;

        public BootstrapBuilder()
        {
            _dialogOptions = new DialogOptions();
            _installSteps = [];
            _uninstallSteps = [];
            _bootstrapperOptions = new BootstrapperOptions();
        }

        public BootstrapBuilder ConfigureDialog(Action<DialogOptions> configureOptions)
        {
            var options = new DialogOptions();
            configureOptions(options);
            _dialogOptions = options;
            return this;
        }



        public BootstrapBuilder UpdateClient(string baseUrl)
        {
            _updateClient = new UpdateClient(baseUrl);
            return this;
        }

        public BootstrapBuilder WithInstallInfo(Action<InstallOptions> configureInstallInfo)
        {
            var info = new InstallOptions();
            configureInstallInfo(info);
            _installOptions = info;
            return this;
        }

        public BootstrapBuilder ConfigureInstall(Action<InstallStepBuilder> configureInstall)
        {
            var stepBuilder = new InstallStepBuilder(_installSteps);
            configureInstall(stepBuilder);
            return this;
        }

        public BootstrapBuilder ConfigureUninstall(Action<UninstallStepBuilder> configureUninstall)
        {
            var stepBuilder = new UninstallStepBuilder(_uninstallSteps);
            configureUninstall(stepBuilder);
            return this;
        }

        public BootstrapBuilder IsQuiet()
        {
            _bootstrapperOptions.IsQuiet = true;
            return this;
        }

        public BootstrapBuilder OnPostInstall(Func<ValueTask> callback)
        {
            _postInstallCallback = callback;
            return this;
        }

        public BootstrapBuilder OnPostUnInstall(Func<ValueTask> callback)
        {
            _postUnInstallCallback = callback;
            return this;
        }

        public BootstrapBuilder WithLogging(string logName, bool enableConsoleLogging = false)
        {
            var config = new LoggerConfiguration();
            config.WriteTo.Sentry(o =>
            {
                o.Dsn = "https://b0936cf2bfa887b863f9f7499e8a2338@o4506983021215744.ingest.us.sentry.io/4507826377392128";
                // Debug and higher are stored as breadcrumbs (default is Information)
                o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                // Warning and higher is sent as event (default is Error)
                o.MinimumEventLevel = LogEventLevel.Warning;
            });
            if (enableConsoleLogging && ConsoleManager.SetupConsole())
            {
                config.WriteTo.Console();
            }
            if (!string.IsNullOrEmpty(logName))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                config.WriteTo.File(Path.Combine(appData, logName), rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true);
            }
            Log.Logger = config.CreateLogger();

            return this;
        }

        public Bootstrapper Build()
        {
            var bootContext = new Context(
               _updateClient ?? throw new InvalidOperationException("UpdateClient must be configured."),
               _installOptions ?? throw new InvalidOperationException("InstallInfo must be configured."),
               []
           );
            return new Bootstrapper(_dialogOptions, _installSteps, _uninstallSteps, bootContext, _bootstrapperOptions, _postInstallCallback, _postUnInstallCallback);
        }
    }
}