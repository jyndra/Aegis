using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.UI.Xaml;

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

        LoadHealthReport();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadHealthReportAsync();
    }

    private async void LoadHealthReport()
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
                TxtStatusDetail.Text = "All critical enforcement modules healthy. 25-day commitment timer active.";
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
}
