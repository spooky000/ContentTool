namespace ContentTool
{
    internal class ConsoleEx
    {
        public static void WriteErrorLine(string value)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(value);
            Console.ForegroundColor = oldColor;
        }

        public static void WriteErrorLine(object value)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(value);
            Console.ForegroundColor = oldColor;
        }
    }
}
