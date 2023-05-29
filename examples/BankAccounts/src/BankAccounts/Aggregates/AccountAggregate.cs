using BankAccounts.Aggregates.Account;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Lego.Aggregate;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Persistence;

namespace BankAccounts.Aggregates;

public class AccountAggregate : Aggregate<AccountAggregateState>
{
    public const string AggregateKind = "AccountAggregate";

    public AccountAggregate(IKeyValueStateStore stateStore, ILogger<Aggregate<AccountAggregateState>> logger)
        : base(stateStore, logger)
    {
        Kind = AggregateKind;
    }

    protected override OperationResponse Prepare(Any action)
    {
        if (action.Is(Add.Descriptor))
        {
            return PrepareAdd(action.Unpack<Add>());
        }

        if (action.Is(Subtract.Descriptor))
        {
            return PrepareSubtract(action.Unpack<Subtract>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Confirm(Any action)
    {
        if (action.Is(Add.Descriptor))
        {
            return ConfirmAdd(action.Unpack<Add>());
        }

        if (action.Is(Subtract.Descriptor))
        {
            return ConfirmSubtract(action.Unpack<Subtract>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Cancel(Any action)
    {
        if (action.Is(Add.Descriptor))
        {
            return CancelAdd(action.Unpack<Add>());
        }

        if (action.Is(Subtract.Descriptor))
        {
            return CancelSubtract(action.Unpack<Subtract>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Execute(Any action)
    {
        if (action.Is(Create.Descriptor))
        {
            return ExecuteCreate(action.Unpack<Create>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    private OperationResponse ExecuteCreate(Create create)
    {
        if (InnerState.Exists)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Already exists"
            };
        }

        InnerState.Exists = true;

        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse PrepareAdd(Add add)
    {
        if (!InnerState.Exists)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Account does not exist"
            };
        }

        if (add.Amount <= 0)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Amount must be positive"
            };
        }

        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse ConfirmAdd(Add add)
    {
        InnerState.TotalFunds += add.Amount;

        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse CancelAdd(Add add)
    {
        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse PrepareSubtract(Subtract subtract)
    {
        if (!InnerState.Exists)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Account does not exist"
            };
        }

        if (subtract.Amount <= 0)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Amount must be positive"
            };
        }

        if (InnerState.TotalFunds - InnerState.BlockedFunds < subtract.Amount)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "Insufficient funds"
            };
        }

        InnerState.BlockedFunds += subtract.Amount;

        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse ConfirmSubtract(Subtract subtract)
    {
        InnerState.TotalFunds -= subtract.Amount;
        InnerState.BlockedFunds -= subtract.Amount;

        return new OperationResponse
        {
            Success = true
        };
    }

    private OperationResponse CancelSubtract(Subtract subtract)
    {
        InnerState.BlockedFunds -= subtract.Amount;

        return new OperationResponse
        {
            Success = true
        };
    }
}