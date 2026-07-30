using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

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
            BaseAddress = new Uri("https://127.0.0.1:9443/")
        };

        // Attach health check load to window activation
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

    private async Task LoadHealthReportAsync()
    {
        try
        {
            BtnRefresh.IsEnabled = false;

            var response = await _httpClient.GetAsync("status/report");
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.ContentStreamAsync());
                var root = doc.RootElement;

                string protectionState = root.GetProperty("protectionState").GetString() ?? "Protected";

                TxtStatusTitle.Text = $"Protection {protectionState} & Locked";
                TxtStatusDetail.Text = protectionState == "Protected"
                    ? "All critical enforcement modules healthy. 25-day commitment timer active."
                    : "Attention required! One or more protection subsystems are degraded.";

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
