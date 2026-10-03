using DISPLAY_SCALER.Infrastructure.Diagnostics;
using DISPLAY_SCALER.Infrastructure.Mvvm;
using DISPLAY_SCALER.Models;
using DISPLAY_SCALER.Services;
using DISPLAY_SCALER.Services.Dialogs;
using DISPLAY_SCALER.Services.Display;
using DISPLAY_SCALER.Services.Profiles;
using DISPLAY_SCALER.Services.Shell;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DISPLAY_SCALER.ViewModels
{
    public sealed partial class MainViewModel : ViewModelBase
    {
        private readonly IDisplayService _display;
        private readonly NvApiService _nv;
        private readonly ICustomResolutionDialogService _customResolutionDialog;
        private readonly IResolutionProfileDialogService _profileDialog;
        private readonly IResolutionProfileService _profiles;
        private readonly DisplayOperationCoordinator _displayOperations;
        private readonly IAppLogger _logger;
        private readonly IExternalLinkService _externalLinks;
        private readonly DisplayDriverRestartService _driverRestart;

        private MonitorInfo _selectedMonitor;
        private GpuInfo _selectedGpu;
        private int _saturation = 50;
        private bool _saturationSupported;
        private string _statusMessage = "Готово";
        private bool _isBusy;
        private bool _nvidiaAvailable;
        private string _nvidiaStatus;
        private CustomResolution _selectedCustomResolution;
        private CustomResolution _selectedDisplayScalerResolution;
        private CustomResolution _selectedExternalCustomResolution;
        private CustomResolution _selectedSystemResolution;
        private CustomResolution _clipboardResolution;
        private MonitorInfo _clipboardSourceMonitor;
        private DisplaySettingOption _selectedRefreshRateOption;
        private DisplaySettingOption _selectedColorDepthOption;
        private bool _syncingResolutionSelection;
        private int _selectedMonitorLoadGeneration;
        private Task<bool> _selectedMonitorLoadTask = Task.FromResult(true);
        private const string DwtTelegramUrl = "https://t.me/dwtweaking";

        public MainViewModel()
            : this(new DisplayService(NvApiService.Instance), NvApiService.Instance, new WindowCustomResolutionDialogService(), new WindowResolutionProfileDialogService(), null)
        {
        }

        public MainViewModel(IDisplayService display, NvApiService nv, ICustomResolutionDialogService customResolutionDialog, IResolutionProfileDialogService profileDialog, IResolutionProfileService profiles)
            : this(display, nv, customResolutionDialog, profileDialog, profiles, null, null, null)
        {
        }

        public MainViewModel(
            IDisplayService display,
            NvApiService nv,
            ICustomResolutionDialogService customResolutionDialog,
            IResolutionProfileDialogService profileDialog,
            IResolutionProfileService profiles,
            DisplayOperationCoordinator displayOperations,
            IAppLogger logger,
            IExternalLinkService externalLinks,
            DisplayDriverRestartService driverRestart = null)
        {
            _nv = nv ?? throw new ArgumentNullException(nameof(nv));
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _customResolutionDialog = customResolutionDialog ?? throw new ArgumentNullException(nameof(customResolutionDialog));
            _profileDialog = profileDialog ?? throw new ArgumentNullException(nameof(profileDialog));
            _driverRestart = driverRestart ?? new DisplayDriverRestartService();
            _profiles = profiles ?? new ResolutionProfileService(_display, _nv, _driverRestart);
            _displayOperations = displayOperations ?? new DisplayOperationCoordinator();
            _logger = logger ?? new DebugLogger();
            _externalLinks = externalLinks ?? new ExternalLinkService();

            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy, OnCommandException);
            RestartGpuDriverCommand = new AsyncRelayCommand(RestartGpuDriverAsync, () => !IsBusy, OnCommandException);
            FullDisplayResetCommand = new AsyncRelayCommand(FullDisplayResetAsync, () => !IsBusy, OnCommandException);
            AddCustomResolutionCommand = new AsyncRelayCommand(OpenAddDialogAsync, () => SelectedMonitor != null && !IsBusy, OnCommandException);
            EditCustomResolutionCommand = new AsyncRelayCommand(EditCustomResolutionAsync, () => SelectedMonitor != null && SelectedCustomResolution != null && SelectedCustomResolution.CanEdit && !IsBusy, OnCommandException);
            RemoveCustomResolutionCommand = new AsyncRelayCommand(RemoveCustomResolutionAsync, () => SelectedMonitor != null && SelectedCustomResolution != null && SelectedCustomResolution.CanDelete && !IsBusy, OnCommandException);
            ClearResolutionSelectionCommand = new Command(ClearResolutionSelection, () => SelectedCustomResolution != null && !IsBusy);
            ExportProfileCommand = new AsyncRelayCommand(ExportProfileAsync, () => SelectedMonitor != null && SelectedCustomResolution != null && !IsBusy, OnCommandException);
            ImportProfileCommand = new AsyncRelayCommand(ImportProfileAsync, () => SelectedMonitor != null && !IsBusy, OnCommandException);
            CopyResolutionCommand = new Command(CopySelectedResolution, () => SelectedCustomResolution != null && !IsBusy);
            PasteResolutionCommand = new AsyncRelayCommand(PasteResolutionAsync, () => SelectedMonitor != null && _clipboardResolution != null && !IsBusy, OnCommandException);
            ApplyPresetCommand = new AsyncRelayCommand<EsportsPreset>(ApplyPresetAsync, p => SelectedMonitor != null && p != null && !IsBusy, OnCommandException);
            ResetSaturationCommand = new AsyncRelayCommand(ResetSaturationAsync, () => SaturationSupported && !IsBusy, OnCommandException);
            ApplySaturationCommand = new AsyncRelayCommand(() => ApplySaturationAsync(Saturation), () => SaturationSupported && !IsBusy, OnCommandException);
            ApplyRefreshRateCommand = new AsyncRelayCommand(ApplyRefreshRateAsync, () => SelectedMonitor != null && SelectedRefreshRateOption != null && !IsBusy, OnCommandException);
            ApplyColorDepthCommand = new AsyncRelayCommand(ApplyColorDepthAsync, () => SelectedMonitor != null && SelectedColorDepthOption != null && !IsBusy, OnCommandException);
            OpenDwtLinkCommand = new Command(OpenDwtLink);

            UpdateNvidiaStatus();
            _ = RefreshAsync();
        }

        public ObservableCollection<MonitorInfo> Monitors { get; } = new ObservableCollection<MonitorInfo>();
        public ObservableCollection<GpuInfo> Gpus { get; } = new ObservableCollection<GpuInfo>();
        public ObservableCollection<CustomResolution> DisplayScalerCustomResolutions { get; } = new ObservableCollection<CustomResolution>();
        public ObservableCollection<CustomResolution> ExternalCustomResolutions { get; } = new ObservableCollection<CustomResolution>();
        public ObservableCollection<CustomResolution> SystemResolutions { get; } = new ObservableCollection<CustomResolution>();
        public ObservableCollection<DisplaySettingOption> RefreshRateOptions { get; } = new ObservableCollection<DisplaySettingOption>();
        public ObservableCollection<DisplaySettingOption> ColorDepthOptions { get; } = new ObservableCollection<DisplaySettingOption>();

        public MonitorInfo SelectedMonitor
        {
            get => _selectedMonitor;
            set
            {
                if (Set(ref _selectedMonitor, value))
                {
                    int loadGeneration = ++_selectedMonitorLoadGeneration;

                    if (_selectedMonitor != null)
                    {
                        Saturation = _selectedMonitor.Saturation;
                        SaturationSupported = _selectedMonitor.SaturationSupported;

                        DisplayScalerCustomResolutions.Clear();
                        ExternalCustomResolutions.Clear();
                        SystemResolutions.Clear();
                        RebuildDisplaySettingOptions();
                        ClearResolutionSelection();

                        _selectedMonitorLoadTask = LoadSelectedMonitorDetailsAsync(_selectedMonitor, loadGeneration);
                    }
                    else
                    {
                        _selectedMonitorLoadTask = Task.FromResult(true);
                        SaturationSupported = false;
                        DisplayScalerCustomResolutions.Clear();
                        ExternalCustomResolutions.Clear();
                        SystemResolutions.Clear();
                        RebuildDisplaySettingOptions();
                        ClearResolutionSelection();
                    }

                    OnPropertyChanged(nameof(HasMonitor));
                    OnPropertyChanged(nameof(HasCustomResolutions));
                    OnPropertyChanged(nameof(HasSelectedCustomResolution));
                    OnPropertyChanged(nameof(SelectedCustomResolutionText));
                    OnPropertyChanged(nameof(AdaptivePresetRefreshText));
                    OnPropertyChanged(nameof(HasRefreshRateOptions));
                    OnPropertyChanged(nameof(HasColorDepthOptions));
                    RaisePreviewProperties();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public GpuInfo SelectedGpu
        {
            get => _selectedGpu;
            set => Set(ref _selectedGpu, value);
        }

        public CustomResolution SelectedCustomResolution
        {
            get => _selectedCustomResolution;
            private set
            {
                if (Set(ref _selectedCustomResolution, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCustomResolution));
                    OnPropertyChanged(nameof(SelectedCustomResolutionText));
                    RaisePreviewProperties();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public CustomResolution SelectedDisplayScalerResolution
        {
            get => _selectedDisplayScalerResolution;
            set
            {
                if (Set(ref _selectedDisplayScalerResolution, value))
                {
                    if (_syncingResolutionSelection) return;

                    _syncingResolutionSelection = true;
                    try
                    {
                        if (value != null)
                        {
                            SelectedExternalCustomResolution = null;
                            SelectedSystemResolution = null;
                        }

                        SelectedCustomResolution = value ?? SelectedExternalCustomResolution ?? SelectedSystemResolution;
                    }
                    finally
                    {
                        _syncingResolutionSelection = false;
                    }
                }
            }
        }

        public CustomResolution SelectedExternalCustomResolution
        {
            get => _selectedExternalCustomResolution;
            set
            {
                if (Set(ref _selectedExternalCustomResolution, value))
                {
                    if (_syncingResolutionSelection) return;

                    _syncingResolutionSelection = true;
                    try
                    {
                        if (value != null)
                        {
                            SelectedDisplayScalerResolution = null;
                            SelectedSystemResolution = null;
                        }

                        SelectedCustomResolution = value ?? SelectedDisplayScalerResolution ?? SelectedSystemResolution;
                    }
                    finally
                    {
                        _syncingResolutionSelection = false;
                    }
                }
            }
        }

        public CustomResolution SelectedSystemResolution
        {
            get => _selectedSystemResolution;
            set
            {
                if (Set(ref _selectedSystemResolution, value))
                {
                    if (_syncingResolutionSelection) return;

                    _syncingResolutionSelection = true;
                    try
                    {
                        if (value != null)
                        {
                            SelectedDisplayScalerResolution = null;
                            SelectedExternalCustomResolution = null;
                        }

                        SelectedCustomResolution = value ?? SelectedDisplayScalerResolution ?? SelectedExternalCustomResolution;
                    }
                    finally
                    {
                        _syncingResolutionSelection = false;
                    }
                }
            }
        }

        public DisplaySettingOption SelectedRefreshRateOption
        {
            get => _selectedRefreshRateOption;
            set
            {
                if (Set(ref _selectedRefreshRateOption, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public DisplaySettingOption SelectedColorDepthOption
        {
            get => _selectedColorDepthOption;
            set
            {
                if (Set(ref _selectedColorDepthOption, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public int Saturation
        {
            get => _saturation;
            set => Set(ref _saturation, value);
        }

        public bool SaturationSupported
        {
            get => _saturationSupported;
            set => Set(ref _saturationSupported, value);
        }

        public bool NvidiaAvailable
        {
            get => _nvidiaAvailable;
            set => Set(ref _nvidiaAvailable, value);
        }

        public string NvidiaStatus
        {
            get => _nvidiaStatus;
            set => Set(ref _nvidiaStatus, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (Set(ref _isBusy, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasMonitor => SelectedMonitor != null;
        public bool HasCustomResolutions => DisplayScalerCustomResolutions.Count > 0 || ExternalCustomResolutions.Count > 0 || SystemResolutions.Count > 0;
        public bool HasSelectedCustomResolution => SelectedCustomResolution != null;
        public bool HasClipboardResolution => _clipboardResolution != null;
        public bool HasRefreshRateOptions => RefreshRateOptions.Count > 0;
        public bool HasColorDepthOptions => ColorDepthOptions.Count > 0;

        public string SelectedCustomResolutionText => SelectedCustomResolution == null
            ? "Ничего не выбрано"
            : "Выбрано: " + SelectedCustomResolution.Label;

        public string ClipboardResolutionText => _clipboardResolution == null
            ? "Буфер пуст"
            : "Буфер: " + _clipboardResolution.Label + (_clipboardSourceMonitor != null ? " - " + _clipboardSourceMonitor.DisplayTitle : string.Empty);

        public EsportsPresetGroup[] EsportsPresetGroups => DISPLAY_SCALER.Models.EsportsPresets.Groups;

        public string AdaptivePresetRefreshText => SelectedMonitor != null && SelectedMonitor.CurrentRefreshRate > 0
            ? "@ " + SelectedMonitor.CurrentRefreshText
            : "@ текущая Гц";

        public AsyncRelayCommand RefreshCommand { get; }
        public AsyncRelayCommand RestartGpuDriverCommand { get; }
        public AsyncRelayCommand FullDisplayResetCommand { get; }
        public AsyncRelayCommand AddCustomResolutionCommand { get; }
        public AsyncRelayCommand EditCustomResolutionCommand { get; }
        public AsyncRelayCommand RemoveCustomResolutionCommand { get; }
        public Command ClearResolutionSelectionCommand { get; }
        public AsyncRelayCommand ExportProfileCommand { get; }
        public AsyncRelayCommand ImportProfileCommand { get; }
        public Command CopyResolutionCommand { get; }
        public AsyncRelayCommand PasteResolutionCommand { get; }
        public AsyncRelayCommand<EsportsPreset> ApplyPresetCommand { get; }
        public AsyncRelayCommand ResetSaturationCommand { get; }
        public AsyncRelayCommand ApplySaturationCommand { get; }
        public AsyncRelayCommand ApplyRefreshRateCommand { get; }
        public AsyncRelayCommand ApplyColorDepthCommand { get; }
        public Command OpenDwtLinkCommand { get; }
    }
}
