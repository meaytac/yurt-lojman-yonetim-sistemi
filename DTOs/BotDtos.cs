using System.ComponentModel.DataAnnotations;

namespace yurt_lojman_yonetim_sistemi.DTOs;

public record BotCreateRequest(
    [Required, MinLength(11), MaxLength(11)] string TcNo,
    [Required] int RoomId,
    [Required, MaxLength(80)] string Category,
    [Required, MaxLength(1000)] string Description,
    string? PhotoUrl);

public record BotWebhookMessage(string? From, string? Text, string? MediaId);
