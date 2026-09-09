using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using ValheimServerGUI.Properties;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Models;

namespace ValheimServerGUI.Tools
{
    public interface IExceptionHandler
    {
        event EventHandler ExceptionHandled;

        void HandleException(Exception e, string contextMessage = null);
    }

    public class ExceptionHandler : IExceptionHandler
    {
        private readonly IApplicationLogger Logger;

        public ExceptionHandler(IApplicationLogger logger)
        {
            Logger = logger;
        }

        public event EventHandler ExceptionHandled;

        public void HandleException(Exception e, string contextMessage = null)
        {
            if (e == null) return;

            e = e.GetPrimaryException();

            contextMessage ??= "Unknown Exception";
            var userMessage = $"A fatal error has occured: {e.Message}{Environment.NewLine}{Environment.NewLine}Would you like to save a crash report to the logs folder?";

            var result = MessageBox.Show(
                userMessage,
                contextMessage,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);

            if (result == DialogResult.Yes)
            {
                SaveCrashReport(e, contextMessage);
            }

            ExceptionHandled?.Invoke(this, EventArgs.Empty);
        }

        private void SaveCrashReport(Exception e, string contextMessage)
        {
            try
            {
                var crashReport = BuildCrashReport(e, contextMessage);
                var fileName = Path.Join(Resources.LogsFolderPath, $"crashreport_{DateTime.Now.ToFilenameISOFormat()}.json");

                File.WriteAllText(fileName, JsonConvert.SerializeObject(crashReport, Formatting.Indented));

                MessageBox.Show(
                    $"Crash report saved:{Environment.NewLine}{fileName}{Environment.NewLine}{Environment.NewLine}Attach it to a GitHub issue if you'd like to report the problem.",
                    "Crash Report",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception saveException)
            {
                Logger.Error(saveException, "Failed to save crash report");
            }
        }

        private CrashReport BuildCrashReport(Exception e, string contextMessage)
        {
            var crashReport = AssemblyHelper.BuildCrashReport();

            var additionalInfo = new Dictionary<string, string>
            {
                { "ExceptionType", e.GetType().Name },
                { "Message", e.Message },
                { "Context", contextMessage },
                { "Source", e.Source },
                { "TargetSite", e.TargetSite?.ToString() },
                { "StackTrace", e.StackTrace },
            };

            crashReport.Source = "CrashReport";
            crashReport.AdditionalInfo = additionalInfo;
            crashReport.Logs = Logger.LogBuffer.Reverse().Take(100).ToList();

            return crashReport;
        }
    }
}
