using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CopilotGameBarBridge
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); Suspending += OnSuspending; }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var frame = Window.Current.Content as Frame ?? new Frame();
            Window.Current.Content = frame;
            if (frame.Content == null) frame.Navigate(typeof(WidgetPage), null);
            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args is XboxGameBarWidgetActivatedEventArgs widgetArgs)
            {
                var frame = Window.Current.Content as Frame ?? new Frame();
                Window.Current.Content = frame;
                frame.Navigate(typeof(WidgetPage), widgetArgs);
                Window.Current.Activate();
                return;
            }
            base.OnActivated(args);
        }

        private void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e) { }
    }
}
