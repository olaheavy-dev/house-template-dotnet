using Common.Application.Behaviors;
using Immediate.Handlers.Shared;

// Every handler in this module runs through these steps, in this order.
[assembly: Behaviors(typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>))]
