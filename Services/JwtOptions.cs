namespace yurt_lojman_yonetim_sistemi.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "MTU.Accommodation";
    public string Audience { get; set; } = "MTU.Accommodation.Clients";
    public string Key { get; set; } = "CHANGE_ME_WITH_A_64_CHARACTER_PRODUCTION_SECRET_KEY_123456789";
    public int ExpireMinutes { get; set; } = 120;
}
