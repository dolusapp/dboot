
namespace dboot.Builder
{
    public class InstallStepBuilder(List<StepFunction> steps)
    {
        public InstallStepBuilder AddStep(StepFunction step)
        {
            steps.Add(step);
            return this;
        }
    }
}