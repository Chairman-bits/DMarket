using System.Collections.Generic;
using DMarket.Models;

namespace DMarket.Config;

internal static class InternalPortfolio
{
    public const string FundCode = "FUND";
    public const string FundName = "小林ファンド";
    public const decimal DisplayDivisor = 69449m;

    // ユーザーが設定画面から変更できないよう、ポートフォリオはアプリ内に固定保持する。
    // 必要があれば配布前にここだけ書き換える。
    public static IReadOnlyList<PortfolioEntry> Entries { get; } = new List<PortfolioEntry>
    {
        new() { Symbol = "1419", Quantity = 200m },
        new() { Symbol = "4063", Quantity = 100m },
        new() { Symbol = "7164", Quantity = 1m },
        new() { Symbol = "7974", Quantity = 100m },
        new() { Symbol = "8001", Quantity = 500m },
        new() { Symbol = "8058", Quantity = 50m },
        new() { Symbol = "8306", Quantity = 400m },
        new() { Symbol = "8591", Quantity = 2m },
        new() { Symbol = "8766", Quantity = 103m },
        new() { Symbol = "9432", Quantity = 1000m },
    };
}
