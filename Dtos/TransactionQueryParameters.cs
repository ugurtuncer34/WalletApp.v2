namespace WalletApp.Dtos;

public class TransactionQueryParameters
{
    public Guid? CategoryId { get; set; }
    public Guid? MerchantId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public Guid? CountryId { get; set; }
    public Guid? CurrencyId { get; set; }

    public int PageNumber { get; set; } = 1;

    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > 50) ? 50 : value;
    }
}