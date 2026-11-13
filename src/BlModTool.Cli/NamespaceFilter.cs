namespace BlModTool.Cli;

public static class NamespaceFilter
{
    public static bool Matches(string ns, IReadOnlyCollection<string> filters)
    {
        if (filters.Count == 0)
            return true;

        foreach (var rawFilter in filters)
        {
            var filter = rawFilter;
            if (filter.EndsWith(".*", StringComparison.Ordinal))
                filter = filter[..^2];

            if (filter.Length == 0)
            {
                if (ns.Length == 0)
                    return true;
            }
            else if (ns.Equals(filter, StringComparison.Ordinal) ||
                     ns.StartsWith(filter + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
