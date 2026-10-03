using System;
using System.IO;
using AutoUpdaterDotNET;
using NLog;

namespace LogViewer.Services.Updates
{
    /// <summary>
    /// <c>update_state.json</c> next to settings.xml, through the library's <see cref="JsonFilePersistenceProvider"/>.
    /// The state is a convenience: a corrupt or unwritable file is logged and the update is offered again.
    /// </summary>
    internal sealed class JsonUpdateStateStore : IUpdateStateStore
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string _path;
        private JsonFilePersistenceProvider _provider;

        public JsonUpdateStateStore(string path)
        {
            _path = path;
        }

        public Version SkippedVersion
        {
            get => Read(p => p.GetSkippedVersion());
            set => Write(p => p.SetSkippedVersion(value));
        }

        public DateTime? RemindLaterAt
        {
            get => Read(p => p.GetRemindLater());
            set => Write(p => p.SetRemindLater(value));
        }

        private T Read<T>(Func<JsonFilePersistenceProvider, T> read)
        {
            var provider = Provider();
            return provider == null ? default(T) : read(provider);
        }

        private void Write(Action<JsonFilePersistenceProvider> write)
        {
            try
            {
                // The provider does not create the folder; settings.xml may not have been saved yet.
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var provider = Provider();
                if (provider != null)
                    write(provider);
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Could not write {_path}");
            }
        }

        private JsonFilePersistenceProvider Provider()
        {
            if (_provider != null)
                return _provider;

            try
            {
                _provider = new JsonFilePersistenceProvider(_path);
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Could not read {_path}, starting with empty update state");
                TryDelete();
                try
                {
                    _provider = new JsonFilePersistenceProvider(_path);
                }
                catch (Exception retry)
                {
                    logger.Warn(retry, $"Could not reset {_path}");
                }
            }
            return _provider;
        }

        private void TryDelete()
        {
            try
            {
                File.Delete(_path);
            }
            catch (Exception e)
            {
                logger.Warn(e, $"Could not delete {_path}");
            }
        }
    }
}
