namespace CUCoreLib.Util;

public static class ConsoleUtils
{
    public static void LogToConsole(string text)
    {
        var console = ConsoleScript.instance;
        if (console) ReflectionUtils.InvokeMethod(console, "LogToConsole", text);
    }

    public static void RunCommand(string commandString)
    {
        var console = ConsoleScript.instance;
        if (console != null) ReflectionUtils.InvokeMethod(console, "RunCommandString", commandString);
    }
}
