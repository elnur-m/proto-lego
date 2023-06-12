namespace Proto.Lego.Workflow;

public class WorkflowClientResponse<TInput>
{
    public WorkflowClientResponse(TInput input, WorkflowResult result)
    {
        Input = input;
        Result = result;
    }

    public TInput Input { get; set; }
    public WorkflowResult Result { get; set; }
}