
namespace dboot.Builder;

public class UninstallStepBuilder(List<StepFunction> steps)
{
    public UninstallStepBuilder AddStep(StepFunction step)
    {
        steps.Add(step);
        return this;
    }
}