# Bank Accounts
This is an example of an app built with Proto.Lego. It consists of an Account aggregate and 3 workflows. Nothing fancy.
Account serves to hold the balance of a person. The goals of workflows are pretty obvious from there names (which is good I assume):
- Create account workflow
- Add funds worfklow
- Transfer funds workflow

## Setup
You need a Postrgesql database to run this project on your local.
- Execute the sql in [script.sql](https://github.com/elnur-m/proto-lego/blob/master/src/Proto.Lego.Persistence.Npgsql/Database/scripts.sql) on your db
- Add the connection string for that db into [appsettings.Development.json](https://github.com/elnur-m/proto-lego/blob/master/examples/BankAccounts/src/BankAccounts.WebApi/appsettings.Development.json)'s ConnectionStrings section as "Postgresql"

That should be it. Run the **BankAccounts.WebApi** app.

## The rules
First, you need to create an account by calling the respective endpoint. Then you need to add funds. And then you can transfer these funds to some other (existing) account.
Basically, to be able to transfer funds both accounts need to be created and the sender needs to have enough funds on his/her balance.
The RequestId field on the request models gets mapped to Workflow's id. This is done so that the client can retry the request with the same id in case of network issues.

## Implementation of the protocol
As you may have noticed the agregates handle Prepare, Confirm, Cancel and Execute operations. This is done to cope with lack of atomicity accross aggregate boundaries.
Let's take TransferFunds workflow as an example. To transfer funds you need to call at least 2 aggregates. Lacking atomicity we need a way to be able to guarantee that in case of
network problems or availability problems of concurrency issues the state will not be corrupted. This is where Prepare comes into play.
The TransferFunds workflow sends a "prepare to subtract N dollars" to aggregate X. If X exists and has enough funds it replies with a success message. At this point X does not subtract the funds from the balance. It just understands that this funds might be subtracted soon and kinda blocks them. Then the workflow then sends "prepare to receive N dollars" to aggregate Y. If Y exists, it replies with success. Then the workflow sends Confirm to both aggregates and the actual transfer happens. Only at this point in time X subtracts the funds from the balance and Y adds them. Happy end.
In case if X does not have enough funds, it replies with a failure message and the workflow just stops there.
The interesting (for a nerd) thing happens if Subtract for X succeeds and add for Y fails. In that case the workflow will send a Cancel message to X and X will unblock the funds that it has blocked.

## The afterlife
When the workflow is done, it starts to clean up after itself. First, it calls the aggregates that it has talked to and sends them a special message which makes the aggregates to
clear the state associated with this workflow. This is done to prevent bloating usage of memory and storage. After the aggregates are done cleaning up, the workflow removes itself
from the WorkflowStorage and stops itself (it's an actor. You remember, right?).
If you want to store the workflow state for a bit longer to give your app a time window where it can safely ignore duplicate messages from the clients, you can override the ```BeforeCleanUpAsync``` method
and add ```await Task.Delay(60*1000)``` there. It will make the workflow to wait for 60 seconds before starting to clean up.
