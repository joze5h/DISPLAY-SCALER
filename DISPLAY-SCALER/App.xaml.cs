using DISPLAY_SCALER.Infrastructure.Composition;
using System.Windows;

namespace DISPLAY_SCALER
{
    public partial class App : Application
    {
        private AppCompositionRoot _compositionRoot;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _compositionRoot = new AppCompositionRoot();
            _compositionRoot.InitializeNativeServices();
            _compositionRoot.RegisterGlobalExceptionHandlers(this);

            MainWindow = _compositionRoot.CreateMainWindow();
            MainWindow.Show();
        }
    }
}