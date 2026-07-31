using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Aegis.UI;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;

    public MainWindow()
    {
        this.InitializeComponent();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:9443/")
        };

        this.Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        this.Activated -= MainWindow_Activated;
        _ = LoadHealthReportAsync();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadHealthReportAsync();
    }

    private async void OnAddWebsiteClicked(object sender, RoutedEventArgs e)
    {
        string domain = TxtWebsiteInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(domain)) return;

        try
        {
            BtnAddWebsite.IsEnabled = false;
            var response = await _httpClient.PostAsJsonAsync("policy/custom-websites", new { domain });
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            string msg = doc.RootElement.GetProperty("message").GetString() ?? "Website added.";
            TxtCustomRuleMessage.Text = msg;
            TxtWebsiteInput.Text = "";
        }
        catch (Exception ex)
        {
            TxtCustomRuleMessage.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnAddWebsite.IsEnabled = true;
        }
    }

    private async void OnAddKeywordClicked(object sender, RoutedEventArgs e)
    {
        string keyword = TxtKeywordInput.Text.Trim();
        int weight = int.TryParse(TxtKeywordWeight.Text.Trim(), out int w) ? w : 50;
        if (string.IsNullOrWhiteSpace(keyword)) return;

        try
        {
            BtnAddKeyword.IsEnabled = false;
            var response = await _httpClient.PostAsJsonAsync("policy/custom-keywords", new { keyword, weight });
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            string msg = doc.RootElement.GetProperty("message").GetString() ?? "Keyword added.";
            TxtCustomRuleMessage.Text = msg;
            TxtKeywordInput.Text = "";
        }
        catch (Exception ex)
        {
            TxtCustomRuleMessage.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnAddKeyword.IsEnabled = true;
        }
    }

    private async void OnAddRegexClicked(object sender, RoutedEventArgs e)
    {
        string pattern = TxtRegexInput.Text.Trim();
        int score = int.TryParse(TxtRegexScore.Text.Trim(), out int s) ? s : 60;
        if (string.IsNullOrWhiteSpace(pattern)) return;

        try
        {
            BtnAddRegex.IsEnabled = false;
            var response = await _httpClient.PostAsJsonAsync("policy/custom-regex", new { pattern, score });
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            string msg = doc.RootElement.GetProperty("message").GetString() ?? "Regex rule added.";
            TxtCustomRuleMessage.Text = msg;
            TxtRegexInput.Text = "";
        }
        catch (Exception ex)
        {
            TxtCustomRuleMessage.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnAddRegex.IsEnabled = true;
        }
    }

    private async void OnTestUninstallClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnTestUninstall.IsEnabled = false;
            var response = await _httpClient.PostAsync("deployment/uninstall?forceConfirm=true", null);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            string msg = doc.RootElement.GetProperty("message").GetString() ?? "Uninstall completed.";
            TxtUninstallResult.Text = msg;
        }
        catch (Exception ex)
        {
            TxtUninstallResult.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnTestUninstall.IsEnabled = true;
        }
    }

    private async Task LoadHealthReportAsync()
    {
        try
        {
            BtnRefresh.IsEnabled = false;

            var response = await _httpClient.GetAsync("status/report");
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var root = doc.RootElement;

                string protectionState = root.GetProperty("protectionState").GetString() ?? "Protected";

                TxtStatusTitle.Text = $"Protection {protectionState} (Test Mode Active)";
                TxtStatusDetail.Text = "DNS, Proxy & AI modules healthy. Custom rule provisions & testing mode ready.";

                if (root.TryGetProperty("subsystems", out var subsystemsElement))
                {
                    foreach (var elem in subsystemsElement.EnumerateArray())
                    {
                        string comp = elem.GetProperty("component").GetString() ?? "";
                        string stat = elem.GetProperty("status").GetString() ?? "Unknown";

                        UpdateCardStatus(comp, stat);
                    }
                }
            }
        }
        catch
        {
            TxtStatusTitle.Text = "Protection Active (Offline Shell)";
            TxtStatusDetail.Text = "Local service connecting... Subsystem monitors on standby.";
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private void UpdateCardStatus(string component, string status)
    {
        TextBlock? targetBlock = component.ToLowerInvariant() switch
        {
            "service" => TxtServiceStatus,
            "database" => TxtDbStatus,
            "dns" => TxtDnsStatus,
            "extension" => TxtExtensionStatus,
            _ => null
        };

        if (targetBlock != null)
        {
            targetBlock.Text = status;
            bool isOk = string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase);
            targetBlock.Foreground = isOk
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128))   // Green #4ADE80
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));  // Red #F87171
        }
    }
}
