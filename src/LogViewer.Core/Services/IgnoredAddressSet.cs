using System;
using System.Collections.Generic;
using System.Net;

namespace LogViewer.Core.Services
{
    /// <summary>
    /// Ignored sender addresses, matched exactly. A substring match made the rule <c>10.0.0.1</c> also hide
    /// <c>10.0.0.10</c>–<c>10.0.0.19</c> and <c>110.0.0.1</c>. Settings only accept full IPv4 addresses, so no
    /// supported rule relied on partial matching.
    /// </summary>
    public sealed class IgnoredAddressSet
    {
        private readonly HashSet<IPAddress> _addresses = new HashSet<IPAddress>();

        // A hand-edited settings.xml may hold something that is not an address; compare it as text.
        private readonly HashSet<string> _unparsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IgnoredAddressSet(IEnumerable<string> rules)
        {
            if (rules == null)
                return;

            foreach (var rule in rules)
            {
                var text = rule?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (IPAddress.TryParse(text, out var address))
                    _addresses.Add(Normalize(address));
                else
                    _unparsed.Add(text);
            }
        }

        public bool IsEmpty => _addresses.Count == 0 && _unparsed.Count == 0;

        public bool Contains(IPAddress address)
        {
            if (address == null || IsEmpty)
                return false;

            var normalized = Normalize(address);
            return _addresses.Contains(normalized) || _unparsed.Contains(normalized.ToString());
        }

        /// <summary>
        /// A dual-mode socket reports IPv4 senders as <c>::ffff:a.b.c.d</c>; the rule is written as IPv4.
        /// </summary>
        private static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
    }
}
