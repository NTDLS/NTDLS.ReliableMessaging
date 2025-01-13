using Microsoft.Extensions.Caching.Memory;

namespace NTDLS.ReliableMessaging.Internal
{
    internal static class Caching
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };

        public static bool CacheTryGet<T>(object key, out T? value)
            => _cache.TryGetValue(key, out value);

        public static void CacheSet(object key, object value, TimeSpan slidingExpiration)
            => _cache.Set(key, value, new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiration });

        public static void CacheSetOneMinute(object key, object value)
            => _cache.Set(key, value, _slidingOneMinute);
    }
}
