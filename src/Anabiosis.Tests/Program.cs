// Лёгкий раннер тестов без внешнего фреймворка — см. RunTests в TestRunner.cs.
if (Environment.GetEnvironmentVariable("DIAG") == "1")
{
    TestRunner.Diagnostic();
    return 0;
}
return TestRunner.Run();
