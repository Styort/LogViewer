using System;
using System.Linq;
using LogViewer.Adapters;
using LogViewer.Core.State;
using LogViewer.Helpers;
using LogViewer.MVVM.Models;
using LogViewer.Services;
using NLog;

namespace LogViewer.MVVM.ViewModels.Log
{
    /// <summary>
    /// Apply the settings window to the session, columns, and receivers.
    /// UDP is recreated entirely: port/ignore-IP cannot be changed on a live socket.
    /// </summary>
    public sealed class SettingsChangeApplier
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Apply settings OK. UDP is recreated at the end: changing the port on a live socket causes PortIsBusy.
        /// </summary>
        public void Apply(
            IAppSettings settings,
            LogViewState state,
            LogSession session,
            ReceiversViewModel receivers,
            ErrorTimelineViewModel timeline,
            Action<bool> setSourceVisible,
            Action<bool> setThreadVisible,
            Action applyFonts,
            Action<System.Windows.Media.SolidColorBrush> setIcon,
            Action<System.Windows.Media.SolidColorBrush> setFont,
            System.Windows.Media.SolidColorBrush currentIcon,
            RowHighlightApplier highlight = null)
        {
            try
            {
                state.IsBusy = true;
                session.AllowMaxMessageBufferSize = settings.IsEnabledMaxMessageBufferSize;
                session.MaxMessageBufferSize = settings.MaxMessageBufferSize;
                session.DeletedMessagesCount = settings.DeletedMessagesCount;
                setSourceVisible(settings.IsShowSourceColumn);
                setThreadVisible(settings.IsShowThreadColumn);
                timeline.Rebuild();
                setFont(new System.Windows.Media.SolidColorBrush().FromARGB(settings.FontColor));
                applyFonts();
                if (settings.CurrentTheme != null && !Equals(settings.CurrentTheme.Color, currentIcon))
                    setIcon(settings.CurrentTheme.Color);

                foreach (var receiver in settings.Receivers)
                {
                    var foundReceiver = receivers.Receivers.FirstOrDefault(x => x.Port == receiver.Port);
                    if (foundReceiver == null)
                        receivers.Receivers.Add(receiver);
                    else
                    {
                        if (foundReceiver.Color.Color != receiver.Color.Color)
                        {
                            foundReceiver.Color = receiver.Color;
                            foreach (var logMessage in state.AllLogs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                logMessage.Receiver.Color = foundReceiver.Color;
                            foreach (var logMessage in state.Logs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                logMessage.Receiver.Color = foundReceiver.Color;
                        }
                        if (foundReceiver.Name != receiver.Name)
                        {
                            foundReceiver.Name = receiver.Name;
                            foreach (var log in state.AllLogs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                log.Receiver.Name = foundReceiver.Name;
                            foreach (var log in state.Logs.Where(x => x.Receiver.Port == foundReceiver.Port))
                                log.Receiver.Name = foundReceiver.Name;
                        }
                    }
                }

                foreach (var receiver in receivers.Receivers.ToList())
                {
                    if (settings.Receivers.All(x => x.Port != receiver.Port))
                        receivers.Receivers.Remove(receiver);
                }

                receivers.RecreateUdpSources();
                receivers.RefreshColorColumnWidthAfterSettings();

                if (highlight != null)
                {
                    highlight.ReplaceRules(settings.HighlightRules == null
                        ? null
                        : settings.HighlightRules.Select(r => r.ToCore()));
                    highlight.RecolorAll(state);
                }
            }
            catch (Exception e)
            {
                Logger.Warn(e, "An error occurred while save settings");
            }
            finally
            {
                state.IsBusy = false;
            }
        }
    }
}
