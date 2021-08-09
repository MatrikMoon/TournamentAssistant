using System;
using System.Collections.Generic;
using System.Linq;

/**
 * Created by Moon on 9/3/2020, 5:08AM
 * These extensions are a workaround for EF/LINQ conflicts
 * https://github.com/dotnet/efcore/issues/18220
 */

namespace TournamentAssistantCore.Discord.Helpers
{
    public static class WorkaroundExtensions
    {
        public static IAsyncEnumerable<TEntity> AsAsyncEnumerable<TEntity>(this Microsoft.EntityFrameworkCore.DbSet<TEntity> obj) where TEntity : class
        {
            return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsAsyncEnumerable(obj);
        }

        public static IQueryable<TEntity> Where<TEntity>(this Microsoft.EntityFrameworkCore.DbSet<TEntity> obj, System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate) where TEntity : class
        {
            return Queryable.Where(obj, predicate);
        }

        public static IQueryable<TResult> Select<TSource, TResult>(this Microsoft.EntityFrameworkCore.DbSet<TSource> obj, System.Linq.Expressions.Expression<Func<TSource, TResult>> predicate) where TSource : class
        {
            return Queryable.Select(obj, predicate);
        }

        public static void ForEach<TSource>(this Microsoft.EntityFrameworkCore.DbSet<TSource> obj, Action<TSource> predicate) where TSource : class
        {
            foreach (var o in obj)
            {
                predicate(o);
            }
        }
    }
}
