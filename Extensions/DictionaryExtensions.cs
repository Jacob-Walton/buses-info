using System;
using System.Collections.Generic;
using System.Linq;

namespace BusInfo.Extensions
{
    public static class DictionaryExtensions
    {
        public static Dictionary<TKey, TElement> ToDictionaryWithFirstValue<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector) where TKey : notnull
        {
            Dictionary<TKey, TElement> dictionary = [];

            foreach (TSource? item in source)
            {
                TKey key = keySelector(item);
                if (!dictionary.ContainsKey(key))
                {
                    dictionary.Add(key, elementSelector(item));
                }
            }

            return dictionary;
        }
    }
}
