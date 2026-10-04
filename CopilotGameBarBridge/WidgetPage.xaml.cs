using Microsoft.Gaming.XboxGameBar;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private bool _initializing;
        private bool _chatViewUsed;
        private readonly List<WebView2> _popups = new List<WebView2>();

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
            // Construct the widget once, on launch activation; repeat activations reuse it.
            if (_widget == null && e.Parameter is XboxGameBarWidgetActivatedEventArgs args && args.IsLaunchActivation)
            {
                _widget = new XboxGameBarWidget(args, Window.Current.CoreWindow, this.Frame);
                _widget.PinningSupported = true;
                _widget.SettingsSupported = false;
            }
        }

        private async void WidgetPage_Loaded(object sender, RoutedEventArgs e) => await InitializeChatAsync();

        // Safe to call again after any failure: "Try again" lands here until a usable core is configured.
        private async Task InitializeChatAsync()
        {
            if (_initialized || _initializing) return;
            _initializing = true;
            try
            {
                ErrorPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;

                // A WebView2 that failed to initialize, or whose browser process exited, can't be reused,
                // so every retry gets a fresh control (which also means no duplicate event handlers).
                if (_chatViewUsed) ReplaceChatView();
                _chatViewUsed = true;

                Exception initError = null;
                ChatView.CoreWebView2Initialized += (s, args) => initError = args.Exception;
                try
                {
                    await ChatView.EnsureCoreWebView2Async();
                }
                catch (Exception ex)
                {
                    initError = ex;
                }

                // WinUI 2 can report a failed environment through CoreWebView2Initialized and still
                // complete EnsureCoreWebView2Async, so a null core counts as a failure too.
                var core = ChatView.CoreWebView2;
                if (core == null)
                {
                    ShowError("The Microsoft Edge WebView2 runtime isn't available" + (initError != null ? " (" + initError.Message + ")." : "."));
                    return;
                }

                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.NewWindowRequested += (s, args) => OpenPopup(null, args);
                core.DocumentTitleChanged += (s, _) => StatusText.Text = string.IsNullOrEmpty(s.DocumentTitle) ? "Copilot" : s.DocumentTitle;
                core.ProcessFailed += Core_ProcessFailed;

                ChatView.NavigationStarting += (s, args) =>
                {
                    ErrorPanel.Visibility = Visibility.Collapsed;
                    LoadingPanel.Visibility = Visibility.Visible;
                };
                ChatView.NavigationCompleted += ChatView_NavigationCompleted;

                _initialized = true;
                ChatView.Source = CopilotHome;
            }
            finally
            {
                _initializing = false;
            }
        }

        private void ReplaceChatView()
        {
            var parent = (Panel)ChatView.Parent;
            var index = parent.Children.IndexOf(ChatView);
            parent.Children.RemoveAt(index);
            try { ChatView.Close(); } catch { }
            ChatView = new WebView2();
            parent.Children.Insert(index, ChatView);
        }

        private void Core_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
        {
            if (args.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                // The core (and every popup sharing its browser process) is gone; Try again rebuilds from scratch.
                ClosePopupsFrom(0);
                _initialized = false;
            }
            ShowError("The chat view stopped unexpectedly (" + args.ProcessFailedKind + ").");
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

        // Sign-in flows open popups. Without this they'd open as separate windows outside Game Bar, so each
        // popup (including popups opened by popups) gets its own WebView stacked in an overlay inside the widget,
        // supplied through NewWindow so window.opener and postMessage keep working.
        private async void OpenPopup(WebView2 opener, CoreWebView2NewWindowRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                // Drop anything stacked above the requesting page; it stays open underneath the new popup.
                ClosePopupsFrom(opener == null ? 0 : _popups.IndexOf(opener) + 1);

                // A new window must be a fresh, never-navigated WebView. Same app, same default environment and profile.
                var popup = new WebView2();
                PopupHost.Children.Add(popup);
                _popups.Add(popup);
                PopupLayer.Visibility = Visibility.Visible;
                PopupTitle.Text = args.Uri;

                await popup.EnsureCoreWebView2Async();
                var core = popup.CoreWebView2 ?? throw new InvalidOperationException("Popup WebView2 didn't initialize.");
                core.WindowCloseRequested += (s, _) => ClosePopupsFrom(_popups.IndexOf(popup));
                core.DocumentTitleChanged += (s, _) => { if (TopPopup == popup) PopupTitle.Text = s.DocumentTitle; };
                core.NewWindowRequested += (s, nested) => OpenPopup(popup, nested);

                args.NewWindow = core;
                args.Handled = true;
            }
            catch
            {
                // Fall back to navigating the requesting view rather than spawning an external window.
                ClosePopupsFrom(opener == null ? 0 : _popups.IndexOf(opener) + 1);
                args.Handled = true;
                (opener ?? ChatView).CoreWebView2?.Navigate(args.Uri);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private WebView2 TopPopup => _popups.Count > 0 ? _popups[_popups.Count - 1] : null;

        // Closes the popup at index and everything opened above it.
        private void ClosePopupsFrom(int index)
        {
            if (index < 0) return;
            while (_popups.Count > index)
            {
                var popup = TopPopup;
                _popups.RemoveAt(_popups.Count - 1);
                PopupHost.Children.Remove(popup);
                try { popup.Close(); } catch { }
            }

            if (TopPopup != null)
            {
                PopupTitle.Text = TopPopup.CoreWebView2?.DocumentTitle ?? string.Empty;
                TopPopup.Focus(FocusState.Programmatic);
            }
            else
            {
                PopupLayer.Visibility = Visibility.Collapsed;
                ChatView.Focus(FocusState.Programmatic);
            }
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

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (!_initialized)
            {
                await InitializeChatAsync();
                return;
            }
            if (ErrorPanel.Visibility == Visibility.Visible) ChatView.Source = CopilotHome;
            else ChatView.Reload();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            if (ChatView.CoreWebView2 != null) ChatView.Source = CopilotHome;
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e) => ClosePopupsFrom(_popups.Count - 1);

        private async void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            if (!await Launcher.LaunchUriAsync(new Uri("ms-copilot:")))
                await Launcher.LaunchUriAsync(CopilotHome);
        }
    }
}
