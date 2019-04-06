using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogViewer.MVVM.Models
{
    public class FileWatcher
    {
        private CancellationTokenSource cancellationToken;

        /// <summary>
        /// Путь к файлу
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Позиция до последнего прочтения
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Шаблон лога данного файла
        /// </summary>
        public LogTemplate Template { get; set; }

        /// <summary>
        /// Событие, возникающее при изменении файла
        /// </summary>
        public event EventHandler<FileWatcher> FileChanged;

        /// <summary>
        /// Начать проверку на изменение файла
        /// </summary>
        public void StartWatch()
        {
            cancellationToken = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (true)
                {
                    if (cancellationToken.Token.IsCancellationRequested)
                        return;

                    long currentLength;
                    using (FileStream stream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        currentLength = stream.Length;
                    }

                    if (currentLength > Position)
                        OnFileChanged(this);

                    Position = currentLength;
                    Thread.Sleep(1000);
                }
            });
        }

        public void StopWatch()
        {
            cancellationToken.Cancel();
        }

        protected virtual void OnFileChanged(FileWatcher fileWatcher)
        {
            FileChanged?.Invoke(this, fileWatcher);
        }
    }
}
