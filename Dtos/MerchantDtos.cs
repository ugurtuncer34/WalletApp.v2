namespace WalletApp.Dtos;

public record MerchantResponseDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    SimpleLookupDto? DefaultCategory
);