using Microsoft.Extensions.Caching.Memory;

namespace NTDLS.ReliableMessaging.Internal
{
    internal static class RmCaching
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };

        public static bool TryGet<T>(object key, out T? value)
            => _cache.TryGetValue(key, out value);

        public static void Set(object key, object value, TimeSpan slidingExpiration)
            => _cache.Set(key, value, new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiration });

        public static void SetOneMinute(object key, object value)
            => _cache.Set(key, value, _slidingOneMinute);

        public static TItem? GetOrCreateOneMinute<TItem>(object key, Func<ICacheEntry, TItem> factory)
            => _cache.GetOrCreate<TItem>(key, factory, _slidingOneMinute);
    }
}
