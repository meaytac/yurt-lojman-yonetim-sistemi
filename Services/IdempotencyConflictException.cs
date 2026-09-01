namespace yurt_lojman_yonetim_sistemi.Services;

public class IdempotencyConflictException(string message) : InvalidOperationException(message);
