using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AIGRAV.Contracts;

/// <summary>
/// Centralized failure handling for tool transactions adopted from RevitCortex.
/// Suppresses modal TaskDialog warnings on the UI thread to prevent freezing the MCP bridge.
/// Warnings are collected into a list, and fatal errors trigger a clean rollback.
/// </summary>
public static class TransactionFailureHandling
{
    public static FailureCapture SuppressWarnings(Transaction tx)
    {
        var capture = new FailureCapture();
        var options = tx.GetFailureHandlingOptions();
        options.SetFailuresPreprocessor(capture);
        options.SetClearAfterRollback(true);
        tx.SetFailureHandlingOptions(options);
        return capture;
    }

    public static string Describe(FailureCapture capture)
    {
        if (capture.Errors.Count == 0)
            return "Revit rolled back the transaction (no error description available)";
        var take = capture.Errors.Count > 3 ? 3 : capture.Errors.Count;
        var head = string.Join("; ", capture.Errors.GetRange(0, take));
        return capture.Errors.Count > take ? head + $"; (+{capture.Errors.Count - take} more)" : head;
    }

    public sealed class FailureCapture : IFailuresPreprocessor
    {
        public List<string> Errors { get; } = new List<string>();
        public int WarningsSuppressed { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var hasError = false;
            foreach (var failure in failuresAccessor.GetFailureMessages())
            {
                var severity = failure.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    WarningsSuppressed++;
                    failuresAccessor.DeleteWarning(failure);
                }
                else if (severity == FailureSeverity.Error
                         || severity == FailureSeverity.DocumentCorruption)
                {
                    hasError = true;
                    Errors.Add(failure.GetDescriptionText());
                }
            }

            return hasError
                ? FailureProcessingResult.ProceedWithRollBack
                : FailureProcessingResult.Continue;
        }
    }
}
