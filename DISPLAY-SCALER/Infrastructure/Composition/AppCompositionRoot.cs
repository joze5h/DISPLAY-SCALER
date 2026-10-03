using DISPLAY_SCALER.Infrastructure.Diagnostics;
using DISPLAY_SCALER.Services;
using DISPLAY_SCALER.Services.Dialogs;
using DISPLAY_SCALER.Services.Display;
using DISPLAY_SCALER.Services.Shell;
using DISPLAY_SCALER.ViewModels;
using DISPLAY_SCALER.Views;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DISPLAY_SCALER.Infrastructure.Composition
{
    internal sealed class AppCompositionRoot
    {
        private readonly IAppLogger _logger;
        private readonly NvApiService _nvApi;
        private readonly DisplayService _displayService;
        private readonly DisplayDriverRestartService _driverRestartService;
        private readonly ResolutionProfileService _profileService;
        private readonly DisplayOperationCoordinator _displayOperationCoordinator;
        private readonly ICustomResolutionDialogService _customResolutionDialogService;
        private readonly IResolutionProfileDialogService _profileDialogService;
        private readonly IExternalLinkService _externalLinkService;

        public AppCompositionRoot()
        {
            _logger = new DebugLogger();
            _nvApi = NvApiService.Instance;
            _displayService = new DisplayService(_nvApi);
            _driverRestartService = new DisplayDriverRestartService();
            _profileService = new ResolutionProfileService(_displayService, _nvApi, _driverRestartService);
            _displayOperationCoordinator = new DisplayOperationCoordinator();
            _customResolutionDialogService = new WindowCustomResolutionDialogService();
            _profileDialogService = new WindowResolutionProfileDialogService();
            _externalLinkService = new ExternalLinkService();
        }

        public void InitializeNativeServices()
        {
            try
            {
                _nvApi.Initialize();
            }
            catch (Exception ex)
            {
                _logger.Error("NVAPI initialization failed", ex);
            }
        }

        public void RegisterGlobalExceptionHandlers(Application app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.DispatcherUnhandledException += (sender, args) =>
            {
                _profileService.LogDeveloperProblem("Unhandled WPF dispatcher exception", null, null, args.Exception.Message, null, args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception exception = args.ExceptionObject as Exception;
                _profileService.LogDeveloperProblem("Unhandled AppDomain exception", null, null, exception == null ? "Non-Exception object" : exception.Message, null, exception);
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                _profileService.LogDeveloperProblem("Unobserved task exception", null, null, args.Exception.Message, null, args.Exception);
            };
        }

        public MainWindow CreateMainWindow()
        {
            var viewModel = new MainViewModel(
                _displayService,
                _nvApi,
                _customResolutionDialogService,
                _profileDialogService,
                _profileService,
                _displayOperationCoordinator,
                _logger,
                _externalLinkService,
                _driverRestartService);

            return new MainWindow
            {
                DataContext = viewModel
            };
        }
    }
}