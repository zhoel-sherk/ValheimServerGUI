using System;
using System.Linq;
using ValheimServerGUI.Tools.Logging;
using Xunit;

namespace ValheimServerGUI.Tests.Tools
{
    /// <summary>
    /// String substitutions must be rendered literally. Serilog's default RenderMessage() quotes
    /// scalar strings, which double-quotes any template that already quotes the token - that made
    /// the server run command log read ""C:\path\exe"" with \"Test Server\" inside. Quotes should
    /// only ever come from the template.
    /// </summary>
    public class LoggerRenderingTests : BaseTest
    {
        [Fact]
        public void StringSubstitutions_AreNotAutoQuoted()
        {
            var logger = GetService<IApplicationLogger>();
            var name = $"Prof-{Guid.NewGuid():N}";

            logger.Information("Loading profile '{name}' on port {port}", name, 2456);

            var line = logger.LogBuffer.Last(l => l.Contains(name));
            Assert.Contains($"Loading profile '{name}' on port 2456", line);
            Assert.DoesNotContain($"\"{name}\"", line);
        }

        [Fact]
        public void QuotedArgument_IsNotDoubleQuotedOrEscaped()
        {
            var logger = GetService<IApplicationLogger>();
            var exe = @"C:\Valheim\valheim_server.exe";
            var name = $"World-{Guid.NewGuid():N}";

            logger.Information("Server run command: \"{exe}\" -name \"{name}\"", exe, name);

            var line = logger.LogBuffer.Last(l => l.Contains(exe));
            Assert.Contains($"\"{exe}\" -name \"{name}\"", line);
            Assert.DoesNotContain("\\\"", line);
            Assert.DoesNotContain($"\"\"{name}\"\"", line);
        }

        [Fact]
        public void NonStringSubstitutions_RenderNormally()
        {
            var logger = GetService<IApplicationLogger>();
            var marker = Guid.NewGuid().ToString("N");

            logger.Information("probe {marker} count {count} ratio {ratio:G}", marker, 42, 1.5);

            var line = logger.LogBuffer.Last(l => l.Contains(marker));
            Assert.Contains("count 42", line);
            Assert.Contains("ratio 1.5", line);
        }
    }
}