# Proto.Lego
Scalable distributed applications with exaclty-once processing out of the box.

## Example application
You can take a look at the example in the [examples/BankAccounts](https://github.com/elnur-m/proto-lego/tree/master/examples/BankAccounts)

## Installing

```
PM> Install-Package Proto.Lego
PM> Install-Package Proto.Lego.Persistence.InMemory
```

## High-level overview
Proto.Lego is a framework for building scalabale distributed applications without distributed transactions.
It's inspired by Pat Helland's paper, "Life beyond Distributed Transactions: an Apostate’s Opinion".
As the paper says, we operate based on 3 assumptions:
- Scalable applications consist of (at least) 2 layers. Lower scale-aware level and upper scale-agnostic level
- Scalable applications use multiple disjoint scopes of transactional serializability instead of global transactional serializability
- Scalable applications use at-least-once messaging

Proto.Lego is built upon Proto.Actor which serves as the lower scale-aware level based on actor model.
The upper level of a Proto.Lego app consists of 2 kinds of virtual actors: **Aggregates** and **Workflows**. Both Aggregates and Workflows are
implemented using virtual actors.

### Components
Aggregates are what Pat Helland calls Entities in his paper. These serve as serializability scopes. An Aggregate holds state and replies to messages.
Diverging from the paper Proto.Lego adds a constraint – Aggregates must be called from Workflows. An implication of this constraint is that
Aggregates do not talk to each other and all processing happens by Workflows orchestrating Aggregates. Aggregates hold per-worfklow state
like the number of received messages and replies sent back to be able to operate with duplicate messages (remember at-least-once?).

### Working with uncertainty. The Prepare-Confirm-Cancel protocol
Since we don't have atomicity accross Aggregate borders we need some kind of mechanism to prevent state corruption when interacting with multiple Aggregates.
The paper talks about uncertainty and working with it by accepting it and resolving on business's level rather than holding locks on resources.
Proto.Lego makes use of a protocol consisting of 3 operations: Prepare, Confirm, Cancel. The way it works if as follows.
If you're uncertain about executing an action (there is a chance you will initiate a rollback) you first wrap this action in an ```Operation``` with ```OperationType``` set to Prepare.
The receiving Aggregate validates the command and if valid it blocks the resources needed to perform this action. In the BankAccount the AccountAggregate
will add the requested amount to its ```BlockedFunds``` property and return a success result. Then, if you decide to proceed with the action, you call the Aggregate
with the same action wrapped in an ```Operation``` with ```OperationType``` set to Confirm. If you want to cancel the action, you set the ```OperationType``` to Cancel
which tells the Aggregate the free up the blocked resources.
There is 1 more operation type which is Execute, but it's there for basically being able to execute the actions that are certain and will not be rollbacked in any case.

## Workflows

### Overview
Workflows are basically durable functions. When triggered with a message containing input, they first persist their state to the WorkflowStore
and then execute whatever is written in their ExecuteAsync method. On errors they retry. On any topology changes (new node added, process failed, etc)
```app.Services.UseWorkflowTriggering()``` makes sure that one of the nodes participating in the cluster triggers all the workflows in the WorkflowState
so that they wake up and start executing if needed.

### Lifecycle
A workflow is instantiated when it receives a message of it's ```TInput``` type. First, it persists its state and starts executing ```ExecuteAsync``` method in the background.
By executing in the background the workflow stays responsive for incoming messages during execution. The execution can either succeed or fail. Every workflow has ```State.Result.Succeeded```
and ```State.Result.ErrorMessages``` properties. You can set their values inside the ```ExecuteAsync``` method if you need these values later.
After ```ExecuteAsync``` is finished the Workflow marks itself as done and persists its state to WorkflowStore. After this is done the ```ExecuteAsync``` method will never be executed again,
even if the Worfklow restarts.
The next step is to call the Aggregates the Workflow interacted with to tell them to clear their state for this workflow. This is done to prevent memory and storage usage bloating.
The Workflow keeps track of Aggregates it has called so you don't need to do it. After the aggregates are done cleaning up, the workflow removes itself
from the WorkflowStorage and stops itself (it's an actor. You remember, right?).
If you want to store the workflow state for a bit longer to give your app a time window where it can safely ignore duplicate messages from the clients, you can override the ```BeforeCleanUpAsync``` method
and add ```await Task.Delay(60*1000)``` there. It will make the workflow to wait for 60 seconds before starting to clean up. 

### Useful bits
- Everything that interacts with a workflow needs to be idempotent. Aggregates adhere to this rule by persisting a per-workflow state
- Workflows are actors and they can live long. You can use things like ```await Task.Delay()``` safely and store state in memory
- Workflows retry infinitely. So make sure you test your workflows properly. If there is a case when a workflow is not able to complete, it will be caught in an endless retry loop.

## Aggregates

### Overview
Aggregates are serializability scopes. You can think of them as entities. User, BankAccount, Car, Order are simple examples of an Aggregate. Basically it encapsulates state and rules to modify it.
Aggregates implement the protocol mentioned above. Aggregates are also virtual actors so they can live long and hold state in their memory which is totally private.
Aggregates hold per-workflow communication state, so if a Workflow restarts and sends a message that the Aggregate has seen before, the Aggregate will not execute the operation the second time.
It will just reply to the Workflow with the message that it had replied with before. This way we achieve exactly-once processing between Aggregates and Workflows.

### Lifecycle
Basically an Aggregate always exists. So if you're implementing something like a bank account and need to make sure that the accounts participating in a transfer transaction are existing
you need to explicitly handle it on the business level. Storing something like a ```Exists``` property in the Aggregate's TState might be enough.
That's pretty much it, the Aggregate always exists.

### Useful bits
- Aggregates validate the incoming messages for adherence to the protocol. If you send a Confirm or Cancel without a preceding Prepare, the Aggregate will automatically respond with a failure message
- Aggregates store the per-workflow state automatically, you don't need to do it manually
