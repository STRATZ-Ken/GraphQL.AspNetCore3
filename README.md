# GraphQL.AspNetCore3

[![NuGet](https://img.shields.io/nuget/v/GraphQL.AspNetCore3.svg)](https://www.nuget.org/packages/GraphQL.AspNetCore3) [![Coverage Status](https://coveralls.io/repos/github/Shane32/GraphQL.AspNetCore3/badge.svg?branch=master)](https://coveralls.io/github/Shane32/GraphQL.AspNetCore3?branch=master)

This package is designed for ASP.Net Core (2.1 through 6.0) to facilitate easy set-up of GraphQL requests
over HTTP.  The code is designed to be used as middleware within the ASP.Net Core pipeline,
serving GET, POST or WebSocket requests.  GET requests process requests from the querystring.
POST requests can be in the form of JSON requests, form submissions, or raw GraphQL strings.
WebSocket requests can use the `graphql-ws` or `graphql-transport-ws` WebSocket sub-protocol,
as defined in the [apollographql/subscriptions-transport-ws](https://github.com/apollographql/subscriptions-transport-ws)
and [enisdenjo/graphql-ws](https://github.com/enisdenjo/graphql-ws) respoitories, respectively.
The `graphql-subscriptions` sub-protocol is not supported.

The middleware can be configured through the `IApplicationBuilder` or `IEndpointRouteBuilder`
builder interfaces.

In addition, an `ExecutionResultActionResult` class is added for returning `ExecutionResult`
instances directly from a controller action.

Authorization is also supported with the included `AuthorizationValidationRule`.  It will
scan GraphQL documents and validate that the schema and all referenced output graph types, fields of
output graph types, and query arguments meet the specified policy and/or roles held by the
authenticated user within the ASP.NET Core authorization framework.  It does not validate
any policies or roles specified for input graph types, fields of input graph types, or
directives.  It does not skip validation for fields that are marked with the `@skip` or
`@include` directives.

See [version history / migration notes](https://github.com/Shane32/GraphQL.AspNetCore3/blob/master/migration.md)
for changes from previous versions.

## Configuration

### Typical configuration with HTTP middleware

First add the `GraphQL.AspNetCore3` nuget package to your application.  It requires
`GraphQL` version 5.3.3 or a later 5.x version.

Second, install the `GraphQL.SystemTextJson` or `GraphQL.NewtonsoftJson` package within your
application if you have not already done so.  For best performance, please use the
`GraphQL.SystemTextJson` package.

Then update your `Program.cs` or `Startup.cs` to register the schema, the serialization engine,
and optionally the HTTP middleware and WebSocket services.  Configure WebSockets and GraphQL
in the HTTP pipeline by calling `UseWebSockets` and `UseGraphQL` at the appropriate point.
Below is a complete sample of a .NET 6 console app that hosts a GraphQL endpoint at
`http://localhost:5000/graphql`:

#### Project file

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="GraphQL.AspNetCore3" Version="4.0.1" />
    <PackageReference Include="GraphQL.SystemTextJson" Version="5.3.3" />
  </ItemGroup>

</Project>
```

#### Program.cs file

```csharp
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using GraphQL.AspNetCore3;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()  // schema
    .AddSystemTextJson());   // serializer

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseWebSockets();
app.UseGraphQL("/graphql");  // url to host GraphQL endpoint
await app.RunAsync();
```

#### Schema

```csharp
public class Query
{
    public static string Hero() => "Luke Skywalker";
}
```

#### Sample request url

```
http://localhost:5000/graphql?query={hero}
```

#### Sample response

```json
{"data":{"hero":"Luke Skywalker"}}
```

### Configuration with endpoint routing

To use endpoint routing, call `MapGraphQL` from inside the endpoint configuration
builder rather than `UseGraphQL` on the application builder.  See below for the
sample of the application builder code:

```csharp
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseRouting();
app.UseEndpoints(endpoints => {
    endpoints.MapGraphQL("graphql");
});
await app.RunAsync();
```

### Configuration with a MVC controller

Although not recommended, you may set up a controller action to execute GraphQL
requests.  You will not need `UseGraphQL` or `MapGraphQL` in the application
startup.  Below is a very basic sample; a much more complete sample can be found
in the `ControllerSample` project within this repository.  Although technically
possible, the sample does not include support for WebSocket requests.

#### HomeController.cs

```csharp
public class HomeController : Controller
{
    private readonly IDocumentExecuter _documentExecuter;

    public TestController(IDocumentExecuter<ISchema> documentExecuter)
    {
        _documentExecuter = documentExecuter;
    }

    [HttpGet]
    public async Task<IActionResult> GraphQL(string query)
    {
        var result = await _documentExecuter.ExecuteAsync(new() {
            Query = query,
            RequestServices = HttpContext.RequestServices,
            CancellationToken = HttpContext.RequestAborted,
        });
        return new ExecutionResultActionResult(result);
    }
}
```

### User context configuration

To set the user context to be used during the execution of GraphQL requests,
call `AddUserContextBuilder` during the GraphQL service setup to set a delegate
which will be called when the user context is built.  Alternatively, you can
register an `IUserContextBuilder` implementation to do the same.

#### Program.cs / Startup.cs

```csharp
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()
    .AddSystemTextJson()
    .AddUserContextBuilder(httpContext => new MyUserContext(httpContext));
```

#### MyUserContext.cs

```csharp
public class MyUserContext : Dictionary<string, object?>
{
    public ClaimsPrincipal User { get; }

    public MyUserContext(HttpContext context) : base()
    {
        User = context.User;
    }
}
```

### Authorization configuration

You can configure authorization for all GraphQL requests, or for individual
graphs, fields and query arguments within your schema.  Both can be used
if desired.

Be sure to call `app.UseAuthentication()` and `app.UseAuthorization()` prior
to the call to `app.UseGraphQL()`.  For example:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.UseGraphQL("/graphql");
```

#### For all GraphQL requests (including introspection requests)

When calling UseGraphQL, specify options as necessary to enable authorization as required.

```csharp
app.UseGraphQL("/graphql", config => {
    // require that the user be authenticated
    config.AuthorizationRequired = true;

    // require that the user be a member of at least one role listed
    config.AuthorizedRoles.Add("MyRole");
    config.AuthorizedRoles.Add("MyAlternateRole");

    // require that the user pass a specific authorization policy
    config.AuthorizedPolicy = "MyPolicy";
});
```

Once configured, the request is authorized prior to parsing of the document or accepting
the WebSocket request.  Since WebSocket requests from browsers cannot typically carry a HTTP
Authorization header, for JWT Bearer authentication you may need to disable authorization
requirements for WebSocket connections and instead install an authentication handler for
connection requests.  To do so, use set the `WebSocketsRequireAuthorization` property to
`false` and register an `IWebSocketAuthorizationService` as a singleton within your
dependency injection framework.  Below is an example:

```cs
services.AddSingleton<IWebSocketAuthorizationService, MyAuthService>();

app.UseGraphQL("/graphql", config => {
    // require that the user be authenticated
    config.AuthorizationRequired = true;
});

class MyAuthService : IWebSocketAuthenticationService
{
    private readonly IGraphQLSerializer _serializer;

    public MyWSAuthService(IGraphQLSerializer serializer)
    {
        _serializer = serializer;
    }

    public async ValueTask<bool> AuthenticateAsync(IWebSocketConnection connection, OperationMessage operationMessage)
    {
        var payload = _serializer.ReadNode<Inputs>(operationMessage.Payload);
        if (payload?.TryGetValue("Authorization", out var value) ?? false) {
            ClaimsPrincipal user;
            // validate token here and set user
            connection.HttpContext.User = user;
            return true; // authentication successful
        }
        return false; // authentication failed
    }
}
```

You may also wish to use `IWebSocketAuthenticationService` to set the `HttpContext.User` property, even
if you allow anonymous requests.

#### For individual graphs, fields and query arguments

To configure ASP.NET Core authorization for GraphQL, add the corresponding
validation rule during GraphQL configuration, typically by calling `.AddAuthorization()`
as shown below:

```csharp
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()
    .AddSystemTextJson()
    .AddAuthorization());
```

Both roles and policies are supported for output graph types, fields on output graph types,
and query arguments.  If multiple policies are specified, all must match; if multiple roles
are specified, any one role must match.  You may also use `.Authorize()` or the
`[Authorize]` attribute to validate that the user has authenticated.  You may also use
`.AllowAnonymous()` and `[AllowAnonymous]` to allow fields to be returned to
unauthenticated users within an graph that has an authorization requirement defined.

Please note that authorization rules do not apply to values returned within introspection requests,
potentially leaking information about protected areas of the schema to unauthenticated users.
You may use the `ISchemaFilter` to restrict what information is returned from introspection
requests, but it will apply to both authenticated and unauthenticated users alike.

Introspection requests are allowed unless the schema has an authorization requirement set on it.
The `@skip` and `@include` directives are honored, skipping authorization checks for fields
skipped by `@skip` or `@include`.

Please note that if you use interfaces, validation might be executed against the graph field
or the interface field, depending on the structure of the query.  For instance:

```gql
{
  cat {
    # validates against Cat.Name
    name

    # validates against Animal.Name
    ... on Animal {
      name
    }
  }
}
```

Similarly for unions, validation occurs on the exact type that is queried.  Be sure to carefully
consider placement of authorization rules when using interfaces and unions, especially when some
fields are marked with `AllowAnonymous`.

### UI configuration

This project does not include user interfaces, such as GraphiQL or Playground,
but you can include references to the ones provided by the [GraphQL Server](https://github.com/graphql-dotnet/server)
repository which work well with ASP.Net Core 3.1+.  Below is a list of the nuget packages offered:

| Package                                              | Downloads                                                                                                                                                                             | NuGet Latest                                                                                                                                                                         |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| GraphQL.Server.Ui.Altair                             | [![Nuget](https://img.shields.io/nuget/dt/GraphQL.Server.Ui.Altair)](https://www.nuget.org/packages/GraphQL.Server.Ui.Altair)                                                         | [![Nuget](https://img.shields.io/nuget/v/GraphQL.Server.Ui.Altair)](https://www.nuget.org/packages/GraphQL.Server.Ui.Altair)                                                         |
| GraphQL.Server.Ui.Playground                         | [![Nuget](https://img.shields.io/nuget/dt/GraphQL.Server.Ui.Playground)](https://www.nuget.org/packages/GraphQL.Server.Ui.Playground)                                                 | [![Nuget](https://img.shields.io/nuget/v/GraphQL.Server.Ui.Playground)](https://www.nuget.org/packages/GraphQL.Server.Ui.Playground)                                                 |
| GraphQL.Server.Ui.GraphiQL                           | [![Nuget](https://img.shields.io/nuget/dt/GraphQL.Server.Ui.GraphiQL)](https://www.nuget.org/packages/GraphQL.Server.Ui.GraphiQL)                                                     | [![Nuget](https://img.shields.io/nuget/v/GraphQL.Server.Ui.GraphiQL)](https://www.nuget.org/packages/GraphQL.Server.Ui.GraphiQL)                                                     |
| GraphQL.Server.Ui.Voyager                            | [![Nuget](https://img.shields.io/nuget/dt/GraphQL.Server.Ui.Voyager)](https://www.nuget.org/packages/GraphQL.Server.Ui.Voyager)                                                       | [![Nuget](https://img.shields.io/nuget/v/GraphQL.Server.Ui.Voyager)](https://www.nuget.org/packages/GraphQL.Server.Ui.Voyager)                                                       |

Here is a sample of how this would be configured in your `Program.cs` file:

```csharp
app.UseGraphQL("/graphql");

// add this:
app.UseGraphQLPlayground(
    new GraphQL.Server.Ui.Playground.PlaygroundOptions {
        GraphQLEndPoint = new PathString("/graphql"),
        SubscriptionsEndPoint = new PathString("/graphql"),
    },
    "/");   // url to host Playground at

await app.RunAsync();
```

### CORS configuration

ASP.NET Core supports CORS requests independently of GraphQL, including CORS pre-flight
requests.  To configure your application for CORS requests, add `AddCors()` and `UseCors()`
into the application pipeline.

```csharp
builder.Services.AddCors();

app.UseCors(policy => {
    // configure default policy here
});
```

To configure GraphQL to use a named CORS policy, configure the application to use endpoint routing
and call `RequireCors()` on the endpoint configuration builder.

```csharp
// ...
builder.Services.AddRouting();
builder.Services.AddCors(builder => {
    // configure named and/or default policies here
});

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseWebSockets();
app.UseRouting();
app.UseCors();
app.UseEndpoints(endpoints => {
    // configure the graphql endpoint with the specified CORS policy
    endpoints.MapGraphQL()
        .RequireCors("MyCorsPolicy");
});
await app.RunAsync();
```

### Response compression

ASP.NET Core supports response compression independently of GraphQL, with brotli and gzip
support automatically based on the compression formats listed as supported in the request headers.
To configure your application for response compression, configure your Program/Startup file as
follows:

```csharp
// add and configure the service
builder.Services.AddResponseCompression(options => {
    options.EnableForHttps = true; //may lead to CRIME and BREACH attacks
    options.MimeTypes = new[] { "application/json", "application/graphql+json" };
})

// place this first/early in the pipeline
app.UseResponseCompression();
```

In order to compress GraphQL responses, the `application/graphql+json` content type must be
added to the `MimeTypes` option.  You may choose to enable other content types as well.

Please note that enabling response compression over HTTPS can lead to CRIME and BREACH
attacks.  These side-channel attacks typically affects sites that rely on cookies for
authentication.  Please read [this](https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-6.0)
and [this](http://www.breachattack.com/#howitworks) for more details.

### ASP.NET Core 2.1

You may choose to use the .NET Core 2.1 runtime or a .NET Framework runtime.
This library has been tested with .NET Core 2.1 and .NET Framework 4.8.

The only additional requirement is that you must add this code in your `Startup.cs` file:

```csharp
services.AddHostApplicationLifetime();
```

Besides that requirement, all features are supported in exactly the same manner as
when using ASP.NET Core 3.1+.  You may find differences in the ASP.NET Core runtime,
such as CORS implementation differences, which are outside the scope of this project.

## Advanced configuration

For more advanced configurations, see the overloads and configuration options
available for the various builder methods, listed below.  Methods and properties
contain XML comments to provide assistance while coding with Visual Studio.

| Builder interface | Method | Description |
|-------------------|--------|-------------|
| `IGraphQLBuilder` | `AddUserContextBuilder` | Sets up a delegate to create the UserContext for each GraphQL request. |
| `IApplicationBuilder`   | `UseGraphQL`      | Adds the GraphQL middleware to the HTTP request pipeline. |
| `IEndpointRouteBuilder` | `MapGraphQL`      | Adds the GraphQL middleware to the HTTP request pipeline. |

A number of the methods contain optional parameters or configuration delegates to
allow further customization.  Please review the overloads of each method to determine
which options are available.  In addition, many methods have more descriptive XML
comments than shown above.

### Configuration options

Below are descriptions of the options available when registering the HTTP middleware
or WebSocket handler.  Note that the HTTP middleware options are configured via the
`UseGraphQL` or `MapGraphQL` methods allowing for different options for each configured
endpoint.

#### GraphQLHttpMiddlewareOptions

| Property                           | Description     | Default value |
|------------------------------------|-----------------|---------------|
| `AuthorizationRequired`            | Requires `HttpContext.User` to represent an authenticated user. | False |
| `AuthorizedPolicy`                 | If set, requires `HttpContext.User` to pass authorization of the specified policy. | |
| `AuthorizedRoles`                  | If set, requires `HttpContext.User` to be a member of any one of a list of roles. | |
| `EnableBatchedRequests`            | Enables handling of batched GraphQL requests for POST requests when formatted as JSON. | True |
| `ExecuteBatchedRequestsInParallel` | Enables parallel execution of batched GraphQL requests. | True |
| `HandleGet`                        | Enables handling of GET requests. | True |
| `HandlePost`                       | Enables handling of POST requests. | True |
| `HandleWebSockets`                 | Enables handling of WebSockets requests. | True |
| `ReadExtensionsFromQueryString`    | Enables reading extensions from the query string. | True |
| `ReadQueryStringOnPost`            | Enables parsing the query string on POST requests. | True |
| `ReadVariablesFromQueryString`     | Enables reading variables from the query string. | True |
| `ValidationErrorsReturnBadRequest` | When enabled, GraphQL requests with validation errors have the HTTP status code set to 400 Bad Request. | True |
| `WebSockets`                       | Returns a set of configuration properties for WebSocket connections. | |

#### GraphQLWebSocketOptions

| Property                    | Description          | Default value |
|-----------------------------|----------------------|---------------|
| `ConnectionInitWaitTimeout` | The amount of time to wait for a GraphQL initialization packet before the connection is closed. | 10 seconds |
| `KeepAliveTimeout`          | The amount of time to wait between sending keep-alive packets. | 30 seconds |
| `DisconnectionTimeout`      | The amount of time to wait to attempt a graceful teardown of the WebSockets protocol. | 10 seconds |
| `DisconnectAfterErrorEvent` | Disconnects a subscription from the client if the subscription source dispatches an `OnError` event. | True |
| `DisconnectAfterAnyError`   | Disconnects a subscription from the client there are any GraphQL errors during a subscription. | False |

### Multi-schema configuration

You may use the generic versions of the various builder methods to map a URL to a particular schema.

```csharp
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL<DogSchema>("/graphql/dogs");
app.UseGraphQL<CatSchema>("/graphql/cats");
await app.RunAsync();
```

### Different global authorization settings for different transports (GET/POST/WebSockets)

You may register the same endpoint multiple times if necessary to configure GET connections
with certain authorization options, and POST connections with other authorization options.

```csharp
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL("/graphql", options => {
    options.HandleGet = true;
    options.HandlePost = false;
    options.HandleWebSockets = false;
    options.AuthorizationRequired = false;
});
app.UseGraphQL("/graphql", options => {
    options.HandleGet = false;
    options.HandlePost = true;
    options.HandleWebSockets = true;
    options.AuthorizationRequired = true;
});
await app.RunAsync();
```

Since POST and WebSockets can be used for query requests, it is recommended not to do the above,
but instead add the authorization validation rule and add authorization metadata on the Mutation
and Subscription portions of your schema, as shown below:

```csharp
builder.Services.AddGraphQL(b => b
    .AddSchema<MySchema>()
    .AddSystemTextJson()
    .AddAuthorization()); // add authorization validation rule

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL();
await app.RunAsync();

// demonstration of code-first schema; also possible with schema-first or type-first schemas
public class MySchema : Schema
{
    public MySchema(IServiceProvider provider, MyQuery query, MyMutation mutation) : base(provider)
    {
        Query = query;
        Mutation = mutation;

        mutation.Authorize(); // require authorization for any mutation request
    }
}
```

### Customizing middleware behavior

GET/POST requests are handled directly by the `GraphQLHttpMiddleware`.
For WebSocket requests an `WebSocketConnection` instance is instated to dispatching incoming
messages and send outgoing messages.  Depending on the WebSocket sub-protocols supported by the
client, the proper implementation of `IOperationMessageProcessor` is instated to act as a
state machine, processing incoming messages and sending outgoing messages through the
`WebSocketConnection` instance.

#### GraphQLHttpMiddleware

The base middleware functionality is contained within `GraphQLHttpMiddleware`, with code
to perform execution of GraphQL requests in the derived class `GraphQLHttpMiddleware<TSchema>`.
The classes are organized as follows:

- `InvokeAsync` is the entry point to the middleware.  For WebSocket connection requests,
  execution is immediately passed to `HandleWebSocketsAsync`.
- Methods that start with `Handle` are passed the `HttpContext` and `RequestDelegate`
  instance, and may handle the request or pass execution to the `RequestDelegate` thereby
  skipping this execution handler.  This includes methods to handle execution of single or
  batch queries or returning error conditions.
- Methods that start with `Write` are for writing responses to the output stream.
- Methods that start with `Execute` are for executing GraphQL requests.

A list of methods are as follows:

| Method                      | Description |
|-----------------------------|-------------|
| `InvokeAsync`               | Entry point of the middleware |
| `HandleRequestAsync`        | Handles a single GraphQL request. |
| `HandleBatchRequestAsync`   | Handles a batched GraphQL request. |
| `HandleWebSocketAsync`      | Handles a WebSocket connection request. |
| `BuildUserContextAsync`     | Builds the user context based on a `HttpContext`. |
| `ExecuteRequestAsync`       | Executes a GraphQL request. |
| `ExecuteScopedRequestAsync` | Executes a GraphQL request with a scoped service provider. |
| `SelectResponseContentType` | Selects a content-type header for the JSON-formatted GraphQL response. |
| `WriteErrorResponseAsync`   | Writes the specified error message as a JSON-formatted GraphQL response, with the specified HTTP status code. |
| `WriteJsonResponseAsync`    | Writes the specified object (usually a GraphQL response) as JSON to the HTTP response stream. |

| Error handling method                         | Description |
|-----------------------------------------------|-------------|
| `HandleBatchedRequestsNotSupportedAsync`      | Writes a '400 Batched requests are not supported.' message to the output. |
| `HandleContentTypeCouldNotBeParsedErrorAsync` | Writes a '415 Invalid Content-Type header: could not be parsed.' message to the output. |
| `HandleDeserializationErrorAsync`             | Writes a '400 JSON body text could not be parsed.' message to the output. |
| `HandleInvalidContentTypeErrorAsync`          | Writes a '415 Invalid Content-Type header: non-supported type.' message to the output. |
| `HandleInvalidHttpMethodErrorAsync`           | Indicates that an unsupported HTTP method was requested. Executes the next delegate in the chain by default. |
| `HandleWebSocketSubProtocolNotSupportedAsync` | Writes a '400 Invalid WebSocket sub-protocol.' message to the output. |

#### WebSocket handler classes

The WebSocket handling code is organized as follows:

| Interface / Class                | Description |
|----------------------------------|-------------|
| `IWebSocketConnection`           | Provides methods to a send a message to a client or close the connection. |
| `IOperationMessageProcessor`     | Handles incoming messages from the client. |
| `GraphQLWebSocketOptions`        | Provides configuration options for WebSocket connections. |
| `WebSocketConnection`            | Standard implementation of a message pump for `OperationMessage` messages across a WebSockets connection.  Implements `IWebSocketConnection` and delivers messages to a specified `IOperationMessageProcessor`. |
| `BaseSubscriptionServer`         | Abstract implementation of `IOperationMessageProcessor`, a message handler for `OperationMessage` messages.  Provides base functionality for managing subscriptions and requests. |
| `GraphQLWs.SubscriptionServer`   | Implementation of `IOperationMessageProcessor` for the `graphql-transport-ws` sub-protocol. |
| `SubscriptionsTransportWs.SubscriptionServer` | Implementation of `IOperationMessageProcessor` for the `graphql-ws` sub-protocol. |
| `IWebSocketAuthorizationService` | Allows authorization of GraphQL requests for WebSocket connections. |

Typically if you wish to change functionality or support another sub-protocol
you will need to perform the following:

1. Derive from either `SubscriptionServer` class, modifying functionality as needed, or to support
   a new protocol, derive from `BaseSubscriptionServer`.
2. Derive from `GraphQLHttpMiddleware` and override `CreateMessageProcessor` and/or
   `SupportedWebSocketSubProtocols` as needed.
3. Change the `app.AddGraphQL()` call to use your custom middleware, being sure to include an instance
   of the options class that your middleware requires (typically `GraphQLHttpMiddlewareOptions`).

There exists a few additional classes to support the above.  Please refer to the source code
of `GraphQLWs.SubscriptionServer` if you are attempting to add support for another protocol.

## Additional notes

### Service scope

By default, a dependency injection service scope is created for each GraphQL execution
in cases where it is possible that multiple GraphQL requests may be executing within the
same service scope:

1. A batched GraphQL request is executed.
2. A GraphQL request over a WebSocket connection is executed.

However, field resolvers for child fields of subscription nodes will not by default execute
with a service scope.  Rather, the `context.RequestServices` property will contain a reference
to a disposed service scope that cannot be used.

To solve this issue, please configure the scoped subscription execution strategy from the
GraphQL.MicrosoftDI package as follows:

```csharp
services.AddGraphQL(b => b
    .AddAutoSchema<Query>()
    .AddSystemTextJson()
    // configure queries for serial execution (optional)
    .AddExecutionStrategy<SerialExecutionStrategy>(OperationType.Query)
    // configure subscription field resolvers for scoped serial execution (parallel is optional)
    .AddScopedSubscriptionExecutionStrategy());
```

For single GET / POST requests, the service scope from the underlying HTTP context is used.

### User context builder

The user context builder interface is executed only once, within the dependency injection
service scope of the original HTTP request.  For batched requests, the same user context
instance is passed to each GraphQL execution.  For WebSocket requests, the same user
context instance is passed to each GraphQL subscription and data event resolver execution.

As such, do not create objects within the user context that rely on having the same
dependency injection service scope as the field resolvers.  Since WebSocket connections
are long-lived, using scoped services within a user context builder will result in those
scoped services having a matching long lifetime.  You may wish to alleviate this by
creating a service scope temporarily within your user context builder.

### Mutations within GET request

For security reasons and pursuant to current recommendations, mutation GraphQL requests
are rejected over HTTP GET connections.  Derive from `GraphQLHttpMiddleware` and override
`ExecuteRequestAsync` to prevent injection of the validation rules that enforce this behavior.

As would be expected, subscription requests are only allowed over WebSocket channels.

## Samples

The following samples are provided to show how to integrate this project with various
typical ASP.Net Core scenarios.

| Sample project          | Description |
|-------------------------|-------------|
| `AuthorizationSample`   | Demonstrates a GraphQL server added to an ASP.NET Core web authentication-enabled template. |
| `BasicSample`           | Demonstrates the minimum required setup for a HTTP GraphQL server. |
| `Chat`                  | A basic schema common to all samples; demonstrates queries, mutations and subscriptions. |
| `ControllerSample`      | Demonstrates using a controller action to serve GraphQL requests; does not support subscriptions. |
| `CorsSample`            | Demonstrates configuring a GraphQL endpoint to use a specified CORS policy. |
| `EndpointRoutingSample` | Demonstrates configuring GraphQL endpoints through endpoint routing. |
| `MultipleSchema`        | Demonstrates multiple GraphQL endpoints served through a single project. |
| `Net48Sample`           | Demonstrates a GraphQL server running on .NET Framework 4.8 (or for linux machines, .NET Core 2.1) |
| `PagesSample`           | Demonstrates configuring GraphQL within a ASP.NET Core Pages project. |

## Credits

Glory to Jehovah, Lord of Lords and King of Kings, creator of Heaven and Earth, who through his Son Jesus Christ,
has reedemed me to become a child of God. -Shane32
