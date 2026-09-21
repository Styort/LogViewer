using System;
using System.Collections.Generic;
using LogViewer.Core.Domain;

namespace LogViewer.Core.Services
{
    /// <summary>LogEntry ↔ XML DTO. Unknown level/transport fall back to Trace / Udp like live parse.</summary>
    public static class SavedSessionMapper
    {
        public static SavedSessionEntry ToDto(LogEntry entry)
        {
            if (entry == null)
                return null;

            var dto = new SavedSessionEntry
            {
                Time = entry.Time,
                Level = entry.Level.ToString(),
                Logger = entry.Logger ?? string.Empty,
                Thread = entry.Thread,
                Address = entry.Address ?? string.Empty,
                ExecutableName = entry.ExecutableName ?? string.Empty,
                ProcessID = entry.ProcessID.HasValue ? entry.ProcessID.Value.ToString() : null,
                ReceiverPort = entry.ReceiverPort,
                ReceiverTransport = entry.ReceiverTransport.ToString(),
                Message = entry.Message ?? string.Empty,
                Throwable = entry.Throwable ?? string.Empty
            };

            if (entry.Properties != null)
            {
                foreach (var pair in entry.Properties)
                {
                    dto.Properties.Add(new SavedSessionProperty
                    {
                        Name = pair.Key ?? string.Empty,
                        Value = pair.Value ?? string.Empty
                    });
                }
            }

            return dto;
        }

        public static LogEntry ToEntry(SavedSessionEntry dto)
        {
            if (dto == null)
                return null;

            var entry = new LogEntry
            {
                Time = dto.Time,
                Level = ParseLevel(dto.Level),
                Logger = dto.Logger ?? string.Empty,
                Thread = dto.Thread,
                Address = dto.Address ?? string.Empty,
                ExecutableName = dto.ExecutableName ?? string.Empty,
                ProcessID = ParseProcessId(dto.ProcessID),
                ReceiverPort = dto.ReceiverPort,
                ReceiverTransport = ReceiverTransportHelper.ParseOrUdp(dto.ReceiverTransport),
                Message = dto.Message ?? string.Empty,
                Throwable = dto.Throwable ?? string.Empty,
                Properties = new Dictionary<string, string>()
            };

            if (dto.Properties != null)
            {
                foreach (var prop in dto.Properties)
                {
                    if (prop == null || string.IsNullOrEmpty(prop.Name))
                        continue;
                    entry.Properties[prop.Name] = prop.Value ?? string.Empty;
                }
            }

            return entry;
        }

        public static List<LogEntry> ToEntries(SavedSessionDocument document)
        {
            var result = new List<LogEntry>();
            if (document?.Entries == null)
                return result;
            for (int i = 0; i < document.Entries.Count; i++)
            {
                var entry = ToEntry(document.Entries[i]);
                if (entry != null)
                    result.Add(entry);
            }
            return result;
        }

        private static LogLevel ParseLevel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LogLevel.Trace;
            LogLevel parsed;
            if (Enum.TryParse(value.Trim(), true, out parsed) && Enum.IsDefined(typeof(LogLevel), parsed))
                return parsed;
            return LogLevel.Trace;
        }

        private static int? ParseProcessId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            int id;
            if (int.TryParse(value.Trim(), out id))
                return id;
            return null;
        }
    }
}
