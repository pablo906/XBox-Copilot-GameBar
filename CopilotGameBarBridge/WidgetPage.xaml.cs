using Microsoft.Gaming.XboxGameBar;
using Microsoft.Web.WebView2.Core;
using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace CopilotGameBarBridge
{
    public sealed partial class WidgetPage : Page
    {
        private static readonly Uri CopilotHome = new Uri("https://copilot.microsoft.com/");

        private XboxGameBarWidget _widget;
        private bool _initialized;
        private WebView2 _popupView;

        public WidgetPage()
        {
            // WinUI 2's WebView2 has no DefaultBackgroundColor property; this keeps it dark (not white) while pages load.
            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "FF171717");
            // Sign in with the account you use for Windows instead of typing it again. WinUI 2's WebView2 can't take
            // CoreWebView2EnvironmentOptions, so this is the browser flag behind AllowSingleSignOnUsingOSPrimaryAccount.
            // It also needs the enterpriseCloudSSO capability in the manifest.
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--enable-features=msSingleSignOnOSForPrimaryAccountIsShared");
            InitializeComponent();
            Loaded += WidgetPage_Loaded;
        }

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

        private async void WidgetPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                await ChatView.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                ShowError("The Microsoft Edge WebView2 runtime isn't available (" + ex.Message + ").");
                return;
            }

            var core = ChatView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.DocumentTitleChanged += (s, _) => StatusText.Text = string.IsNullOrEmpty(s.DocumentTitle) ? "Copilot" : s.DocumentTitle;
            core.ProcessFailed += (s, args) => ShowError("The chat view stopped unexpectedly (" + args.ProcessFailedKind + ").");

            ChatView.NavigationStarting += (s, args) =>
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
            };
            ChatView.NavigationCompleted += ChatView_NavigationCompleted;

            ChatView.Source = CopilotHome;
        }

        private void ChatView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            BackButton.IsEnabled = ChatView.CanGoBack;

            if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            {
                ShowError("Check your connection and try again (" + args.WebErrorStatus + ").");
                return;
            }

            // Put the keyboard in the chat box's page so typing works as soon as the widget opens.
            ChatView.Focus(FocusState.Programmatic);
        }

        // Sign-in flows open popups. Without this they'd open as separate windows outside Game Bar,
        // so host them in an overlay inside the widget and keep window.opener working.
        private async void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                // A new window must be a fresh, never-navigated WebView, so build one per popup.
                ClosePopup();
                var popup = new WebView2();
                PopupHost.Children.Add(popup);
                _popupView = popup;
                await popup.EnsureCoreWebView2Async();
                popup.CoreWebView2.WindowCloseRequested += (s, _) => ClosePopup();
                popup.CoreWebView2.DocumentTitleChanged += (s, _) => PopupTitle.Text = s.DocumentTitle;
                popup.CoreWebView2.NewWindowRequested += (s, nested) =>
                {
                    // Nested popups just navigate in place.
                    nested.Handled = true;
                    s.Navigate(nested.Uri);
                };

                PopupTitle.Text = args.Uri;
                PopupLayer.Visibility = Visibility.Visible;
                args.NewWindow = popup.CoreWebView2;
                args.Handled = true;
            }
            catch
            {
                // Fall back to navigating the main view rather than spawning an external window.
                args.Handled = true;
                sender.Navigate(args.Uri);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ClosePopup()
        {
            if (_popupView == null) return;
            PopupLayer.Visibility = Visibility.Collapsed;
            PopupHost.Children.Remove(_popupView);
            _popupView.Close();
            _popupView = null;
            ChatView.Focus(FocusState.Programmatic);
        }

        private void ShowError(string message)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (ChatView.CanGoBack) ChatView.GoBack();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (ChatView.CoreWebView2 == null) return;
            if (ErrorPanel.Visibility == Visibility.Visible) ChatView.Source = CopilotHome;
            else ChatView.Reload();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            if (ChatView.CoreWebView2 != null) ChatView.Source = CopilotHome;
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e) => ClosePopup();

        private async void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            if (!await Launcher.LaunchUriAsync(new Uri("ms-copilot:")))
                await Launcher.LaunchUriAsync(CopilotHome);
        }
    }
}
