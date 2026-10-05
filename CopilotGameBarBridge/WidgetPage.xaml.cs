using Microsoft.Gaming.XboxGameBar;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
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
        private XboxGameBarAppTargetTracker _tracker;
        private string _gameName;
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

                _tracker = new XboxGameBarAppTargetTracker(_widget);
                _tracker.TargetChanged += async (s, _) => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, UpdateGame);
                _tracker.SettingChanged += async (s, _) => await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, UpdateGame);
                UpdateGame();
            }
        }

        // Current game from Game Bar's target tracker; null when tracking is off, nothing is targeted, or it isn't a game.
        private void UpdateGame()
        {
            string name = null;
            string hint = "Game context needs Game Bar to be open over a game";
            try
            {
                if (_tracker != null)
                {
                    if (_tracker.Setting != XboxGameBarAppTargetSetting.Enabled)
                    {
                        hint = _tracker.Setting == XboxGameBarAppTargetSetting.DisabledByUser
                            ? "Game tracking is turned off in Game Bar settings"
                            : "Game tracking isn't available right now";
                    }
                    else
                    {
                        var target = _tracker.GetTarget();
                        if (target != null && target.IsGame && !string.IsNullOrWhiteSpace(target.DisplayName))
                        {
                            name = target.DisplayName.Trim();
                        }
                    }
                }
            }
            catch
            {
                // Tracking unavailable: leave the button disabled.
            }

            _gameName = name;
            GameButton.IsEnabled = name != null;
            ToolTipService.SetToolTip(GameButton, name != null ? "Add \"I'm playing " + name + "\" to your question" : hint);
        }

        // Only ever script the real Copilot chat page in the main view, never sign-in pages or popups.
        private bool IsCopilotChat()
        {
            var core = ChatView.CoreWebView2;
            if (core == null || ChatView.Visibility != Visibility.Visible) return false;
            return Uri.TryCreate(core.Source, UriKind.Absolute, out var uri)
                && uri.Scheme == "https" && uri.Host == "copilot.microsoft.com";
        }

        private async void GameContext_Click(object sender, RoutedEventArgs e)
        {
            var name = _gameName;
            if (name == null) return;
            var prefix = "I'm playing " + name + ". My question is: ";

            string result = "unavailable";
            if (IsCopilotChat() && PopupLayer.Visibility != Visibility.Visible)
            {
                // The prefix goes in as a JSON string literal, so any characters in the game name stay data.
                var literal = JsonValue.CreateStringValue(prefix).Stringify();
                var script = "(function(prefix){"
                    + "var el=document.querySelector('textarea')||document.querySelector('[contenteditable=\"true\"]');"
                    + "if(!el)return 'nocomposer';"
                    + "var isText=el.tagName==='TEXTAREA';"
                    + "var cur=isText?el.value:el.innerText;"
                    + "if(cur.indexOf(prefix)===0)return 'present';"
                    + "var old=/^I'm playing [\\s\\S]*?\\. My question is: /.exec(cur);"
                    + "var rest=old?cur.slice(old[0].length):cur;"
                    + "if(isText){var set=Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set;"
                    + "set.call(el,prefix+rest);el.dispatchEvent(new Event('input',{bubbles:true}));el.focus();}"
                    + "else{el.focus();var want=prefix+rest;"
                    + "if(!document.execCommand('selectAll')||!document.execCommand('insertText',false,want))return 'failed';"
                    + "if(el.innerText.replace(/\\s+/g,' ').trim()!==want.replace(/\\s+/g,' ').trim())return 'failed';}"
                    + "return 'ok';})(" + literal + ")";
                try
                {
                    result = (await ChatView.CoreWebView2.ExecuteScriptAsync(script)).Trim('"');
                }
                catch
                {
                    result = "unavailable";
                }
            }

            if (result == "ok" || result == "present")
            {
                StatusText.Text = result == "ok" ? "Added game context. Type your question and send." : "Game context is already in your question.";
                return;
            }

            // Couldn't reach the chat box: hand over the text instead.
            var data = new DataPackage();
            data.SetText(prefix);
            Clipboard.SetContent(data);
            StatusText.Text = "Couldn't reach the chat box. Copied \"" + prefix.Trim() + "\" so you can paste it.";
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
            WebView2 popup = null;
            try
            {
                // Drop anything stacked above the requesting page; it stays open underneath the new popup.
                ClosePopupsFrom(opener == null ? 0 : _popups.IndexOf(opener) + 1);

                // A new window must be a fresh, never-navigated WebView. Same app, same default environment and profile.
                popup = new WebView2();
                PopupHost.Children.Add(popup);
                _popups.Add(popup);
                PopupLayer.Visibility = Visibility.Visible;
                PopupTitle.Text = args.Uri;

                await popup.EnsureCoreWebView2Async();

                // The overlay is visible while that runs, so the user may have closed this popup already.
                if (!_popups.Contains(popup))
                {
                    args.Handled = true;
                    return;
                }

                var core = popup.CoreWebView2 ?? throw new InvalidOperationException("Popup WebView2 didn't initialize.");
                core.WindowCloseRequested += (s, _) => ClosePopupsFrom(_popups.IndexOf(popup));
                core.DocumentTitleChanged += (s, _) => { if (TopPopup == popup) PopupTitle.Text = s.DocumentTitle; };
                core.NewWindowRequested += (s, nested) => OpenPopup(popup, nested);

                args.NewWindow = core;
                args.Handled = true;
            }
            catch
            {
                args.Handled = true;

                // Dismissed while starting: treat as cancelled and leave the requesting view and other popups alone.
                if (popup != null && !_popups.Contains(popup)) return;

                // Otherwise fall back to navigating the requesting view rather than spawning an external window.
                if (popup != null) ClosePopupsFrom(_popups.IndexOf(popup));
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
            // Navigate the core directly: setting Source to the URL it already holds (the page that just
            // failed to load) is ignored, so Try again would do nothing.
            if (ErrorPanel.Visibility == Visibility.Visible) ChatView.CoreWebView2?.Navigate(CopilotHome.AbsoluteUri);
            else ChatView.Reload();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            ChatView.CoreWebView2?.Navigate(CopilotHome.AbsoluteUri);
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e) => ClosePopupsFrom(_popups.Count - 1);

        private async void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            if (!await Launcher.LaunchUriAsync(new Uri("ms-copilot:")))
                await Launcher.LaunchUriAsync(CopilotHome);
        }
    }
}
