using System;

// 경영 정책(배당·재투자·비축)을 고르기 전에 "이걸 고르면 다음 결산에 어떻게 되는가"를 미리 셈해 보여 준다.
// 결산과 같은 식을 쓴다(CompanyBusiness.DividendBudget / FinishClose). 기준 이익은 마지막 결산의 순이익이고, 첫 결산 전에는 평소 이익이다.
// 보여 주기만 한다. 회사의 돈이나 상태는 건드리지 않는다.
public struct PolicyOutlook
{
    /// <summary>주주 전체에게 나가는 배당과 그 가운데 내 몫.</summary>
    public decimal Dividend, Mine;
    /// <summary>생산성에 들어가는 돈, 회사에 남는 돈.</summary>
    public decimal Invest, Kept;
    /// <summary>재투자로 늘어나는 하루 매출과 하루 순이익.</summary>
    public decimal RevenueGain, ProfitGain;
}

public sealed partial class CorporateEconomy
{
    /// <summary>재투자한 돈이 이 날수 안에 순이익으로 돌아오면 재투자를 권한다.</summary>
    public const float AdvicePaybackDays = 20f;

    /// <summary>미리 셈할 때 기준으로 삼는 하루 순이익.</summary>
    public decimal AdviceProfit(Stock stock)
    {
        var data = stock?.business;
        if (data == null) { return 0m; }
        if (data.CloseFinished) { return data.Profit; }
        return data.growth == null ? 0m : data.growth.AverageProfit;
    }
    private void ProductivityTerms(CompanyBusiness data, out decimal limit, out decimal gain)
    {
        bool founder = data.growth != null && data.growth.FounderBusiness;
        limit = founder && GrowthRules.founderProductivity > 0 ? (decimal)GrowthRules.founderProductivity : (decimal)Rules.maximumProductivity;
        gain = founder && GrowthRules.founderProductivityGain > 0 ? (decimal)GrowthRules.founderProductivityGain * (1m + (decimal)LadderSynergy) : (decimal)Rules.productivityGain;
    }
    /// <summary>이 정책이면 다음 결산에 어떻게 나뉘는가.</summary>
    public PolicyOutlook Outlook(Stock stock, CompanyPolicy policy)
    {
        var result = new PolicyOutlook(); var data = stock?.business;
        if (data == null || stock.issuedShares <= 0m) { return result; }
        decimal profit = Math.Max(0m, AdviceProfit(stock));
        if (data.Debt > 0m || profit <= 0m) { result.Kept = AdviceProfit(stock); return result; }
        double payout = policy == CompanyPolicy.Dividend ? Rules.dividendPayout : policy == CompanyPolicy.Investment ? Rules.investmentPayout : Rules.reservePayout;
        double reinvest = policy == CompanyPolicy.Investment ? Rules.growthInvestment : policy == CompanyPolicy.Dividend ? Rules.dividendInvestment : 0;
        decimal cash = data.Cash + (data.CloseFinished ? 0m : profit);
        result.Dividend = Math.Min(cash, decimal.Floor(profit * (decimal)payout));
        decimal owned = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(stock) : 0m;
        result.Mine = CompanyBusiness.SharePayment(result.Dividend, owned, stock.issuedShares);
        ProductivityTerms(data, out decimal limit, out decimal gain);
        decimal capacity = gain <= 0m || data.ReferenceCapital <= 0m ? 0m : Math.Max(0m, limit - data.Productivity) * data.ReferenceCapital / gain;
        result.Invest = Math.Max(0m, Math.Min(decimal.Floor(capacity), Math.Min(cash - result.Dividend, decimal.Floor(profit * (decimal)reinvest))));
        result.Kept = profit - result.Dividend - result.Invest;
        if (result.Invest > 0m && data.ReferenceCapital > 0m)
        {
            decimal lift = Math.Min(limit - data.Productivity, result.Invest / data.ReferenceCapital * gain);
            decimal margin = Math.Max(0m, 1m - (decimal)Rules.variableCostRatio - (decimal)Rules.fixedCostRatio);
            result.RevenueGain = decimal.Floor(data.BaseRevenue * lift);
            // 고정비는 매출이 늘어도 그대로다. 늘어난 매출에서는 변동비만 빠진다.
            result.ProfitGain = decimal.Floor(result.RevenueGain * Math.Max(margin, 1m - (decimal)Rules.variableCostRatio));
        }
        return result;
    }
    /// <summary>지금 권하는 정책과 그 까닭(지금 언어).</summary>
    public CompanyPolicy Recommend(Stock stock, out string reason)
    {
        var data = stock?.business; reason = "";
        if (data == null) { return CompanyPolicy.Dividend; }
        if (data.Debt > 0m)
        { reason = Local("미지급 비용이 있는 동안은 배당도 재투자도 나가지 않습니다. 먼저 현금을 쌓으세요.", "While costs are unpaid, nothing is paid out or reinvested. Build cash first.", "未払い費用がある間は配当も再投資も出ません。まず現金を貯めましょう。"); return CompanyPolicy.Reserve; }
        if (AdviceProfit(stock) <= 0m)
        { reason = Local("이익이 없어 나눌 돈이 없습니다. 현금을 지키세요.", "No profit to split. Protect the cash.", "利益がなく分けるお金がありません。現金を守りましょう。"); return CompanyPolicy.Reserve; }
        var grow = Outlook(stock, CompanyPolicy.Investment);
        if (grow.Invest > 0m && grow.ProfitGain > 0m)
        {
            decimal days = grow.Invest / grow.ProfitGain;
            if (days <= (decimal)AdvicePaybackDays)
            {
                string count = decimal.Ceiling(days).ToString("0");
                reason = Local("재투자한 돈이 약 " + count + "일이면 늘어난 이익으로 돌아옵니다.", "Reinvested money comes back as extra profit in about " + count + " days.", "再投資したお金は約" + count + "日で増えた利益として戻ります。");
                return CompanyPolicy.Investment;
            }
        }
        // 생산성이 다 찼거나 돌아오는 데 오래 걸린다. 다음 사업 확장에 쓸 돈이 모자라면 모으고, 넉넉하면 배당으로 받는다.
        var growth = data.growth; decimal cheapest = 0m;
        if (growth != null)
        {
            foreach (CompanyProject type in Enum.GetValues(typeof(CompanyProject)))
            {
                if (growth.Level(type) >= GrowthRules.maximumLevel || growth.Running(type)) { continue; }
                decimal cost = growth.ProjectCost(data, type, GrowthRules);
                if (cost > 0m && (cheapest <= 0m || cost < cheapest)) { cheapest = cost; }
            }
        }
        if (cheapest > 0m && data.Cash < cheapest && WorldDramaDirector.Instance != null && WorldDramaDirector.Instance.Controls(stock))
        {
            reason = Local("재투자보다 사업 확장이 빠릅니다. 다음 확장 비용(" + cheapest.ToString("N0") + "원)을 모으세요.", "An expansion pays better than reinvesting. Save for the next one (" + cheapest.ToString("N0") + " KRW).", "再投資より事業拡張のほうが効きます。次の拡張費用(" + cheapest.ToString("N0") + "ウォン)を貯めましょう。");
            return CompanyPolicy.Reserve;
        }
        reason = grow.Invest <= 0m
            ? Local("생산성이 한도에 닿았습니다. 이익은 배당으로 받는 편이 낫습니다.", "Productivity is at its cap. Take the profit as dividends.", "生産性が上限です。利益は配当で受け取りましょう。")
            : Local("재투자는 본전까지 오래 걸립니다. 배당으로 받는 편이 낫습니다.", "Reinvesting takes long to pay back. Dividends are the better deal.", "再投資は元を取るまで時間がかかります。配当のほうが得です。");
        return CompanyPolicy.Dividend;
    }
}
