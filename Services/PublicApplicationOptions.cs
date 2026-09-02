namespace yurt_lojman_yonetim_sistemi.Services;

public class PublicApplicationOptions
{
    public int TrackingTokenDays { get; set; } = 30;
    public int ActivationTokenHours { get; set; } = 48;
    public int MaxDocumentMegabytes { get; set; } = 5;
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
}

public class EmailOptions
{
    public string FromAddress { get; set; } = "noreply@localhost";
}
