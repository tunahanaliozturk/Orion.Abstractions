using Moongazing.Orion.Abstractions.Demo;

ConsoleUi.Banner();

ConsoleUi.Section(1, "Fault-safe observer invocation (SafeObserverInvoker)");
await new SafeObserverDemo().RunAsync();

ConsoleUi.Section(2, "OpenTelemetry instrumentation conventions (OrionInstrumentation)");
new InstrumentationDemo().Run();

ConsoleUi.Section(3, "Testable clock (IOrionClock / SystemOrionClock)");
await new ClockDemo().RunAsync();

ConsoleUi.Section(4, "One-line DI registration (AddOrionAbstractions)");
new DependencyInjectionDemo().Run();

ConsoleUi.Done();
