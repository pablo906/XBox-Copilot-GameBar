using Microsoft.Gaming.XboxGameBar;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace CopilotGameBarBridge
{
    public sealed partial class WidgetPage : Page
    {
        private XboxGameBarWidget _widget;
        public WidgetPage() { InitializeComponent(); }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is XboxGameBarWidgetActivatedEventArgs args)
            {
                _widget = new XboxGameBarWidget(args, Window.Current.CoreWindow, this.Frame);
                _widget.PinningSupported = true;
                _widget.SettingsSupported = false;
            }
        }

        private void CopyPrompt()
        {
            var package = new DataPackage();
            package.SetText(PromptBox.Text ?? string.Empty);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }

        private async void OpenCopilot_Click(object sender, RoutedEventArgs e)
        {
            var prompt = (PromptBox.Text ?? string.Empty).Trim();
            if (CopyFirstCheckBox.IsChecked == true) CopyPrompt();

            // The installed unified Copilot currently registers ms-copilot on many systems.
            // Query-prefill parameters are not a documented public contract, so failure falls back
            // to opening Copilot without a query, then to the web app.
            bool opened = false;
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                var uri = new Uri("ms-copilot:chat?q=" + Uri.EscapeDataString(prompt));
                opened = await Launcher.LaunchUriAsync(uri);
            }
            if (!opened) opened = await Launcher.LaunchUriAsync(new Uri("ms-copilot:"));
            if (!opened) await Launcher.LaunchUriAsync(new Uri("https://copilot.cloud.microsoft/"));
        }

        private void Copy_Click(object sender, RoutedEventArgs e) => CopyPrompt();
        private void Clear_Click(object sender, RoutedEventArgs e) => PromptBox.Text = string.Empty;
    }
}
