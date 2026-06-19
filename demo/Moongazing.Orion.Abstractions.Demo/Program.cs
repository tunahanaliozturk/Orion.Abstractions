using Moongazing.Orion.Abstractions.Demo;

ConsoleUi.Banner();

ConsoleUi.Section(1, "Fault-safe observer invocation (SafeObserverInvoker)");
await new SafeObserverDemo().RunAsync();

ConsoleUi.Section(2, "OpenTelemetry instrumentation conventions (OrionInstrumentation)");
new InstrumentationDemo().Run();

ConsoleUi.Section(3, "Instance-scoped instrumentation (per-instance Meter tags + ListensTo)");
new InstanceScopedInstrumentationDemo().Run();

ConsoleUi.Section(4, "Testable clock (IOrionClock / SystemOrionClock)");
await new ClockDemo().RunAsync();

ConsoleUi.Section(5, "One-line DI registration (AddOrionAbstractions)");
new DependencyInjectionDemo().Run();

ConsoleUi.Done();
