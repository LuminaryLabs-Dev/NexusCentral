using System.Linq;
using System.Text;

namespace NexusCentral.V3.Features.Lab;

public static class LabTranscriptFormatter
{
    public static string FormatMessage(ChatMessage message)
    {
        var builder = new StringBuilder();
        builder.AppendLine(message.Role.ToString());

        foreach (var attachment in message.Attachments)
        {
            builder.AppendLine($"  + {attachment.FileName}");
        }

        foreach (var step in message.ToolSteps)
        {
            builder.AppendLine($"  Using {step.ToolName}");
            if (!string.IsNullOrWhiteSpace(step.Summary))
            {
                builder.AppendLine($"    {step.Summary}");
            }
        }

        foreach (var line in (message.Text ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine($"  {line}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
