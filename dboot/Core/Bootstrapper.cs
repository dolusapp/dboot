using dboot.Builder;
using dboot.Builder.Options;
using dboot.SubSystem;
using Serilog;
namespace dboot.Core;

public class Bootstrapper
{

    private readonly DialogOptions _dialogOptions;
    private readonly List<StepFunction> _installSteps;
    private readonly List<StepFunction> _uninstallSteps;
    private readonly Context _bootContext;
    private readonly BootstrapperOptions _options;
    private readonly Func<ValueTask>? _postInstallCallback;
    private readonly Func<ValueTask>? _postUnInstallCallback;
    public Bootstrapper(DialogOptions? dialogOptions,
                        List<StepFunction> installSteps,
                        List<StepFunction> uninstallSteps,
                        Context bootContext,
                        BootstrapperOptions options,
                        Func<ValueTask>? postInstallCallback,
                        Func<ValueTask>? postUnInstallCallback)
    {
        _dialogOptions = dialogOptions ?? throw new ArgumentNullException(nameof(dialogOptions));
        _installSteps = installSteps ?? throw new ArgumentNullException(nameof(installSteps));
        _uninstallSteps = uninstallSteps ?? throw new ArgumentNullException(nameof(uninstallSteps));
        _bootContext = bootContext ?? throw new ArgumentNullException(nameof(bootContext));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _postInstallCallback = postInstallCallback;
        _postUnInstallCallback = postUnInstallCallback;
    }

    public async ValueTask<bool> Install(CancellationToken cancellationToken = default)
    {
        bool success = await ExecuteSteps(_installSteps, "Installation", cancellationToken);
        if (success && _postInstallCallback != null)
        {
            await _postInstallCallback();
        }
        return success;
    }


    public async ValueTask<bool> Uninstall(CancellationToken cancellationToken = default)
    {
        bool success = await ExecuteSteps(_uninstallSteps, "Uninstallation", cancellationToken);
        if (success && _postUnInstallCallback != null)
        {
            await _postUnInstallCallback();
        }
        return success;
    }

    private async ValueTask<bool> ExecuteSteps(List<StepFunction> steps, string operationType, CancellationToken cancellationToken)
    {
        ProgressDialog? progressDialog = null;

        try
        {
            progressDialog = new ProgressDialog
            {
                Title = _dialogOptions.Title,
                IconData = _dialogOptions.Icon,
                AutoClose = _dialogOptions.AutoClose,
                ShowTimeRemaining = _dialogOptions.ShowTimeRemaining,
                Marquee = true
            };

            if (!_options.IsQuiet)
            {
                progressDialog.Show();
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Warning("{OperationType} cancelled by user.", operationType);
                    return false;
                }

                // Reset dialog lines before each step
                progressDialog.Line1 = string.Empty;
                progressDialog.Line2 = string.Empty;
                progressDialog.Line3 = string.Empty;

                try
                {
                    StepResult stepResult = await steps[i](progressDialog, _bootContext, cancellationToken);

                    switch (stepResult)
                    {
                        case StepResult.Continue:

                            continue;
                        case StepResult.Abort:
                            Log.Error("{OperationType} aborted at step {StepNumber}.", operationType, i + 1);
                            if (!_options.IsQuiet)
                            {
                                // Preserve custom messages if they were set
                                if (string.IsNullOrEmpty(progressDialog.Line1))
                                    progressDialog.Line1 = $"{operationType} failed!";
                                if (string.IsNullOrEmpty(progressDialog.Line2))
                                    progressDialog.Line2 = $"Step {i + 1} encountered an error.";
                                progressDialog.SetCancelButtonText("Close");
                                progressDialog.CancelMessage = "";
                                await WaitForUserAcknowledgment(progressDialog, cancellationToken);
                            }
                            return false; // Immediately return false on Abort
                        case StepResult.Stop:
                            Log.Information("{OperationType} stopped at step {StepNumber}. No further actions required.", operationType, i + 1);
                            return true; // Stop means successful completion
                    }
                }
                catch (OperationCanceledException ex) when (ex is TaskCanceledException)
                {
                    Log.Warning(ex, "{OperationType} step {StepNumber} was canceled.", operationType, i + 1);
                    if (!_options.IsQuiet)
                    {
                        // Preserve custom messages if they were set
                        if (string.IsNullOrEmpty(progressDialog.Line1))
                            progressDialog.Line1 = $"{operationType} was canceled.";
                        if (string.IsNullOrEmpty(progressDialog.Line2))
                            progressDialog.Line2 = $"Step {i + 1} did not complete successfully.";
                        await WaitForUserAcknowledgment(progressDialog, cancellationToken);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{OperationType} step {StepNumber} encountered an unexpected error.", operationType, i + 1);
                    SentrySdk.CaptureException(ex);
                    if (!_options.IsQuiet)
                    {
                        // Preserve custom messages if they were set
                        if (string.IsNullOrEmpty(progressDialog.Line1))
                            progressDialog.Line1 = $"{operationType} failed!";
                        if (string.IsNullOrEmpty(progressDialog.Line2))
                            progressDialog.Line2 = $"Step {i + 1} encountered an error.";
                        progressDialog.SetCancelButtonText("Close");
                        progressDialog.CancelMessage = "";
                        await WaitForUserAcknowledgment(progressDialog, cancellationToken);
                    }
                    return false; // Immediately return false on unexpected error
                }
            }

            // All steps completed successfully
            if (!_options.IsQuiet && !_bootContext.IsAlreadyInstalled)
            {
                progressDialog.Line1 = $"{operationType} completed successfully!";
                progressDialog.Line2 = "You can close this window.";
                progressDialog.Line3 = string.Empty;
                progressDialog.SetCancelButtonText("Close");
                progressDialog.CancelMessage = "";

                if (_dialogOptions.AutoClose)
                {
                    progressDialog.Close();
                }
                else
                {
                    await WaitForUserAcknowledgment(progressDialog, cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{OperationType} encountered an unexpected error.", operationType);
            SentrySdk.CaptureException(ex);
            return false;
        }
        finally
        {
            progressDialog?.Close();
            progressDialog?.Dispose();
        }
    }

    private async Task WaitForUserAcknowledgment(ProgressDialog progressDialog, CancellationToken cancellationToken)
    {
        while (!progressDialog.HasUserCancelled && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

}