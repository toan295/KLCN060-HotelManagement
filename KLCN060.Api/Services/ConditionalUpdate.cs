using System.Linq.Expressions;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace KLCN060.Api.Services;

public static class ConditionalUpdate
{
    /// <summary>
    /// Cập nhật có điều kiện (compare-and-set) trong một câu lệnh SQL, trả về số dòng đã đổi.
    /// CSDL trong bộ nhớ (test đơn vị) không hỗ trợ cập nhật hàng loạt nên dùng <paramref name="apDungTrongBoNho"/> trên các thực thể khớp điều kiện.
    /// </summary>
    public static async Task<int> CapNhatCoDieuKienAsync<T>(this IQueryable<T> khop, KLCN060DbContext context,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters, Action<T> apDungTrongBoNho) where T : class
    {
        if (context.Database.IsRelational()) return await khop.ExecuteUpdateAsync(setters);
        var dong = await khop.ToListAsync();
        foreach (var d in dong) apDungTrongBoNho(d);
        return dong.Count;
    }
}
