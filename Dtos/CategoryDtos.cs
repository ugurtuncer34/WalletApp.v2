namespace WalletApp.Dtos;

public record SimpleLookupDto(Guid Id, string Name);

public record CategoryResponseDto(
    Guid Id,
    string Name,
    string? Icon,
    DateTime CreatedAt,
    SimpleLookupDto? ParentCategory
);