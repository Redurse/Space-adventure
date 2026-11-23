// Лёгкий раннер тестов без внешнего фреймворка — см. RunTests в TestRunner.cs.
if (Environment.GetEnvironmentVariable("DIAG") == "1")
{
    TestRunner.Diagnostic();
    return 0;
}
// Промотка войны на галактической карте на много часов: WAR_HOURS=150 (см. WarFastForward.cs).
if (Environment.GetEnvironmentVariable("WAR_HOURS") is not null)
    return WarFastForward.Run();
return TestRunner.Run();
