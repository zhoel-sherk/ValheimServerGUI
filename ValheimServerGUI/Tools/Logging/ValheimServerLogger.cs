using Serilog;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging.Components;

namespace ValheimServerGUI.Tools.Logging
{
    public interface IValheimServerLogger : IBaseLogger
    {
    }

    public class ValheimServerLogger : BaseLogger, IValheimServerLogger
    {
        private readonly IValheimServerOptions Options;

        public ValheimServerLogger(IValheimServerOptions options)
        {
            Options = options;

            // Remove default timestamp when it's present on a log
            AddRule(RegexTransformer.Remove(@"^\d+\/\d+\/\d+ \d+:\d+:\d+:\s+"));

            if (!Options.LogFilteringDisabled)
            {
                // Ignore excess Unity logs
                AddRule(RegexFilter.Exclude(@"^\(Filename:"));
                AddRule(RegexFilter.Exclude(@"^Console: "));

                // Ignore Unity engine spam (graphics, asset, & physics noise)
                AddRule(RegexFilter.Exclude(@"^Unloading \d+ [Uu]nused"));
                AddRule(RegexFilter.Exclude(@"^UnloadTime: "));
                AddRule(RegexFilter.Exclude(@"^Total: [\d\.]+ ms \("));
                AddRule(RegexFilter.Exclude(@"^OnGUI function detected"));
                AddRule(RegexFilter.Exclude(@"^The shader "));
                AddRule(RegexFilter.Exclude(@"^The image effect "));
                AddRule(RegexFilter.Exclude(@"^Could not find (video decode shader|material Hidden)"));
                AddRule(RegexFilter.Exclude(@"^\[AmplifyOcclusion\]"));
                AddRule(RegexFilter.Exclude(@"^HDR Render Texture not supported"));
                AddRule(RegexFilter.Exclude(@"^This custom render path shader"));
                AddRule(RegexFilter.Exclude(@"^The referenced script on this Behaviour"));
                AddRule(RegexFilter.Exclude(@"^AsyncResourceUpload failed"));
                AddRule(RegexFilter.Exclude(@"^\[Physics::Module\]"));
                AddRule(RegexFilter.Exclude(@"^(Input System module|Input System polling thread)"));
                AddRule(RegexFilter.Exclude(@"^(Setting breakpad minidump|SteamInternal_SetMinidump)"));
                AddRule(RegexFilter.Exclude(@"^Microsoft Media Foundation"));

                // Ignore world generation & matchmaking chatter
                AddRule(RegexFilter.Exclude(@"^Failed to place all "));
                AddRule(RegexFilter.Exclude(@"^Retry join-code check "));
                AddRule(RegexFilter.Exclude(@"^Semaphore type: "));

                // Ignore empty lines
                AddRule(RegexFilter.Exclude(@"^\s*?$"));
            }

            // Add a timestamp after filtering
            AddRule(TimestampTransformer.Default);
        }

        #region BaseLogger overrides

        protected override void ConfigureLogger(LoggerConfiguration config)
        {
            if (Options.LogToFile) AddFileLogging(config, $"ServerLogs-{Options.Name}");
        }

        #endregion
    }
}
