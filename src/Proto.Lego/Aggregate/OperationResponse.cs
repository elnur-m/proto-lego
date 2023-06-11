namespace Proto.Lego.Aggregate;

public partial class OperationResponse
{
    public OperationResponse(bool success)
    {
        Success = success;
    }

    public OperationResponse(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    public static implicit operator OperationResponse(bool success) => new(success);

    public static implicit operator OperationResponse(string errorMessage) => new(errorMessage);
}