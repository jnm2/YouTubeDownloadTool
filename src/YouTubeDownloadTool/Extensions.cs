using System;
using System.Collections.Generic;

namespace YouTubeDownloadTool
{
    internal static class Extensions
    {
        public static IEnumerable<TResult> SelectWhere<T, TResult>(this IEnumerable<T> source, Func<T, (bool success, TResult result)> tryPatternSelector)
        {
            foreach (var value in source)
            {
                if (tryPatternSelector.Invoke(value) is (true, var result))
                {
                    yield return result;
                }
            }
        }
    }
}
