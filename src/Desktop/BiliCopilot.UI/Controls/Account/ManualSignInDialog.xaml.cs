// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;

namespace BiliCopilot.UI.Controls.Account;

/// <summary>
/// 手动登录对话框，用于粘贴浏览器中的 Cookie 完成登录.
/// </summary>
public sealed partial class ManualSignInDialog : ContentDialog
{
    private const string SessionCookieKey = "SESSDATA";
    private const string CsrfCookieKey = "bili_jct";

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isBusy;
    private bool _isClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualSignInDialog"/> class.
    /// </summary>
    public ManualSignInDialog()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    /// <summary>
    /// 获取一个值，该值表示是否已经通过手动登录成功登录.
    /// </summary>
    public bool IsSignedIn { get; private set; }

    private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        => DispatcherQueue.TryEnqueue(() => CookieBox.Focus(FocusState.Programmatic));

    private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        _isClosed = true;
        _cancellationTokenSource?.Cancel();
    }

    private async void OnPrimaryButtonClickAsync(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 始终阻止对话框自动关闭，登录成功后再主动关闭.
        args.Cancel = true;
        if (_isBusy)
        {
            return;
        }

        var cookies = ReadCookies();
        if (cookies is null)
        {
            return;
        }

        _isBusy = true;
        SetBusyState(true);
        _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await this.Get<IAuthenticationService>()
                .SignInAsync(new AuthorizeExecutionSettings { Cookies = cookies }, _cancellationTokenSource.Token);
            if (this.Get<IBiliTokenResolver>().GetToken() is null)
            {
                await ResetCookiesAsync();
                ShowError(ResourceToolkit.GetLocalizedString(StringNames.ManualSignInNoTokenTip));
                return;
            }

            IsSignedIn = true;
        }
        catch (OperationCanceledException)
        {
            await ResetCookiesAsync();
            ShowError(ResourceToolkit.GetLocalizedString(StringNames.ManualSignInTimeoutTip));
        }
        catch (Exception ex)
        {
            await ResetCookiesAsync();
            ShowError(string.Format(ResourceToolkit.GetLocalizedString(StringNames.ManualSignInFailedTip), ex.Message));
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _isBusy = false;
            SetBusyState(false);
        }

        if (IsSignedIn && !_isClosed)
        {
            Hide();
        }
    }

    private Dictionary<string, string>? ReadCookies()
    {
        var text = CookieBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ShowError(ResourceToolkit.GetLocalizedString(StringNames.ManualSignInEmptyTip));
            return null;
        }

        var cookies = ParseCookies(text);
        if (cookies.Count == 0)
        {
            ShowError(ResourceToolkit.GetLocalizedString(StringNames.ManualSignInInvalidTip));
            return null;
        }

        if (!cookies.ContainsKey(SessionCookieKey) || !cookies.ContainsKey(CsrfCookieKey))
        {
            ShowError(string.Format(
                ResourceToolkit.GetLocalizedString(StringNames.ManualSignInMissingCookieTip),
                $"{SessionCookieKey} / {CsrfCookieKey}"));
            return null;
        }

        return cookies;
    }

    private static Dictionary<string, string> ParseCookies(string text)
    {
        // 允许用户直接粘贴完整的请求头，例如「Cookie: SESSDATA=xxx; bili_jct=xxx」.
        if (text.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Cookie:".Length..].Trim();
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var segment in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (name.Length > 0)
            {
                parsed[name] = value;
            }
        }

        // 内核通过正则 bili_jct=(.*?); 读取 CSRF，若 bili_jct 位于 Cookie 字符串末尾则匹配失败，
        // 因此把它固定放在最前面，保证它后面始终有其它 Cookie.
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parsed.Remove(CsrfCookieKey, out var csrf))
        {
            result[CsrfCookieKey] = csrf;
        }

        foreach (var item in parsed)
        {
            result[item.Key] = item.Value;
        }

        return result;
    }

    private async Task ResetCookiesAsync()
    {
        // 登录失败时清掉刚刚写入的 Cookie，避免留下无法使用但看起来已登录的状态.
        try
        {
            await this.Get<IAuthenticationService>().SignOutAsync();
        }
        catch (Exception ex)
        {
            // 清理失败不影响错误提示.
            System.Diagnostics.Debug.WriteLine($"清理登录 Cookie 失败: {ex.Message}");
        }
    }

    private void SetBusyState(bool isBusy)
    {
        CookieBox.IsEnabled = !isBusy;
        IsPrimaryButtonEnabled = !isBusy;
        BusyPanel.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        if (isBusy)
        {
            HideError();
        }
    }

    private void ShowError(string message)
    {
        ErrorBlock.Text = message;
        ErrorBlock.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorBlock.Text = string.Empty;
        ErrorBlock.Visibility = Visibility.Collapsed;
    }
}
